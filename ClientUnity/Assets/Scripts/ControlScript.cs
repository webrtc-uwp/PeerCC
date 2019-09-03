using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

#if !UNITY_EDITOR
using Windows.UI.Core;
using Windows.Foundation;
using Windows.Media.Core;
using System.Linq;
using System.Threading.Tasks;
using PeerConnectionClient.Signalling;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;

#if USE_CX_VERSION
using UseMediaStreamTrack = Org.WebRtc.MediaStreamTrack;
#else
using UseMediaStreamTrack = Org.WebRtc.IMediaStreamTrack;
#endif

#endif

public class ControlScript : MonoBehaviour
{
    public static ControlScript Instance { get; private set; }

    public uint LocalTextureWidth = 160;
    public uint LocalTextureHeight = 120;
    public uint RemoteTextureWidth = 640;
    public uint RemoteTextureHeight = 480;
    
    public RawImage LocalVideoImage;
    public RawImage RemoteVideoImage;

    public InputField ServerAddressInputField;
    public Button ConnectButton;
    public Button CallButton;
    public RectTransform PeerContent;
    public GameObject TextItemPreftab;

    private enum Status
    {
        NotConnected,
        Connecting,
        Disconnecting,
        Connected,
        Calling,
        EndingCall,
        InCall
    }

    private enum CommandType
    {
        Empty,
        SetNotConnected,
        SetConnected,
        SetInCall,
        AddRemotePeer,
        RemoveRemotePeer
    }

    private struct Command
    {
        public CommandType type;
#if !UNITY_EDITOR
        public Conductor.Peer remotePeer;
#endif
    }

    private Status status = Status.NotConnected;
    private List<Command> commandQueue = new List<Command>();
    private int selectedPeerIndex = -1;

    public ControlScript()
    {
    }

