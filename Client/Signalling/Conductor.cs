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
using PeerConnectionClient.Model;
using System.Collections.ObjectModel;
using System.Threading;
using System.Text.RegularExpressions;
using static System.String;
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
using PeerConnectionClient.Stats;
#if USE_CX_VERSION
using UseMediaStreamTrack = Org.WebRtc.MediaStreamTrack;
using UseRTCPeerConnectionIceEvent = Org.WebRtc.RTCPeerConnectionIceEvent;
using UseRTCTrackEvent = Org.WebRtc.RTCTrackEvent;
using UseRTCSessionDescription = Org.WebRtc.RTCSessionDescription;
using UseConstraint = Org.WebRtc.Constraint;
using UseMediaConstraints = Org.WebRtc.MediaConstraints;
#else
using UseMediaStreamTrack = Org.WebRtc.IMediaStreamTrack;
using UseRTCPeerConnectionIceEvent = Org.WebRtc.IRTCPeerConnectionIceEvent;
using UseRTCTrackEvent = Org.WebRtc.IRTCTrackEvent;
using UseRTCSessionDescription = Org.WebRtc.IRTCSessionDescription;
using UseConstraint = Org.WebRtc.IConstraint;
using UseMediaConstraints = Org.WebRtc.IMediaConstraints;
#endif
#endif

namespace PeerConnectionClient.Signalling
{
    /// <summary>
    /// A singleton conductor for WebRTC session.
    /// </summary>
    internal class Conductor
    {
        //public CallStatsClient callStatsClient = new CallStatsClient();

        private static readonly StatsController SC = StatsController.Instance;

        private string _localSDPForCallStats;

        private static readonly object InstanceLock = new object();
        private static Conductor _instance;
#if ORTCLIB
        private RTCPeerConnectionSignalingMode _signalingMode;
#endif

        /// <summary>
        /// Represents video camera capture capabilities.
        /// </summary>
        public class CaptureCapability
        {
            /// <summary>
            /// Gets the width in pixes of a video capture device capibility.
            /// </summary>
            public uint Width { get; set; }

            /// <summary>
            /// Gets the height in pixes of a video capture device capibility.
            /// </summary>
            public uint Height { get; set; }

            /// <summary>
            /// Gets the frame rate in frames per second of a video capture device capibility.
            /// </summary>
            public uint FrameRate { get; set; }

            /// <summary>
            /// Get the aspect ratio of the pixels of a video capture device capibility.
            /// </summary>
            public Windows.Media.MediaProperties.MediaRatio PixelAspectRatio { get; set; }

            /// <summary>
            /// Get a displayable string describing all the features of a
            /// video capture device capability. Displays resolution, frame rate,
            /// and pixel aspect ratio.
            /// </summary>
            public string FullDescription { get; set; }

            /// <summary>
            /// Get a displayable string describing the resolution of a
            /// video capture device capability.
            /// </summary>
            public string ResolutionDescription { get; set; }

            /// <summary>
            /// Get a displayable string describing the frame rate in
            // frames per second of a video capture device capability.
            /// </summary>
            public string FrameRateDescription { get; set; }
        }

#if !ORTCLIB
        /// <summary>
        /// Represents a local media device, such as a microphone or a camera.
        /// </summary>
        public class MediaDevice
        {
            /// <summary>
            /// Gets or sets an identifier of the media device.
            /// This value defaults to a unique OS assigned identifier of the media device.
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// Get or sets a displayable name that describes the media device.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the location of the media device.
            /// </summary>
            public EnclosureLocation Location { get; set; }

            /// <summary>
            /// Retrieves video capabilities for a given device.
            /// </summary>
            /// <returns>This is an asynchronous method. The result is a vector of the
            /// capabilities supported by the video device.</returns>
            public IAsyncOperation<IList<CaptureCapability>> GetVideoCaptureCapabilities()
            {
                if (Id == null)
                    return null;

                MediaCapture mediaCapture = new MediaCapture();
                MediaCaptureInitializationSettings mediaSettings =
                    new MediaCaptureInitializationSettings();
                mediaSettings.VideoDeviceId = Id;

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
                            FrameRateDescription = $"{frameRate} fps",
                            ResolutionDescription = $"{property.Width} x {property.Height}"
                        });
                    }
                    return capabilityList;
                }).AsAsyncOperation<IList<CaptureCapability>>();
            }
        }
#endif

        public enum MediaDeviceType
        {
            AudioCapture,
            AudioPlayout,
            VideoCapture
        };

#if !ORTCLIB
        public class CodecInfo
        {
            public byte PreferredPayloadType { get; set; }
            public string Name { get; set; }
            public int ClockRate { get; set; }
        }
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
        RTCPeerConnection _peerConnection;

        MediaDevice _selectedVideoDevice = null;

        public ObservableCollection<Peer> Peers;
        public Peer Peer;
        readonly List<RTCIceServer> _iceServers;

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
                if (_peerConnection != null)
                {
                    //_peerConnection.EtwStatsEnabled = value;
                }
