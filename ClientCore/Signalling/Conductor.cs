//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Linq;
using System.Collections.Generic;
using Windows.Networking.Connectivity;
using Windows.Networking;
using Windows.Data.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Threading;
using System.Text.RegularExpressions;
using static System.String;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Devices.Enumeration;
using Windows.Media.MediaProperties;
#if ORTCLIB
using Org.Ortc;
using Org.Ortc.Adapter;
using PeerConnectionClient.Ortc;
using PeerConnectionClient.Ortc.Utilities;
using CodecInfo = Org.Ortc.RTCRtpCodecCapability;
using MediaVideoTrack = Org.Ortc.MediaStreamTrack;
using MediaAudioTrack = Org.Ortc.MediaStreamTrack;
using RTCIceCandidate = Org.Ortc.Adapter.RTCIceCandidate;
using MediaDevice = PeerConnectionClient.Ortc.MediaDevice;

using UseMediaStreamTrack = Org.Ortc.IMediaStreamTrack;
using UseRTCPeerConnectionIceEvent = Org.Ortc.Adapter.IRTCPeerConnectionIceEvent;
using UseRTCTrackEvent = Org.Ortc.Adapter.IRTCTrackEvent;
using UseRTCSessionDescription = Org.Ortc.Adapter.IRTCSessionDescription;
#else
using Org.WebRtc;
using PeerConnectionClient.Utilities;
#if !UNITY
using PeerConnectionClientCore.Stats;
#endif
using System.Text;
using UseMediaStreamTrack = Org.WebRtc.IMediaStreamTrack;
using UseRTCPeerConnectionIceEvent = Org.WebRtc.IRTCPeerConnectionIceEvent;
using UseRTCTrackEvent = Org.WebRtc.IRTCTrackEvent;
using UseRTCSessionDescription = Org.WebRtc.IRTCSessionDescription;
using UseConstraint = Org.WebRtc.IConstraint;
using UseMediaConstraints = Org.WebRtc.IMediaConstraints;
#endif

namespace PeerConnectionClient.Signalling
{
    /// <summary>
    /// A singleton conductor for WebRTC session.
    /// </summary>
    public class Conductor
    {
#if !UNITY
        private static readonly StatsController SC = StatsController.Instance;
#endif

        private string _localSDPForCallStats;

        public class Peer
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public class IceServer
        {
            public enum ServerType { STUN, TURN };

            public ServerType Type { get; set; }
            public string Host { get; set; }
            public string Credential { get; set; }
            public string Username { get; set; }
        }

        public enum MediaDeviceType
        {
            AudioCapture,
            AudioPlayout,
            VideoCapture
        };

        public delegate void MediaDevicesChanged(MediaDeviceType type);

        public class MediaDevice
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        public class CaptureCapability
        {
            public uint Width { get; set; }
            public uint Height { get; set; }
            public uint FrameRate { get; set; }
            public bool MrcEnabled { get; set; }
            public string ResolutionDescription { get; set; }
            public string FrameRateDescription { get; set; }
        }

        public class CodecInfo
        {
            public byte PreferredPayloadType { get; set; }
            public string Name { get; set; }
            public int ClockRate { get; set; }
        }

        public enum LogLevel
        {
            Sensitive,
            Verbose,
            Info,
            Warning,
            Error
        };

        public class PeerConnectionHealthStats {
            public long ReceivedBytes { get; set; }
            public long ReceivedKpbs { get; set; }
            public long SentBytes { get; set; }
            public long SentKbps { get; set; }
            public long RTT { get; set; }
            public string LocalCandidateType { get; set; }
            public string RemoteCandidateType { get; set; }
        };

        private static readonly object InstanceLock = new object();
        private static Conductor _instance;
#if ORTCLIB
        private RTCPeerConnectionSignalingMode _signalingMode;
#endif

        /// <summary>
        ///  The single instance of the Conductor class.
        /// </summary>
        public static Conductor Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new Conductor();
                        }
                    }
                }
                return _instance;
            }
        }

        private readonly Signaller _signaller;

        /// <summary>
        /// The signaller property.
        /// Helps to pass WebRTC session signals between client and server.
        /// </summary>
        public Signaller Signaller => _signaller;

        private UseMediaStreamTrack _peerVideoTrack;
        private UseMediaStreamTrack _selfVideoTrack;
        private UseMediaStreamTrack _peerAudioTrack;
        private UseMediaStreamTrack _selfAudioTrack;

        public Windows.UI.Xaml.Controls.MediaElement SelfVideo { get; set; }
        public Windows.UI.Xaml.Controls.MediaElement PeerVideo { get; set; }

        /// <summary>
        /// Video codec used in WebRTC session.
        /// </summary>
        public CodecInfo VideoCodec { get; set; }

        /// <summary>
        /// Audio codec used in WebRTC session.
        /// </summary>
        public CodecInfo AudioCodec { get; set; }

        /// <summary>
        /// Video capture details (frame rate, resolution)
        /// </summary>
        public CaptureCapability VideoCaptureProfile;

        // SDP negotiation attributes
        private static readonly string kCandidateSdpMidName = "sdpMid";
        private static readonly string kCandidateSdpMlineIndexName = "sdpMLineIndex";
        private static readonly string kCandidateSdpName = "candidate";
        private static readonly string kSessionDescriptionTypeName = "type";
        private static readonly string kSessionDescriptionSdpName = "sdp";
#if ORTCLIB
        private static readonly string kSessionDescriptionJsonName = "session";
