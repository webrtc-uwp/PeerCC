using CallStatsLib;
using CallStatsLib.Request;
using Jose;
using Org.WebRtc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Connectivity;

namespace PeerConnectionClient
{
    public class CallStatsClient
    {
        #region Properties
        private static readonly string newConnection = RTCIceConnectionState.New.ToString().ToLower();
        private static readonly string checking = RTCIceConnectionState.Checking.ToString().ToLower();
        private static readonly string connected = RTCIceConnectionState.Connected.ToString().ToLower();
        private static readonly string completed = RTCIceConnectionState.Completed.ToString().ToLower();
        private static readonly string failed = RTCIceConnectionState.Failed.ToString().ToLower();
        private static readonly string disconnected = RTCIceConnectionState.Disconnected.ToString().ToLower();
        private static readonly string closed = RTCIceConnectionState.Closed.ToString().ToLower();

        private static readonly string newGathering = RTCIceGatheringState.New.ToString().ToLower();
        private static readonly string gathering = RTCIceGatheringState.Gathering.ToString().ToLower();
        private static readonly string complete = RTCIceGatheringState.Complete.ToString().ToLower();

        System.Timers.Timer _getAllStatsTimer = new System.Timers.Timer(10000);

        private static string _userID = GetLocalPeerName();
        private static string _localID = GetLocalPeerName();
        private static string _appID = (string)Config.localSettings.Values["appID"];
        private static string _keyID = (string)Config.localSettings.Values["keyID"];
        private static readonly string _jti = new Func<string>(() =>
        {
            Random random = new Random();
            const string chars = "abcdefghijklmnopqrstuvxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const int length = 10;
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        })();

        private static string _confID = Config.localSettings.Values["confID"].ToString();

        private static string _originID = null;
        private static string _deviceID = "desktop";
        private static string _connectionID = $"{GetLocalPeerName()}-{_confID}";
        private static string _remoteID = "RemotePeer";

        private List<object> _statsObjects = new List<object>();

        private List<IceCandidatePairStats> _iceCandidatePairStatsList = new List<IceCandidatePairStats>();
        private List<IceCandidatePair> _iceCandidatePairList = new List<IceCandidatePair>();

        private List<IceCandidateStats> _iceCandidateStatsList = new List<IceCandidateStats>();
        private List<IceCandidate> _localIceCandidates = new List<IceCandidate>();
        private List<IceCandidate> _remoteIceCandidates = new List<IceCandidate>();

        private List<SSRCData> _ssrcDataList = new List<SSRCData>();

        long _gateheringTimeStart;
        long _gateheringTimeStop;

        long _connectingTimeStart;
        long _connectingTimeStop;

        long _totalSetupTimeStart;
        long _totalSetupTimeStop;

        private IceCandidatePair _currIceCandidatePairObj;
        private IceCandidatePair _prevIceCandidatePairObj;

        private string _prevIceConnectionState;
        private string _newIceConnectionState;

        private string _prevIceGatheringState;
        private string _newIceGatheringState;

        private RTCIceCandidatePairStats _currIceCandidatePair;

        private string _prevSelectedCandidateId;
        private string _currSelectedCandidateId;

        private CallStats callstats = new CallStats(_localID, _appID, _keyID, _confID, GenerateJWT());
        #endregion

        #region Generate JWT
        private static string GenerateJWT()
        {
            var header = new Dictionary<string, object>()
            {
                { "typ", "JWT" },
                { "alg", "ES256" }
            };

            var payload = new Dictionary<string, object>()
            {
                { "userID", _localID},
                { "appID", _appID},
                { "keyID", _keyID },
                { "iat", DateTime.UtcNow.ToUnixTimeStampSeconds() },
                { "nbf", DateTime.UtcNow.AddMinutes(-5).ToUnixTimeStampSeconds() },
                { "exp", DateTime.UtcNow.AddHours(1).ToUnixTimeStampSeconds() },
                { "jti", _jti }
            };

            try
            {
                string eccKey = @"ecc-key.p12";
                if (File.Exists(eccKey))
                {
                    if (new FileInfo(eccKey).Length != 0)
                    {
                        return JWT.Encode(payload, new X509Certificate2(eccKey,
                            (string)Config.localSettings.Values["secret"]).GetECDsaPrivateKey(),
                            JwsAlgorithm.ES256, extraHeaders: header);
                    }
                    else
                    {
                        Debug.WriteLine("[Error] Private key file is empty. Create .p12 certificate and copy to ecc-key.p12 file.");
                        return string.Empty;
                    }
                }
                else
                {
                    Debug.WriteLine("[Error] ecc-key.p12 certificate file does not exist.");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Error] GenerateJWT: {ex.Message}");
                return string.Empty;
            }
        }
        #endregion

        #region Start CallStats
        public async Task SendStartCallStats()
        {
            //callstats = new CallStats(_localID, _appID, _keyID, _confID, GenerateJWT());

            await callstats.StartCallStats(CreateConference(), UserAlive());

            SendUserDetails();

            _totalSetupTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
        }
        #endregion

        #region User Action Events
        private CreateConferenceData CreateConference()
        {
            EndpointInfo endpointInfo = new EndpointInfo();
            endpointInfo.type = "native";
            endpointInfo.os = Environment.OSVersion.ToString();
            endpointInfo.buildName = "UWP";
            endpointInfo.buildVersion = "10.0";
            endpointInfo.appVersion = "1.0";

            CreateConferenceData ccd = new CreateConferenceData();
            ccd.localID = _localID;
            ccd.originID = _originID;
            ccd.deviceID = GetLocalPeerName();
            ccd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ccd.endpointInfo = endpointInfo;

            return ccd;
        }

        private UserAliveData UserAlive()
        {
            UserAliveData uad = new UserAliveData();
            uad.localID = _localID;
            uad.originID = _originID;
            uad.deviceID = _deviceID;
            uad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

            return uad;
        }

        private void SendUserDetails()
        {
            UserDetailsData udd = new UserDetailsData();
            udd.localID = _localID;
            udd.originID = _originID;
            udd.deviceID = _deviceID;
            udd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            udd.userName = _userID;

            Debug.WriteLine("UserDetails: ");
            var task = callstats.UserDetails(udd);
        }

        public void SendUserLeft()
        {
            UserLeftData uld = new UserLeftData();
            uld.localID = _localID;
            uld.originID = _originID;
            uld.deviceID = GetLocalPeerName();
            uld.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

            Debug.WriteLine("SendUserLeft: ");
            var task = callstats.UserLeft(uld);
        }
        #endregion

        #region Fabric Events
        private async Task SendFabricSetup(long gatheringDelayMiliseconds, long connectivityDelayMiliseconds, long totalSetupDelay)
        {
            IceCandidateStatsData();
            AddToIceCandidatePairsList();

            FabricSetupData fsd = new FabricSetupData();
            fsd.localID = _localID;
            fsd.originID = _originID;
            fsd.deviceID = _deviceID;
            fsd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fsd.connectionID = _connectionID;
            fsd.remoteID = _remoteID;
            fsd.delay = totalSetupDelay;
            fsd.iceGatheringDelay = gatheringDelayMiliseconds;
            fsd.iceConnectivityDelay = connectivityDelayMiliseconds;
            fsd.fabricTransmissionDirection = "sendrecv";
            fsd.remoteEndpointType = "peer";
            fsd.localIceCandidates = _localIceCandidates;
            fsd.remoteIceCandidates = _remoteIceCandidates;
            fsd.iceCandidatePairs = _iceCandidatePairList;

            Debug.WriteLine("FabricSetup: ");
            await callstats.FabricSetup(fsd);
        }

        public void SendFabricSetupFailed(string reason, string name, string message, string stack)
        {
            // MediaConfigError, MediaPermissionError, MediaDeviceError, NegotiationFailure,
            // SDPGenerationError, TransportFailure, SignalingError, IceConnectionFailure

            FabricSetupFailedData fsfd = new FabricSetupFailedData();
            fsfd.localID = _localID;
            fsfd.originID = _originID;
            fsfd.deviceID = _deviceID;
            fsfd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fsfd.fabricTransmissionDirection = "sendrecv";
            fsfd.remoteEndpointType = "peer";
            fsfd.reason = reason;
            fsfd.name = name;
            fsfd.message = message;
            fsfd.stack = stack;

            Debug.WriteLine("FabricSetupFailed: ");
            var task = callstats.FabricSetupFailed(fsfd);
        }

