using CallStatsLib;
using CallStatsLib.Request;
using Jose;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace PeerConnectionClient.Stats
{
    public class CallStatsClient
    {
        private CallStats callstats;

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
                { "userID", SharedProperties.localID},
                { "appID", SharedProperties.appID},
                { "keyID", SharedProperties.keyID },
                { "iat", DateTime.UtcNow.ToUnixTimeStampSeconds() },
                { "nbf", DateTime.UtcNow.AddMinutes(-5).ToUnixTimeStampSeconds() },
                { "exp", DateTime.UtcNow.AddHours(1).ToUnixTimeStampSeconds() },
                { "jti", SharedProperties.jti }
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
            callstats = new CallStats(SharedProperties.localID, SharedProperties.appID, SharedProperties.keyID, SharedProperties.confID, GenerateJWT());

            await callstats.StartCallStats(CreateConference(), UserAlive());

            SendUserDetails();

            SharedProperties.totalSetupTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
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
            ccd.localID = SharedProperties.localID;
            ccd.originID = SharedProperties.originID;
            ccd.deviceID = SharedProperties.GetLocalPeerName();
            ccd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ccd.endpointInfo = endpointInfo;

            return ccd;
        }

        private UserAliveData UserAlive()
        {
            UserAliveData uad = new UserAliveData();
            uad.localID = SharedProperties.localID;
            uad.originID = SharedProperties.originID;
            uad.deviceID = SharedProperties.deviceID;
            uad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

            return uad;
        }

        private void SendUserDetails()
        {
            UserDetailsData udd = new UserDetailsData();
            udd.localID = SharedProperties.localID;
            udd.originID = SharedProperties.originID;
            udd.deviceID = SharedProperties.deviceID;
            udd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            udd.userName = SharedProperties.userID;

            Debug.WriteLine("UserDetails: ");
            var task = callstats.UserDetails(udd);
        }

        public void SendUserLeft()
        {
            UserLeftData uld = new UserLeftData();
            uld.localID = SharedProperties.localID;
            uld.originID = SharedProperties.originID;
            uld.deviceID = SharedProperties.GetLocalPeerName();
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
            fsd.localID = SharedProperties.localID;
            fsd.originID = SharedProperties.originID;
            fsd.deviceID = SharedProperties.deviceID;
            fsd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fsd.connectionID = SharedProperties.connectionID;
            fsd.remoteID = SharedProperties.remoteID;
            fsd.delay = totalSetupDelay;
            fsd.iceGatheringDelay = gatheringDelayMiliseconds;
            fsd.iceConnectivityDelay = connectivityDelayMiliseconds;
            fsd.fabricTransmissionDirection = "sendrecv";
            fsd.remoteEndpointType = "peer";
            fsd.localIceCandidates = SharedProperties.localIceCandidates;
            fsd.remoteIceCandidates = SharedProperties.remoteIceCandidates;
            fsd.iceCandidatePairs = SharedProperties.iceCandidatePairList;

            Debug.WriteLine("FabricSetup: ");
            await callstats.FabricSetup(fsd);
        }

        public void SendFabricSetupFailed(string reason, string name, string message, string stack)
        {
            // MediaConfigError, MediaPermissionError, MediaDeviceError, NegotiationFailure,
            // SDPGenerationError, TransportFailure, SignalingError, IceConnectionFailure

            FabricSetupFailedData fsfd = new FabricSetupFailedData();
            fsfd.localID = SharedProperties.localID;
            fsfd.originID = SharedProperties.originID;
            fsfd.deviceID = SharedProperties.deviceID;
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
            ftd.localID = SharedProperties.localID;
            ftd.originID = SharedProperties.originID;
            ftd.deviceID = SharedProperties.deviceID;
            ftd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ftd.connectionID = SharedProperties.connectionID;
            ftd.remoteID = SharedProperties.remoteID;

            Debug.WriteLine("FabricTerminated: ");
            var task = callstats.FabricTerminated(ftd);
        }

        public async Task SendFabricStateChange(string prevState, string newState, string changedState)
        {
            FabricStateChangeData fscd = new FabricStateChangeData();
            fscd.localID = SharedProperties.localID;
            fscd.originID = SharedProperties.originID;
            fscd.deviceID = SharedProperties.deviceID;
            fscd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fscd.remoteID = SharedProperties.remoteID;
            fscd.connectionID = SharedProperties.connectionID;
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
            ftcd.localID = SharedProperties.localID;
            ftcd.originID = SharedProperties.originID;
            ftcd.deviceID = SharedProperties.deviceID;
            ftcd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ftcd.remoteID = SharedProperties.remoteID;
            ftcd.connectionID = SharedProperties.connectionID;
            ftcd.localIceCandidates = SharedProperties.localIceCandidates;
            ftcd.remoteIceCandidates = SharedProperties.remoteIceCandidates;
            ftcd.currIceCandidatePair = SharedProperties.currIceCandidatePairObj;
            ftcd.prevIceCandidatePair = SharedProperties.prevIceCandidatePairObj;
            ftcd.currIceConnectionState = SharedProperties.newIceConnectionState;
            ftcd.prevIceConnectionState = SharedProperties.prevIceConnectionState;
            ftcd.delay = 0;
            ftcd.relayType = $"";

            Debug.WriteLine("FabricTransportChange: ");
            await callstats.FabricTransportChange(ftcd);
        }

        public void SendFabricDropped()
        {
            FabricDroppedData fdd = new FabricDroppedData();

            fdd.localID = SharedProperties.localID;
            fdd.originID = SharedProperties.originID;
            fdd.deviceID = SharedProperties.deviceID;
            fdd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fdd.remoteID = SharedProperties.remoteID;
            fdd.connectionID = SharedProperties.connectionID;
            fdd.currIceCandidatePair = GetIceCandidatePairData();
            fdd.currIceConnectionState = SharedProperties.newIceConnectionState;
            fdd.prevIceConnectionState = SharedProperties.prevIceConnectionState;
            fdd.delay = 0;

            Debug.WriteLine("FabricDropped: ");
            var task = callstats.FabricDropped(fdd);
        }

        // TODO: fabricHold or fabricResume
        private void SendFabricAction()
        {
            FabricActionData fad = new FabricActionData();
            fad.eventType = "fabricHold";  // fabricResume
            fad.localID = SharedProperties.localID;
            fad.originID = SharedProperties.originID;
            fad.deviceID = SharedProperties.deviceID;
            fad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fad.remoteID = SharedProperties.remoteID;
            fad.connectionID = SharedProperties.connectionID;

            Debug.WriteLine("SendFabricAction: ");
            var task = callstats.FabricAction(fad);
        }
        #endregion

        #region Stats Submission
        public async Task SendConferenceStatsSubmission()
        {
            ConferenceStatsSubmissionData cssd = new ConferenceStatsSubmissionData();
            cssd.localID = SharedProperties.localID;
            cssd.originID = SharedProperties.originID;
            cssd.deviceID = SharedProperties.deviceID;
            cssd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            cssd.connectionID = SharedProperties.connectionID;
            cssd.remoteID = SharedProperties.remoteID;
            cssd.stats = SharedProperties.statsObjects;

            SharedProperties.sec = DateTime.UtcNow.ToUnixTimeStampSeconds();

            Debug.WriteLine("ConferenceStatsSubmission: ");
            await callstats.ConferenceStatsSubmission(cssd);
        }

        // TODO: add values
        public void SendSystemStatusStatsSubmission()
        {
            SystemStatusStatsSubmissionData sssd = new SystemStatusStatsSubmissionData();
            sssd.localID = SharedProperties.localID;
            sssd.originID = SharedProperties.originID;
            sssd.deviceID = SharedProperties.deviceID;
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

            for (int i = 0; i < SharedProperties.remoteIceCandidates.Count; i++)
                remoteIDList.Add(SharedProperties.remoteIceCandidates[i].id);

            MediaActionData mad = new MediaActionData();
            mad.eventType = eventType;
            mad.localID = SharedProperties.localID;
            mad.originID = SharedProperties.originID;
            mad.deviceID = SharedProperties.deviceID;
            mad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            mad.connectionID = SharedProperties.connectionID;
            mad.remoteID = SharedProperties.remoteID;
            mad.ssrc = "";
            mad.mediaDeviceID = SharedProperties.deviceID;
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
            ids.localID = SharedProperties.localID;
            ids.originID = SharedProperties.originID;
            ids.deviceID = SharedProperties.deviceID;
            ids.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ids.remoteID = SharedProperties.remoteID;
            ids.connectionID = SharedProperties.connectionID;
            ids.currIceCandidatePair = SharedProperties.currIceCandidatePairObj;
            ids.currIceConnectionState = SharedProperties.newIceConnectionState;
            ids.prevIceConnectionState = SharedProperties.prevIceConnectionState;

            Debug.WriteLine("IceDisruptionStart: ");
            await callstats.IceDisruptionStart(ids);
        }

        public async Task SendIceDisruptionEnd()
        {
            IceDisruptionEndData ide = new IceDisruptionEndData();
            ide.eventType = "iceDisruptionEnd";
            ide.localID = SharedProperties.localID;
            ide.originID = SharedProperties.originID;
            ide.deviceID = SharedProperties.deviceID;
            ide.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ide.remoteID = SharedProperties.remoteID;
            ide.connectionID = SharedProperties.connectionID;
            ide.currIceCandidatePair = SharedProperties.currIceCandidatePairObj;
            ide.prevIceCandidatePair = SharedProperties.prevIceCandidatePairObj;
            ide.currIceConnectionState = SharedProperties.newIceConnectionState;
            ide.prevIceConnectionState = SharedProperties.prevIceConnectionState;

            Debug.WriteLine("IceDisruptionEnd: ");
            await callstats.IceDisruptionEnd(ide);
        }

        public async Task SendIceRestart()
        {
            IceRestartData ird = new IceRestartData();
            ird.eventType = "iceRestarted";
            ird.localID = SharedProperties.localID;
            ird.originID = SharedProperties.originID;
            ird.deviceID = SharedProperties.deviceID;
            ird.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ird.remoteID = SharedProperties.remoteID;
            ird.connectionID = SharedProperties.connectionID;
            ird.prevIceCandidatePair = SharedProperties.prevIceCandidatePairObj;
            ird.currIceConnectionState = SharedProperties.newConnection;
            ird.prevIceConnectionState = SharedProperties.prevIceConnectionState;

            Debug.WriteLine("IceRestart: ");
            await callstats.IceRestart(ird);
        }

        public async Task SendIceFailed()
        {
            IceFailedData ifd = new IceFailedData();
            ifd.eventType = "iceFailed";
            ifd.localID = SharedProperties.localID;
            ifd.originID = SharedProperties.originID;
            ifd.deviceID = SharedProperties.deviceID;
            ifd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ifd.remoteID = SharedProperties.remoteID;
            ifd.connectionID = SharedProperties.connectionID;
            ifd.localIceCandidates = SharedProperties.localIceCandidates;
            ifd.remoteIceCandidates = SharedProperties.remoteIceCandidates;
            ifd.iceCandidatePairs = SharedProperties.iceCandidatePairList;
            ifd.currIceConnectionState = SharedProperties.newIceConnectionState;
            ifd.prevIceConnectionState = SharedProperties.prevIceConnectionState;
            ifd.delay = 9;

            Debug.WriteLine("IceFailed: ");
            await callstats.IceFailed(ifd);
        }

        public async Task SendIceAborted()
        {
            IceAbortedData iad = new IceAbortedData();
            iad.eventType = "iceFailed";
            iad.localID = SharedProperties.localID;
            iad.originID = SharedProperties.originID;
            iad.deviceID = SharedProperties.deviceID;
            iad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            iad.remoteID = SharedProperties.remoteID;
            iad.connectionID = SharedProperties.connectionID;
            iad.localIceCandidates = SharedProperties.localIceCandidates;
            iad.remoteIceCandidates = SharedProperties.remoteIceCandidates;
            iad.iceCandidatePairs = SharedProperties.iceCandidatePairList;
            iad.currIceConnectionState = SharedProperties.newIceConnectionState;
            iad.prevIceConnectionState = SharedProperties.prevIceConnectionState;
            iad.delay = 3;

            Debug.WriteLine("IceAborted: ");
            await callstats.IceAborted(iad);
        }

        public async Task SendIceTerminated()
        {
            IceTerminatedData itd = new IceTerminatedData();
            itd.eventType = "iceTerminated";
            itd.localID = SharedProperties.localID;
            itd.originID = SharedProperties.originID;
            itd.deviceID = SharedProperties.deviceID;
            itd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            itd.remoteID = SharedProperties.remoteID;
            itd.connectionID = SharedProperties.connectionID;
            itd.prevIceCandidatePair = SharedProperties.prevIceCandidatePairObj;
            itd.currIceConnectionState = SharedProperties.newIceConnectionState;
            itd.prevIceConnectionState = SharedProperties.prevIceConnectionState;

            Debug.WriteLine("IceTerminated: ");
            await callstats.IceTerminated(itd);
        }

        public async Task SendIceConnectionDisruptionStart()
        {
            IceConnectionDisruptionStartData icds = new IceConnectionDisruptionStartData();
            icds.eventType = "iceConnectionDisruptionStart";
            icds.localID = SharedProperties.localID;
            icds.originID = SharedProperties.originID;
            icds.deviceID = SharedProperties.deviceID;
            icds.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            icds.remoteID = SharedProperties.remoteID;
            icds.connectionID = SharedProperties.connectionID;
            icds.currIceConnectionState = SharedProperties.newIceConnectionState;
            icds.prevIceConnectionState = SharedProperties.prevIceConnectionState;

            Debug.WriteLine("IceConnectionDisruptionStart: ");
            await callstats.IceConnectionDisruptionStart(icds);
        }

        public async Task SendIceConnectionDisruptionEnd()
        {
            IceConnectionDisruptionEndData icde = new IceConnectionDisruptionEndData();
            icde.eventType = "iceConnectionDisruptionEnd";
            icde.localID = SharedProperties.localID;
            icde.originID = SharedProperties.originID;
            icde.deviceID = SharedProperties.deviceID;
            icde.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            icde.remoteID = SharedProperties.remoteID;
            icde.connectionID = SharedProperties.connectionID;
            icde.currIceConnectionState = SharedProperties.newIceConnectionState;
            icde.prevIceConnectionState = SharedProperties.prevIceConnectionState;
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
            cadd.localID = SharedProperties.localID;
            cadd.originID = SharedProperties.originID;
            cadd.deviceID = SharedProperties.deviceID;
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
            aeld.localID = SharedProperties.localID;
            aeld.originID = SharedProperties.originID;
            aeld.deviceID = SharedProperties.deviceID;
            aeld.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            aeld.connectionID = SharedProperties.connectionID;
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
            cufd.localID = SharedProperties.localID;
            cufd.originID = SharedProperties.originID;
            cufd.deviceID = SharedProperties.deviceID;
            cufd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            cufd.remoteID = SharedProperties.remoteID;
            cufd.feedback = feedbackObj;

            Debug.WriteLine("ConferenceUserFeedback: ");
            var task = callstats.ConferenceUserFeedback(cufd);
        }

        public void SSRCMapDataSetup(string sdp, string streamType, string reportType)
        {
            var dict = Parsing.ParseSdp(sdp, "a=ssrc:");

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
                ssrcData.userID = SharedProperties.userID;
                ssrcData.localStartTime = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                SharedProperties.ssrcDataList.Add(ssrcData);
            }
        }

        public async Task SendSSRCMap()
        {
            SSRCMapData ssrcMapData = new SSRCMapData();
            ssrcMapData.localID = SharedProperties.localID;
            ssrcMapData.originID = SharedProperties.originID;
            ssrcMapData.deviceID = SharedProperties.deviceID;
            ssrcMapData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ssrcMapData.connectionID = SharedProperties.connectionID;
            ssrcMapData.remoteID = SharedProperties.remoteID;
            ssrcMapData.ssrcData = SharedProperties.ssrcDataList;

            Debug.WriteLine("SSRCMap: ");
            await callstats.SSRCMap(ssrcMapData);
        }

        public void SendSDP(string localSDP, string remoteSDP)
        {
            SDPEventData sdpEventData = new SDPEventData();
            sdpEventData.localID = SharedProperties.localID;
            sdpEventData.originID = SharedProperties.originID;
            sdpEventData.deviceID = SharedProperties.deviceID;
            sdpEventData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            sdpEventData.connectionID = SharedProperties.connectionID;
            sdpEventData.remoteID = SharedProperties.remoteID;
            sdpEventData.localSDP = localSDP;
            sdpEventData.remoteSDP = remoteSDP;

            Debug.WriteLine("SDPEvent: ");
            var task = callstats.SDPEvent(sdpEventData);
        }
        #endregion

        #region Ice Candidates Data
        private void IceCandidateStatsData()
        {
            for (int i = 0; i < SharedProperties.iceCandidateStatsList.Count; i++)
            {
                IceCandidateStats ics = SharedProperties.iceCandidateStatsList[i];

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
                    SharedProperties.localIceCandidates.Add(iceCandidate);

                if (ics.type.Contains("remote"))
                    SharedProperties.remoteIceCandidates.Add(iceCandidate);
            }
        }

        private void AddToIceCandidatePairsList()
        {
            for (int i = 0; i < SharedProperties.iceCandidatePairStatsList.Count; i++)
            {
                IceCandidatePairStats icps = SharedProperties.iceCandidatePairStatsList[i];

                IceCandidatePair iceCandidatePair = new IceCandidatePair();

                iceCandidatePair.id = icps.id;
                iceCandidatePair.localCandidateId = icps.localCandidateId;
                iceCandidatePair.remoteCandidateId = icps.remoteCandidateId;
                iceCandidatePair.state = icps.state;
                iceCandidatePair.priority = 1;
                iceCandidatePair.nominated = icps.nominated;

                SharedProperties.iceCandidatePairList.Add(iceCandidatePair);
            }
        }

        public IceCandidatePair GetIceCandidatePairData()
        {
            IceCandidatePair icp = new IceCandidatePair();
            icp.id = SharedProperties.currIceCandidatePair.Id;
            icp.localCandidateId = SharedProperties.currIceCandidatePair.LocalCandidateId;
            icp.remoteCandidateId = SharedProperties.currIceCandidatePair.RemoteCandidateId;
            icp.state = SharedProperties.currIceCandidatePair.State.ToString().ToLower();
            icp.priority = 1;
            icp.nominated = SharedProperties.currIceCandidatePair.Nominated;

            return icp;
        }
        #endregion
    }
}
