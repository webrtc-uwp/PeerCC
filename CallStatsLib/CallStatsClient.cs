using CallStatsLib.Request;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CallStatsLib
{
    public class CallStatsClient
    {
        private static string localID;
        private static string appID;
        private static string keyID;
        private static string confID;
        private static string userID;

        public static string originID = null;
        public static string deviceID = "desktop";
        public static string connectionID = $"{localID}-{confID}";
        public static string remoteID = "RemotePeer";

        public CallStatsClient(
            string localIDCSC, string appIDCSC, string keyIDCSC, string confIDCSC, string userIDCSC)
        {
            localID = localIDCSC;
            appID = appIDCSC;
            keyID = keyIDCSC;
            confID = confIDCSC;
            userID = userIDCSC;
        }

        private CallStats callstats;

        #region Start CallStats
        public async Task SendStartCallStats(string type, string buildName, string buildVersion, 
            string appVersion, string token, long totalSetupTimeStart)
        {
            callstats = new CallStats(localID, appID, keyID, confID, token);

            await callstats.StartCallStats(
                CreateConference(type, buildName, buildVersion, appVersion), UserAlive());

            SendUserDetails();

            totalSetupTimeStart = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
        }
        #endregion

        #region User Action Events
        private CreateConferenceData CreateConference(
            string type, string buildName, string buildVersion, string appVersion)
        {
            EndpointInfo endpointInfo = new EndpointInfo();
            endpointInfo.type = type;
            endpointInfo.os = Environment.OSVersion.ToString();
            endpointInfo.buildName = buildName;
            endpointInfo.buildVersion = buildVersion;
            endpointInfo.appVersion = appVersion;

            CreateConferenceData ccd = new CreateConferenceData();
            ccd.localID = localID;
            ccd.originID = originID;
            ccd.deviceID = deviceID;
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
            uld.deviceID = deviceID;
            uld.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

            Debug.WriteLine("SendUserLeft: ");
            var task = callstats.UserLeft(uld);
        }
        #endregion

        #region Fabric Events
        public async Task SendFabricSetup(
            string fabricTransmissionDirection, string remoteEndpointType, long gatheringDelayMiliseconds, 
            long connectivityDelayMiliseconds, long totalSetupDelay, List<IceCandidate> localIceCandidates, 
            List<IceCandidate> remoteIceCandidates, List<IceCandidatePair> iceCandidatePairList)
        {
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
            fsd.fabricTransmissionDirection = fabricTransmissionDirection;
            fsd.remoteEndpointType = remoteEndpointType;
            fsd.localIceCandidates = localIceCandidates;
            fsd.remoteIceCandidates = remoteIceCandidates;
            fsd.iceCandidatePairs = iceCandidatePairList;

            Debug.WriteLine("FabricSetup: ");
            await callstats.FabricSetup(fsd);
        }

        public void SendFabricSetupFailed(
            string fabricTransmissionDirection, string remoteEndpointType, 
            string reason, string name, string message, string stack)
        {
            // MediaConfigError, MediaPermissionError, MediaDeviceError, NegotiationFailure,
            // SDPGenerationError, TransportFailure, SignalingError, IceConnectionFailure

            FabricSetupFailedData fsfd = new FabricSetupFailedData();
            fsfd.localID = localID;
            fsfd.originID = originID;
            fsfd.deviceID = deviceID;
            fsfd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fsfd.fabricTransmissionDirection = fabricTransmissionDirection;
            fsfd.remoteEndpointType = remoteEndpointType;
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

        public async Task SendFabricTransportChange(int delay, string relayType, 
            List<IceCandidate> localIceCandidates, List<IceCandidate> remoteIceCandidates, 
            IceCandidatePair currIceCandidatePairObj, IceCandidatePair prevIceCandidatePairObj, 
            string newIceConnectionState, string prevIceConnectionState)
        {
            FabricTransportChangeData ftcd = new FabricTransportChangeData();
            ftcd.localID = localID;
            ftcd.originID = originID;
            ftcd.deviceID = deviceID;
            ftcd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ftcd.remoteID = remoteID;
            ftcd.connectionID = connectionID;
            ftcd.localIceCandidates = localIceCandidates;
            ftcd.remoteIceCandidates = remoteIceCandidates;
            ftcd.currIceCandidatePair = currIceCandidatePairObj;
            ftcd.prevIceCandidatePair = prevIceCandidatePairObj;
            ftcd.currIceConnectionState = newIceConnectionState;
            ftcd.prevIceConnectionState = prevIceConnectionState;
            ftcd.delay = delay;
            ftcd.relayType = relayType;

            Debug.WriteLine("FabricTransportChange: ");
            await callstats.FabricTransportChange(ftcd);
        }

        public void SendFabricDropped(int delay, IceCandidatePair currIceCandidatePair, 
            string newIceConnectionState, string prevIceConnectionState)
        {
            FabricDroppedData fdd = new FabricDroppedData();

            fdd.localID = localID;
            fdd.originID = originID;
            fdd.deviceID = deviceID;
            fdd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fdd.remoteID = remoteID;
            fdd.connectionID = connectionID;
            fdd.currIceCandidatePair = currIceCandidatePair;
            fdd.currIceConnectionState = newIceConnectionState;
            fdd.prevIceConnectionState = prevIceConnectionState;
            fdd.delay = delay;

            Debug.WriteLine("FabricDropped: ");
            var task = callstats.FabricDropped(fdd);
        }

        // TODO: Call SendFabricAction method
        private void SendFabricAction(string eventType)
        {
            FabricActionData fad = new FabricActionData();
            fad.eventType = eventType;  // fabricHold, fabricResume
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
        public async Task SendConferenceStatsSubmission(List<object> statsObjects)
        {
            ConferenceStatsSubmissionData cssd = new ConferenceStatsSubmissionData();
            cssd.localID = localID;
            cssd.originID = originID;
            cssd.deviceID = deviceID;
            cssd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            cssd.connectionID = connectionID;
            cssd.remoteID = remoteID;
            cssd.stats = statsObjects;

            Debug.WriteLine("ConferenceStatsSubmission: ");
            await callstats.ConferenceStatsSubmission(cssd);
        }

        public void SendSystemStatusStatsSubmission(
            int cpuLevel, int batteryLevel, int memoryUsage, int memoryAvailable, int threadCount)
        {
            SystemStatusStatsSubmissionData sssd = new SystemStatusStatsSubmissionData();
            sssd.localID = localID;
            sssd.originID = originID;
            sssd.deviceID = deviceID;
            sssd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            sssd.cpuLevel = cpuLevel;
            sssd.batteryLevel = batteryLevel;
            sssd.memoryUsage = memoryUsage;
            sssd.memoryAvailable = memoryAvailable;
            sssd.threadCount = threadCount;

            Debug.WriteLine("SystemStatusStatsSubmission: ");
            var task = callstats.SystemStatusStatsSubmission(sssd);
        }
        #endregion

        #region Media Events
        public void SendMediaAction(string eventType, string ssrc, List<IceCandidate> remoteIceCandidates)
        {
            List<string> remoteIDList = new List<string>();

            for (int i = 0; i < remoteIceCandidates.Count; i++)
                remoteIDList.Add(remoteIceCandidates[i].id);

            MediaActionData mad = new MediaActionData();
            mad.eventType = eventType;
            mad.localID = localID;
            mad.originID = originID;
            mad.deviceID = deviceID;
            mad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            mad.connectionID = connectionID;
            mad.remoteID = remoteID;
            mad.ssrc = ssrc;
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
        public async Task SendIceDisruptionStart(IceCandidatePair currIceCandidatePairObj, 
            string newIceConnectionState, string prevIceConnectionState)
        {
            IceDisruptionStartData ids = new IceDisruptionStartData();
            ids.eventType = "iceDisruptionStart";
            ids.localID = localID;
            ids.originID = originID;
            ids.deviceID = deviceID;
            ids.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ids.remoteID = remoteID;
            ids.connectionID = connectionID;
            ids.currIceCandidatePair = currIceCandidatePairObj;
            ids.currIceConnectionState = newIceConnectionState;
            ids.prevIceConnectionState = prevIceConnectionState;

            Debug.WriteLine("IceDisruptionStart: ");
            await callstats.IceDisruptionStart(ids);
        }

        public async Task SendIceDisruptionEnd(IceCandidatePair currIceCandidatePairObj, 
            IceCandidatePair prevIceCandidatePairObj, string newIceConnectionState, string prevIceConnectionState)
        {
            IceDisruptionEndData ide = new IceDisruptionEndData();
            ide.eventType = "iceDisruptionEnd";
            ide.localID = localID;
            ide.originID = originID;
            ide.deviceID = deviceID;
            ide.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ide.remoteID = remoteID;
            ide.connectionID = connectionID;
            ide.currIceCandidatePair = currIceCandidatePairObj;
            ide.prevIceCandidatePair = prevIceCandidatePairObj;
            ide.currIceConnectionState = newIceConnectionState;
            ide.prevIceConnectionState = prevIceConnectionState;

            Debug.WriteLine("IceDisruptionEnd: ");
            await callstats.IceDisruptionEnd(ide);
        }

        public async Task SendIceRestart(IceCandidatePair prevIceCandidatePairObj, 
            string currIceConnectionState, string prevIceConnectionState)
        {
            IceRestartData ird = new IceRestartData();
            ird.eventType = "iceRestarted";
            ird.localID = localID;
            ird.originID = originID;
            ird.deviceID = deviceID;
            ird.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ird.remoteID = remoteID;
            ird.connectionID = connectionID;
            ird.prevIceCandidatePair = prevIceCandidatePairObj;
            ird.currIceConnectionState = currIceConnectionState;
            ird.prevIceConnectionState = prevIceConnectionState;

            Debug.WriteLine("IceRestart: ");
            await callstats.IceRestart(ird);
        }

        public async Task SendIceFailed(
            List<IceCandidate> localIceCandidates, List<IceCandidate> remoteIceCandidates, 
            List<IceCandidatePair> iceCandidatePairList, string newIceConnectionState, 
            string prevIceConnectionState, int delay)
        {
            IceFailedData ifd = new IceFailedData();
            ifd.eventType = "iceFailed";
            ifd.localID = localID;
            ifd.originID = originID;
            ifd.deviceID = deviceID;
            ifd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ifd.remoteID = remoteID;
            ifd.connectionID = connectionID;
            ifd.localIceCandidates = localIceCandidates;
            ifd.remoteIceCandidates = remoteIceCandidates;
            ifd.iceCandidatePairs = iceCandidatePairList;
            ifd.currIceConnectionState = newIceConnectionState;
            ifd.prevIceConnectionState = prevIceConnectionState;
            ifd.delay = delay;

            Debug.WriteLine("IceFailed: ");
            await callstats.IceFailed(ifd);
        }

        public async Task SendIceAborted(
            List<IceCandidate> localIceCandidates, List<IceCandidate> remoteIceCandidates, 
            List<IceCandidatePair> iceCandidatePairList, string newIceConnectionState, 
            string prevIceConnectionState, int delay)
        {
            IceAbortedData iad = new IceAbortedData();
            iad.eventType = "iceFailed";
            iad.localID = localID;
            iad.originID = originID;
            iad.deviceID = deviceID;
            iad.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            iad.remoteID = remoteID;
            iad.connectionID = connectionID;
            iad.localIceCandidates = localIceCandidates;
            iad.remoteIceCandidates = remoteIceCandidates;
            iad.iceCandidatePairs = iceCandidatePairList;
            iad.currIceConnectionState = newIceConnectionState;
            iad.prevIceConnectionState = prevIceConnectionState;
            iad.delay = delay;

            Debug.WriteLine("IceAborted: ");
            await callstats.IceAborted(iad);
        }

        public async Task SendIceTerminated(IceCandidatePair prevIceCandidatePairObj, 
            string newIceConnectionState, string prevIceConnectionState)
        {
            IceTerminatedData itd = new IceTerminatedData();
            itd.eventType = "iceTerminated";
            itd.localID = localID;
            itd.originID = originID;
            itd.deviceID = deviceID;
            itd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            itd.remoteID = remoteID;
            itd.connectionID = connectionID;
            itd.prevIceCandidatePair = prevIceCandidatePairObj;
            itd.currIceConnectionState = newIceConnectionState;
            itd.prevIceConnectionState = prevIceConnectionState;

            Debug.WriteLine("IceTerminated: ");
            await callstats.IceTerminated(itd);
        }

        public async Task SendIceConnectionDisruptionStart(
            string newIceConnectionState, string prevIceConnectionState)
        {
            IceConnectionDisruptionStartData icds = new IceConnectionDisruptionStartData();
            icds.eventType = "iceConnectionDisruptionStart";
            icds.localID = localID;
            icds.originID = originID;
            icds.deviceID = deviceID;
            icds.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            icds.remoteID = remoteID;
            icds.connectionID = connectionID;
            icds.currIceConnectionState = newIceConnectionState;
            icds.prevIceConnectionState = prevIceConnectionState;

            Debug.WriteLine("IceConnectionDisruptionStart: ");
            await callstats.IceConnectionDisruptionStart(icds);
        }

        public async Task SendIceConnectionDisruptionEnd(
            string newIceConnectionState, string prevIceConnectionState, int delay)
        {
            IceConnectionDisruptionEndData icde = new IceConnectionDisruptionEndData();
            icde.eventType = "iceConnectionDisruptionEnd";
            icde.localID = localID;
            icde.originID = originID;
            icde.deviceID = deviceID;
            icde.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            icde.remoteID = remoteID;
            icde.connectionID = connectionID;
            icde.currIceConnectionState = newIceConnectionState;
            icde.prevIceConnectionState = prevIceConnectionState;
            icde.delay = delay;

            Debug.WriteLine("IceConnectionDisruptionEnd: ");
            await callstats.IceConnectionDisruptionEnd(icde);
        }
        #endregion

        #region Device Events
        // TODO: call SendConnectedOrActiveDevices
        private void SendConnectedOrActiveDevices(string mediaDeviceID, string kind, string label, 
            string groupID, string eventType)
        {
            List<MediaDevice> mediaDeviceList = new List<MediaDevice>();
            MediaDevice mediaDeviceObj = new MediaDevice();
            mediaDeviceObj.mediaDeviceID = mediaDeviceID;
            mediaDeviceObj.kind = kind;
            mediaDeviceObj.label = label;
            mediaDeviceObj.groupID = groupID;
            mediaDeviceList.Add(mediaDeviceObj);

            ConnectedOrActiveDevicesData cadd = new ConnectedOrActiveDevicesData();
            cadd.localID = localID;
            cadd.originID = originID;
            cadd.deviceID = deviceID;
            cadd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            cadd.mediaDeviceList = mediaDeviceList;
            cadd.eventType = eventType;

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

        public async Task SendSSRCMap(List<SSRCData> ssrcDataList)
        {
            SSRCMapData ssrcMapData = new SSRCMapData();
            ssrcMapData.localID = localID;
            ssrcMapData.originID = originID;
            ssrcMapData.deviceID = deviceID;
            ssrcMapData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ssrcMapData.connectionID = connectionID;
            ssrcMapData.remoteID = remoteID;
            ssrcMapData.ssrcData = ssrcDataList;

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