#endif
            }
        }

        bool _peerConnectionStatsEnabled;

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
                if (_peerConnection != null)
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
                    Location = deviceInfo.Info.EnclosureLocation
#endif
                });
            }
            return deviceList;
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
            Debug.Assert(_peerConnection == null);
            if(cancelationToken.IsCancellationRequested)
            {
                return false;
            }
            
            var config = new RTCConfiguration()
            {
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
            _peerConnection = new RTCPeerConnection(config);

            await SC.callStatsClient.SendStartCallStats();

            _peerConnection.OnIceGatheringStateChange += async() =>
            {
                await PeerConnectionStateChange.StatsOnIceGatheringStateChange(_peerConnection);

                Debug.WriteLine("Conductor: Ice connection state change, gathering-state=" + _peerConnection.IceGatheringState.ToString().ToLower());
            };

            _peerConnection.OnIceConnectionStateChange += async () =>
            {
                if (_peerConnection != null)
                    await PeerConnectionStateChange.StatsOnIceConnectionStateChange(_peerConnection);
                else
                {
                    await PeerConnectionStateChange.PeerConnectionClosedStateChange();
                    ClosePeerConnection();
                }

                // TODO: Add to GUI
                //await callStatsClient.SendConferenceUserFeedback(4, 3, 5, Empty);

                Debug.WriteLine("Conductor: Ice connection state change, state=" + (null != _peerConnection ? _peerConnection.IceConnectionState.ToString().ToLower() : "closed"));
            };

            if (_peerConnection == null)
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

            _peerConnection.OnIceCandidate += PeerConnection_OnIceCandidate;
#if ORTCLIB
            _peerConnection.OnTrack += PeerConnection_OnAddTrack;
            _peerConnection.OnTrackGone += PeerConnection_OnRemoveTrack;
            _peerConnection.OnIceConnectionStateChange += () => { Debug.WriteLine("Conductor: Ice connection state change, state=" + (null != _peerConnection ? _peerConnection.IceConnectionState.ToString() : "closed")); };
#else
            _peerConnection.OnTrack += PeerConnection_OnTrack;
            _peerConnection.OnRemoveTrack += PeerConnection_OnRemoveTrack;
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
            var videoCapturer = VideoCapturer.Create(_selectedVideoDevice.Name, _selectedVideoDevice.Id);
            var videoTrackSource = VideoTrackSource.Create(videoCapturer, mediaConstraints);
            var localVideoTrack = MediaStreamTrack.CreateVideoTrack("SELF_VIDEO", videoTrackSource);

            AudioOptions audioOptions = new AudioOptions();
            var audioTrackSource = AudioTrackSource.Create(audioOptions);
            var localAudioTrack = MediaStreamTrack.CreateAudioTrack("SELF_AUDIO", audioTrackSource);
#endif

            if (cancelationToken.IsCancellationRequested)
            {
                return false;
            }

#if !ORTCLIB
            Debug.WriteLine("Conductor: Adding local media tracks.");
            _peerConnection.AddTrack(localVideoTrack);
            _peerConnection.AddTrack(localAudioTrack);
#endif
            OnAddLocalTrack?.Invoke(localVideoTrack);
            OnAddLocalTrack?.Invoke(localAudioTrack);

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
                if (_peerConnection != null)
                {
                    _peerId = -1;

                    OnPeerConnectionClosed?.Invoke();

#if !USE_CX_VERSION
                    (_peerConnection as IDisposable)?.Dispose();
#endif

                    SessionId = null;
#if ORTCLIB
                    OrtcStatsManager.Instance.CallEnded();
#endif
                    _peerConnection = null;

                    OnReadyToConnect?.Invoke();

                    
                    GC.Collect(); // Ensure all references are truly dropped.
                }
            }
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

            double index = null != evt.Candidate.SdpMLineIndex ? (double)evt.Candidate.SdpMLineIndex : -1;

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
        private async void PeerConnection_OnTrack(UseRTCTrackEvent evt)
        {
            OnAddRemoteTrack?.Invoke(evt.Track);

            await SC.callStatsClient.SendSSRCMap();
        }

        /// <summary>
        /// Invoked when the remote peer removed a media stream from the peer connection.
        /// </summary>
        public event Action<UseMediaStreamTrack> OnRemoveRemoteTrack;
        private void PeerConnection_OnRemoveTrack(UseRTCTrackEvent evt)
        {
            OnRemoveRemoteTrack?.Invoke(evt.Track);
        }

        /// <summary>
        /// Invoked when new connection health stats are available.
        /// Use ToggleConnectionHealthStats to turn on/of the connection health stats.
        /// </summary>
        //public event Action<RTCPeerConnectionHealthStats> OnConnectionHealthStats;
        public event Action<String> OnConnectionHealthStats;
        //private void PeerConnection_OnConnectionHealthStats(RTCPeerConnectionHealthStats stats)
        private void PeerConnection_OnConnectionHealthStats(String stats)
        {
            OnConnectionHealthStats?.Invoke(stats);
        }
