using CallStatsLib;
using CallStatsLib.Request;
using Jose;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Connectivity;

namespace PeerConnectionClient.Stats
{
    public class CallStatsClient
    {
        private CallStats callstats;

        private static readonly StatsController SC = StatsController.Instance;

        private static string userID = GetLocalPeerName();
        private static string localID = GetLocalPeerName();
        private static string appID = (string)Config.localSettings.Values["appID"];
        private static string keyID = (string)Config.localSettings.Values["keyID"];
        private static string confID = Config.localSettings.Values["confID"].ToString();

        private static readonly string jti = new Func<string>(() =>
        {
            Random random = new Random();
            const string chars = "abcdefghijklmnopqrstuvxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const int length = 10;
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        })();

        private static string originID = null;
        private static string deviceID = "desktop";
        private static string connectionID = $"{GetLocalPeerName()}-{confID}";
        private static string remoteID = "RemotePeer";

        public static string GetLocalPeerName()
        {
            HostName hostname = 
                NetworkInformation.GetHostNames().FirstOrDefault(h => h.Type == HostNameType.DomainName);

            return hostname?.CanonicalName.ToLower() ?? "<unknown host>";
        }

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
                { "userID", localID},
                { "appID", appID},
                { "keyID", keyID },
                { "iat", DateTime.UtcNow.ToUnixTimeStampSeconds() },
                { "nbf", DateTime.UtcNow.AddMinutes(-5).ToUnixTimeStampSeconds() },
                { "exp", DateTime.UtcNow.AddHours(1).ToUnixTimeStampSeconds() },
                { "jti", jti }
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
                        Debug.WriteLine("[Error] ecc-key.p12 certificate file is empty.");
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
            callstats = new CallStats(localID, appID, keyID, confID, GenerateJWT());

            await callstats.StartCallStats(CreateConference(), UserAlive());

            SendUserDetails();

            SC.totalSetupTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
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
            ccd.localID = localID;
            ccd.originID = originID;
            ccd.deviceID = GetLocalPeerName();
            ccd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ccd.endpointInfo = endpointInfo;

            return ccd;
        }

        private UserAliveData UserAlive()
        {
            UserAliveData uad = new UserAliveData();
            uad.localID = localID;
            uad.originID = originID;
            uad.deviceID = deviceID;
            uad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

            return uad;
        }

        private void SendUserDetails()
        {
            UserDetailsData udd = new UserDetailsData();
            udd.localID = localID;
            udd.originID = originID;
            udd.deviceID = deviceID;
            udd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            udd.userName = userID;

            Debug.WriteLine("UserDetails: ");
            var task = callstats.UserDetails(udd);
        }

        public void SendUserLeft()
        {
            UserLeftData uld = new UserLeftData();
            uld.localID = localID;
            uld.originID = originID;
            uld.deviceID = GetLocalPeerName();
            uld.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

            Debug.WriteLine("SendUserLeft: ");
            var task = callstats.UserLeft(uld);
        }
        #endregion

        #region Fabric Events
        public async Task SendFabricSetup(long gatheringDelayMiliseconds, long connectivityDelayMiliseconds, long totalSetupDelay)
        {
            IceCandidateStatsData();
            AddToIceCandidatePairsList();

            FabricSetupData fsd = new FabricSetupData();
            fsd.localID = localID;
            fsd.originID = originID;
            fsd.deviceID = deviceID;
            fsd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fsd.connectionID = connectionID;
            fsd.remoteID = remoteID;
            fsd.delay = totalSetupDelay;
            fsd.iceGatheringDelay = gatheringDelayMiliseconds;
            fsd.iceConnectivityDelay = connectivityDelayMiliseconds;
            fsd.fabricTransmissionDirection = "sendrecv";
            fsd.remoteEndpointType = "peer";
            fsd.localIceCandidates = SC.localIceCandidates;
            fsd.remoteIceCandidates = SC.remoteIceCandidates;
            fsd.iceCandidatePairs = SC.iceCandidatePairList;

            Debug.WriteLine("FabricSetup: ");
            await callstats.FabricSetup(fsd);
        }