    void Awake()
    {
    }

#if !UNITY_EDITOR
    void RequestAccessForMediaCaptureAndInit()
    {
        Conductor.RequestAccessForMediaCapture().AsTask().ContinueWith(async (antecedent) =>
        {
            if (antecedent.Result)
            {
                Conductor.Instance.Initialized += Conductor_Initialized;
                Conductor.Instance.EnableLogging(Conductor.LogLevel.Verbose);
                Conductor.Instance.Initialize(CoreApplication.MainView.CoreWindow.Dispatcher);

                List<Conductor.IceServer> iceServers = new List<Conductor.IceServer>();
                iceServers.Add(new Conductor.IceServer { Host = "stun.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
                iceServers.Add(new Conductor.IceServer { Host = "stun1.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
                iceServers.Add(new Conductor.IceServer { Host = "stun2.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
                iceServers.Add(new Conductor.IceServer { Host = "stun3.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
                iceServers.Add(new Conductor.IceServer { Host = "stun4.l.google.com:19302", Type = Conductor.IceServer.ServerType.STUN });
                Conductor.IceServer turnServer = new Conductor.IceServer { Host = "turnserver3dstreaming.centralus.cloudapp.azure.com:5349", Type = Conductor.IceServer.ServerType.TURN };
                turnServer.Credential = "3Dtoolkit072017";
                turnServer.Username = "user";
                iceServers.Add(turnServer);
                Conductor.Instance.ConfigureIceServers(iceServers);

                var audioCodecList = Conductor.GetAudioCodecs();
                Conductor.Instance.AudioCodec = audioCodecList.FirstOrDefault(c => c.Name == "opus");
                System.Diagnostics.Debug.WriteLine("Selected audio codec - " + Conductor.Instance.AudioCodec.Name);

                // Order the video codecs so that the stable VP8 is in front.
                var videoCodecList = Conductor.GetVideoCodecs();
                Conductor.Instance.VideoCodec = videoCodecList.FirstOrDefault(c => c.Name == "H264");
                System.Diagnostics.Debug.WriteLine("Selected video codec - " + Conductor.Instance.VideoCodec.Name);

                Conductor.MediaDevice device = (await Conductor.GetVideoCaptureDevices()).First();

                Conductor.Instance.GetVideoCaptureCapabilities(device.Id).AsTask().ContinueWith(capabilities =>
                {
                    uint preferredWidth = 896;
                    uint preferredHeght = 504;
                    uint preferredFrameRate = 15;
                    uint minSizeDiff = uint.MaxValue;
                    Conductor.MediaDevice selectedDevice = null;
                    Conductor.CaptureCapability selectedCapability = null;

                    foreach (Conductor.CaptureCapability capability in capabilities.Result)
                    {
                        uint sizeDiff = (uint)Math.Abs(preferredWidth - capability.Width) + (uint)Math.Abs(preferredHeght - capability.Height);
                        if (sizeDiff < minSizeDiff)
                        {
                            selectedDevice = device;
                            selectedCapability = capability;
                            minSizeDiff = sizeDiff;
                        }
                        System.Diagnostics.Debug.WriteLine("Video device capability - " + device.Name + " - " + capability.Width + "x" + capability.Height + "@" + capability.FrameRate);
                    }

                    if (selectedDevice != null)
                    {
                        selectedCapability.FrameRate = preferredFrameRate;
                        selectedCapability.MrcEnabled = true;
                        Conductor.Instance.SelectVideoDevice(selectedDevice);
                        Conductor.Instance.VideoCaptureProfile = selectedCapability;
                        System.Diagnostics.Debug.WriteLine("Selected video device - " + selectedDevice.Name);
                        System.Diagnostics.Debug.WriteLine("Selected video device capability - " + selectedCapability.Width + "x" + selectedCapability.Height + "@" + selectedCapability.FrameRate);
                    }
                });

                DeviceInformation audioInput = (await DeviceInformation.FindAllAsync(MediaDevice.GetAudioCaptureSelector())).First();
                Conductor.MediaDevice audioCaptureDevice = new Conductor.MediaDevice();
                audioCaptureDevice.Id = audioInput.Id;
                audioCaptureDevice.Name = audioInput.Name;
                Conductor.Instance.SelectAudioCaptureDevice(audioCaptureDevice);

                DeviceInformation audioOutput = (await DeviceInformation.FindAllAsync(MediaDevice.GetAudioRenderSelector())).First();
                Conductor.MediaDevice audioPlayoutDevice = new Conductor.MediaDevice();
                audioPlayoutDevice.Id = audioOutput.Id;
                audioPlayoutDevice.Name = audioOutput.Name;
                Conductor.Instance.SelectAudioPlayoutDevice(audioPlayoutDevice);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Failed to obtain access to media devices");
            }
        });
    }
#endif

    void Start()
    {
        Instance = this;
#if !UNITY_EDITOR
        RunOnUiThread(() =>
        {
            RequestAccessForMediaCaptureAndInit();
        });
#endif
        ServerAddressInputField.text = "peercc-server.ortclib.org";
    }

    private void OnEnable()
    {
        {
            Plugin.CreateLocalMediaPlayback();
            IntPtr nativeTex = IntPtr.Zero;
            Plugin.GetLocalPrimaryTexture(LocalTextureWidth, LocalTextureHeight, out nativeTex);
            var primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)LocalTextureWidth, (int)LocalTextureHeight, TextureFormat.BGRA32, false, false, nativeTex);
            LocalVideoImage.texture = primaryPlaybackTexture;
        }

        {
            Plugin.CreateRemoteMediaPlayback();
            IntPtr nativeTex = IntPtr.Zero;
            Plugin.GetRemotePrimaryTexture(RemoteTextureWidth, RemoteTextureHeight, out nativeTex);
            var primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)RemoteTextureWidth, (int)RemoteTextureHeight, TextureFormat.BGRA32, false, false, nativeTex);
            RemoteVideoImage.texture = primaryPlaybackTexture;
        }
    }

    private void OnDisable()
    {
        LocalVideoImage.texture = null;
        Plugin.ReleaseLocalMediaPlayback();
        RemoteVideoImage.texture = null;
        Plugin.ReleaseRemoteMediaPlayback();
    }

    private void Update()
    {
#if !UNITY_EDITOR
        lock (this)
        {
            switch (status)
            {
                case Status.NotConnected:
                    if (!ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = true;
                    if (!ConnectButton.enabled)
                        ConnectButton.enabled = true;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.Connecting:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.Disconnecting:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.Connected:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (!ConnectButton.enabled)
                        ConnectButton.enabled = true;
                    if (!CallButton.enabled)
                        CallButton.enabled = true;
                    break;
                case Status.Calling:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.EndingCall:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (CallButton.enabled)
                        CallButton.enabled = false;
                    break;
                case Status.InCall:
                    if (ServerAddressInputField.enabled)
                        ServerAddressInputField.enabled = false;
                    if (ConnectButton.enabled)
                        ConnectButton.enabled = false;
                    if (!CallButton.enabled)
                        CallButton.enabled = true;
                    break;
                default:
                    break;
            }

            while (commandQueue.Count != 0)
            {
                Command command = commandQueue.First();
                commandQueue.RemoveAt(0);
                switch (status)
                {
                    case Status.NotConnected:
                        if (command.type == CommandType.SetNotConnected)
                        {
                            ConnectButton.GetComponentInChildren<Text>().text = "Connect";
                            CallButton.GetComponentInChildren<Text>().text = "Call";
                        }
                        break;
                    case Status.Connected:
                        if (command.type == CommandType.SetConnected)
                        {
                            ConnectButton.GetComponentInChildren<Text>().text = "Disconnect";
                            CallButton.GetComponentInChildren<Text>().text = "Call";
                        }
                        break;
                    case Status.InCall:
                        if (command.type == CommandType.SetInCall)
                        {
                            ConnectButton.GetComponentInChildren<Text>().text = "Disconnect";
                            CallButton.GetComponentInChildren<Text>().text = "Hang Up";
                        }
                        break;
                    default:
                        break;
                }
                if (command.type == CommandType.AddRemotePeer)
                {
                    GameObject textItem = (GameObject)Instantiate(TextItemPreftab);
                    textItem.transform.SetParent(PeerContent);
                    textItem.GetComponent<Text>().text = command.remotePeer.Name;
                    EventTrigger trigger = textItem.GetComponentInChildren<EventTrigger>();
                    EventTrigger.Entry entry = new EventTrigger.Entry();
                    entry.eventID = EventTriggerType.PointerDown;
                    entry.callback.AddListener((data) => { OnRemotePeerItemClick((PointerEventData)data); });
                    trigger.triggers.Add(entry);
                    if (selectedPeerIndex == -1)
                    {
                        textItem.GetComponent<Text>().fontStyle = FontStyle.Bold;
                        selectedPeerIndex = PeerContent.transform.childCount - 1;
                    }
                }
                else if (command.type == CommandType.RemoveRemotePeer)
                {
                    for (int i = 0; i < PeerContent.transform.childCount; i++)
                    {
                        if (PeerContent.GetChild(i).GetComponent<Text>().text == command.remotePeer.Name)
                        {
                            PeerContent.GetChild(i).SetParent(null);
                            if (selectedPeerIndex == i)
                            {
                                if (PeerContent.transform.childCount > 0)
                                {
                                    PeerContent.GetChild(0).GetComponent<Text>().fontStyle = FontStyle.Bold;
                                    selectedPeerIndex = 0;
                                }
                                else
                                {
                                    selectedPeerIndex = -1;
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }
#endif
    }

    private void Conductor_Initialized(bool succeeded)
    {
        if (succeeded)
        {
#if !UNITY_EDITOR
            Initialize();
#endif
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Conductor initialization failed");
        }
    }

    public void OnConnectClick()
    {
#if !UNITY_EDITOR
        lock (this)
        {
            if (status == Status.NotConnected)
            {
                Task.Run(async () =>
                {
                    Conductor.Instance.StartLogin(ServerAddressInputField.text, "8888");
                });
                status = Status.Connecting;
            }
            else if (status == Status.Connected)
            {
                Task.Run(async () =>
                {
                    var task = Conductor.Instance.DisconnectFromServer();
                });

                status = Status.Disconnecting;
                selectedPeerIndex = -1;
                PeerContent.DetachChildren();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("OnConnectClick() - wrong status - " + status);
            }
        }
#endif
    }

    public void OnCallClick()
    {
#if !UNITY_EDITOR
        lock (this)
        {
            if (status == Status.Connected)
            {
                if (selectedPeerIndex == -1)
                    return;
                Task.Run(async () =>
                {
                    Conductor.Peer conductorPeer = Conductor.Instance.GetPeers()[selectedPeerIndex];
                    if (conductorPeer != null)
                    {
                        Conductor.Instance.ConnectToPeer(conductorPeer);
                    }
                });
                status = Status.Calling;
            }
            else if (status == Status.InCall)
            {
                Task.Run(async () =>
                {
                    var task = Conductor.Instance.DisconnectFromPeer();
                });
                status = Status.EndingCall;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("OnCallClick() - wrong status - " + status);
            }
        }
#endif
    }

    public void OnRemotePeerItemClick(PointerEventData data)
    {
#if !UNITY_EDITOR
        for (int i = 0; i < PeerContent.transform.childCount; i++)
        {
            if (PeerContent.GetChild(i) == data.selectedObject.transform)
            {
                data.selectedObject.GetComponent<Text>().fontStyle = FontStyle.Bold;
                selectedPeerIndex = i;
            }
            else
            {
                PeerContent.GetChild(i).GetComponent<Text>().fontStyle = FontStyle.Normal;
            }
        }
#endif
    }

    public void OnRemotePeerItemClick(GameObject item)
    {
#if !UNITY_EDITOR
        for (int i = 0; i < PeerContent.transform.childCount; i++)
        {
            if (PeerContent.GetChild(i) == item.transform)
            {
                item.GetComponent<Text>().fontStyle = FontStyle.Bold;
                selectedPeerIndex = i;
            }
            else
            {
                PeerContent.GetChild(i).GetComponent<Text>().fontStyle = FontStyle.Normal;
            }
        }
#endif
    }

#if !UNITY_EDITOR
    public async Task OnAppSuspending()
    {
        Conductor.Instance.CancelConnectingToPeer();

        await Conductor.Instance.DisconnectFromPeer();
        await Conductor.Instance.DisconnectFromServer();

        Conductor.Instance.OnAppSuspending();
    }

    private void RunOnUiThread(Action fn)
    {
        if(!UnityEngine.WSA.Application.RunningOnUIThread())
        {
            UnityEngine.WSA.Application.InvokeOnUIThread(() =>
            {
                fn.Invoke();
            }, false);
        }
        else
        {
            fn.Invoke();
        }
    }
#endif

#if !UNITY_EDITOR
    public void Initialize()
    {
        // A Peer is connected to the server event handler
        Conductor.Instance.Signaller.OnPeerConnected += (peerId, peerName) =>
        {
            RunOnUiThread(() =>
            {
                lock (this)
                {
                    Conductor.Peer peer = new Conductor.Peer { Id = peerId, Name = peerName };
                    Conductor.Instance.AddPeer(peer);
                    commandQueue.Add(new Command { type = CommandType.AddRemotePeer, remotePeer = peer });
                }
            });
        };

        // A Peer is disconnected from the server event handler
        Conductor.Instance.Signaller.OnPeerDisconnected += peerId =>
        {
            RunOnUiThread(() =>
            {
                lock (this)
                {
                    var peerToRemove = Conductor.Instance.GetPeers().FirstOrDefault(p => p.Id == peerId);
                    if (peerToRemove != null)
                    {
                        Conductor.Peer peer = new Conductor.Peer { Id = peerToRemove.Id, Name = peerToRemove.Name };
                        Conductor.Instance.RemovePeer(peer);
                        commandQueue.Add(new Command { type = CommandType.RemoveRemotePeer, remotePeer = peer });
                    }
                }
            });
        };

        // The user is Signed in to the server event handler
        Conductor.Instance.Signaller.OnSignedIn += () =>
        {
            RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.Connecting)
                    {
                        status = Status.Connected;
                        commandQueue.Add(new Command { type = CommandType.SetConnected });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Signaller.OnSignedIn() - wrong status - " + status);
                    }
                }
            });
        };

        // Failed to connect to the server event handler
        Conductor.Instance.Signaller.OnServerConnectionFailure += () =>
        {
            RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.Connecting)
                    {
                        status = Status.NotConnected;
                        commandQueue.Add(new Command { type = CommandType.SetNotConnected });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Signaller.OnServerConnectionFailure() - wrong status - " + status);
                    }
                }
            });
        };

        // The current user is disconnected from the server event handler
        Conductor.Instance.Signaller.OnDisconnected += () =>
        {
            RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.Disconnecting)
                    {
                        status = Status.NotConnected;
                        commandQueue.Add(new Command { type = CommandType.SetNotConnected });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Signaller.OnDisconnected() - wrong status - " + status);
                    }
                }
            });
        };