        private void SendFabricSetupTerminated()
        {
            FabricTerminatedData ftd = new FabricTerminatedData();
            ftd.localID = _localID;
            ftd.originID = _originID;
            ftd.deviceID = _deviceID;
            ftd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ftd.connectionID = _connectionID;
            ftd.remoteID = _remoteID;

            Debug.WriteLine("FabricTerminated: ");
            var task = callstats.FabricTerminated(ftd);
        }

        private async Task SendFabricStateChange(string prevState, string newState, string changedState)
        {
            FabricStateChangeData fscd = new FabricStateChangeData();
            fscd.localID = _localID;
            fscd.originID = _originID;
            fscd.deviceID = _deviceID;
            fscd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fscd.remoteID = _remoteID;
            fscd.connectionID = _connectionID;
            fscd.prevState = prevState;
            fscd.newState = newState;
            fscd.changedState = changedState;

            Debug.WriteLine("FabricStateChange: ");
            await callstats.FabricStateChange(fscd);
        }

        private async Task SendFabricTransportChange()
        {
            IceCandidateStatsData();

            FabricTransportChangeData ftcd = new FabricTransportChangeData();
            ftcd.localID = _localID;
            ftcd.originID = _originID;
            ftcd.deviceID = _deviceID;
            ftcd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ftcd.remoteID = _remoteID;
            ftcd.connectionID = _connectionID;
            ftcd.localIceCandidates = _localIceCandidates;
            ftcd.remoteIceCandidates = _remoteIceCandidates;
            ftcd.currIceCandidatePair = _currIceCandidatePairObj;
            ftcd.prevIceCandidatePair = _prevIceCandidatePairObj;
            ftcd.currIceConnectionState = _newIceConnectionState;
            ftcd.prevIceConnectionState = _prevIceConnectionState;
            ftcd.delay = 0;
            ftcd.relayType = $"";

            Debug.WriteLine("FabricTransportChange: ");
            await callstats.FabricTransportChange(ftcd);
        }

        private void SendFabricDropped()
        {
            FabricDroppedData fdd = new FabricDroppedData();

            fdd.localID = _localID;
            fdd.originID = _originID;
            fdd.deviceID = _deviceID;
            fdd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fdd.remoteID = _remoteID;
            fdd.connectionID = _connectionID;
            fdd.currIceCandidatePair = GetIceCandidatePairData();
            fdd.currIceConnectionState = _newIceConnectionState;
            fdd.prevIceConnectionState = _prevIceConnectionState;
            fdd.delay = 0;

            Debug.WriteLine("FabricDropped: ");
            var task = callstats.FabricDropped(fdd);
        }

        // TODO: fabricHold or fabricResume
        private void SendFabricAction()
        {
            FabricActionData fad = new FabricActionData();
            fad.eventType = "fabricHold";  // fabricResume
            fad.localID = _localID;
            fad.originID = _originID;
            fad.deviceID = _deviceID;
            fad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fad.remoteID = _remoteID;
            fad.connectionID = _connectionID;

            Debug.WriteLine("SendFabricAction: ");
            var task = callstats.FabricAction(fad);
        }
        #endregion

        #region Stats Submission
        private async Task SendConferenceStatsSubmission()
        {
            ConferenceStatsSubmissionData cssd = new ConferenceStatsSubmissionData();
            cssd.localID = _localID;
            cssd.originID = _originID;
            cssd.deviceID = _deviceID;
            cssd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            cssd.connectionID = _connectionID;
            cssd.remoteID = _remoteID;
            cssd.stats = _statsObjects;

            Debug.WriteLine("ConferenceStatsSubmission: ");
            await callstats.ConferenceStatsSubmission(cssd);
        }

        // TODO: add values
        private void SendSystemStatusStatsSubmission()
        {
            SystemStatusStatsSubmissionData sssd = new SystemStatusStatsSubmissionData();
            sssd.localID = _localID;
            sssd.originID = _originID;
            sssd.deviceID = _deviceID;
            sssd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            sssd.cpuLevel = 1;
            sssd.batteryLevel = 1;
            sssd.memoryUsage = 1;
            sssd.memoryAvailable = 1;
            sssd.threadCount = 1;

            Debug.WriteLine("SystemStatusStatsSubmission: ");
            var task = callstats.SystemStatusStatsSubmission(sssd);
        }
        #endregion

        #region Media Events
        public void SendMediaAction(string eventType)
        {
            List<string> remoteIDList = new List<string>();

            for (int i = 0; i < _remoteIceCandidates.Count; i++)
                remoteIDList.Add(_remoteIceCandidates[i].id);

            MediaActionData mad = new MediaActionData();
            mad.eventType = eventType;
            mad.localID = _localID;
            mad.originID = _originID;
            mad.deviceID = _deviceID;
            mad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            mad.connectionID = _connectionID;
            mad.remoteID = _remoteID;
            mad.ssrc = "";
            mad.mediaDeviceID = _deviceID;
            mad.remoteIDList = remoteIDList;

            if (callstats != null)
            {
                Debug.WriteLine("MediaAction: ");
                var task = callstats.MediaAction(mad);
            }
        }
        #endregion

        #region Ice Events
        private async Task SendIceDisruptionStart()
        {
            IceDisruptionStartData ids = new IceDisruptionStartData();
            ids.eventType = "iceDisruptionStart";
            ids.localID = _localID;
            ids.originID = _originID;
            ids.deviceID = _deviceID;
            ids.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ids.remoteID = _remoteID;
            ids.connectionID = _connectionID;
            ids.currIceCandidatePair = _currIceCandidatePairObj;
            ids.currIceConnectionState = _newIceConnectionState;
            ids.prevIceConnectionState = _prevIceConnectionState;

            Debug.WriteLine("IceDisruptionStart: ");
            await callstats.IceDisruptionStart(ids);
        }

        private async Task SendIceDisruptionEnd()
        {
            IceDisruptionEndData ide = new IceDisruptionEndData();
            ide.eventType = "iceDisruptionEnd";
            ide.localID = _localID;
            ide.originID = _originID;
            ide.deviceID = _deviceID;
            ide.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ide.remoteID = _remoteID;
            ide.connectionID = _connectionID;
            ide.currIceCandidatePair = _currIceCandidatePairObj;
            ide.prevIceCandidatePair = _prevIceCandidatePairObj;
            ide.currIceConnectionState = _newIceConnectionState;
            ide.prevIceConnectionState = _prevIceConnectionState;

            Debug.WriteLine("IceDisruptionEnd: ");
            await callstats.IceDisruptionEnd(ide);
        }

        private async Task SendIceRestart()
        {
            IceRestartData ird = new IceRestartData();
            ird.eventType = "iceRestarted";
            ird.localID = _localID;
            ird.originID = _originID;
            ird.deviceID = _deviceID;
            ird.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ird.remoteID = _remoteID;
            ird.connectionID = _connectionID;
            ird.prevIceCandidatePair = _prevIceCandidatePairObj;
            ird.currIceConnectionState = newConnection;
            ird.prevIceConnectionState = _prevIceConnectionState;

            Debug.WriteLine("IceRestart: ");
            await callstats.IceRestart(ird);
        }

        private async Task SendIceFailed()
        {
            IceFailedData ifd = new IceFailedData();
            ifd.eventType = "iceFailed";
            ifd.localID = _localID;
            ifd.originID = _originID;
            ifd.deviceID = _deviceID;
            ifd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ifd.remoteID = _remoteID;
            ifd.connectionID = _connectionID;
            ifd.localIceCandidates = _localIceCandidates;
            ifd.remoteIceCandidates = _remoteIceCandidates;
            ifd.iceCandidatePairs = _iceCandidatePairList;
            ifd.currIceConnectionState = _newIceConnectionState;
            ifd.prevIceConnectionState = _prevIceConnectionState;
            ifd.delay = 9;

            Debug.WriteLine("IceFailed: ");
            await callstats.IceFailed(ifd);
        }

        private async Task SendIceAborted()
        {
            IceAbortedData iad = new IceAbortedData();
            iad.eventType = "iceFailed";
            iad.localID = _localID;
            iad.originID = _originID;
            iad.deviceID = _deviceID;
            iad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            iad.remoteID = _remoteID;
            iad.connectionID = _connectionID;
            iad.localIceCandidates = _localIceCandidates;
            iad.remoteIceCandidates = _remoteIceCandidates;
            iad.iceCandidatePairs = _iceCandidatePairList;
            iad.currIceConnectionState = _newIceConnectionState;
            iad.prevIceConnectionState = _prevIceConnectionState;
            iad.delay = 3;

            Debug.WriteLine("IceAborted: ");
            await callstats.IceAborted(iad);
        }