        public void SendFabricSetupFailed(string reason, string name, string message, string stack)
        {
            // MediaConfigError, MediaPermissionError, MediaDeviceError, NegotiationFailure,
            // SDPGenerationError, TransportFailure, SignalingError, IceConnectionFailure

            FabricSetupFailedData fsfd = new FabricSetupFailedData();
            fsfd.localID = localID;
            fsfd.originID = originID;
            fsfd.deviceID = deviceID;
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

        public void SendFabricSetupTerminated()
        {
            FabricTerminatedData ftd = new FabricTerminatedData();
            ftd.localID = localID;
            ftd.originID = originID;
            ftd.deviceID = deviceID;
            ftd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ftd.connectionID = connectionID;
            ftd.remoteID = remoteID;

            Debug.WriteLine("FabricTerminated: ");
            var task = callstats.FabricTerminated(ftd);
        }

        public async Task SendFabricStateChange(string prevState, string newState, string changedState)
        {
            FabricStateChangeData fscd = new FabricStateChangeData();
            fscd.localID = localID;
            fscd.originID = originID;
            fscd.deviceID = deviceID;
            fscd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fscd.remoteID = remoteID;
            fscd.connectionID = connectionID;
            fscd.prevState = prevState;
            fscd.newState = newState;
            fscd.changedState = changedState;

            Debug.WriteLine("FabricStateChange: ");
            await callstats.FabricStateChange(fscd);
        }

        public async Task SendFabricTransportChange()
        {
            IceCandidateStatsData();

            FabricTransportChangeData ftcd = new FabricTransportChangeData();
            ftcd.localID = localID;
            ftcd.originID = originID;
            ftcd.deviceID = deviceID;
            ftcd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ftcd.remoteID = remoteID;
            ftcd.connectionID = connectionID;
            ftcd.localIceCandidates = SC.localIceCandidates;
            ftcd.remoteIceCandidates = SC.remoteIceCandidates;
            ftcd.currIceCandidatePair = SC.currIceCandidatePairObj;
            ftcd.prevIceCandidatePair = SC.prevIceCandidatePairObj;
            ftcd.currIceConnectionState = SC.newIceConnectionState;
            ftcd.prevIceConnectionState = SC.prevIceConnectionState;
            ftcd.delay = 0;
            ftcd.relayType = $"";

            Debug.WriteLine("FabricTransportChange: ");
            await callstats.FabricTransportChange(ftcd);
        }

        public void SendFabricDropped()
        {
            FabricDroppedData fdd = new FabricDroppedData();

            fdd.localID = localID;
            fdd.originID = originID;
            fdd.deviceID = deviceID;
            fdd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fdd.remoteID = remoteID;
            fdd.connectionID = connectionID;
            fdd.currIceCandidatePair = GetIceCandidatePairData();
            fdd.currIceConnectionState = SC.newIceConnectionState;
            fdd.prevIceConnectionState = SC.prevIceConnectionState;
            fdd.delay = 0;

            Debug.WriteLine("FabricDropped: ");
            var task = callstats.FabricDropped(fdd);
        }

        // TODO: fabricHold or fabricResume
        private void SendFabricAction()
        {
            FabricActionData fad = new FabricActionData();
            fad.eventType = "fabricHold";  // fabricResume
            fad.localID = localID;
            fad.originID = originID;
            fad.deviceID = deviceID;
            fad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fad.remoteID = remoteID;
            fad.connectionID = connectionID;

            Debug.WriteLine("SendFabricAction: ");
            var task = callstats.FabricAction(fad);
        }
        #endregion

        #region Stats Submission
        public async Task SendConferenceStatsSubmission()
        {
            ConferenceStatsSubmissionData cssd = new ConferenceStatsSubmissionData();
            cssd.localID = localID;
            cssd.originID = originID;
            cssd.deviceID = deviceID;
            cssd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            cssd.connectionID = connectionID;
            cssd.remoteID = remoteID;
            cssd.stats = SC.statsObjects;

            SC.sec = DateTime.UtcNow.ToUnixTimeStampSeconds();

            Debug.WriteLine("ConferenceStatsSubmission: ");
            await callstats.ConferenceStatsSubmission(cssd);
        }

        // TODO: add values
        public void SendSystemStatusStatsSubmission()
        {
            SystemStatusStatsSubmissionData sssd = new SystemStatusStatsSubmissionData();
            sssd.localID = localID;
            sssd.originID = originID;
            sssd.deviceID = deviceID;
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

            for (int i = 0; i < SC.remoteIceCandidates.Count; i++)
                remoteIDList.Add(SC.remoteIceCandidates[i].id);

            MediaActionData mad = new MediaActionData();
            mad.eventType = eventType;
            mad.localID = localID;
            mad.originID = originID;
            mad.deviceID = deviceID;
            mad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            mad.connectionID = connectionID;
            mad.remoteID = remoteID;
            mad.ssrc = "";
            mad.mediaDeviceID = deviceID;
            mad.remoteIDList = remoteIDList;

            if (callstats != null)
            {
                Debug.WriteLine("MediaAction: ");
                var task = callstats.MediaAction(mad);
            }
        }
        #endregion

        #region Ice Events
        public async Task SendIceDisruptionStart()
        {
            IceDisruptionStartData ids = new IceDisruptionStartData();
            ids.eventType = "iceDisruptionStart";
            ids.localID = localID;
            ids.originID = originID;
            ids.deviceID = deviceID;
            ids.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ids.remoteID = remoteID;
            ids.connectionID = connectionID;
            ids.currIceCandidatePair = SC.currIceCandidatePairObj;
            ids.currIceConnectionState = SC.newIceConnectionState;
            ids.prevIceConnectionState = SC.prevIceConnectionState;

            Debug.WriteLine("IceDisruptionStart: ");
            await callstats.IceDisruptionStart(ids);
        }

        public async Task SendIceDisruptionEnd()
        {
            IceDisruptionEndData ide = new IceDisruptionEndData();
            ide.eventType = "iceDisruptionEnd";
            ide.localID = localID;
            ide.originID = originID;
            ide.deviceID = deviceID;
            ide.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ide.remoteID = remoteID;
            ide.connectionID = connectionID;
            ide.currIceCandidatePair = SC.currIceCandidatePairObj;
            ide.prevIceCandidatePair = SC.prevIceCandidatePairObj;
            ide.currIceConnectionState = SC.newIceConnectionState;
            ide.prevIceConnectionState = SC.prevIceConnectionState;

            Debug.WriteLine("IceDisruptionEnd: ");
            await callstats.IceDisruptionEnd(ide);
        }

        public async Task SendIceRestart()
        {
            IceRestartData ird = new IceRestartData();
            ird.eventType = "iceRestarted";
            ird.localID = localID;
            ird.originID = originID;
            ird.deviceID = deviceID;
            ird.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ird.remoteID = remoteID;
            ird.connectionID = connectionID;
            ird.prevIceCandidatePair = SC.prevIceCandidatePairObj;
            ird.currIceConnectionState = "new";
            ird.prevIceConnectionState = SC.prevIceConnectionState;

            Debug.WriteLine("IceRestart: ");
            await callstats.IceRestart(ird);
        }

        public async Task SendIceFailed()
        {
            IceFailedData ifd = new IceFailedData();
            ifd.eventType = "iceFailed";
            ifd.localID = localID;
            ifd.originID = originID;
            ifd.deviceID = deviceID;
            ifd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ifd.remoteID = remoteID;
            ifd.connectionID = connectionID;
            ifd.localIceCandidates = SC.localIceCandidates;
            ifd.remoteIceCandidates = SC.remoteIceCandidates;
            ifd.iceCandidatePairs = SC.iceCandidatePairList;
            ifd.currIceConnectionState = SC.newIceConnectionState;
            ifd.prevIceConnectionState = SC.prevIceConnectionState;
            ifd.delay = 9;

            Debug.WriteLine("IceFailed: ");
            await callstats.IceFailed(ifd);
        }

        public async Task SendIceAborted()
        {
            IceAbortedData iad = new IceAbortedData();
            iad.eventType = "iceFailed";
            iad.localID = localID;
            iad.originID = originID;
            iad.deviceID = deviceID;
            iad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            iad.remoteID = remoteID;
            iad.connectionID = connectionID;
            iad.localIceCandidates = SC.localIceCandidates;
            iad.remoteIceCandidates = SC.remoteIceCandidates;
            iad.iceCandidatePairs = SC.iceCandidatePairList;
            iad.currIceConnectionState = SC.newIceConnectionState;
            iad.prevIceConnectionState = SC.prevIceConnectionState;
            iad.delay = 3;

            Debug.WriteLine("IceAborted: ");
            await callstats.IceAborted(iad);
        }

        public async Task SendIceTerminated()
        {
            IceTerminatedData itd = new IceTerminatedData();
            itd.eventType = "iceTerminated";
            itd.localID = localID;
            itd.originID = originID;
            itd.deviceID = deviceID;
            itd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            itd.remoteID = remoteID;
            itd.connectionID = connectionID;
            itd.prevIceCandidatePair = SC.prevIceCandidatePairObj;
            itd.currIceConnectionState = SC.newIceConnectionState;
            itd.prevIceConnectionState = SC.prevIceConnectionState;

            Debug.WriteLine("IceTerminated: ");
            await callstats.IceTerminated(itd);
        }

        public async Task SendIceConnectionDisruptionStart()
        {
            IceConnectionDisruptionStartData icds = new IceConnectionDisruptionStartData();
            icds.eventType = "iceConnectionDisruptionStart";
            icds.localID = localID;
            icds.originID = originID;
            icds.deviceID = deviceID;
            icds.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            icds.remoteID = remoteID;
            icds.connectionID = connectionID;
            icds.currIceConnectionState = SC.newIceConnectionState;
            icds.prevIceConnectionState = SC.prevIceConnectionState;

            Debug.WriteLine("IceConnectionDisruptionStart: ");
            await callstats.IceConnectionDisruptionStart(icds);
        }

        public async Task SendIceConnectionDisruptionEnd()
        {
            IceConnectionDisruptionEndData icde = new IceConnectionDisruptionEndData();
            icde.eventType = "iceConnectionDisruptionEnd";
            icde.localID = localID;
            icde.originID = originID;
            icde.deviceID = deviceID;
            icde.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            icde.remoteID = remoteID;
            icde.connectionID = connectionID;
            icde.currIceConnectionState = SC.newIceConnectionState;
            icde.prevIceConnectionState = SC.prevIceConnectionState;
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
            cadd.localID = localID;
            cadd.originID = originID;
            cadd.deviceID = deviceID;
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
            aeld.localID = localID;
            aeld.originID = originID;
            aeld.deviceID = deviceID;
            aeld.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            aeld.connectionID = connectionID;
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
            cufd.localID = localID;
            cufd.originID = originID;
            cufd.deviceID = deviceID;
            cufd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            cufd.remoteID = remoteID;
            cufd.feedback = feedbackObj;

            Debug.WriteLine("ConferenceUserFeedback: ");
            var task = callstats.ConferenceUserFeedback(cufd);
        }

        public void SSRCMapDataSetup(string sdp, string streamType, string reportType)
        {
            var dict = SC.ParseSdp(sdp, "a=ssrc:");

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
                ssrcData.userID = userID;
                ssrcData.localStartTime = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                SC.ssrcDataList.Add(ssrcData);
            }
        }