#endif
        /// <summary>
        /// Private constructor for singleton class.
        /// </summary>
        private Conductor()
        {
            Config.AppSettings();
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
        private void Signaller_OnPeerHangup(int peerId)
        {
            SC.callStatsClient.SendUserLeft();

            if (peerId != _peerId) return;

            _peerConnection = null;

            Debug.WriteLine("Conductor: Our peer hung up.");
            //ClosePeerConnection();
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

            SC.callStatsClient.SendApplicationErrorLogs("error", "Connection to server failed!", "text");
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

                    SC.callStatsClient.SendApplicationErrorLogs("error", "Received a message from unknown peer while already in a conversation with a different peer", "text");

                    return;
                }

                JsonObject jMessage;
                if (!JsonObject.TryParse(message, out jMessage))
                {
                    Debug.WriteLine("[Error] Conductor: Received unknown message." + message);

                    SC.callStatsClient.SendApplicationErrorLogs("error", "Received unknown message.", "text");

                    return;
                }

                string type = jMessage.ContainsKey(kSessionDescriptionTypeName) ? jMessage.GetNamedString(kSessionDescriptionTypeName) : null;
#if ORTCLIB
                bool created = false;
#endif
                if (_peerConnection == null)
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

                            IEnumerable<Peer> enumerablePeer = Peers.Where(x => x.Id == peerId);
                            Peer = enumerablePeer.First();
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

                                SC.callStatsClient.SendApplicationErrorLogs("error", "Failed to initialize our PeerConnection instance.", "text");

                                await Signaller.SignOut();
                                return;
                            }
                            else if (_peerId != peerId)
                            {
                                Debug.WriteLine("[Error] Conductor: Received a message from unknown peer while already in a conversation with a different peer.");

                                SC.callStatsClient.SendApplicationErrorLogs("error", "Received a message from unknown peer while already in a conversation with a different peer.", "text");

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

                if (_peerConnection != null && !IsNullOrEmpty(type))
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

                        SC.callStatsClient.SendFabricSetupFailed("NegotiationFailure", "IsNullOrEmpty(sdp)", "Can't parse received session description message.", Empty);

                        SC.callStatsClient.SendApplicationErrorLogs("error", "Can't parse received session description message.", "text");

                        return;
                    }

                    SC.callStatsClient.SSRCMapDataSetup(sdp, "inbound", "remote");

                    SC.callStatsClient.SendSDP(_localSDPForCallStats, sdp);

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
                    await _peerConnection.SetRemoteDescription(description);

#if ORTCLIB
                    if ((messageType == RTCSessionDescriptionSignalingType.SdpOffer) ||
                        ((created) && (messageType == RTCSessionDescriptionSignalingType.Json)))
#else
                    if (messageType == RTCSdpType.Offer)
#endif
                    {
                        RTCAnswerOptions answerOptions = new RTCAnswerOptions();
                        var answer = await _peerConnection.CreateAnswer(answerOptions);
                        await _peerConnection.SetLocalDescription(answer);
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

                            SC.callStatsClient.SendApplicationErrorLogs("error", $"Can't parse received message.\n {message}", "text");

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
                    await _peerConnection.AddIceCandidate(candidate);
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

            if (_peerConnection != null)
            {
                Debug.WriteLine("[Error] Conductor: We only support connecting to one peer at a time");

                SC.callStatsClient.SendApplicationErrorLogs("error", "We only support connecting to one peer at a time", "text");

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
                var offer = await _peerConnection.CreateOffer(offerOptions);

                if (IsNullOrEmpty(offer.Sdp))
                {
                    SC.callStatsClient.SendFabricSetupFailed("SDPGenerationError", "IsNullOrEmpty(sdp)", "Can't parse received session description message.", Empty);
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
                await _peerConnection.SetLocalDescription(modifiedOffer);
                Debug.WriteLine("Conductor: Sending offer:\n" + modifiedOffer.Sdp);
                SendSdp(modifiedOffer);

                _localSDPForCallStats = offer.Sdp;

                SC.callStatsClient.SSRCMapDataSetup(offer.Sdp, "outbound", "local");
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
            _peerConnection = null;
            //ClosePeerConnection();
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

            json.Add(kSessionDescriptionTypeName, JsonValue.CreateStringValue(messageType));
            json.Add(kSessionDescriptionSdpName, JsonValue.CreateStringValue(description.Sdp));
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
        /// Receives a new list of Ice servers and updates the local list of servers.
        /// </summary>
        /// <param name="iceServers">List of Ice servers to configure.</param>
        public void ConfigureIceServers(Collection<IceServer> iceServers)
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
                url += iceServer.Host.Value;
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
        /// cancelation and wait the operation to cancel (blocks curren thread).
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
    }
}