        private async Task SendIceTerminated()
        {
            IceTerminatedData itd = new IceTerminatedData();
            itd.eventType = "iceTerminated";
            itd.localID = _localID;
            itd.originID = _originID;
            itd.deviceID = _deviceID;
            itd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            itd.remoteID = _remoteID;
            itd.connectionID = _connectionID;
            itd.prevIceCandidatePair = _prevIceCandidatePairObj;
            itd.currIceConnectionState = _newIceConnectionState;
            itd.prevIceConnectionState = _prevIceConnectionState;

            Debug.WriteLine("IceTerminated: ");
            await callstats.IceTerminated(itd);
        }

        private async Task SendIceConnectionDisruptionStart()
        {
            IceConnectionDisruptionStartData icds = new IceConnectionDisruptionStartData();
            icds.eventType = "iceConnectionDisruptionStart";
            icds.localID = _localID;
            icds.originID = _originID;
            icds.deviceID = _deviceID;
            icds.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            icds.remoteID = _remoteID;
            icds.connectionID = _connectionID;
            icds.currIceConnectionState = _newIceConnectionState;
            icds.prevIceConnectionState = _prevIceConnectionState;

            Debug.WriteLine("IceConnectionDisruptionStart: ");
            await callstats.IceConnectionDisruptionStart(icds);
        }

        private async Task SendIceConnectionDisruptionEnd()
        {
            IceConnectionDisruptionEndData icde = new IceConnectionDisruptionEndData();
            icde.eventType = "iceConnectionDisruptionEnd";
            icde.localID = _localID;
            icde.originID = _originID;
            icde.deviceID = _deviceID;
            icde.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            icde.remoteID = _remoteID;
            icde.connectionID = _connectionID;
            icde.currIceConnectionState = _newIceConnectionState;
            icde.prevIceConnectionState = _prevIceConnectionState;
            icde.delay = 2;

            Debug.WriteLine("IceConnectionDisruptionEnd: ");
            await callstats.IceConnectionDisruptionEnd(icde);
        }
        #endregion

        #region Device Events
        // TODO: call SendConnectedOrActiveDevices
        private void SendConnectedOrActiveDevices()
        {
            List<MediaDevice> mediaDeviceList = new List<MediaDevice>();
            MediaDevice mediaDeviceObj = new MediaDevice();
            mediaDeviceObj.mediaDeviceID = "mediaDeviceID";
            mediaDeviceObj.kind = "videoinput";
            mediaDeviceObj.label = "external USB Webcam";
            mediaDeviceObj.groupID = "groupID";
            mediaDeviceList.Add(mediaDeviceObj);

            ConnectedOrActiveDevicesData cadd = new ConnectedOrActiveDevicesData();
            cadd.localID = _localID;
            cadd.originID = _originID;
            cadd.deviceID = _deviceID;
            cadd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            cadd.mediaDeviceList = mediaDeviceList;
            cadd.eventType = "connectedDeviceList";

            Debug.WriteLine("ConnectedOrActiveDevices: ");
            var task = callstats.ConnectedOrActiveDevices(cadd);
        }
        #endregion

        #region Special Events
        public void SendApplicationErrorLogs(string level, string message, string messageType)
        {
            ApplicationErrorLogsData aeld = new ApplicationErrorLogsData();
            aeld.localID = _localID;
            aeld.originID = _originID;
            aeld.deviceID = _deviceID;
            aeld.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            aeld.connectionID = _connectionID;
            aeld.level = level;
            aeld.message = message;
            aeld.messageType = messageType;

            Debug.WriteLine("ApplicationErrorLogs: ");
            var task = callstats.ApplicationErrorLogs(aeld);
        }

        public void SendConferenceUserFeedback(int overallRating, int videoQualityRating, int audioQualityRating, string comments)
        {
            Feedback feedbackObj = new Feedback();
            feedbackObj.overallRating = overallRating;
            feedbackObj.videoQualityRating = videoQualityRating;
            feedbackObj.audioQualityRating = audioQualityRating;
            feedbackObj.comments = comments;

            ConferenceUserFeedbackData cufd = new ConferenceUserFeedbackData();
            cufd.localID = _localID;
            cufd.originID = _originID;
            cufd.deviceID = _deviceID;
            cufd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            cufd.remoteID = _remoteID;
            cufd.feedback = feedbackObj;

            Debug.WriteLine("ConferenceUserFeedback: ");
            var task = callstats.ConferenceUserFeedback(cufd);
        }

        public void SSRCMapDataSetup(string sdp, string streamType, string reportType)
        {
            var dict = ParseSdp(sdp, "a=ssrc:");

            foreach (var d in dict)
            {
                SSRCData ssrcData = new SSRCData();

                ssrcData.ssrc = d.Key;

                foreach (var k in d.Value)
                {
                    if (k.Key == "cname")
                        ssrcData.cname = k.Value.Replace("\r", "");

                    if (k.Key == "msid")
                        ssrcData.msid = k.Value.Replace("\r", "");

                    if (k.Key == "mslabel")
                        ssrcData.mslabel = k.Value.Replace("\r", "");

                    if (k.Key == "label")
                    {
                        ssrcData.label = k.Value.Replace("\r", "");

                        if (k.Value.ToLower().Contains("audio"))
                            ssrcData.mediaType = "audio";

                        if (k.Value.ToLower().Contains("video"))
                            ssrcData.mediaType = "video";

                        if (k.Value.ToLower().Contains("screen"))
                            ssrcData.mediaType = "screen";
                    }
                }

                ssrcData.streamType = streamType;
                ssrcData.reportType = reportType;
                ssrcData.userID = _userID;
                ssrcData.localStartTime = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                _ssrcDataList.Add(ssrcData);
            }
        }

        private static Dictionary<string, Dictionary<string, string>> ParseSdp(string sdp, string searchFirstStr)
        {
            var dict = new Dictionary<string, Dictionary<string, string>>();

            List<string> listSdpLines = sdp.Split('\n').ToList();
            List<string> listFirstStr = new List<string>();

            string firstId = string.Empty;

            string searchFirstId = searchFirstStr + firstId;

            for (int i = 0; i < listSdpLines.Count; i++)
            {
                if (listSdpLines[i].StartsWith(searchFirstStr))
                    listFirstStr.Add(listSdpLines[i]);
            }

            for (int i = 0; i < listFirstStr.Count; i++)
            {
                int statrtIndex = listFirstStr[i].IndexOf(":") + 1;
                int endIndex = listFirstStr[i].IndexOf(" ");

                string id = listFirstStr[i].Substring(statrtIndex, endIndex - statrtIndex);

                if (id != firstId)
                {
                    firstId = id;
                    dict.Add(firstId, new Dictionary<string, string>());
                }

                int start = searchFirstId.Length + 1;

                string sub = listFirstStr[i].Substring(start);

                int startValue = sub.IndexOf(":");
                int startProperty = sub.IndexOf(" ") + 1;

                string property = sub.Substring(startProperty, startValue - startProperty);
                string value = sub.Substring(startValue + 1);

                dict[firstId].Add(property, value);
            }
            return dict;
        }

        public async Task SendSSRCMap()
        {
            SSRCMapData ssrcMapData = new SSRCMapData();
            ssrcMapData.localID = _localID;
            ssrcMapData.originID = _originID;
            ssrcMapData.deviceID = _deviceID;
            ssrcMapData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ssrcMapData.connectionID = _connectionID;
            ssrcMapData.remoteID = _remoteID;
            ssrcMapData.ssrcData = _ssrcDataList;

            Debug.WriteLine("SSRCMap: ");
            await callstats.SSRCMap(ssrcMapData);
        }

        public void SendSDP(string localSDP, string remoteSDP)
        {
            SDPEventData sdpEventData = new SDPEventData();
            sdpEventData.localID = _localID;
            sdpEventData.originID = _originID;
            sdpEventData.deviceID = _deviceID;
            sdpEventData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            sdpEventData.connectionID = _connectionID;
            sdpEventData.remoteID = _remoteID;
            sdpEventData.localSDP = localSDP;
            sdpEventData.remoteSDP = remoteSDP;

            Debug.WriteLine("SDPEvent: ");
            var task = callstats.SDPEvent(sdpEventData);
        }
        #endregion