        public async Task SendSSRCMap()
        {
            SSRCMapData ssrcMapData = new SSRCMapData();
            ssrcMapData.localID = localID;
            ssrcMapData.originID = originID;
            ssrcMapData.deviceID = deviceID;
            ssrcMapData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ssrcMapData.connectionID = connectionID;
            ssrcMapData.remoteID = remoteID;
            ssrcMapData.ssrcData = SC.ssrcDataList;

            Debug.WriteLine("SSRCMap: ");
            await callstats.SSRCMap(ssrcMapData);
        }

        public void SendSDP(string localSDP, string remoteSDP)
        {
            SDPEventData sdpEventData = new SDPEventData();
            sdpEventData.localID = localID;
            sdpEventData.originID = originID;
            sdpEventData.deviceID = deviceID;
            sdpEventData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            sdpEventData.connectionID = connectionID;
            sdpEventData.remoteID = remoteID;
            sdpEventData.localSDP = localSDP;
            sdpEventData.remoteSDP = remoteSDP;

            Debug.WriteLine("SDPEvent: ");
            var task = callstats.SDPEvent(sdpEventData);
        }
        #endregion

        #region Ice Candidates Data
        private void IceCandidateStatsData()
        {
            for (int i = 0; i < SC.iceCandidateStatsList.Count; i++)
            {
                IceCandidateStats ics = SC.iceCandidateStatsList[i];

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
                    SC.localIceCandidates.Add(iceCandidate);

                if (ics.type.Contains("remote"))
                    SC.remoteIceCandidates.Add(iceCandidate);
            }
        }