#endif
        private readonly object _peerConnectionLock = new object();
        private RTCPeerConnection _peerConnection_DoNotUse;
        private RTCPeerConnection PeerConnection
        {
            get
            {
                lock (_peerConnectionLock)
                {
                    return _peerConnection_DoNotUse;
                }
            }
            set
            {
                lock (_peerConnectionLock)
                {
                    if (null == value)
                    {
                        if (null != _peerConnection_DoNotUse)
                        {
                            (_peerConnection_DoNotUse as IDisposable)?.Dispose();
                        }
                    }
                    _peerConnection_DoNotUse = value;
                }
            }
        }

        WebRtcFactory _factory;

        MediaDevice _selectedVideoDevice = null;

        private List<Peer> _peers = new List<Peer>();
        private Peer _peer;
        readonly List<RTCIceServer> _iceServers;

        private CoreDispatcher _uiDispatcher;

        private int _peerId = -1;
        protected bool VideoEnabled = true;
        protected bool AudioEnabled = true;
        protected string SessionId;

        bool _etwStatsEnabled;

        /// <summary>
        /// Enable/Disable ETW stats used by WebRTCDiagHubTool Visual Studio plugin.
        /// If the ETW Stats are disabled, no data will be sent to the plugin.
        /// </summary>
        public bool EtwStatsEnabled
        {
            get
            {
                return _etwStatsEnabled;
            }
            set
            {
                _etwStatsEnabled = value;
#if !ORTCLIB
                if (PeerConnection != null)
                {
                    //_peerConnection.EtwStatsEnabled = value;
                }
#endif
            }
        }

        bool _videoLoopbackEnabled = true;
        public bool VideoLoopbackEnabled
        {
            get
            {
                return _videoLoopbackEnabled;
            }
            set
            {
                if (_videoLoopbackEnabled == value)
                    return;

                _videoLoopbackEnabled = value;
                if (_videoLoopbackEnabled)
                {
                    if (_selfVideoTrack != null)
                    {
                        Debug.WriteLine("Enabling video loopback");
#if !UNITY && !ORTCLIB
                        _selfVideoTrack.Element = Org.WebRtc.MediaElementMaker.Bind(SelfVideo);
                        ((MediaStreamTrack)_selfVideoTrack).OnFrameRateChanged += (float frameRate) =>
                        {
                            FramesPerSecondChanged?.Invoke("SELF", frameRate.ToString("0.0"));
                        };
                        ((MediaStreamTrack)_selfVideoTrack).OnResolutionChanged += (uint width, uint height) =>
                        {
                            ResolutionChanged?.Invoke("SELF", width, height);
                        };
#endif
                        Debug.WriteLine("Video loopback enabled");
                    }
                }
                else
                {
                    // This is a hack/workaround for destroying the internal stream source (RTMediaStreamSource)
                    // instance inside webrtc winuwp api when loopback is disabled.
                    // For some reason, the RTMediaStreamSource instance is not destroyed when only SelfVideo.Source
                    // is set to null.
                    // For unknown reasons, when executing the above sequence (set to null, stop, set to null), the
                    // internal stream source is destroyed.
                    // Apparently, with webrtc package version < 1.1.175, the internal stream source was destroyed
                    // corectly, only by setting SelfVideo.Source to null.
#if !UNITY && !ORTCLIB
                    _selfVideoTrack.Element = null; // Org.WebRtc.MediaElementMaker.Bind(obj)
#endif
                    GC.Collect(); // Ensure all references are truly dropped.
                }
            }
        }

        bool _tracingEnabled;
        public bool TracingEnabled
        {
            get
            {
                return _tracingEnabled;
            }
            set
            {
                _tracingEnabled = value;
#if ORTCLIB
                if (_tracingEnabled)
                {
                    Org.Ortc.Ortc.StartMediaTracing();
                }
                else
                {
                    Org.Ortc.Ortc.StopMediaTracing();
                    Org.Ortc.Ortc.SaveMediaTrace(_traceServerIp, Int32.Parse(_traceServerPort));
                }
#else
                if (_tracingEnabled)
                {
                    //WebRTC.StartTracing("webrtc-trace.txt");
                }
                else
                {
                    //WebRTC.StopTracing();
                }
#endif
            }
        }

        bool _peerConnectionStatsEnabled;

        public void Initialize(CoreDispatcher uiDispatcher)
        {
            _uiDispatcher = uiDispatcher;
#if !ORTCLIB
            var queue = Org.WebRtc.EventQueueMaker.Bind(uiDispatcher);
            var configuration = new Org.WebRtc.WebRtcLibConfiguration();
            configuration.Queue = queue;
            configuration.AudioCaptureFrameProcessingQueue = Org.WebRtc.EventQueue.GetOrCreateThreadQueueByName("AudioCaptureProcessingQueue");
            configuration.AudioRenderFrameProcessingQueue = Org.WebRtc.EventQueue.GetOrCreateThreadQueueByName("AudioRenderProcessingQueue");
            configuration.VideoFrameProcessingQueue = Org.WebRtc.EventQueue.GetOrCreateThreadQueueByName("VideoFrameProcessingQueue");
            configuration.CustomAudioQueue = Org.WebRtc.EventQueue.GetOrCreateThreadQueueByName("CustomAudioQueue");
            configuration.CustomVideoQueue = Org.WebRtc.EventQueue.GetOrCreateThreadQueueByName("CustomVideoQueue");
            Org.WebRtc.WebRtcLib.Setup(configuration);
#endif

            Initialized?.Invoke(true);
        }

        public event Action<bool> Initialized;

        public event Action<MediaDeviceType> OnMediaDevicesChanged;

        public event Action<string, string> FramesPerSecondChanged;

        public event Action<string, uint, uint> ResolutionChanged;

        public void EnableLogging(LogLevel level)
        {
        }

        public void DisableLogging()
        {
        }

        public Windows.Storage.StorageFolder LogFolder
        {
            get
            {
                return null;
            }
        }

        public String LogFileName
        {
            get
            {
                return "";
            }
        }

        public void SynNTPTime(long ntpTime)
        {
        }

        public double CpuUsage
        {
            get
            {
                return 0.0;
            }
            set
            {
            }
        }

        public long MemoryUsage
        {
            get
            {
                return 0;
            }
            set
            {
            }
        }

        public void OnAppSuspending()
        {
        }

        /// <summary>
        /// Enable/Disable connection health stats.
        /// Connection health stats are delivered by the OnConnectionHealthStats event. 
        /// </summary>
        public bool PeerConnectionStatsEnabled
        {
            get
            {
                return _peerConnectionStatsEnabled;
            }
            set
            {
                _peerConnectionStatsEnabled = value;
#if !ORTCLIB
                if (PeerConnection != null)
                {
                    //_peerConnection.ConnectionHealthStatsEnabled = value;
                }
#endif
            }
        }

        public object MediaLock { get; set; } = new object();

        CancellationTokenSource _connectToPeerCancelationTokenSource;
        Task<bool> _connectToPeerTask;

        // Public events for adding and removing the local track
        public event Action<UseMediaStreamTrack> OnAddLocalTrack;

        // Public events to notify about connection status
        public event Action OnPeerConnectionCreated;
        public event Action OnPeerConnectionClosed;
        public event Action OnReadyToConnect;

        /// <summary>
        /// Gets permission from the OS to get access to a media capture device. If
        /// permissions are not enabled for the calling application, the OS will
        /// display a prompt asking the user for permission.
        /// This function must be called from the UI thread.
        /// </summary>
        public static IAsyncOperation<bool> RequestAccessForMediaCapture()
        {
            MediaCapture mediaAccessRequester = new MediaCapture();
            MediaCaptureInitializationSettings mediaSettings =
                new MediaCaptureInitializationSettings();
            mediaSettings.AudioDeviceId = "";
            mediaSettings.VideoDeviceId = "";
            mediaSettings.StreamingCaptureMode =
                Windows.Media.Capture.StreamingCaptureMode.AudioAndVideo;
            mediaSettings.PhotoCaptureSource =
                Windows.Media.Capture.PhotoCaptureSource.VideoPreview;
            Task initTask = mediaAccessRequester.InitializeAsync(mediaSettings).AsTask();
            return initTask.ContinueWith(initResult => {
                bool accessRequestAccepted = true;
                if (initResult.Exception != null)
                {
                    Debug.WriteLine("Failed to obtain media access permission: " + initResult.Exception.Message);
                    accessRequestAccepted = false;
                }
                return accessRequestAccepted;
            }).AsAsyncOperation<bool>();
        }

        async public static Task<IList<MediaDevice>> GetVideoCaptureDevices()
        {
#if ORTCLIB
            var devices = (await MediaDevices.EnumerateDevices());
#else
            var devices = (await VideoCapturer.GetDevices());
#endif

            IList<MediaDevice> deviceList = new List<MediaDevice>();
            foreach (var deviceInfo in devices)
            {
                deviceList.Add(new MediaDevice
                {
#if ORTCLIB
                    Id = deviceInfo.DeviceId,
                    Name = deviceInfo.Label
#else
                    Id = deviceInfo.Info.Id,
                    Name = deviceInfo.Info.Name,
                    //Location = deviceInfo.Info.EnclosureLocation
#endif
                });
            }
            return deviceList;
        }

        public IAsyncOperation<IList<CaptureCapability>> GetVideoCaptureCapabilities(string deviceId)
        {
            MediaCapture mediaCapture = new MediaCapture();
            MediaCaptureInitializationSettings mediaSettings =
                new MediaCaptureInitializationSettings();
            mediaSettings.VideoDeviceId = deviceId;

            Task initTask = mediaCapture.InitializeAsync(mediaSettings).AsTask();
            return initTask.ContinueWith(initResult => {
                if (initResult.Exception != null)
                {
                    Debug.WriteLine("Failed to initialize video device: " + initResult.Exception.Message);
                    return null;
                }
                var streamProperties =
                    mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoRecord);
                IList<CaptureCapability> capabilityList = new List<CaptureCapability>();
                foreach (VideoEncodingProperties property in streamProperties)
                {
                    uint frameRate = (uint)(property.FrameRate.Numerator /
                        property.FrameRate.Denominator);
                    capabilityList.Add(new CaptureCapability
                    {
                        Width = (uint)property.Width,
                        Height = (uint)property.Height,
                        FrameRate = frameRate,
                        MrcEnabled = true,
                        FrameRateDescription = $"{frameRate} fps",
                        ResolutionDescription = $"{property.Width} x {property.Height}"
                    });
                }
                return capabilityList;
            }).AsAsyncOperation<IList<CaptureCapability>>();
        }

        public static IList<CodecInfo> GetAudioCodecs()
        {
            var ret = new List<CodecInfo>
            {
                new CodecInfo { PreferredPayloadType = 111, ClockRate = 48000, Name = "opus" },
                new CodecInfo { PreferredPayloadType = 103, ClockRate = 16000, Name = "ISAC" },
                new CodecInfo { PreferredPayloadType = 104, ClockRate = 32000, Name = "ISAC" },
                new CodecInfo { PreferredPayloadType = 9, ClockRate = 8000, Name = "G722" },
                new CodecInfo { PreferredPayloadType = 102, ClockRate = 8000, Name = "ILBC" },
                new CodecInfo { PreferredPayloadType = 0, ClockRate = 8000, Name = "PCMU" },
                new CodecInfo { PreferredPayloadType = 8, ClockRate = 8000, Name = "PCMA" }
            };
            return ret;
		}

        public static IList<CodecInfo> GetVideoCodecs()
        {
            var ret = new List<CodecInfo>
            {
                new CodecInfo { PreferredPayloadType = 96, ClockRate = 90000, Name = "VP8" },
                new CodecInfo { PreferredPayloadType = 98, ClockRate = 90000, Name = "VP9" },
                new CodecInfo { PreferredPayloadType = 100, ClockRate = 90000, Name = "H264" }
            };
            return ret;
		}

        public void SelectVideoDevice(MediaDevice device)
        {
            _selectedVideoDevice = device;
        }

        /// <summary>
        /// Creates a peer connection.
        /// </summary>
        /// <returns>True if connection to a peer is successfully created.</returns>
        async private Task<bool> CreatePeerConnection(CancellationToken cancelationToken)
        {
            Debug.Assert(PeerConnection == null);
            if(cancelationToken.IsCancellationRequested)
            {
                return false;
            }

            var factoryConfig = new WebRtcFactoryConfiguration();
            factoryConfig.CustomVideoFactory = null;
            _factory = new WebRtcFactory(factoryConfig);

#if ENABLE_AUDIO_PROCESSING
            _factory.OnAudioPostCaptureInitialize += Factory_HandleAudioPostCaptureInit;
            _factory.OnAudioPostCaptureRuntimeSetting += Factory_HandleAudioPostCaptureRuntimeSetting;
            _factory.OnAudioPostCapture += Factory_HandleAudioPostCaptureBuffer;

            _factory.OnAudioPreRenderInitialize += Factory_HandleAudioPreRenderInit;
            _factory.OnAudioPreRenderRuntimeSetting += Factory_HandleAudioPreRenderRuntimeSetting;
            _factory.OnAudioPreRender += Factory_HandleAudioPreRenderBuffer;
#endif

            var config = new RTCConfiguration()
            {
                Factory = _factory,
                BundlePolicy = RTCBundlePolicy.Balanced,
#if ORTCLIB
                SignalingMode = _signalingMode,
                GatherOptions = new RTCIceGatherOptions()
                {
                    IceServers = new List<RTCIceServer>(_iceServers),
                }
#else
                IceTransportPolicy = RTCIceTransportPolicy.All,
                IceServers = _iceServers
#endif
            };

            Debug.WriteLine("Conductor: Creating peer connection.");
            PeerConnection = new RTCPeerConnection(config);

#if !UNITY
            if (SC.callStatsClient != null)
                await SC.callStatsClient.SendStartCallStats("native", "UWP", "10.0", "1.0", SC.GenerateJWT());

            SC.totalSetupTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

            PeerConnection.OnIceGatheringStateChange += async () =>
            {
                var peerConnection = PeerConnection;
                if (null == peerConnection)
                    return;
                if (SC.callStatsClient != null)
                    await PeerConnectionStateChange.StatsOnIceGatheringStateChange(peerConnection);

                Debug.WriteLine("Conductor: Ice connection state change, gathering-state=" + peerConnection.IceGatheringState.ToString().ToLower());
            };
#endif

            PeerConnection.OnIceConnectionStateChange += async () =>
            {
                var peerConnection = PeerConnection;
                if (peerConnection != null)
                {
#if !UNITY
                    if (SC.callStatsClient != null)
                        await PeerConnectionStateChange.StatsOnIceConnectionStateChange(peerConnection);
#endif
                }
                else
                {
#if !UNITY
                    if (SC.callStatsClient != null)
                    {
                        await PeerConnectionStateChange.PeerConnectionClosedStateChange();
                        ClosePeerConnection();
                    }
#else
                    ClosePeerConnection();
#endif
                }

                // TODO: Add to GUI
                //await callStatsClient.SendConferenceUserFeedback(4, 3, 5, Empty);

                Debug.WriteLine("Conductor: Ice connection state change, state=" + (null != peerConnection ? peerConnection.IceConnectionState.ToString().ToLower() : "closed"));
            };

            if (null == PeerConnection)
                throw new NullReferenceException("Peer connection is not created.");

#if !ORTCLIB
            //_peerConnection.EtwStatsEnabled = _etwStatsEnabled;
            //_peerConnection.ConnectionHealthStatsEnabled = _peerConnectionStatsEnabled;
#endif
            if (cancelationToken.IsCancellationRequested)
            {
                return false;
            }
#if ORTCLIB
            OrtcStatsManager.Instance.Initialize(_peerConnection);
#endif
            OnPeerConnectionCreated?.Invoke();

            PeerConnection.OnIceCandidate += PeerConnection_OnIceCandidate;
#if ORTCLIB
            _peerConnection.OnTrack += PeerConnection_OnAddTrack;
            _peerConnection.OnTrackGone += PeerConnection_OnRemoveTrack;
            _peerConnection.OnIceConnectionStateChange += () => { Debug.WriteLine("Conductor: Ice connection state change, state=" + (null != _peerConnection ? _peerConnection.IceConnectionState.ToString() : "closed")); };
#else
            PeerConnection.OnTrack += PeerConnection_OnTrack;
            PeerConnection.OnRemoveTrack += PeerConnection_OnRemoveTrack;
            //_peerConnection.OnConnectionHealthStats += PeerConnection_OnConnectionHealthStats;

            Debug.WriteLine("Conductor: Getting user media.");

            IReadOnlyList<UseConstraint> mandatoryConstraints = new List<UseConstraint>() {
                new Constraint("maxWidth", VideoCaptureProfile.Width.ToString()),
                new Constraint("minWidth", VideoCaptureProfile.Width.ToString()),
                new Constraint("maxHeight", VideoCaptureProfile.Height.ToString()),
                new Constraint("minHeight", VideoCaptureProfile.Height.ToString()),
                new Constraint("maxFrameRate", VideoCaptureProfile.FrameRate.ToString()),
                new Constraint("minFrameRate", VideoCaptureProfile.FrameRate.ToString())
            };
            IReadOnlyList<UseConstraint> optionalConstraints = new List<UseConstraint>();
            UseMediaConstraints mediaConstraints = new MediaConstraints(mandatoryConstraints, optionalConstraints);

            if (cancelationToken.IsCancellationRequested)
            {
                return false;
            }
#endif
#if ORTCLIB
            var mediaStreamConstraints = new MediaStreamConstraints();
            var tracks = await (MediaDevices.GetUserMedia(mediaStreamConstraints).AsTask<IReadOnlyList<IMediaStreamTrack>>());
            IMediaStreamTrack localVideoTrack = null;
            IMediaStreamTrack localAudioTrack = null;
            if (tracks != null)
            {
                var audioCapabilities = RTCRtpSender.GetCapabilities(MediaStreamTrackKind.Audio);
                var videoCapabilities = RTCRtpSender.GetCapabilities(MediaStreamTrackKind.Video);

                var mediaStream = new MediaStream(tracks);
                Debug.WriteLine("Conductor: Adding local media stream.");
                var mediaStreamList = new List<IMediaStream>();
                mediaStreamList.Add(mediaStream);
                foreach (var mediaStreamTrack in tracks)
                {
                    //Create stream track configuration based on capabilities
                    RTCMediaStreamTrackConfiguration configuration = null;
                    if (mediaStreamTrack.Kind == MediaStreamTrackKind.Audio && audioCapabilities != null)
                    {
                        localAudioTrack = mediaStreamTrack;
                        configuration =
                            await Helper.GetTrackConfigurationForCapabilities(audioCapabilities, AudioCodec);
                    }
                    else if (mediaStreamTrack.Kind == MediaStreamTrackKind.Video && videoCapabilities != null)
                    {
                        localVideoTrack = mediaStreamTrack;
                        configuration =
                            await Helper.GetTrackConfigurationForCapabilities(videoCapabilities, VideoCodec);
                    }
                    if (configuration != null)
                        await _peerConnection.AddTrack(mediaStreamTrack, mediaStreamList, configuration);
                }
            }
#else
            var parameters = new VideoCapturerCreationParameters();
            parameters.Name = _selectedVideoDevice.Name;
            parameters.Id = _selectedVideoDevice.Id;
            var videoCapturer = VideoCapturer.Create(parameters);
#if ENABLE_VIDEO_PROCESSING
            ((VideoCapturer)videoCapturer).OnVideoFrame += (IVideoFrameBufferEvent evt) =>
            {
                Process_VideoFrameBufferEvent(evt);
            };
#endif //ENABLE_VIDEO_PROCESSING

            VideoOptions options = new VideoOptions();
            options.Factory = _factory;
            options.Capturer = videoCapturer;
            options.Constraints = mediaConstraints;

            var videoTrackSource = VideoTrackSource.Create(options);
            _selfVideoTrack = MediaStreamTrack.CreateVideoTrack("SELF_VIDEO", videoTrackSource);

            AudioOptions audioOptions = new AudioOptions();
            audioOptions.Factory = _factory;
            var audioTrackSource = AudioTrackSource.Create(audioOptions);
            _selfAudioTrack = MediaStreamTrack.CreateAudioTrack("SELF_AUDIO", audioTrackSource);
#endif

            if (cancelationToken.IsCancellationRequested)
            {
                return false;
            }

#if !ORTCLIB
            Debug.WriteLine("Conductor: Adding local media tracks.");
            PeerConnection.AddTrack(_selfVideoTrack);
            PeerConnection.AddTrack(_selfAudioTrack);
#endif
            OnAddLocalTrack?.Invoke(_selfVideoTrack);
            OnAddLocalTrack?.Invoke(_selfAudioTrack);
            if (_selfVideoTrack != null)
            {
                if (VideoLoopbackEnabled)
                {
#if !UNITY && !ORTCLIB
                    _selfVideoTrack.Element = Org.WebRtc.MediaElementMaker.Bind(SelfVideo);
                    ((MediaStreamTrack)_selfVideoTrack).OnFrameRateChanged += (float frameRate) =>
                    {
                        FramesPerSecondChanged?.Invoke("SELF", frameRate.ToString("0.0"));
                    };
                    ((MediaStreamTrack)_selfVideoTrack).OnResolutionChanged += (uint width, uint height) =>
                    {
                        ResolutionChanged?.Invoke("SELF", width, height);
                    };
#if ENABLE_VIDEO_PROCESSING
                    ((MediaStreamTrack)_selfVideoTrack).OnVideoFrame += (IVideoFrameBufferEvent evt) =>
                    {
                        Process_VideoFrameBufferEvent(evt);
                    };
#endif //ENABLE_VIDEO_PROCESSING

#endif
                }
            }

            if (cancelationToken.IsCancellationRequested)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Closes a peer connection.
        /// </summary>
        private void ClosePeerConnection()
        {
            lock (MediaLock)
            {
                if (PeerConnection != null)
                {
                    _peerId = -1;

                    PeerConnection.OnIceCandidate -= PeerConnection_OnIceCandidate;
                    PeerConnection.OnTrack -= PeerConnection_OnTrack;
                    PeerConnection.OnRemoveTrack -= PeerConnection_OnRemoveTrack;
                    //_peerConnection.OnConnectionHealthStats -= PeerConnection_OnConnectionHealthStats;

#if !UNITY && !ORTCLIB
                    if (null != _peerVideoTrack) _peerVideoTrack.Element = null; // Org.WebRtc.MediaElementMaker.Bind(obj);
                    if (null != _selfVideoTrack) _selfVideoTrack.Element = null; // Org.WebRtc.MediaElementMaker.Bind(obj);
#endif
                    (_peerVideoTrack as IDisposable)?.Dispose();
                    (_selfVideoTrack as IDisposable)?.Dispose();
                    (_peerAudioTrack as IDisposable)?.Dispose();
                    (_selfAudioTrack as IDisposable)?.Dispose();
                    _peerVideoTrack = null;
                    _selfVideoTrack = null;
                    _peerAudioTrack = null;
                    _selfAudioTrack = null;

                    OnPeerConnectionClosed?.Invoke();

#if ORTCLIB
                    SessionId = null;
                    OrtcStatsManager.Instance.CallEnded();
#endif
                    PeerConnection = null;

                    OnReadyToConnect?.Invoke();
                    
                    GC.Collect(); // Ensure all references are truly dropped.
                }
            }
        }

        public void AddPeer(Peer peer)
        {
            _peers.Add(peer);
        }

        public void RemovePeer(Peer peer)
        {
            _peers.RemoveAll(p => p.Id == peer.Id);
        }

        public List<Peer> GetPeers()
        {
            return new List<Peer>(_peers);
        }

        /// <summary>
        /// Called when WebRTC detects another ICE candidate. 
        /// This candidate needs to be sent to the other peer.
        /// </summary>
        /// <param name="evt">Details about RTC Peer Connection Ice event.</param>
        private void PeerConnection_OnIceCandidate(UseRTCPeerConnectionIceEvent evt)
        {
            if (evt.Candidate == null) // relevant: GlobalObserver::OnIceComplete in Org.WebRtc
            {
                return;
            }

            double index = (double)evt.Candidate.SdpMLineIndex;

            JsonObject json = null;
#if ORTCLIB
            if (RTCPeerConnectionSignalingMode.Json == _signalingMode)
            {
                json = JsonObject.Parse(evt.Candidate.ToJson().ToString());
            }
#else
            {
                json = new JsonObject
                {
                    {kCandidateSdpMidName, JsonValue.CreateStringValue(evt.Candidate.SdpMid)},
                    {kCandidateSdpMlineIndexName, JsonValue.CreateNumberValue(index)},
                    {kCandidateSdpName, JsonValue.CreateStringValue(evt.Candidate.Candidate)}
                };
            }
#endif
            Debug.WriteLine("Conductor: Sending ice candidate:\n" + json?.Stringify());
            SendMessage(json);
        }

#if ORTCLIB
        /// <summary>
        /// Invoked when the remote peer added a media track to the peer connection.
        /// </summary>
        public event Action<IRTCTrackEvent> OnAddRemoteTrack;
        private void PeerConnection_OnAddTrack(IRTCTrackEvent evt)
        {
            OnAddRemoteTrack?.Invoke(evt);
        }

        /// <summary>
        /// Invoked when the remote peer removed a media track from the peer connection.
        /// </summary>
        public event Action<IRTCTrackEvent> OnRemoveTrack;
        private void PeerConnection_OnRemoveTrack(IRTCTrackEvent evt)
        {
            OnRemoveTrack?.Invoke(evt);
        }
#else
        /// <summary>
        /// Invoked when the remote peer added a media stream to the peer connection.
        /// </summary>
        public event Action<UseMediaStreamTrack> OnAddRemoteTrack;
        private void PeerConnection_OnTrack(UseRTCTrackEvent evt)
        {
#if !UNITY && !ORTCLIB
            if (evt.Track.Kind == "video")
            {
                _peerVideoTrack = evt.Track;

                if (_peerVideoTrack != null)
                {
                    _peerVideoTrack.Element = Org.WebRtc.MediaElementMaker.Bind(PeerVideo);
                    ((MediaStreamTrack)_peerVideoTrack).OnFrameRateChanged += (float frameRate) =>
                    {
                        FramesPerSecondChanged?.Invoke("PEER", frameRate.ToString("0.0"));
                    };
                    ((MediaStreamTrack)_peerVideoTrack).OnResolutionChanged += (uint width, uint height) =>
                    {
                        ResolutionChanged?.Invoke("PEER", width, height);
                    };
                }
            }
            else if (evt.Track.Kind == "audio")
            {
                _peerAudioTrack = evt.Track;
            }
#endif
            OnAddRemoteTrack?.Invoke(evt.Track);

#if !UNITY
            SC.callStatsClient?.SendSSRCMap(SC.ssrcDataList);
#endif
        }

        /// <summary>
        /// Invoked when the remote peer removed a media stream from the peer connection.
        /// </summary>
        public event Action<UseMediaStreamTrack> OnRemoveRemoteTrack;
        private void PeerConnection_OnRemoveTrack(UseRTCTrackEvent evt)
        {
#if !UNITY && !ORTCLIB
            if (evt.Track.Kind == "video")
            {
                _peerVideoTrack.Element = null; //Org.WebRtc.MediaElementMaker.Bind(obj)
            }
#endif
            OnRemoveRemoteTrack?.Invoke(evt.Track);
        }

        /// <summary>
        /// Invoked when new connection health stats are available.
        /// Use ToggleConnectionHealthStats to turn on/of the connection health stats.
        /// </summary>
        //public event Action<RTCPeerConnectionHealthStats> OnConnectionHealthStats;
        public event Action<string> OnConnectionHealthStats;
        private void PeerConnection_OnConnectionHealthStats(string stats)
        { 
            OnConnectionHealthStats?.Invoke("");
        }
#endif
            /// <summary>
            /// Private constructor for singleton class.
            /// </summary>
            private Conductor()
        {
#if ORTCLIB
            _signalingMode = RTCPeerConnectionSignalingMode.Json;
//#else
            //_signalingMode = RTCPeerConnectionSignalingMode.Sdp;
#endif
            _signaller = new Signaller();

            Signaller.OnDisconnected += Signaller_OnDisconnected;
            Signaller.OnMessageFromPeer += Signaller_OnMessageFromPeer;
            Signaller.OnPeerConnected += Signaller_OnPeerConnected;
            Signaller.OnPeerHangup += Signaller_OnPeerHangup;
            Signaller.OnPeerDisconnected += Signaller_OnPeerDisconnected;
            Signaller.OnServerConnectionFailure += Signaller_OnServerConnectionFailure;
            Signaller.OnSignedIn += Signaller_OnSignedIn;

            _iceServers = new List<RTCIceServer>();
        }

        /// <summary>
        /// Handler for Signaller's OnPeerHangup event.
        /// </summary>
        /// <param name="peerId">ID of the peer to hung up the call with.</param>
        void Signaller_OnPeerHangup(int peerId)
        {
#if !UNITY
            SC.callStatsClient?.SendUserLeft();
#endif

            if (peerId != _peerId) return;

            Debug.WriteLine("Conductor: Our peer hung up.");

#if !UNITY
            if (SC.callStatsClient == null)
                ClosePeerConnection();
            else
                PeerConnection = null;
#else
            ClosePeerConnection();
#endif
        }

        /// <summary>
        /// Handler for Signaller's OnSignedIn event.
        /// </summary>
        private void Signaller_OnSignedIn()
        {
        }

        /// <summary>
        /// Handler for Signaller's OnServerConnectionFailure event.
        /// </summary>
        private void Signaller_OnServerConnectionFailure()
        {
            Debug.WriteLine("[Error]: Connection to server failed!");


#if !UNITY
            SC.callStatsClient?.SendApplicationErrorLogs("error", "Connection to server failed!", "text");
#endif
        }

        /// <summary>
        /// Handler for Signaller's OnPeerDisconnected event.
        /// </summary>
        /// <param name="peerId">ID of disconnected peer.</param>
        private void Signaller_OnPeerDisconnected(int peerId)
        {
            // is the same peer or peer_id does not exist (0) in case of 500 Error
            if (peerId != _peerId && peerId != 0) return;

            Debug.WriteLine("Conductor: Our peer disconnected.");
            ClosePeerConnection();
        }

        /// <summary>
        /// Handler for Signaller's OnPeerConnected event.
        /// </summary>
        /// <param name="id">ID of the connected peer.</param>
        /// <param name="name">Name of the connected peer.</param>
        private void Signaller_OnPeerConnected(int id, string name)
        {
        }

        /// <summary>
        /// Handler for Signaller's OnMessageFromPeer event.
        /// </summary>
        /// <param name="peerId">ID of the peer.</param>
        /// <param name="message">Message from the peer.</param>
        private void Signaller_OnMessageFromPeer(int peerId, string message)
        {
            Task.Run(async () =>
            {
                Debug.Assert(_peerId == peerId || _peerId == -1);
                Debug.Assert(message.Length > 0);

                if (_peerId != peerId && _peerId != -1)
                {
                    Debug.WriteLine("[Error] Conductor: Received a message from unknown peer while already in a conversation with a different peer.");

#if !UNITY
                    SC.callStatsClient?.SendApplicationErrorLogs("error", "Received a message from unknown peer while already in a conversation with a different peer", "text");
#endif

                    return;
                }

                if (!JsonObject.TryParse(message, out JsonObject jMessage))
                {
                    Debug.WriteLine("[Error] Conductor: Received unknown message." + message);

#if !UNITY
                    SC.callStatsClient?.SendApplicationErrorLogs("error", "Received unknown message.", "text");
#endif

                    return;
                }

                string type = jMessage.ContainsKey(kSessionDescriptionTypeName) ? jMessage.GetNamedString(kSessionDescriptionTypeName) : null;
#if ORTCLIB
                bool created = false;
#endif
                if (PeerConnection == null)
                {
                    if (!IsNullOrEmpty(type))
                    {
                        // Create the peer connection only when call is
                        // about to get initiated. Otherwise ignore the
                        // messages from peers which could be a result
                        // of old (but not yet fully closed) connections.
                        if (type == "offer" || type == "answer" || type == "json")
                        {
                            Debug.Assert(_peerId == -1);
                            _peerId = peerId;              

                            IEnumerable<Peer> enumerablePeer = _peers.Where(x => x.Id == peerId);
                            _peer = enumerablePeer.First();
#if ORTCLIB
                            created = true;
                            _signalingMode = Helper.SignalingModeForClientName(Peer.Name);
#endif
                            _connectToPeerCancelationTokenSource = new CancellationTokenSource();
                            bool connectResult = await CreatePeerConnection(_connectToPeerCancelationTokenSource.Token);
                            _connectToPeerTask = null;
                            _connectToPeerCancelationTokenSource.Dispose();
                            if (!connectResult)
                            {
                                Debug.WriteLine("[Error] Conductor: Failed to initialize our PeerConnection instance");

#if !UNITY
                                SC.callStatsClient?.SendApplicationErrorLogs("error", "Failed to initialize our PeerConnection instance.", "text");
#endif

                                await Signaller.SignOut();
                                return;
                            }
                            else if (_peerId != peerId)
                            {
                                Debug.WriteLine("[Error] Conductor: Received a message from unknown peer while already in a conversation with a different peer.");

#if !UNITY
                                SC.callStatsClient?.SendApplicationErrorLogs("error", "Received a message from unknown peer while already in a conversation with a different peer.", "text");
#endif

                                return;
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[Warn] Conductor: Received an untyped message after closing peer connection.");
                        return;
                    }
                }

                if (PeerConnection != null && !IsNullOrEmpty(type))
                {
                    if (type == "offer-loopback")
                    {
                        // Loopback not supported
                        Debug.Assert(false);
                    }
                    string sdp = null;
#if ORTCLIB
                    if (jMessage.ContainsKey(kSessionDescriptionJsonName))
                    {
                        var containerObject = new JsonObject { { kSessionDescriptionJsonName, jMessage.GetNamedObject(kSessionDescriptionJsonName) } };
                        sdp = containerObject.Stringify();
                    }
                    else if (jMessage.ContainsKey(kSessionDescriptionSdpName))
                    {
                        sdp = jMessage.GetNamedString(kSessionDescriptionSdpName);
                    }
#else
                    sdp = jMessage.ContainsKey(kSessionDescriptionSdpName) ? jMessage.GetNamedString(kSessionDescriptionSdpName) : null;
#endif
                    if (IsNullOrEmpty(sdp))
                    {
                        Debug.WriteLine("[Error] Conductor: Can't parse received session description message.");

#if !UNITY
                        SC.callStatsClient?.SendFabricSetupFailed("sendrecv", "peer", "NegotiationFailure", "IsNullOrEmpty(sdp)", "Can't parse received session description message.", Empty);

                        SC.callStatsClient?.SendApplicationErrorLogs("error", "Can't parse received session description message.", "text");
#endif

                        return;
                    }

#if !UNITY
                    SC.SSRCMapDataSetup(sdp, "inbound", "remote");

                    SC.callStatsClient?.SendSDP(_localSDPForCallStats, sdp);
#endif

                    Debug.WriteLine("Conductor: Received session description:\n" + message);
#if ORTCLIB
                    RTCSessionDescriptionSignalingType messageType = RTCSessionDescriptionSignalingType.SdpOffer;
                    switch (type)
                    {
                        case "json": messageType = RTCSessionDescriptionSignalingType.Json; break;
                        case "offer": messageType = RTCSessionDescriptionSignalingType.SdpOffer; break;
                        case "answer": messageType = RTCSessionDescriptionSignalingType.SdpAnswer; break;
                        case "pranswer": messageType = RTCSessionDescriptionSignalingType.SdpPranswer; break;
                        default: Debug.Assert(false, type); break;
                    }
                    var description = new RTCSessionDescription(messageType, sdp);
#else
                    RTCSdpType messageType = RTCSdpType.Offer;
                    switch (type)
                    {
                        case "offer": messageType = RTCSdpType.Offer; break;
                        case "answer": messageType = RTCSdpType.Answer; break;
                        case "pranswer": messageType = RTCSdpType.Pranswer; break;
                        default: Debug.Assert(false, type); break;
                    }
                    RTCSessionDescriptionInit sdpInit = new RTCSessionDescriptionInit();
                    sdpInit.Sdp = sdp;
                    sdpInit.Type = messageType;
                    var description = new RTCSessionDescription(sdpInit);
#endif
                    await PeerConnection.SetRemoteDescription(description);

#if ORTCLIB
                    if ((messageType == RTCSessionDescriptionSignalingType.SdpOffer) ||
                        ((created) && (messageType == RTCSessionDescriptionSignalingType.Json)))
#else
                    if (messageType == RTCSdpType.Offer)
#endif
                    {
                        RTCAnswerOptions answerOptions = new RTCAnswerOptions();
                        var answer = await PeerConnection.CreateAnswer(answerOptions);
                        await PeerConnection.SetLocalDescription(answer);
                        // Send answer
                        SendSdp(answer);
#if ORTCLIB
                        OrtcStatsManager.Instance.StartCallWatch(SessionId, false);
#endif
                    }
                }
                else
                {
                    RTCIceCandidate candidate = null;
#if ORTCLIB
                    if (RTCPeerConnectionSignalingMode.Json != _signalingMode)
#endif
                    {
                        var sdpMid = jMessage.ContainsKey(kCandidateSdpMidName)
                            ? jMessage.GetNamedString(kCandidateSdpMidName)
                            : null;
                        var sdpMlineIndex = jMessage.ContainsKey(kCandidateSdpMlineIndexName)
                            ? jMessage.GetNamedNumber(kCandidateSdpMlineIndexName)
                            : -1;
                        var sdp = jMessage.ContainsKey(kCandidateSdpName)
                            ? jMessage.GetNamedString(kCandidateSdpName)
                            : null;
                        //TODO: Check is this proper condition ((String.IsNullOrEmpty(sdpMid) && (sdpMlineIndex == -1)) || String.IsNullOrEmpty(sdp))
                        if (IsNullOrEmpty(sdpMid) || sdpMlineIndex == -1 || IsNullOrEmpty(sdp))
                        {
                            Debug.WriteLine("[Error] Conductor: Can't parse received message.\n" + message);

#if !UNITY
                            SC.callStatsClient?.SendApplicationErrorLogs("error", $"Can't parse received message.\n {message}", "text");
#endif

                            return;
                        }
#if ORTCLIB
                        candidate = IsNullOrEmpty(sdpMid) ? RTCIceCandidate.FromSdpStringWithMLineIndex(sdp, (ushort)sdpMlineIndex) : RTCIceCandidate.FromSdpStringWithMid(sdp, sdpMid);
#else
                        RTCIceCandidateInit candidateInit = new RTCIceCandidateInit();
                        candidateInit.Candidate = sdp;
                        candidateInit.SdpMid = sdpMid;
                        candidateInit.SdpMLineIndex = (ushort)sdpMlineIndex;
                        candidate = new RTCIceCandidate(candidateInit);
#endif
                    }
#if ORTCLIB
                    else
                    {
                        candidate = RTCIceCandidate.WithJson(new Json(message));
                    }
                    _peerConnection?.AddIceCandidate(candidate);
#else
                    await PeerConnection.AddIceCandidate(candidate);
#endif


                    Debug.WriteLine("Conductor: Receiving ice candidate:\n" + message);
                }
            }).Wait();
        }

        /// <summary>
        /// Handler for Signaller's OnDisconnected event handler.
        /// </summary>
        private void Signaller_OnDisconnected()
        {
            ClosePeerConnection();
        }

        /// <summary>
        /// Starts the login to server process.
        /// </summary>
        /// <param name="server">The host server.</param>
        /// <param name="port">The port to connect to.</param>
        public void StartLogin(string server, string port)
        {
            if (_signaller.IsConnected())
            {
                return;
            }
            _signaller.Connect(server, port, GetLocalPeerName());
        }
       
        /// <summary>
        /// Calls to disconnect the user from the server.
        /// </summary>
        public async Task DisconnectFromServer()
        {
            if (_signaller.IsConnected())
            {
                await _signaller.SignOut();
                _peers.Clear();
            }
        }

        /// <summary>
        /// Calls to connect to the selected peer.
        /// </summary>
        /// <param name="peer">Peer to connect to.</param>
        public async void ConnectToPeer(Peer peer)
        {
            Debug.Assert(peer != null);
            Debug.Assert(_peerId == -1);

            if (PeerConnection != null)
            {
                Debug.WriteLine("[Error] Conductor: We only support connecting to one peer at a time");

#if !UNITY
                SC.callStatsClient?.SendApplicationErrorLogs("error", "We only support connecting to one peer at a time", "text");
#endif

                return;
            }
#if ORTCLIB
            _signalingMode = Helper.SignalingModeForClientName(peer.Name);
#endif
            _connectToPeerCancelationTokenSource = new System.Threading.CancellationTokenSource();
            bool connectResult = await CreatePeerConnection(_connectToPeerCancelationTokenSource.Token);
            _connectToPeerTask = null;
            _connectToPeerCancelationTokenSource.Dispose();
            if (connectResult)
            {
                _peerId = peer.Id;
                var offerOptions = new RTCOfferOptions();
                offerOptions.OfferToReceiveAudio = true;
                offerOptions.OfferToReceiveVideo = true;
                var offer = await PeerConnection.CreateOffer(offerOptions);

                if (IsNullOrEmpty(offer.Sdp))
                {
#if !UNITY
                    SC.callStatsClient?.SendFabricSetupFailed("sendrecv", "peer", "SDPGenerationError", "IsNullOrEmpty(sdp)", "Can't parse received session description message.", Empty);
#endif
                }
#if ORTCLIB
                var modifiedOffer = offer;
#else
                // Alter sdp to force usage of selected codecs
                string modifiedSdp = offer.Sdp;
                SdpUtils.SelectCodecs(ref modifiedSdp, AudioCodec.PreferredPayloadType, VideoCodec.PreferredPayloadType);
                RTCSessionDescriptionInit sdpInit = new RTCSessionDescriptionInit();
                sdpInit.Sdp = modifiedSdp;
                sdpInit.Type = offer.SdpType;
                var modifiedOffer = new RTCSessionDescription(sdpInit);
#endif
                await PeerConnection.SetLocalDescription(modifiedOffer);
                Debug.WriteLine("Conductor: Sending offer:\n" + modifiedOffer.Sdp);
                SendSdp(modifiedOffer);

                _localSDPForCallStats = offer.Sdp;

#if !UNITY
                SC.SSRCMapDataSetup(offer.Sdp, "outbound", "local");
#endif
#if ORTCLIB
                OrtcStatsManager.Instance.StartCallWatch(SessionId, true);
#endif
            }
        }

        /// <summary>
        /// Calls to disconnect from peer.
        /// </summary>
        public async Task DisconnectFromPeer()
        {
            await SendHangupMessage();

#if !UNITY
            if (SC.callStatsClient == null)
                ClosePeerConnection();
            else
                PeerConnection = null;
#else
            ClosePeerConnection();
#endif
        }

        /// <summary>
        /// Constructs and returns the local peer name.
        /// </summary>
        /// <returns>The local peer name.</returns>
        private string GetLocalPeerName()
        {
            var hostname = NetworkInformation.GetHostNames().FirstOrDefault(h => h.Type == HostNameType.DomainName);
            string ret = hostname?.CanonicalName ?? "<unknown host>";
#if ORTCLIB
            ret = ret + "-dual";
#endif
            return ret;
        }

        /// <summary>
        /// Sends SDP message.
        /// </summary>
        /// <param name="description">RTC session description.</param>
        private void SendSdp(UseRTCSessionDescription description)
        {
            JsonObject json = null;
            Debug.WriteLine("Conductor: Sent session description: " + description.Sdp);
#if ORTCLIB
            var type = description.Type.ToString().ToLower();
            string formattedDescription = description.FormattedDescription;

            if (description.Type == RTCSessionDescriptionSignalingType.Json)
            {
                if (IsNullOrEmpty(SessionId))
                {
                    var match = Regex.Match(formattedDescription, "{\"username\":\"-*[a-zA-Z0-9]*\",\"id\":\"([0-9]+)\"");
                    if (match.Success)
                    {
                        SessionId = match.Groups[1].Value;
                    }
                }
                var jsonDescription = JsonObject.Parse(formattedDescription);
                var sessionValue = jsonDescription.GetNamedObject(kSessionDescriptionJsonName);
                json = new JsonObject
                {
                    {kSessionDescriptionTypeName, JsonValue.CreateStringValue(type)},
                    {kSessionDescriptionJsonName,  sessionValue}
                };
            }
            else
            {
                var match = Regex.Match(formattedDescription, "o=[^ ]+ ([0-9]+) [0-9]+ [a-zA-Z]+ [a-zA-Z0-9]+ [0-9\\.]+");
                if (match.Success)
                {
                    SessionId = match.Groups[1].Value;
                }

                var prefix = type.Substring(0, "sdp".Length);
                if (prefix == "sdp")
                {
                    type = type.Substring("sdp".Length);
                }

                json = new JsonObject
                {
                    {kSessionDescriptionTypeName, JsonValue.CreateStringValue(type)},
                    {kSessionDescriptionSdpName, JsonValue.CreateStringValue(formattedDescription)}
                };
            }
#else
            json = new JsonObject();
            string messageType = null;
            switch (description.SdpType)
            {
                case RTCSdpType.Offer: messageType = "offer"; break;
                case RTCSdpType.Answer: messageType = "answer"; break;
                case RTCSdpType.Pranswer: messageType = "pranswer"; break;
                default: Debug.Assert(false, description.SdpType.ToString()); break;
            }

            json = new JsonObject
            {
                { kSessionDescriptionTypeName, JsonValue.CreateStringValue(messageType) },
                { kSessionDescriptionSdpName, JsonValue.CreateStringValue(description.Sdp) }
            };
#endif
            SendMessage(json);
        }

        /// <summary>
        /// Helper method to send a message to a peer.
        /// </summary>
        /// <param name="json">Message body.</param>
        private void SendMessage(IJsonValue json)
        {
            // Don't await, send it async.
            var task = _signaller.SendToPeer(_peerId, json);
        }

        /// <summary>
        /// Helper method to send a hangup message to a peer.
        /// </summary>
        private async Task SendHangupMessage()
        {
            await _signaller.SendToPeer(_peerId, "BYE");
        }

        /// <summary>
        /// Enables the local video stream.
        /// </summary>
        public void EnableLocalVideoStream()
        {
            lock (MediaLock)
            {
                if (_selfVideoTrack != null)
                {
                    _selfVideoTrack.Enabled = true;
                }
                VideoEnabled = true;
            }
        }

        /// <summary>
        /// Disables the local video stream.
        /// </summary>
        public void DisableLocalVideoStream()
        {
            lock (MediaLock)
            {
                if (_selfVideoTrack != null)
                {
                    _selfVideoTrack.Enabled = false;
                }
                VideoEnabled = false;
            }
        }

        /// <summary>
        /// Mutes the microphone.
        /// </summary>
        public void MuteMicrophone()
        {
            lock (MediaLock)
            {
                if (_selfAudioTrack != null)
                {
                    _selfAudioTrack.Enabled = false;
                }
                AudioEnabled = false;

#if !UNITY
                SC.callStatsClient?.SendMediaAction("audioMute", "", SC.remoteIceCandidates);
#endif
            }
        }

        /// <summary>
        /// Unmutes the microphone.
        /// </summary>
        public void UnmuteMicrophone()
        {
            lock (MediaLock)
            {
                if (_selfAudioTrack != null)
                {
                    _selfAudioTrack.Enabled = true;
                }
                AudioEnabled = true;

#if !UNITY
                SC.callStatsClient?.SendMediaAction("audioUnmute", "", SC.remoteIceCandidates);
#endif
            }
        }

        /// <summary>
        /// Receives a new list of Ice servers and updates the local list of servers.
        /// </summary>
        /// <param name="iceServers">List of Ice servers to configure.</param>
        public void ConfigureIceServers(List<IceServer> iceServers)
        {
            _iceServers.Clear();
            foreach(IceServer iceServer in iceServers)
            {
                //Url format: stun:stun.l.google.com:19302
                string url = "stun:";
                if (iceServer.Type == IceServer.ServerType.TURN)
                {
                    url = "turn:";
                }
                RTCIceServer server = null;
                url += iceServer.Host;
                server = new RTCIceServer { Urls = new List<string> { url } };
                if (iceServer.Credential != null)
                {
                    server.Credential = iceServer.Credential;
                }
                if (iceServer.Username != null)
                {
                    server.Username = iceServer.Username;
                }
                _iceServers.Add(server);
            }
        }

        /// <summary>
        /// If a connection to a peer is establishing, requests it's
        /// cancellation and wait the operation to cancel (blocks current
        /// thread).
        /// </summary>
        public void CancelConnectingToPeer()
        {
            if(_connectToPeerTask != null)
            {
                Debug.WriteLine("Conductor: Connecting to peer in progress, canceling");
                _connectToPeerCancelationTokenSource.Cancel();
                _connectToPeerTask.Wait();
                Debug.WriteLine("Conductor: Connecting to peer flow canceled");
            }
        }

        private void DisplayAudioBufferEvent(
            ref int totalSamples,
            String type,
            Org.WebRtc.IAudioBufferEvent evt)
        {
            var buffer = evt.Buffer;
            var data = buffer.Channel(0);

            var dataArray = new Int16[data.Length];
            data.GetData(dataArray);

#if DISPLAY_AUDIO_BUFFER_OUTPUT
            var sb = new StringBuilder();

            sb.Append("== AUDIO FRAME PROCESSING ==\n");
            sb.Append("Type: " + type + "\n");
            sb.Append("Activity: " + buffer.Activity.ToString() + "\n");
            sb.Append("Channels: " + buffer.Channels.ToString() + "\n");
            sb.Append("Bands: " + buffer.Bands.ToString() + "\n");
            sb.Append("SamplesPerChannel: " + buffer.SamplesPerChannel.ToString() + "\n");
            sb.Append("FramesPerBand: " + buffer.FramesPerBand.ToString() + "\n");

            sb.Append("Samples: ");
#endif // DISPLAY_AUDIO_BUFFER_OUTPUT

            double scale = Math.Pow(2, 10);

            for (int index = 0; index < dataArray.Count(); ++index, ++totalSamples)
            {
#if DISPLAY_AUDIO_BUFFER_OUTPUT
                sb.Append(dataArray[index] + ",");
#endif // DISPLAY_AUDIO_BUFFER_OUTPUT
                dataArray[index] = (Int16)(Math.Sin(totalSamples) * scale);
            }

            data.SetData(dataArray);

#if DISPLAY_AUDIO_BUFFER_OUTPUT
            sb.Append("\n== AUDIO FRAME PROCESSING ==\n");
            Debug.WriteLine(sb.ToString());
#endif // DISPLAY_AUDIO_BUFFER_OUTPUT
        }

        private void Factory_HandleAudioPostCaptureInit(Org.WebRtc.IAudioProcessingInitializeEvent evt)
        {
            (evt as IDisposable).Dispose();
        }

        private void Factory_HandleAudioPostCaptureRuntimeSetting(Org.WebRtc.IAudioProcessingRuntimeSettingEvent evt)
        {
            (evt as IDisposable).Dispose();
        }

        private static int totalCaptureSamples_ = 0;
        private void Factory_HandleAudioPostCaptureBuffer(Org.WebRtc.IAudioBufferEvent evt)
        {
            DisplayAudioBufferEvent(ref totalCaptureSamples_, "post capture", evt);
            (evt as IDisposable).Dispose();
        }

        private void Factory_HandleAudioPreRenderInit(Org.WebRtc.IAudioProcessingInitializeEvent evt)
        {
            (evt as IDisposable).Dispose();
        }

        private void Factory_HandleAudioPreRenderRuntimeSetting(Org.WebRtc.IAudioProcessingRuntimeSettingEvent evt)
        {
            (evt as IDisposable).Dispose();
        }

        private static int totalRenderSamples_ = 0;
        private void Factory_HandleAudioPreRenderBuffer(Org.WebRtc.IAudioBufferEvent evt)
        {
            DisplayAudioBufferEvent(ref totalRenderSamples_, "pre-render", evt);
            (evt as IDisposable).Dispose();
        }

        private void Process_VideoFrameBufferEvent(IVideoFrameBufferEvent evt)
        {
            var buffer = evt?.Buffer;
            var i420Frame = buffer?.YuvFrame;
            if (null == i420Frame)
                i420Frame = buffer?.ToI420();
            var nativeFrame = buffer?.NativeFrame;
            var mediaSample = nativeFrame?.Sample;

            int width = 0;
            int height = 0;
            if (null != nativeFrame)
            {
                width = nativeFrame.Width;
                height = nativeFrame.Height;
            }
            if (null != i420Frame)
            {
                width = i420Frame.Width;
                height = i420Frame.Height;
            }

            var y = i420Frame?.Y;
            var u = i420Frame?.U;
            var v = i420Frame?.V;

            var array = new IVideoData[] { y, u, v };

            foreach (var colorSpace in array) {
                if (null == colorSpace)
                    continue;

                byte[] buffer8bit = null;
                ushort[] buffer16bit = null;
                Windows.Foundation.IMemoryBuffer bits8View;
                Windows.Foundation.IMemoryBuffer bits16View;

                var hasBit8 = colorSpace.Is8BitColorSpace;
                if (hasBit8)
                {
                    // copy the data
                    buffer8bit = new byte[colorSpace.Length];
                    colorSpace.GetData8bit(buffer8bit);
                    // or get a buffer view
                    bits8View = colorSpace.Data8bit;
                }

                var hasBit16 = colorSpace.Is16BitColorSpace;
                if (hasBit16)
                {
                    // copy the data
                    buffer16bit = new ushort[colorSpace.Length];
                    colorSpace.GetData16bit(buffer16bit);
                    // or get a buffer view
                    bits16View = colorSpace.Data16bit;
                }
            }

            IReadOnlyList<float> viewTransform = mediaSample?.GetCameraViewTransform();
            IReadOnlyList<float> projectionTransform = mediaSample?.GetCameraProjectionTransform();

            (mediaSample as IDisposable)?.Dispose();
            (buffer as IDisposable)?.Dispose();
            (nativeFrame as IDisposable)?.Dispose();
            (evt as IDisposable)?.Dispose();
        }

    }
}