        #region Stats OnIceConnectionStateChange
        public async Task StatsOnIceConnectionStateChange(RTCPeerConnection pc)
        {
            if (pc.IceConnectionState == RTCIceConnectionState.Checking)
            {
                _connectingTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (_newIceConnectionState != checking)
                {
                    await SetIceConnectionStates(checking);
                }

                _statsObjects.Clear();

                await GetAllStats(pc);

                if (_prevIceConnectionState == connected || _prevIceConnectionState == completed
                    || _prevIceConnectionState == failed || _prevIceConnectionState == disconnected
                    || _prevIceConnectionState == closed)
                {
                    await SendIceRestart();
                }

                if (_prevIceConnectionState == disconnected)
                {
                    await SendIceDisruptionEnd();

                    await SendIceConnectionDisruptionEnd();
                }
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Connected)
            {
                _connectingTimeStop = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                _totalSetupTimeStop = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (_newIceConnectionState != connected)
                {
                    await SetIceConnectionStates(connected);
                }

                _statsObjects.Clear();

                await GetAllStats(pc);

                long gatheringDelayMiliseconds;
                long connectivityDelayMiliseconds;
                long totalSetupDelay;

                gatheringDelayMiliseconds = _gateheringTimeStop - _gateheringTimeStart;

                if (_connectingTimeStart != 0)
                    connectivityDelayMiliseconds = _connectingTimeStop - _connectingTimeStart;
                else
                    connectivityDelayMiliseconds = 0;

                totalSetupDelay = _totalSetupTimeStop - _totalSetupTimeStart;

                //fabricSetup must be sent whenever iceConnectionState changes from "checking" to "connected" state.
                await SendFabricSetup(gatheringDelayMiliseconds, connectivityDelayMiliseconds, totalSetupDelay);

                if (_prevIceConnectionState == disconnected)
                {
                    await SendIceDisruptionEnd();
                }
                
                _getAllStatsTimer.Elapsed += async (sender, e) =>
                {
                    _statsObjects.Clear();

                    _prevSelectedCandidateId = _currSelectedCandidateId;

                    await GetAllStats(pc);

                    await SendConferenceStatsSubmission();

                    _sec = DateTime.UtcNow.ToUnixTimeStampSeconds();

                    SendSystemStatusStatsSubmission();
 
                    if (_prevIceConnectionState == connected || _prevIceConnectionState == completed)
                    {
                        if (_prevSelectedCandidateId != null && _prevSelectedCandidateId != null && _prevSelectedCandidateId != _currSelectedCandidateId)
                        {
                            await SendFabricTransportChange();
                        }
                    }
                };
                _getAllStatsTimer.Start();
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Completed)
            {
                if (_newIceConnectionState != completed)
                {
                    await SetIceConnectionStates(completed);
                }

                _statsObjects.Clear();

                await GetAllStats(pc);

                if (_prevIceConnectionState == disconnected)
                {
                    await SendIceDisruptionEnd();
                }
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Failed)
            {
                if (_newIceConnectionState != failed)
                {
                    await SetIceConnectionStates(failed);
                }

                _statsObjects.Clear();

                await GetAllStats(pc);

                if (_prevIceConnectionState == checking || _prevIceConnectionState == disconnected)
                {
                    await SendIceFailed();
                }

                if (_prevIceConnectionState == disconnected || _prevIceConnectionState == completed)
                {
                    SendFabricDropped();
                }

                SendFabricSetupFailed("IceConnectionFailure", string.Empty, string.Empty, string.Empty);
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Disconnected)
            {
                if (_newIceConnectionState != disconnected)
                {
                    await SetIceConnectionStates(disconnected);
                }

                _statsObjects.Clear();

                await GetAllStats(pc);

                if (_prevIceConnectionState == connected || _prevIceConnectionState == completed)
                {
                    await SendIceDisruptionStart();
                }

                if (_prevIceConnectionState == checking)
                {
                    await SendIceConnectionDisruptionStart();
                }
            }

            if (pc.IceConnectionState == RTCIceConnectionState.Closed)
            {
                if (_newIceConnectionState != closed)
                {
                    await SetIceConnectionStates(closed);
                }

                _statsObjects.Clear();

                await GetAllStats(pc);

                SendFabricSetupTerminated();

                if (_prevIceConnectionState == checking || _prevIceConnectionState == newConnection)
                {
                    await SendIceAborted();
                }

                if (_prevIceConnectionState == connected || _prevIceConnectionState == completed
                    || _prevIceConnectionState == failed || _prevIceConnectionState == disconnected)
                {
                    await SendIceTerminated();
                }
            }
        }

        public async Task PeerConnectionClosedStateChange()
        {
            _getAllStatsTimer.Stop();

            if (_newIceConnectionState != closed)
            {
                await SetIceConnectionStates(closed);
            }

            SendFabricSetupTerminated();

            if (_prevIceConnectionState == checking || _prevIceConnectionState == newConnection)
            {
                await SendIceAborted();
            }

            if (_prevIceConnectionState == connected || _prevIceConnectionState == completed
                || _prevIceConnectionState == failed || _prevIceConnectionState == disconnected)
            {
                await SendIceTerminated();
            }
        }

        private async Task SetIceConnectionStates(string newState)
        {
            if (_prevIceConnectionState == null || _newIceConnectionState == null)
            {
                _prevIceConnectionState = newConnection;
                _newIceConnectionState = newState;
            }
            else
            {
                _prevIceConnectionState = _newIceConnectionState;
                _newIceConnectionState = newState;
            }

            await SendFabricStateChange(_prevIceConnectionState, _newIceConnectionState, "iceConnectionState");
        }
        #endregion

        #region Stats OnIceGatheringStateChange
        public async Task StatsOnIceGatheringStateChange(RTCPeerConnection pc)
        {
            if (pc.IceGatheringState == RTCIceGatheringState.Gathering)
            {
                _gateheringTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (_newIceGatheringState != gathering)
                {
                    await SetIceGatheringStates(gathering);
                }
            }

            if (pc.IceGatheringState == RTCIceGatheringState.Complete)
            {
                _gateheringTimeStop = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                if (_newIceGatheringState != complete)
                {
                    await SetIceGatheringStates(complete);
                }
            }
        }

        private async Task SetIceGatheringStates(string newState)
        {
            if (_prevIceGatheringState == null || _newIceGatheringState == null)
            {
                _prevIceGatheringState = newGathering;
                _newIceGatheringState = newState;
            }
            else
            {
                _prevIceGatheringState = _newIceGatheringState;
                _newIceGatheringState = newState;
            }

            await SendFabricStateChange(_prevIceGatheringState, _newIceGatheringState, "iceGatheringState");
        }
        #endregion

        #region WebRtc Stats
        public static Dictionary<RTCStatsType, object> MakeDictionaryOfAllStats()
        {
            return new Dictionary<RTCStatsType, object>
            {
                { RTCStatsType.Codec, null },
                { RTCStatsType.InboundRtp, null },
                { RTCStatsType.OutboundRtp, null },
                { RTCStatsType.RemoteInboundRtp, null },
                { RTCStatsType.RemoteOutboundRtp, null },
                { RTCStatsType.Csrc, null },
                { RTCStatsType.PeerConnection, null },
                { RTCStatsType.DataChannel, null },
                { RTCStatsType.Stream, null },
                { RTCStatsType.Track, null },
                { RTCStatsType.Sender, null },
                { RTCStatsType.Receiver, null },
                { RTCStatsType.Transport, null },
                { RTCStatsType.CandidatePair, null },
                { RTCStatsType.LocalCandidate, null },
                { RTCStatsType.RemoteCandidate, null },
                { RTCStatsType.Certificate, null }
            };
        }

        RTCStatsTypeSet statsType = new RTCStatsTypeSet(MakeDictionaryOfAllStats());

        public async Task GetAllStats(RTCPeerConnection pc)
        {
            IRTCStatsReport statsReport = await Task.Run(async () => await pc.GetStats(statsType));

            GetAllStatsData(statsReport);
        }

        private double _prevBytesReceived = 0.0;
        private double _prevBytesSent = 0.0;
        private long _sec = 1;

