using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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

    private List<Peer> peers;

    public ControlScript()
    {
        peers = new List<Peer>();
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
        if (true)
        {
            new Task(() =>
            {
                Conductor.Instance.StartLogin(ServerAddressInputField.text, "8888");
            }).Start();
        }
        else
        {
            new Task(() =>
            {
                var task = Conductor.Instance.DisconnectFromServer();
            }).Start();

            peers?.Clear();
        }
#endif
    }

    public void OnCallClick()
    {
#if !UNITY_EDITOR
        if (true)
        {
            new Task(() =>
            {
                Conductor.Peer conductorPeer = new Conductor.Peer();
                Peer peer = peers.FirstOrDefault();
                if (peer != null)
                {
                    conductorPeer.Id = peers.FirstOrDefault().Id;
                    conductorPeer.Name = peers.FirstOrDefault().Name;
                    Conductor.Instance.ConnectToPeer(conductorPeer);
                }
            }).Start();
        }
        else
        {
            new Task(() => { var task = Conductor.Instance.DisconnectFromPeer(); }).Start();
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
                peers.Add(new Peer { Id = peerId, Name = peerName });
                Conductor.Peer peer = new Conductor.Peer { Id = peerId, Name = peerName };
                Conductor.Instance.AddPeer(peer);
            }).AsTask().Wait();
        };

        // A Peer is disconnected from the server event handler
        Conductor.Instance.Signaller.OnPeerDisconnected += peerId =>
        {
            RunOnUiThread(() =>
            {
                var peerToRemove = peers?.FirstOrDefault(p => p.Id == peerId);
                if (peerToRemove != null)
                    peers.Remove(peerToRemove);
            }).AsTask().Wait();
        };

        // The user is Signed in to the server event handler
        Conductor.Instance.Signaller.OnSignedIn += () =>
        {
            RunOnUiThread(() =>
            {
            }).AsTask().Wait();
        };

        // Failed to connect to the server event handler
        Conductor.Instance.Signaller.OnServerConnectionFailure += () =>
        {
            RunOnUiThread(() =>
            {
            }).AsTask().Wait();
        };

        // The current user is disconnected from the server event handler
        Conductor.Instance.Signaller.OnDisconnected += () =>
        {
            RunOnUiThread(() =>
            {
                peers?.Clear();
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
            }).AsTask().Wait();
        };

        // Connection between the current user and a peer is closed event handler
        Conductor.Instance.OnPeerConnectionClosed += () =>
        {
            RunOnUiThread(() =>
            {
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
            var source = Conductor.Instance.CreateRemoteMediaStreamSource("H264");
            Plugin.LoadRemoteMediaStreamSource((MediaStreamSource)source);
            Plugin.RemotePlay();
        }).AsTask();
#endif
    }

    private void Conductor_OnRemoveRemoteStream()
    {
    }

    private void Conductor_OnAddLocalStream()
    {
#if !UNITY_EDITOR
        RunOnUiThread(() =>
        {
            var source = Conductor.Instance.CreateLocalMediaStreamSource("I420");
            Plugin.LoadLocalMediaStreamSource((MediaStreamSource)source);
            Plugin.LocalPlay();

            Conductor.Instance.EnableLocalVideoStream();
            Conductor.Instance.UnmuteMicrophone();
        }).AsTask().Wait();
#endif
    }

    public void CreateLocalMediaStreamSource(object track, string type, string id)
    {
        Plugin.CreateLocalMediaPlayback();
        IntPtr nativeTex = IntPtr.Zero;
        Plugin.GetLocalPrimaryTexture(LocalTextureWidth, LocalTextureHeight, out nativeTex);
        var primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)LocalTextureWidth, (int)LocalTextureHeight, TextureFormat.BGRA32, false, false, nativeTex);
        LocalVideoImage.texture = primaryPlaybackTexture;
#if !UNITY_EDITOR
        MediaVideoTrack videoTrack = (MediaVideoTrack)track;
        var source = Media.CreateMedia().CreateMediaStreamSource(videoTrack, type, id);
        Plugin.LoadLocalMediaStreamSource((MediaStreamSource)source);
        Plugin.LocalPlay();
#endif
    }

    public void DestroyLocalMediaStreamSource()
    {
        LocalVideoImage.texture = null;
        Plugin.ReleaseLocalMediaPlayback();
    }

    public void CreateRemoteMediaStreamSource(object track, string type, string id)
    {
        Plugin.CreateRemoteMediaPlayback();
        IntPtr nativeTex = IntPtr.Zero;
        Plugin.GetRemotePrimaryTexture(RemoteTextureWidth, RemoteTextureHeight, out nativeTex);
        var primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)RemoteTextureWidth, (int)RemoteTextureHeight, TextureFormat.BGRA32, false, false, nativeTex);
        RemoteVideoImage.texture = primaryPlaybackTexture;
#if !UNITY_EDITOR
        MediaVideoTrack videoTrack = (MediaVideoTrack)track;
        var source = Media.CreateMedia().CreateMediaStreamSource(videoTrack, type, id);
        Plugin.LoadRemoteMediaStreamSource((MediaStreamSource)source);
        Plugin.RemotePlay();
#endif
    }

    public void DestroyRemoteMediaStreamSource()
    {
        RemoteVideoImage.texture = null;
        Plugin.ReleaseRemoteMediaPlayback();
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
