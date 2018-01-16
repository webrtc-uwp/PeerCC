using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

#if !UNITY_EDITOR
using Org.WebRtc;
using Windows.Media.Core;
#endif

public class ControlScript : MonoBehaviour
{
    public uint LocalTextureWidth = 160;
    public uint LocalTextureHeight = 120;
    public uint RemoteTextureWidth = 640;
    public uint RemoteTextureHeight = 480;
    
    public RawImage LocalVideoImage;
    public RawImage RemoteVideoImage;

	void Awake()
    {
    }
    
    void Start()
    {
	}

    private void OnInitialized()
    {
    }

    private void OnEnable()
    {
        Plugin.CreateLocalMediaPlayback();
        Plugin.CreateRemoteMediaPlayback();
        GetPlaybackTexturesFromPlugin();
    }

    private void OnDisable()
    {
        Plugin.ReleaseLocalMediaPlayback();
        Plugin.ReleaseRemoteMediaPlayback();
    }

    void Update()
    {
    }

    public void CreateLocalMediaStreamSource(object track, string type, string id)
    {
#if !UNITY_EDITOR
        MediaVideoTrack videoTrack = (MediaVideoTrack)track;
        var source = Media.CreateMedia().CreateMediaStreamSource(videoTrack, type, id);
        Plugin.LoadLocalMediaStreamSource((MediaStreamSource)source);
        Plugin.LocalPlay();
#endif
    }

    public void CreateRemoteMediaStreamSource(object track, string type, string id)
    {
#if !UNITY_EDITOR
        MediaVideoTrack videoTrack = (MediaVideoTrack)track;
        var source = Media.CreateMedia().CreateMediaStreamSource(videoTrack, type, id);
        Plugin.LoadRemoteMediaStreamSource((MediaStreamSource)source);
        Plugin.RemotePlay();
#endif
    }

    private void GetPlaybackTexturesFromPlugin()
    {
        IntPtr localNativeTex = IntPtr.Zero;
        IntPtr remoteNativeTex = IntPtr.Zero;
        Plugin.GetLocalPrimaryTexture(LocalTextureWidth, LocalTextureHeight, out localNativeTex);
        Plugin.GetRemotePrimaryTexture(RemoteTextureWidth, RemoteTextureHeight, out remoteNativeTex);
        var localPrimaryPlaybackTexture = Texture2D.CreateExternalTexture((int)LocalTextureWidth, (int)LocalTextureHeight, TextureFormat.BGRA32, false, false, localNativeTex);
        var remotePrimaryPlaybackTexture = Texture2D.CreateExternalTexture((int)RemoteTextureWidth, (int)RemoteTextureHeight, TextureFormat.BGRA32, false, false, remoteNativeTex);

        LocalVideoImage.texture = localPrimaryPlaybackTexture;
        RemoteVideoImage.texture = remotePrimaryPlaybackTexture;
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
