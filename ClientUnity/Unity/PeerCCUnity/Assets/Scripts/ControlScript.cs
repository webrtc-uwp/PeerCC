using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.EventSystems;

#if !UNITY_EDITOR
using Org.WebRtc;
using Windows.UI.Core;
using Windows.Foundation;
using Windows.Media.Core;
using System.Linq;
using System.Threading.Tasks;
using PeerConnectionClient.Signalling;
using Windows.ApplicationModel.Core;
#endif

public class ControlScript : MonoBehaviour
{
    public class Peer
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Id + ": " + Name;
        }
    }

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

    private enum Command
    {
        Empty,
        SetNotConnected,
        SetConnected,
        SetInCall,
        SetRemotePeers
    }

    private List<Peer> remotePeers;

    private Status status;
    private Command command;

    public ControlScript()
    {
        remotePeers = new List<Peer>();
        status = Status.NotConnected;
        command = Command.Empty;
    }

    void Awake()
    {
    }
    
    void Start()
    {
#if !UNITY_EDITOR
        Conductor.Instance.Initialized += Conductor_Initialized;
        Conductor.Instance.Initialize(CoreApplication.MainView.CoreWindow.Dispatcher);
        Conductor.Instance.EnableLogging(Conductor.LogLevel.Verbose);
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
                    if (command == Command.SetNotConnected)
                    {
                        ConnectButton.GetComponentInChildren<Text>().text = "Connect";
                        CallButton.GetComponentInChildren<Text>().text = "Call";
                    }
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
                    if (command == Command.SetConnected)
                    {
                        ConnectButton.GetComponentInChildren<Text>().text = "Disconnect";
                        CallButton.GetComponentInChildren<Text>().text = "Call";
                    }
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
                    if (command == Command.SetInCall)
                    {
                        ConnectButton.GetComponentInChildren<Text>().text = "Disconnect";
                        CallButton.GetComponentInChildren<Text>().text = "Hang Up";
                    }
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
            if (command == Command.SetRemotePeers)
            {
                GameObject textItem = (GameObject)Instantiate(TextItemPreftab);
                textItem.transform.SetParent(PeerContent);
                textItem.GetComponent<Text>().text = remotePeers.FirstOrDefault().Name;
                EventTrigger trigger = textItem.GetComponentInChildren<EventTrigger>();
                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerDown;
                entry.callback.AddListener((data) => { OnRemotePeerItemClick((PointerEventData)data); });
                trigger.triggers.Add(entry);
            }
            command = Command.Empty;
        }
