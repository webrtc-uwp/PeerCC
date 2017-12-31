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
    public uint TextureWidth = 640;
    public uint TextureHeight = 480;
    public uint FrameRate = 30;
    
    public RawImage Canvas;

    public Camera Camera;

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
        Plugin.CreateMediaPlayback();
        GetPlaybackTextureFromPlugin();
    }

    private void OnDisable()
    {
        Plugin.ReleaseMediaPlayback();
    }

    void Update()
    {
    }

    public void CreateMediaStreamSource(object track, string type, string id)
    {
#if !UNITY_EDITOR
        MediaVideoTrack videoTrack = (MediaVideoTrack)track;
        var source = Media.CreateMedia().CreateMediaStreamSource(videoTrack, type, id);
        Plugin.LoadMediaStreamSource((MediaStreamSource)source);
        Plugin.Play();
#endif
    }

    private void GetPlaybackTextureFromPlugin()
    {
        IntPtr nativeTex = IntPtr.Zero;
        Plugin.GetPrimaryTexture(TextureWidth, TextureHeight, out nativeTex);
        var primaryPlaybackTexture = Texture2D.CreateExternalTexture((int)TextureWidth, (int)TextureHeight, TextureFormat.BGRA32, false, false, nativeTex);

        Canvas.texture = primaryPlaybackTexture;
    }

	private static class Plugin
    {
        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateMediaPlayback")]
        internal static extern void CreateMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "ReleaseMediaPlayback")]
        internal static extern void ReleaseMediaPlayback();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "GetPrimaryTexture")]
        internal static extern void GetPrimaryTexture(UInt32 width, UInt32 height, out System.IntPtr playbackTexture);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadContent")]
        internal static extern void LoadContent([MarshalAs(UnmanagedType.BStr)] string sourceURL);
#if !UNITY_EDITOR

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadMediaSource")]
        internal static extern void LoadMediaSource(IMediaSource IMediaSourceHandler);

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "LoadMediaStreamSource")]
        internal static extern void LoadMediaStreamSource(MediaStreamSource IMediaSourceHandler);
#endif
    
        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "Play")]
        internal static extern void Play();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "Pause")]
        internal static extern void Pause();

        [DllImport("MediaEngineUWP", CallingConvention = CallingConvention.StdCall, EntryPoint = "Stop")]
        internal static extern void Stop();
	}
}