        public void GetAllStatsData(IRTCStatsReport statsReport)
        {
            Dictionary<string, string> candidatePairsDict = new Dictionary<string, string>();

            for (int i = 0; i < statsReport.StatsIds.Count; i++)
            {
                IRTCStats rtcStats = statsReport.GetStats(statsReport.StatsIds[i]);

                RTCStatsType? statsType = rtcStats.StatsType;

                string statsTypeOther = rtcStats.StatsTypeOther;

                if (statsType == null)
                {
                    if (statsTypeOther == "ice-candidate")
                    {
                        RTCIceCandidateStats iceCandidateStats = RTCIceCandidateStats.Cast(rtcStats);

                        IceCandidateStats ics = new IceCandidateStats();
                        ics.candidateType = iceCandidateStats.CandidateType.ToString().ToLower();
                        ics.deleted = iceCandidateStats.Deleted;
                        ics.id = iceCandidateStats.Id;
                        ics.ip = iceCandidateStats.Ip;
                        ics.networkType = iceCandidateStats.NetworkType.ToString();
                        ics.port = iceCandidateStats.Port;
                        ics.priority = iceCandidateStats.Priority;
                        ics.protocol = iceCandidateStats.Protocol.ToLower();
                        ics.relayProtocol = iceCandidateStats.RelayProtocol;
                        ics.type = candidatePairsDict[ics.id];
                        ics.statsTypeOther = iceCandidateStats.StatsTypeOther;
                        ics.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        ics.transportId = iceCandidateStats.TransportId;
                        ics.url = iceCandidateStats.Url;

                        _iceCandidateStatsList.Add(ics);

                        _statsObjects.Add(ics);
                    }
                }

                else if (statsType == RTCStatsType.Codec)
                {
                    RTCCodecStats codecStats = RTCCodecStats.Cast(rtcStats);

                    CodecStats cs = new CodecStats();
                    cs.channels = codecStats.Channels;
                    cs.clockRate = codecStats.ClockRate;
                    cs.codecType = codecStats.CodecType.ToString();
                    cs.id = codecStats.Id;
                    cs.implementation = codecStats.Implementation;
                    cs.mimeType = codecStats.MimeType;
                    cs.payloadType = codecStats.PayloadType;
                    cs.sdpFmtpLine = codecStats.SdpFmtpLine;
                    cs.type = codecStats.StatsType.ToString().ToLower();
                    cs.statsTypeOther = codecStats.StatsTypeOther;
                    cs.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    cs.transportId = codecStats.TransportId;

                    _statsObjects.Add(cs);
                }

                else if (statsType == RTCStatsType.InboundRtp)
                {
                    RTCInboundRtpStreamStats inboundRtpStats = RTCInboundRtpStreamStats.Cast(rtcStats);

                    InboundRtpStreamStats irss = new InboundRtpStreamStats();
                    irss.averageRtcpInterval = inboundRtpStats.AverageRtcpInterval;
                    irss.burstDiscardCount = inboundRtpStats.BurstDiscardCount;
                    irss.burstDiscardRate = inboundRtpStats.BurstDiscardRate;
                    irss.burstLossCount = inboundRtpStats.BurstLossCount;
                    irss.burstLossRate = inboundRtpStats.BurstLossRate;
                    irss.burstPacketsDiscarded = inboundRtpStats.BurstPacketsLost;
                    irss.burstPacketsLost = inboundRtpStats.BurstPacketsLost;
                    irss.bytesReceived = inboundRtpStats.BytesReceived;
                    irss.codecId = inboundRtpStats.CodecId;
                    irss.fecPacketsReceived = inboundRtpStats.FecPacketsReceived;
                    irss.firCount = inboundRtpStats.FirCount;
                    irss.framesDecoded = inboundRtpStats.FramesDecoded;
                    irss.gapDiscardRate = inboundRtpStats.GapDiscardRate;
                    irss.gapLossRate = inboundRtpStats.GapLossRate;
                    irss.id = inboundRtpStats.Id;
                    irss.jitter = inboundRtpStats.Jitter;
                    irss.kind = inboundRtpStats.Kind;
                    //irss.lastPacketReceivedTimestamp = inboundRtpStats.LastPacketReceivedTimestamp;
                    irss.nackCount = inboundRtpStats.NackCount;
                    irss.packetsDiscarded = inboundRtpStats.PacketsDiscarded;
                    irss.packetsDuplicated = inboundRtpStats.PacketsDuplicated;
                    irss.packetsFailedDecryption = inboundRtpStats.PacketsFailedDecryption;
                    irss.packetsLost = inboundRtpStats.PacketsLost;
                    irss.packetsReceived = inboundRtpStats.PacketsReceived;
                    irss.packetsRepaired = inboundRtpStats.PacketsRepaired;
                    //irss.perDscpPacketsReceived = (Dictionary<string, ulong>)inboundRtpStats.PerDscpPacketsReceived;
                    irss.pliCount = inboundRtpStats.PliCount;
                    irss.qpSum = inboundRtpStats.QpSum;
                    irss.receiverId = inboundRtpStats.ReceiverId;
                    irss.remoteId = inboundRtpStats.RemoteId;
                    irss.sliCount = inboundRtpStats.SliCount;
                    irss.ssrc = inboundRtpStats.Ssrc;
                    irss.type = "inbound-rtp";
                    irss.statsTypeOther = inboundRtpStats.StatsTypeOther;
                    irss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    irss.trackId = inboundRtpStats.TrackId;
                    irss.transportId = inboundRtpStats.TransportId;

                    long csioIntMs = DateTime.UtcNow.ToUnixTimeStampSeconds() - _sec;

                    irss.csioIntBRKbps = (irss.bytesReceived - _prevBytesReceived) / csioIntMs;

                    _prevBytesReceived = irss.bytesReceived;

                    _statsObjects.Add(irss);
                }

                else if (statsType == RTCStatsType.OutboundRtp)
                {
                    RTCOutboundRtpStreamStats outboundRtpStats = RTCOutboundRtpStreamStats.Cast(rtcStats);

                    OutboundRtpStreamStat orss = new OutboundRtpStreamStat();
                    orss.averageRtcpInterval = outboundRtpStats.AverageRtcpInterval;
                    orss.bytesDiscardedOnSend = outboundRtpStats.BytesDiscardedOnSend;
                    orss.bytesSent = outboundRtpStats.BytesSent;
                    orss.codecId = outboundRtpStats.CodecId;
                    orss.fecPacketsSent = outboundRtpStats.FecPacketsSent;
                    orss.firCount = outboundRtpStats.FirCount;
                    orss.framesEncoded = outboundRtpStats.FramesEncoded;
                    orss.id = outboundRtpStats.Id;
                    orss.kind = outboundRtpStats.Kind;
                    orss.nackCount = outboundRtpStats.NackCount;
                    orss.packetsDiscardedOnSend = outboundRtpStats.PacketsDiscardedOnSend;
                    orss.packetsSent = outboundRtpStats.PacketsSent;
                    //orss.perDscpPacketsSent = (Dictionary<string, ulong>)outboundRtpStats.PerDscpPacketsSent;
                    orss.pliCount = outboundRtpStats.PliCount;
                    orss.qpSum = outboundRtpStats.QpSum;
                    //orss.qualityLimitationReason = outboundRtpStats.QualityLimitationDurations.ToString();
                    orss.remoteId = outboundRtpStats.RemoteId;
                    orss.senderId = outboundRtpStats.SenderId;
                    orss.sliCount = outboundRtpStats.SliCount;
                    orss.ssrc = outboundRtpStats.Ssrc;
                    orss.type = "outbound-rtp";
                    orss.statsTypeOther = outboundRtpStats.StatsTypeOther;
                    orss.targetBitrate = outboundRtpStats.TargetBitrate;
                    orss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    orss.trackId = outboundRtpStats.TrackId;
                    orss.transportId = outboundRtpStats.TransportId;

                    long csioIntMs = DateTime.UtcNow.ToUnixTimeStampSeconds() - _sec;

                    orss.csioIntBRKbps = (orss.bytesSent - _prevBytesSent) / csioIntMs;

                    _prevBytesSent = orss.bytesSent;

                    _statsObjects.Add(orss);
                }

                else if (statsType == RTCStatsType.RemoteInboundRtp)
                {
                    RTCRemoteInboundRtpStreamStats remoteInboundRtpStats = RTCRemoteInboundRtpStreamStats.Cast(rtcStats);

                    RemoteInboundRtpStreamStats rirss = new RemoteInboundRtpStreamStats();
                    rirss.burstDiscardCount = remoteInboundRtpStats.BurstDiscardCount;
                    rirss.burstDiscardRate = remoteInboundRtpStats.BurstDiscardRate;
                    rirss.burstLossCount = remoteInboundRtpStats.BurstLossCount;
                    rirss.burstLossRate = remoteInboundRtpStats.BurstLossRate;
                    rirss.burstPacketsDiscarded = remoteInboundRtpStats.BurstPacketsDiscarded;
                    rirss.burstPacketsLost = remoteInboundRtpStats.BurstPacketsLost;
                    rirss.codecId = remoteInboundRtpStats.CodecId;
                    rirss.firCount = remoteInboundRtpStats.FirCount;
                    rirss.fractionLost = remoteInboundRtpStats.FractionLost;
                    rirss.gapDiscardRate = remoteInboundRtpStats.GapDiscardRate;
                    rirss.gapLossRate = remoteInboundRtpStats.GapLossRate;
                    rirss.id = remoteInboundRtpStats.Id;
                    rirss.jitter = remoteInboundRtpStats.Jitter;
                    rirss.kind = remoteInboundRtpStats.Kind;
                    rirss.localId = remoteInboundRtpStats.LocalId;
                    rirss.nackCount = remoteInboundRtpStats.NackCount;
                    rirss.packetsDiscarded = remoteInboundRtpStats.PacketsDiscarded;
                    rirss.packetsLost = remoteInboundRtpStats.PacketsLost;
                    rirss.packetsReceived = remoteInboundRtpStats.PacketsReceived;
                    rirss.packetsRepaired = remoteInboundRtpStats.PacketsRepaired;
                    rirss.pliCount = remoteInboundRtpStats.PliCount;
                    rirss.qpSum = remoteInboundRtpStats.QpSum;
                    rirss.roundTripTime = remoteInboundRtpStats.RoundTripTime;
                    rirss.sliCount = remoteInboundRtpStats.SliCount;
                    rirss.ssrc = remoteInboundRtpStats.Ssrc;
                    rirss.type = remoteInboundRtpStats.StatsType.ToString().ToLower();
                    rirss.statsTypeOther = remoteInboundRtpStats.StatsTypeOther;
                    rirss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    rirss.transportId = remoteInboundRtpStats.TransportId;

                    _statsObjects.Add(rirss);
                }

                else if (statsType == RTCStatsType.RemoteOutboundRtp)
                {
                    RTCRemoteOutboundRtpStreamStats remoteOutboundRtpStats = RTCRemoteOutboundRtpStreamStats.Cast(rtcStats);

                    RemoteOutboundRtpStreamStats rorss = new RemoteOutboundRtpStreamStats();
                    rorss.bytesDiscardedOnSend = remoteOutboundRtpStats.BytesDiscardedOnSend;
                    rorss.bytesSent = remoteOutboundRtpStats.BytesSent;
                    rorss.codecId = remoteOutboundRtpStats.CodecId;
                    rorss.fecPacketsSent = remoteOutboundRtpStats.FecPacketsSent;
                    rorss.firCount = remoteOutboundRtpStats.FirCount;
                    rorss.id = remoteOutboundRtpStats.Id;
                    rorss.kind = remoteOutboundRtpStats.Kind;
                    rorss.localId = remoteOutboundRtpStats.LocalId;
                    rorss.nackCount = remoteOutboundRtpStats.NackCount;
                    rorss.packetsDiscardedOnSend = remoteOutboundRtpStats.PacketsDiscardedOnSend;
                    rorss.packetsSent = remoteOutboundRtpStats.PacketsSent;
                    rorss.pliCount = remoteOutboundRtpStats.PliCount;
                    rorss.qpSum = remoteOutboundRtpStats.QpSum;
                    rorss.sliCount = remoteOutboundRtpStats.SliCount;
                    rorss.ssrc = remoteOutboundRtpStats.Ssrc;
                    rorss.type = remoteOutboundRtpStats.StatsType.ToString().ToLower();
                    rorss.StatsTypeOther = remoteOutboundRtpStats.StatsTypeOther;
                    rorss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    rorss.TransportId = remoteOutboundRtpStats.TransportId;

                    _statsObjects.Add(rorss);
                }

                else if (statsType == RTCStatsType.Csrc)
                {
                    RTCRtpContributingSourceStats csrcStats = RTCRtpContributingSourceStats.Cast(rtcStats);

                    RtpContributingSourceStats rcss = new RtpContributingSourceStats();
                    rcss.audioLevel = csrcStats.AudioLevel;
                    rcss.contributorSsrc = csrcStats.ContributorSsrc;
                    rcss.id = csrcStats.Id;
                    rcss.inboundRtpStreamId = csrcStats.InboundRtpStreamId;
                    rcss.packetsContributedTo = csrcStats.PacketsContributedTo;
                    rcss.type = csrcStats.StatsType.ToString().ToLower();
                    rcss.statsTypeOther = csrcStats.StatsTypeOther;
                    rcss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                    _statsObjects.Add(rcss);
                }

                else if (statsType == RTCStatsType.PeerConnection)
                {
                    RTCPeerConnectionStats peerConnectionStats = RTCPeerConnectionStats.Cast(rtcStats);

                    PeerConnectionStats pcs = new PeerConnectionStats();
                    pcs.dataChannelsAccepted = peerConnectionStats.DataChannelsAccepted;
                    pcs.dataChannelsClosed = peerConnectionStats.DataChannelsClosed;
                    pcs.dataChannelsOpened = peerConnectionStats.DataChannelsOpened;
                    pcs.dataChannelsRequested = peerConnectionStats.DataChannelsRequested;
                    pcs.id = peerConnectionStats.Id;
                    pcs.type = "peer-connection";
                    pcs.statsTypeOther = peerConnectionStats.StatsTypeOther;
                    pcs.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                    _statsObjects.Add(pcs);
                }

                else if (statsType == RTCStatsType.DataChannel)
                {
                    RTCDataChannelStats dataChannelStats = RTCDataChannelStats.Cast(rtcStats);

                    DataChannelStats dc = new DataChannelStats();
                    dc.bytesReceived = dataChannelStats.BytesReceived;
                    dc.bytesSent = dataChannelStats.BytesSent;
                    dc.dataChannelIdentifier = dataChannelStats.DataChannelIdentifier;
                    dc.id = dataChannelStats.Id;
                    dc.label = dataChannelStats.Label;
                    dc.messagesReceived = dataChannelStats.MessagesReceived;
                    dc.messagesSent = dataChannelStats.MessagesSent;
                    dc.protocol = dataChannelStats.Protocol;
                    dc.state = dataChannelStats.State.ToString().ToLower();
                    dc.type = "data-channel";
                    dc.statsTypeOther = dataChannelStats.StatsTypeOther;
                    dc.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    dc.transportId = dataChannelStats.TransportId;

                    _statsObjects.Add(dc);
                }

                else if (statsType == RTCStatsType.Stream)
                {
                    RTCMediaStreamStats mediaStreamStats = RTCMediaStreamStats.Cast(rtcStats);

                    MediaStreamStats mss = new MediaStreamStats();
                    mss.id = mediaStreamStats.Id;
                    mss.type = mediaStreamStats.StatsType.ToString().ToLower();
                    mss.statsTypeOther = mediaStreamStats.StatsTypeOther;
                    mss.streamIdentifier = mediaStreamStats.StreamIdentifier;
                    mss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    mss.trackIds = mediaStreamStats.TrackIds.ToList();

                    _statsObjects.Add(mss);
                }

                else if (statsType == RTCStatsType.Track)
                {
                    RTCMediaHandlerStats mediaHandlerStats = RTCMediaHandlerStats.Cast(rtcStats);

                    if (mediaHandlerStats.Kind == "audio")
                    {
                        RTCSenderAudioTrackAttachmentStats audioTrackStats = RTCSenderAudioTrackAttachmentStats.Cast(rtcStats);

                        SenderAudioTrackAttachmentStats satas = new SenderAudioTrackAttachmentStats();
                        satas.audioLevel = audioTrackStats.AudioLevel;
                        satas.echoReturnLoss = audioTrackStats.EchoReturnLoss;
                        satas.echoReturnLossEnhancement = audioTrackStats.EchoReturnLossEnhancement;
                        satas.ended = audioTrackStats.Ended;
                        satas.id = audioTrackStats.Id;
                        satas.kind = audioTrackStats.Kind;
                        satas.priority = audioTrackStats.Priority.ToString();
                        satas.remoteSource = audioTrackStats.RemoteSource;
                        satas.type = audioTrackStats.StatsType.ToString().ToLower();
                        satas.statsTypeOther = audioTrackStats.StatsTypeOther;
                        satas.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        satas.totalAudioEnergy = audioTrackStats.TotalAudioEnergy;
                        satas.totalSamplesDuration = audioTrackStats.TotalSamplesDuration;
                        satas.totalSamplesSent = audioTrackStats.TotalSamplesSent;
                        satas.trackIdentifier = audioTrackStats.TrackIdentifier;
                        satas.voiceActivityFlag = audioTrackStats.VoiceActivityFlag;

                        _statsObjects.Add(satas);
                    }

                    else if (mediaHandlerStats.Kind == "video")
                    {
                        RTCSenderVideoTrackAttachmentStats videoTrackStats = RTCSenderVideoTrackAttachmentStats.Cast(rtcStats);

                        SenderVideoTrackAttachmentStats svtas = new SenderVideoTrackAttachmentStats();
                        svtas.ended = videoTrackStats.Ended;
                        svtas.frameHeight = videoTrackStats.FrameHeight;
                        svtas.framesCaptured = videoTrackStats.FramesCaptured;
                        svtas.framesPerSecond = videoTrackStats.FramesPerSecond;
                        svtas.framesSent = videoTrackStats.FramesSent;
                        svtas.frameWidth = videoTrackStats.FrameWidth;
                        svtas.hugeFramesSent = videoTrackStats.HugeFramesSent;
                        svtas.id = videoTrackStats.Id;
                        svtas.keyFramesSent = videoTrackStats.KeyFramesSent;
                        svtas.kind = videoTrackStats.Kind;
                        svtas.priority = videoTrackStats.Priority.ToString();
                        svtas.remoteSource = (bool)videoTrackStats.RemoteSource;
                        svtas.type = videoTrackStats.StatsType.ToString().ToLower();
                        svtas.statsTypeOther = videoTrackStats.StatsTypeOther;
                        svtas.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        svtas.trackIdentifier = videoTrackStats.TrackIdentifier;

                        _statsObjects.Add(svtas);
                    }
                }

                else if (statsType == RTCStatsType.Sender)
                {
                    RTCMediaHandlerStats mediaHandlerStats = RTCMediaHandlerStats.Cast(rtcStats);

                    if (mediaHandlerStats.Kind == "audio")
                    {
                        RTCAudioSenderStats audioSenderStats = RTCAudioSenderStats.Cast(rtcStats);

                        AudioSenderStats aus = new AudioSenderStats();
                        aus.audioLevel = audioSenderStats.AudioLevel;
                        aus.echoReturnLoss = audioSenderStats.EchoReturnLoss;
                        aus.echoReturnLossEnhancement = audioSenderStats.EchoReturnLossEnhancement;
                        aus.ended = audioSenderStats.Ended;
                        aus.id = audioSenderStats.Id;
                        aus.kind = audioSenderStats.Kind;
                        aus.priority = audioSenderStats.Priority.ToString();
                        aus.remoteSource = audioSenderStats.RemoteSource;
                        aus.statsTypeOther = audioSenderStats.StatsTypeOther;
                        aus.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        aus.totalAudioEnergy = audioSenderStats.TotalAudioEnergy;
                        aus.totalSamplesDuration = audioSenderStats.TotalSamplesDuration;
                        aus.totalSamplesSent = audioSenderStats.TotalSamplesSent;
                        aus.trackIdentifier = audioSenderStats.TrackIdentifier;
                        aus.type = audioSenderStats.StatsType.ToString();
                        aus.voiceActivityFlag = audioSenderStats.VoiceActivityFlag;

                        _statsObjects.Add(aus);
                    }

                    else if (mediaHandlerStats.Kind == "video")
                    {
                        RTCVideoSenderStats videoSenderStats = RTCVideoSenderStats.Cast(rtcStats);

                        VideoSenderStats vis = new VideoSenderStats();
                        vis.ended = videoSenderStats.Ended;
                        vis.frameHeight = videoSenderStats.FrameHeight;
                        vis.framesCaptured = videoSenderStats.FramesCaptured;
                        vis.framesPerSecond = videoSenderStats.FramesPerSecond;
                        vis.framesSent = videoSenderStats.FramesSent;
                        vis.frameWidth = videoSenderStats.FrameWidth;
                        vis.hugeFramesSent = videoSenderStats.HugeFramesSent;
                        vis.id = videoSenderStats.Id;
                        vis.keyFramesSent = videoSenderStats.KeyFramesSent;
                        vis.kind = videoSenderStats.Kind;
                        vis.priority = videoSenderStats.Priority.ToString();
                        vis.remoteSource = videoSenderStats.RemoteSource;
                        vis.statsTypeOther = videoSenderStats.StatsTypeOther;
                        vis.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        vis.trackIdentifier = videoSenderStats.TrackIdentifier;
                        vis.type = videoSenderStats.StatsType.ToString();

                        _statsObjects.Add(vis);
                    }
                }

                else if (statsType == RTCStatsType.Receiver)
                {
                    RTCMediaHandlerStats mediaHandlerStats = RTCMediaHandlerStats.Cast(rtcStats);

                    if (mediaHandlerStats.Kind == "audio")
                    {
                        RTCAudioReceiverStats audioReceiverStats = RTCAudioReceiverStats.Cast(rtcStats);

                        AudioReceiverStats aur = new AudioReceiverStats();
                        aur.audioLevel = audioReceiverStats.AudioLevel;
                        aur.concealedSamples = audioReceiverStats.ConcealedSamples;
                        aur.concealmentEvents = audioReceiverStats.ConcealmentEvents;
                        aur.ended = audioReceiverStats.Ended;
                        aur.id = audioReceiverStats.Id;
                        aur.jitterBufferEmittedCount = audioReceiverStats.JitterBufferEmittedCount;
                        aur.kind = audioReceiverStats.Kind;
                        aur.priority = audioReceiverStats.Priority.ToString();
                        aur.remoteSource = audioReceiverStats.RemoteSource;
                        aur.statsTypeOther = audioReceiverStats.StatsTypeOther;
                        aur.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        aur.totalAudioEnergy = audioReceiverStats.TotalAudioEnergy;
                        aur.totalSamplesDuration = audioReceiverStats.TotalSamplesDuration;
                        aur.totalSamplesReceived = audioReceiverStats.TotalSamplesReceived;
                        aur.trackIdentifier = audioReceiverStats.TrackIdentifier;
                        aur.type = audioReceiverStats.StatsType.ToString();
                        aur.voiceActivityFlag = audioReceiverStats.VoiceActivityFlag;

                        _statsObjects.Add(aur);
                    }

                    else if (mediaHandlerStats.Kind == "video")
                    {
                        RTCVideoReceiverStats videoReceiverStats = RTCVideoReceiverStats.Cast(rtcStats);

                        VideoReceiverStats vir = new VideoReceiverStats();
                        vir.ended = videoReceiverStats.Ended;
                        vir.frameHeight = videoReceiverStats.FrameHeight;
                        vir.framesDecoded = videoReceiverStats.FramesDecoded;
                        vir.framesDropped = videoReceiverStats.FramesDropped;
                        vir.framesPerSecond = videoReceiverStats.FramesPerSecond;
                        vir.framesReceived = videoReceiverStats.FramesReceived;
                        vir.frameWidth = videoReceiverStats.FrameWidth;
                        vir.fullFramesLost = videoReceiverStats.FullFramesLost;
                        vir.id = videoReceiverStats.Id;
                        vir.jitterBufferEmittedCount = videoReceiverStats.JitterBufferEmittedCount;
                        vir.keyFramesReceived = videoReceiverStats.KeyFramesReceived;
                        vir.kind = videoReceiverStats.Kind;
                        vir.partialFramesLost = videoReceiverStats.PartialFramesLost;
                        vir.priority = videoReceiverStats.Priority.ToString();
                        vir.remoteSource = videoReceiverStats.RemoteSource;
                        vir.StatsTypeOther = videoReceiverStats.StatsTypeOther;
                        vir.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                        vir.trackIdentifier = videoReceiverStats.TrackIdentifier;
                        vir.type = videoReceiverStats.StatsType.ToString();

                        _statsObjects.Add(vir);
                    }
                }

                else if (statsType == RTCStatsType.Transport)
                {
                    RTCTransportStats transportStats = RTCTransportStats.Cast(rtcStats);

                    TransportStats ts = new TransportStats();
                    ts.bytesReceived = transportStats.BytesReceived;
                    ts.bytesSent = transportStats.BytesSent;
                    ts.dtlsCipher = transportStats.DtlsCipher;
                    ts.dtlsState = transportStats.DtlsState.ToString();
                    ts.iceRole = transportStats.IceRole.ToString();
                    ts.id = transportStats.Id;
                    ts.localCertificateId = transportStats.LocalCertificateId;
                    ts.packetsReceived = transportStats.PacketsReceived;
                    ts.packetsSent = transportStats.PacketsSent;
                    ts.remoteCertificateId = transportStats.RemoteCertificateId;
                    ts.rtcpTransportStatsId = transportStats.RtcpTransportStatsId;
                    ts.selectedCandidatePairId = transportStats.SelectedCandidatePairId;
                    ts.srtpCipher = transportStats.SrtpCipher;
                    ts.type = transportStats.StatsType.ToString().ToLower();
                    ts.statsTypeOther = transportStats.StatsTypeOther;
                    ts.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                    _statsObjects.Add(ts);

                    _currSelectedCandidateId = transportStats.SelectedCandidatePairId;
                }

                else if (statsType == RTCStatsType.CandidatePair)
                {
                    RTCIceCandidatePairStats candidatePairStats = RTCIceCandidatePairStats.Cast(rtcStats);

                    _prevIceCandidatePairObj = _currIceCandidatePairObj;

                    IceCandidatePairStats icp = new IceCandidatePairStats();
                    //icp.availableIncomingBitrate = candidatePairStats.AvailableIncomingBitrate;
                    //icp.availableOutgoingBitrate = candidatePairStats.AvailableOutgoingBitrate;
                    icp.bytesReceived = candidatePairStats.BytesReceived;
                    icp.bytesSent = candidatePairStats.BytesSent;
                    //icp.circuitBreakerTriggerCount = candidatePairStats.CircuitBreakerTriggerCount;
                    //icp.consentExpiredTimestamp = candidatePairStats.ConsentExpiredTimestamp;
                    icp.consentRequestsSent = candidatePairStats.ConsentRequestsSent;
                    //icp.currentRoundTripTime = candidatePairStats.CurrentRoundTripTime;
                    icp.currentRoundTripTime = 0.199;
                    //icp.firstRequestTimestamp = candidatePairStats.FirstRequestTimestamp;
                    icp.id = candidatePairStats.Id;
                    //icp.lastPacketReceivedTimestamp = candidatePairStats.LastPacketReceivedTimestamp;
                    //icp.lastPacketSentTimestamp = candidatePairStats.LastPacketSentTimestamp;
                    //icp.lastRequestTimestamp = candidatePairStats.LastRequestTimestamp;
                    //icp.lastResponseTimestamp = candidatePairStats.LastResponseTimestamp;
                    icp.localCandidateId = candidatePairStats.LocalCandidateId;
                    icp.nominated = candidatePairStats.Nominated;
                    icp.packetsReceived = candidatePairStats.PacketsReceived;
                    icp.packetsSent = candidatePairStats.PacketsSent;
                    icp.remoteCandidateId = candidatePairStats.RemoteCandidateId;
                    icp.requestsReceived = candidatePairStats.RequestsReceived;
                    icp.requestsSent = candidatePairStats.RequestsSent;
                    icp.responsesReceived = candidatePairStats.ResponsesReceived;
                    icp.responsesSent = candidatePairStats.ResponsesSent;
                    icp.retransmissionsReceived = candidatePairStats.RetransmissionsReceived;
                    icp.retransmissionsSent = candidatePairStats.RetransmissionsSent;
                    icp.state = candidatePairStats.State.ToString().ToLower();
                    //icp.type = candidatePairStats.StatsType.ToString().ToLower();
                    icp.type = "candidate-pair";
                    icp.statsTypeOther = candidatePairStats.StatsTypeOther;
                    icp.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    //icp.totalRoundTripTime = candidatePairStats.TotalRoundTripTime;
                    icp.totalRoundTripTime = 1.234;
                    icp.transportId = candidatePairStats.TransportId;

                    _statsObjects.Add(icp);

                    if (!candidatePairsDict.ContainsKey(candidatePairStats.LocalCandidateId))
                        candidatePairsDict.Add(candidatePairStats.LocalCandidateId, "local-candidate");

                    if (!candidatePairsDict.ContainsKey(candidatePairStats.RemoteCandidateId))
                        candidatePairsDict.Add(candidatePairStats.RemoteCandidateId, "remote-candidate");

                    _iceCandidatePairStatsList.Add(icp);

                    _currIceCandidatePair = candidatePairStats;

                    _currIceCandidatePairObj = GetIceCandidatePairData();
                }

                else if (statsType == RTCStatsType.Certificate)
                {
                    RTCCertificateStats certificateStats = RTCCertificateStats.Cast(rtcStats);

                    CertificateStats cs = new CertificateStats();
                    cs.base64Certificate = certificateStats.Base64Certificate;
                    cs.fingerprint = certificateStats.Fingerprint;
                    cs.fingerprintAlgorithm = certificateStats.FingerprintAlgorithm;
                    cs.id = certificateStats.Id;
                    cs.issuerCertificateId = certificateStats.IssuerCertificateId;
                    cs.type = certificateStats.StatsType.ToString().ToLower();
                    cs.statsTypeOther = certificateStats.StatsTypeOther;
                    cs.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                    _statsObjects.Add(cs);
                }
            }
        }
        #endregion