        private void AddToIceCandidatePairsList()
        {
            for (int i = 0; i < SC.iceCandidatePairStatsList.Count; i++)
            {
                IceCandidatePairStats icps = SC.iceCandidatePairStatsList[i];

                IceCandidatePair iceCandidatePair = new IceCandidatePair();

                iceCandidatePair.id = icps.id;
                iceCandidatePair.localCandidateId = icps.localCandidateId;
                iceCandidatePair.remoteCandidateId = icps.remoteCandidateId;
                iceCandidatePair.state = icps.state;
                iceCandidatePair.priority = 1;
                iceCandidatePair.nominated = icps.nominated;

                SC.iceCandidatePairList.Add(iceCandidatePair);
            }
        }

        public IceCandidatePair GetIceCandidatePairData()
        {
            IceCandidatePair icp = new IceCandidatePair();
            icp.id = SC.currIceCandidatePair.Id;
            icp.localCandidateId = SC.currIceCandidatePair.LocalCandidateId;
            icp.remoteCandidateId = SC.currIceCandidatePair.RemoteCandidateId;
            icp.state = SC.currIceCandidatePair.State.ToString().ToLower();
            icp.priority = 1;
            icp.nominated = SC.currIceCandidatePair.Nominated;

            return icp;
        }
        #endregion
    }
}
