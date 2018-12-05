using CallStatsLib;
using CallStatsLib.Request;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PeerConnectionClient.Stats
{
    public class CallStatsClient
    {
        private CallStats callstats;

        private static readonly StatsController SC = StatsController.Instance;

        #region Start CallStats
        public async Task SendStartCallStats()
        {
            callstats = new CallStats(
                Settings.localID, Settings.appID, Settings.keyID, Settings.confID, SC.GenerateJWT());

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
            ccd.localID = Settings.localID;
            ccd.originID = Settings.originID;
            ccd.deviceID = Settings.GetLocalPeerName();
            ccd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ccd.endpointInfo = endpointInfo;

            return ccd;
        }

        private UserAliveData UserAlive()
        {
            UserAliveData uad = new UserAliveData();
            uad.localID = Settings.localID;
            uad.originID = Settings.originID;
            uad.deviceID = Settings.deviceID;
            uad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

            return uad;
        }

        private void SendUserDetails()
        {
            UserDetailsData udd = new UserDetailsData();
            udd.localID = Settings.localID;
            udd.originID = Settings.originID;
            udd.deviceID = Settings.deviceID;
            udd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            udd.userName = Settings.userID;

            Debug.WriteLine("UserDetails: ");
            var task = callstats.UserDetails(udd);
        }

        public void SendUserLeft()
        {
            UserLeftData uld = new UserLeftData();
            uld.localID = Settings.localID;
            uld.originID = Settings.originID;
            uld.deviceID = Settings.GetLocalPeerName();
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
            fsd.localID = Settings.localID;
            fsd.originID = Settings.originID;
            fsd.deviceID = Settings.deviceID;
            fsd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fsd.connectionID = Settings.connectionID;
            fsd.remoteID = Settings.remoteID;
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
            fsfd.localID = Settings.localID;
            fsfd.originID = Settings.originID;
            fsfd.deviceID = Settings.deviceID;
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
            ftd.localID = Settings.localID;
            ftd.originID = Settings.originID;
            ftd.deviceID = Settings.deviceID;
            ftd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ftd.connectionID = Settings.connectionID;
            ftd.remoteID = Settings.remoteID;

            Debug.WriteLine("FabricTerminated: ");
            var task = callstats.FabricTerminated(ftd);
        }

        public async Task SendFabricStateChange(string prevState, string newState, string changedState)
        {
            FabricStateChangeData fscd = new FabricStateChangeData();
            fscd.localID = Settings.localID;
            fscd.originID = Settings.originID;
            fscd.deviceID = Settings.deviceID;
            fscd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fscd.remoteID = Settings.remoteID;
            fscd.connectionID = Settings.connectionID;
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
            ftcd.localID = Settings.localID;
            ftcd.originID = Settings.originID;
            ftcd.deviceID = Settings.deviceID;
            ftcd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ftcd.remoteID = Settings.remoteID;
            ftcd.connectionID = Settings.connectionID;
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

            fdd.localID = Settings.localID;
            fdd.originID = Settings.originID;
            fdd.deviceID = Settings.deviceID;
            fdd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fdd.remoteID = Settings.remoteID;
            fdd.connectionID = Settings.connectionID;
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
            fad.localID = Settings.localID;
            fad.originID = Settings.originID;
            fad.deviceID = Settings.deviceID;
            fad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fad.remoteID = Settings.remoteID;
            fad.connectionID = Settings.connectionID;

            Debug.WriteLine("SendFabricAction: ");
            var task = callstats.FabricAction(fad);
        }
        #endregion

        #region Stats Submission
        public async Task SendConferenceStatsSubmission()
        {
            ConferenceStatsSubmissionData cssd = new ConferenceStatsSubmissionData();
            cssd.localID = Settings.localID;
            cssd.originID = Settings.originID;
            cssd.deviceID = Settings.deviceID;
            cssd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            cssd.connectionID = Settings.connectionID;
            cssd.remoteID = Settings.remoteID;
            cssd.stats = SC.statsObjects;

            SC.sec = DateTime.UtcNow.ToUnixTimeStampSeconds();

            Debug.WriteLine("ConferenceStatsSubmission: ");
            await callstats.ConferenceStatsSubmission(cssd);
        }

        // TODO: add values
        public void SendSystemStatusStatsSubmission()
        {
            SystemStatusStatsSubmissionData sssd = new SystemStatusStatsSubmissionData();
            sssd.localID = Settings.localID;
            sssd.originID = Settings.originID;
            sssd.deviceID = Settings.deviceID;
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
            mad.localID = Settings.localID;
            mad.originID = Settings.originID;
            mad.deviceID = Settings.deviceID;
            mad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            mad.connectionID = Settings.connectionID;
            mad.remoteID = Settings.remoteID;
            mad.ssrc = "";
            mad.mediaDeviceID = Settings.deviceID;
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
            ids.localID = Settings.localID;
            ids.originID = Settings.originID;
            ids.deviceID = Settings.deviceID;
            ids.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ids.remoteID = Settings.remoteID;
            ids.connectionID = Settings.connectionID;
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
            ide.localID = Settings.localID;
            ide.originID = Settings.originID;
            ide.deviceID = Settings.deviceID;
            ide.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ide.remoteID = Settings.remoteID;
            ide.connectionID = Settings.connectionID;
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
            ird.localID = Settings.localID;
            ird.originID = Settings.originID;
            ird.deviceID = Settings.deviceID;
            ird.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ird.remoteID = Settings.remoteID;
            ird.connectionID = Settings.connectionID;
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
            ifd.localID = Settings.localID;
            ifd.originID = Settings.originID;
            ifd.deviceID = Settings.deviceID;
            ifd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ifd.remoteID = Settings.remoteID;
            ifd.connectionID = Settings.connectionID;
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
            iad.localID = Settings.localID;
            iad.originID = Settings.originID;
            iad.deviceID = Settings.deviceID;
            iad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            iad.remoteID = Settings.remoteID;
            iad.connectionID = Settings.connectionID;
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
            itd.localID = Settings.localID;
            itd.originID = Settings.originID;
            itd.deviceID = Settings.deviceID;
            itd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            itd.remoteID = Settings.remoteID;
            itd.connectionID = Settings.connectionID;
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
            icds.localID = Settings.localID;
            icds.originID = Settings.originID;
            icds.deviceID = Settings.deviceID;
            icds.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            icds.remoteID = Settings.remoteID;
            icds.connectionID = Settings.connectionID;
            icds.currIceConnectionState = SC.newIceConnectionState;
            icds.prevIceConnectionState = SC.prevIceConnectionState;

            Debug.WriteLine("IceConnectionDisruptionStart: ");
            await callstats.IceConnectionDisruptionStart(icds);
        }

        public async Task SendIceConnectionDisruptionEnd()
        {
            IceConnectionDisruptionEndData icde = new IceConnectionDisruptionEndData();
            icde.eventType = "iceConnectionDisruptionEnd";
            icde.localID = Settings.localID;
            icde.originID = Settings.originID;
            icde.deviceID = Settings.deviceID;
            icde.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            icde.remoteID = Settings.remoteID;
            icde.connectionID = Settings.connectionID;
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
            cadd.localID = Settings.localID;
            cadd.originID = Settings.originID;
            cadd.deviceID = Settings.deviceID;
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
            aeld.localID = Settings.localID;
            aeld.originID = Settings.originID;
            aeld.deviceID = Settings.deviceID;
            aeld.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            aeld.connectionID = Settings.connectionID;
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
            cufd.localID = Settings.localID;
            cufd.originID = Settings.originID;
            cufd.deviceID = Settings.deviceID;
            cufd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            cufd.remoteID = Settings.remoteID;
            cufd.feedback = feedbackObj;

            Debug.WriteLine("ConferenceUserFeedback: ");
            var task = callstats.ConferenceUserFeedback(cufd);
        }

        public async Task SendSSRCMap()
        {
            SSRCMapData ssrcMapData = new SSRCMapData();
            ssrcMapData.localID = Settings.localID;
            ssrcMapData.originID = Settings.originID;
            ssrcMapData.deviceID = Settings.deviceID;
            ssrcMapData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ssrcMapData.connectionID = Settings.connectionID;
            ssrcMapData.remoteID = Settings.remoteID;
            ssrcMapData.ssrcData = SC.ssrcDataList;

            Debug.WriteLine("SSRCMap: ");
            await callstats.SSRCMap(ssrcMapData);
        }

        public void SendSDP(string localSDP, string remoteSDP)
        {
            SDPEventData sdpEventData = new SDPEventData();
            sdpEventData.localID = Settings.localID;
            sdpEventData.originID = Settings.originID;
            sdpEventData.deviceID = Settings.deviceID;
            sdpEventData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            sdpEventData.connectionID = Settings.connectionID;
            sdpEventData.remoteID = Settings.remoteID;
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