        Conductor.Instance.OnAddRemoteTrack += Conductor_OnAddRemoteTrack;
        Conductor.Instance.OnRemoveRemoteTrack += Conductor_OnRemoveRemoteTrack;
        Conductor.Instance.OnAddLocalTrack += Conductor_OnAddLocalTrack;

        // Connected to a peer event handler
        Conductor.Instance.OnPeerConnectionCreated += () =>
        {
            RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.Calling)
                    {
                        status = Status.InCall;
                        commandQueue.Add(new Command { type = CommandType.SetInCall });
                    }
                    else if (status == Status.Connected)
                    {
                        status = Status.InCall;
                        commandQueue.Add(new Command { type = CommandType.SetInCall });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Conductor.OnPeerConnectionCreated() - wrong status - " + status);
                    }
                }
            });
        };

        // Connection between the current user and a peer is closed event handler
        Conductor.Instance.OnPeerConnectionClosed += () =>
        {
            RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.EndingCall || status == Status.InCall)
                    {
                        Plugin.UnloadLocalMediaStreamSource();
                        Plugin.UnloadRemoteMediaStreamSource();
                        status = Status.Connected;
                        commandQueue.Add(new Command { type = CommandType.SetConnected });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Conductor.OnPeerConnectionClosed() - wrong status - " + status);
                    }
                }
            });
        };

        // Ready to connect to the server event handler
        Conductor.Instance.OnReadyToConnect += () => { RunOnUiThread(() => { }); };
    }