        #region Ice Candidates Data
        private void AddToIceCandidatePairsList()
        {
            for (int i = 0; i < _iceCandidatePairStatsList.Count; i++)
            {
                IceCandidatePairStats icps = _iceCandidatePairStatsList[i];

                IceCandidatePair iceCandidatePair = new IceCandidatePair();

                iceCandidatePair.id = icps.id;
                iceCandidatePair.localCandidateId = icps.localCandidateId;
                iceCandidatePair.remoteCandidateId = icps.remoteCandidateId;
                iceCandidatePair.state = icps.state;
                iceCandidatePair.priority = 1;
                iceCandidatePair.nominated = icps.nominated;

                _iceCandidatePairList.Add(iceCandidatePair);
            }
        }

        private void IceCandidateStatsData()
        {
            for (int i = 0; i < _iceCandidateStatsList.Count; i++)
            {
                IceCandidateStats ics = _iceCandidateStatsList[i];

                IceCandidate iceCandidate = new IceCandidate();

                iceCandidate.id = ics.id;
                iceCandidate.type = ics.type;
                iceCandidate.ip = ics.ip;
                iceCandidate.port = ics.port;
                iceCandidate.candidateType = ics.candidateType;
                iceCandidate.transport = ics.protocol;

                if (iceCandidate.candidateType == "srflex")
                    iceCandidate.candidateType = "srflx";

                if (ics.type.Contains("local"))
                    _localIceCandidates.Add(iceCandidate);

                if (ics.type.Contains("remote"))
                    _remoteIceCandidates.Add(iceCandidate);
            }
        }