#endif
    }

        private void Conductor_Initialized(bool succeeded)
    {
        if (succeeded)
        {
            Initialize();
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
                new Task(() =>
                {
                    Conductor.Instance.StartLogin(ServerAddressInputField.text, "8888");
                }).Start();
                status = Status.Connecting;
            }
            else if (status == Status.Connected)
            {
                new Task(() =>
                {
                    var task = Conductor.Instance.DisconnectFromServer();
                }).Start();

                remotePeers?.Clear();
                status = Status.Disconnecting;
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
                new Task(() =>
                {
                    Conductor.Peer conductorPeer = new Conductor.Peer();
                    Peer peer = remotePeers.FirstOrDefault();
                    if (peer != null)
                    {
                        conductorPeer.Id = remotePeers.FirstOrDefault().Id;
                        conductorPeer.Name = remotePeers.FirstOrDefault().Name;
                        Conductor.Instance.ConnectToPeer(conductorPeer);
                    }
                }).Start();
                status = Status.Calling;
            }
            else if (status == Status.InCall)
            {
                new Task(() => { var task = Conductor.Instance.DisconnectFromPeer(); }).Start();
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
        int i = 0;
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

    private IAsyncAction RunOnUiThread(Action fn)
    {
        return CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, new DispatchedHandler(fn));
    }
#endif

    public void Initialize()
    {
#if !UNITY_EDITOR
        // A Peer is connected to the server event handler
        Conductor.Instance.Signaller.OnPeerConnected += (peerId, peerName) =>
        {
            RunOnUiThread(() =>
            {
                lock (this)
                {
                    remotePeers.Add(new Peer { Id = peerId, Name = peerName });
                    Conductor.Peer peer = new Conductor.Peer { Id = peerId, Name = peerName };
                    Conductor.Instance.AddPeer(peer);
                    command = Command.SetRemotePeers;
                }
            }).AsTask().Wait();
        };

        // A Peer is disconnected from the server event handler
        Conductor.Instance.Signaller.OnPeerDisconnected += peerId =>
        {
            RunOnUiThread(() =>
            {
                lock (this)
                {
                    var peerToRemove = remotePeers?.FirstOrDefault(p => p.Id == peerId);
                    if (peerToRemove != null)
                        remotePeers.Remove(peerToRemove);
                }
            }).AsTask().Wait();
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
                        command = Command.SetConnected;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Signaller.OnSignedIn() - wrong status - " + status);
                    }
                }
            }).AsTask().Wait();
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
                        command = Command.SetNotConnected;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Signaller.OnServerConnectionFailure() - wrong status - " + status);
                    }
                }
            }).AsTask().Wait();
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
                        remotePeers?.Clear();
                        status = Status.NotConnected;
                        command = Command.SetNotConnected;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Signaller.OnDisconnected() - wrong status - " + status);
                    }
                }
            }).AsTask().Wait();
        };

        Conductor.Instance.OnAddRemoteStream += Conductor_OnAddRemoteStream;
        Conductor.Instance.OnRemoveRemoteStream += Conductor_OnRemoveRemoteStream;
        Conductor.Instance.OnAddLocalStream += Conductor_OnAddLocalStream;

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
                        command = Command.SetInCall;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Conductor.OnPeerConnectionCreated() - wrong status - " + status);
                    }
                }
            }).AsTask().Wait();
        };

        // Connection between the current user and a peer is closed event handler
        Conductor.Instance.OnPeerConnectionClosed += () =>
        {
            RunOnUiThread(() =>
            {
                lock (this)
                {
                    if (status == Status.EndingCall)
                    {
                        status = Status.Connected;
                        command = Command.SetConnected;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Conductor.OnPeerConnectionClosed() - wrong status - " + status);
                    }
                }
            }).AsTask().Wait();
        };

        // Ready to connect to the server event handler
        Conductor.Instance.OnReadyToConnect += () => { RunOnUiThread(() => { }).AsTask().Wait(); };

        var audioCodecList = Conductor.Instance.GetAudioCodecs();
        Conductor.Instance.AudioCodec = audioCodecList.First();

        // Order the video codecs so that the stable VP8 is in front.
        var videoCodecList = Conductor.Instance.GetVideoCodecs().OrderBy(codec =>
        {
            switch (codec.Name)
            {
                case "VP8": return 1;
                case "VP9": return 2;
                case "H264": return 3;
                default: return 99;
            }
        });
        Conductor.Instance.VideoCodec = videoCodecList.ElementAt(2);

#endif
    }

    private void Conductor_OnAddRemoteStream()
    {
#if !UNITY_EDITOR
        RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall)
                {
                    var source = Conductor.Instance.CreateRemoteMediaStreamSource("H264");
                    Plugin.LoadRemoteMediaStreamSource((MediaStreamSource)source);
                    Plugin.RemotePlay();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnAddRemoteStream() - wrong status - " + status);
                }
            }
        }).AsTask();
#endif
    }

    private void Conductor_OnRemoveRemoteStream()
    {
#if !UNITY_EDITOR
        RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall)
                {
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnRemoveRemoteStream() - wrong status - " + status);
                }
            }
        }).AsTask();
#endif
    }

    private void Conductor_OnAddLocalStream()
    {
#if !UNITY_EDITOR
        RunOnUiThread(() =>
        {
            lock (this)
            {
                if (status == Status.InCall)
                {
                    var source = Conductor.Instance.CreateLocalMediaStreamSource("I420");
                    Plugin.LoadLocalMediaStreamSource((MediaStreamSource)source);
                    Plugin.LocalPlay();

                    Conductor.Instance.EnableLocalVideoStream();
                    Conductor.Instance.UnmuteMicrophone();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Conductor.OnAddLocalStream() - wrong status - " + status);
                }
            }
        }).AsTask().Wait();
#endif
    }

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
        internal static extern void LoadLocalMediaStreamSource(MediaStreamSource IMediaSourceHandler);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadRemoteMediaStreamSource")]
        internal static extern void LoadRemoteMediaStreamSource(MediaStreamSource IMediaSourceHandler);
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