#endif

#if !UNITY_EDITOR
    private void Conductor_OnAddRemoteTrack(UseMediaStreamTrack track)
    {
        RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall || status == Status.Connected)
                {
                    ((Org.WebRtc.MediaStreamTrack)track).OnMediaSourceChanged += () =>
                    {
                        RunOnUiThread(() =>
                        {
                            lock (this)
                            {
                                Plugin.LoadRemoteMediaStreamSource(((Org.WebRtc.MediaSource)track.Source).Source);
                            }
                        });
                    };
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnAddRemoteStream() - wrong status - " + status);
                }
            }
        });
}
#endif

#if !UNITY_EDITOR
    private void Conductor_OnRemoveRemoteTrack(UseMediaStreamTrack track)
    {
        RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall || status == Status.Connected)
                {
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnRemoveRemoteStream() - wrong status - " + status);
                }
            }
        });
}
#endif

#if !UNITY_EDITOR
    private void Conductor_OnAddLocalTrack(UseMediaStreamTrack track)
    {
        RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall || status == Status.Connected)
                {
                    ((Org.WebRtc.MediaStreamTrack)track).OnMediaSourceChanged += () =>
                    {
                        RunOnUiThread(() =>
                        {
                            lock (this)
                            {
                                Plugin.LoadLocalMediaStreamSource(((Org.WebRtc.MediaSource)track.Source).Source);
                            }
                        });
                    };
                    Conductor.Instance.EnableLocalVideoStream();
                    Conductor.Instance.UnmuteMicrophone();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnAddLocalStream() - wrong status - " + status);
                }
            }
        });
    }