        private IceCandidatePair GetIceCandidatePairData()
        {
            IceCandidatePair icp = new IceCandidatePair();
            icp.id = _currIceCandidatePair.Id;
            icp.localCandidateId = _currIceCandidatePair.LocalCandidateId;
            icp.remoteCandidateId = _currIceCandidatePair.RemoteCandidateId;
            icp.state = _currIceCandidatePair.State.ToString().ToLower();
            icp.priority = 1;
            icp.nominated = _currIceCandidatePair.Nominated;

            return icp;
        }
        #endregion

        private static string GetLocalPeerName()
        {
            var hostname = NetworkInformation.GetHostNames().FirstOrDefault(h => h.Type == HostNameType.DomainName);
            string ret = hostname?.CanonicalName ?? "<unknown host>";
            return ret;
        }
    }

    public static class DateTimeExtensions
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long ToUnixTimeStampSeconds(this DateTime dateTimeUtc)
        {
            return (long)Math.Round((dateTimeUtc.ToUniversalTime() - UnixEpoch).TotalSeconds);
        }

        public static long ToUnixTimeStampMiliseconds(this DateTime dateTimeUtc)
        {
            return (long)Math.Round((dateTimeUtc.ToUniversalTime() - UnixEpoch).TotalMilliseconds);
        }
    }
}