#endif

    private static class Plugin
    {
        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateLocalMediaPlayback")]
        internal static extern void CreateLocalMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateRemoteMediaPlayback")]
        internal static extern void CreateRemoteMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "ReleaseLocalMediaPlayback")]
        internal static extern void ReleaseLocalMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "ReleaseRemoteMediaPlayback")]
        internal static extern void ReleaseRemoteMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetLocalPrimaryTexture")]
        internal static extern void GetLocalPrimaryTexture(UInt32 width, UInt32 height, out System.IntPtr playbackTexture);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetRemotePrimaryTexture")]
        internal static extern void GetRemotePrimaryTexture(UInt32 width, UInt32 height, out System.IntPtr playbackTexture);

#if !UNITY_EDITOR
        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadLocalMediaStreamSource")]
        internal static extern void LoadLocalMediaStreamSource(Windows.Media.Core.IMediaSource mediaSource);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "UnloadLocalMediaStreamSource")]
        internal static extern void UnloadLocalMediaStreamSource();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadRemoteMediaStreamSource")]
        internal static extern void LoadRemoteMediaStreamSource(Windows.Media.Core.IMediaSource mediaSource);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "UnloadRemoteMediaStreamSource")]
        internal static extern void UnloadRemoteMediaStreamSource();
#endif

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LocalPlay")]
        internal static extern void LocalPlay();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "RemotePlay")]
        internal static extern void RemotePlay();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LocalPause")]
        internal static extern void LocalPause();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "RemotePause")]
        internal static extern void RemotePause();
    }
}
