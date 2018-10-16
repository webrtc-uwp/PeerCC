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
        private static string _connectionID = $"{GetLocalPeerName()}-{_jti}";
        private static string _remoteID = "RemotePeer";

        private CallStats callstats;

        private List<object> statsObjects = new List<object>();

        private FabricSetupData fabricSetupData = new FabricSetupData();

        private List<IceCandidatePairStats> iceCandidatePairs = new List<IceCandidatePairStats>();
        private List<IceCandidatePair> _iceCandidatePairsList = new List<IceCandidatePair>();

        private List<IceCandidateStats> iceCandidateStatsList = new List<IceCandidateStats>();
        private List<IceCandidate> _localIceCandidates = new List<IceCandidate>();
        private List<IceCandidate> _remoteIceCandidates = new List<IceCandidate>();

        private SSRCMapData ssrcMapData = new SSRCMapData();
        private List<SSRCData> ssrcDataList = new List<SSRCData>();

        private ConferenceStatsSubmissionData conferenceStatsSubmissionData = new ConferenceStatsSubmissionData();

        Stopwatch _setupClock;
        Stopwatch _gatheringClock;
        Stopwatch _connectivityClock;

        private IceCandidatePair _currIceCandidatePairObj;
        private IceCandidatePair _prevIceCandidatePairObj;

        private string _prevIceConnectionState;
        private string _newIceConnectionState;

        private int _gatheringDelayMiliseconds;
        private int _connectivityDelayMiliseconds;
        private int _totalSetupDelay;

        private string _prevIceGatheringState;
        private string _newIceGatheringState;

        private RTCIceCandidatePairStats _currIceCandidatePair;
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
            _setupClock = Stopwatch.StartNew();

            callstats = new CallStats(_localID, _appID, _keyID, _confID, GenerateJWT());

            await callstats.StartCallStats(CreateConference(), UserAlive());
            
            _gatheringClock = Stopwatch.StartNew();
            _connectivityClock = Stopwatch.StartNew();

            _setupClock.Stop();

            Debug.WriteLine("UserDetails: ");
            await SendUserDetails();
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

            CreateConferenceData data = new CreateConferenceData();
            data.localID = _localID;
            data.originID = _originID;
            data.deviceID = GetLocalPeerName();
            data.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            data.endpointInfo = endpointInfo;

            return data;
        }

        private UserAliveData UserAlive()
        {
            UserAliveData data = new UserAliveData();
            data.localID = _localID;
            data.originID = _originID;
            data.deviceID = _deviceID;
            data.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

            return data;
        }

        private async Task SendUserDetails()
        {
            UserDetailsData userDetailsData = new UserDetailsData();
            userDetailsData.localID = _localID;
            userDetailsData.originID = _originID;
            userDetailsData.deviceID = _deviceID;
            userDetailsData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            userDetailsData.userName = _userID;

            Debug.WriteLine("UserDetails: ");
            await callstats.UserDetails(userDetailsData);
        }

        public async Task SendUserLeft()
        {
            UserLeftData userLeftData = new UserLeftData();
            userLeftData.localID = _localID;
            userLeftData.originID = _originID;
            userLeftData.deviceID = GetLocalPeerName();
            userLeftData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

            Debug.WriteLine("SendUserLeft: ");
            await callstats.UserLeft(userLeftData);
        }
        #endregion

        #region Fabric Events
        private async Task SendFabricSetup(int gatheringDelayMiliseconds, int connectivityDelayMiliseconds, int totalSetupDelay)
        {
            IceCandidateStatsData();
            AddToIceCandidatePairsList();

            fabricSetupData.localID = _localID;
            fabricSetupData.originID = _originID;
            fabricSetupData.deviceID = _deviceID;
            fabricSetupData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fabricSetupData.connectionID = _connectionID;
            fabricSetupData.remoteID = _remoteID;
            fabricSetupData.delay = totalSetupDelay;
            fabricSetupData.iceGatheringDelay = gatheringDelayMiliseconds;
            fabricSetupData.iceConnectivityDelay = connectivityDelayMiliseconds;
            fabricSetupData.fabricTransmissionDirection = "sendrecv";
            fabricSetupData.remoteEndpointType = "peer";
            fabricSetupData.localIceCandidates = _localIceCandidates;
            fabricSetupData.remoteIceCandidates = _remoteIceCandidates;
            fabricSetupData.iceCandidatePairs = _iceCandidatePairsList;

            Debug.WriteLine("FabricSetup: ");
            await callstats.FabricSetup(fabricSetupData);
        }

        private async Task SendFabricSetupFailed(string reason, string name, string message, string stack)
        {
            // MediaConfigError, MediaPermissionError, MediaDeviceError, NegotiationFailure,
            // SDPGenerationError, TransportFailure, SignalingError, IceConnectionFailure

            FabricSetupFailedData fabricSetupFailedData = new FabricSetupFailedData();
            fabricSetupFailedData.localID = _localID;
            fabricSetupFailedData.originID = _originID;
            fabricSetupFailedData.deviceID = _deviceID;
            fabricSetupFailedData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fabricSetupFailedData.fabricTransmissionDirection = "sendrecv";
            fabricSetupFailedData.remoteEndpointType = "peer";
            fabricSetupFailedData.reason = reason;
            fabricSetupFailedData.name = name;
            fabricSetupFailedData.message = message;
            fabricSetupFailedData.stack = stack;

            Debug.WriteLine("FabricSetupFailed: ");
            await callstats.FabricSetupFailed(fabricSetupFailedData);
        }

        private async Task SendFabricSetupTerminated()
        {
            FabricTerminatedData fabricTerminatedData = new FabricTerminatedData();
            fabricTerminatedData.localID = _localID;
            fabricTerminatedData.originID = _originID;
            fabricTerminatedData.deviceID = _deviceID;
            fabricTerminatedData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fabricTerminatedData.connectionID = _connectionID;
            fabricTerminatedData.remoteID = _remoteID;

            Debug.WriteLine("FabricTerminated: ");
            await callstats.FabricTerminated(fabricTerminatedData);
        }

        private async Task SendFabricStateChange(string prevState, string newState, string changedState)
        {
            FabricStateChangeData fabricStateChangeData = new FabricStateChangeData();
            fabricStateChangeData.localID = _localID;
            fabricStateChangeData.originID = _originID;
            fabricStateChangeData.deviceID = _deviceID;
            fabricStateChangeData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fabricStateChangeData.remoteID = _remoteID;
            fabricStateChangeData.connectionID = _connectionID;
            fabricStateChangeData.prevState = prevState;
            fabricStateChangeData.newState = newState;
            fabricStateChangeData.changedState = changedState;

            Debug.WriteLine("FabricStateChange: ");
            await callstats.FabricStateChange(fabricStateChangeData);
        }

        private async Task SendFabricTransportChange()
        {
            FabricTransportChangeData fabricTransportChangeData = new FabricTransportChangeData();
            fabricTransportChangeData.localID = _localID;
            fabricTransportChangeData.originID = _originID;
            fabricTransportChangeData.deviceID = _deviceID;
            fabricTransportChangeData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fabricTransportChangeData.remoteID = _remoteID;
            fabricTransportChangeData.connectionID = _connectionID;
            fabricTransportChangeData.localIceCandidates = null;
            fabricTransportChangeData.remoteIceCandidates = null;
            fabricTransportChangeData.currIceCandidatePair = _currIceCandidatePairObj;
            fabricTransportChangeData.prevIceCandidatePair = _prevIceCandidatePairObj;
            fabricTransportChangeData.currIceConnectionState = _prevIceConnectionState;
            fabricTransportChangeData.prevIceConnectionState = _newIceConnectionState;
            fabricTransportChangeData.delay = 2;
            fabricTransportChangeData.relayType = "";

            Debug.WriteLine("FabricTransportChange: ");
            await callstats.FabricTransportChange(fabricTransportChangeData);
        }

        private async Task SendFabricDropped(string currIceConnectionState, string prevIceConnectionState, int delay)
        {
            FabricDroppedData fdd = new FabricDroppedData();

            fdd.localID = _localID;
            fdd.originID = _originID;
            fdd.deviceID = _deviceID;
            fdd.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            fdd.remoteID = _remoteID;
            fdd.connectionID = _connectionID;
            fdd.currIceCandidatePair = GetIceCandidatePairData();
            fdd.currIceConnectionState = currIceConnectionState;
            fdd.prevIceConnectionState = prevIceConnectionState;
            fdd.delay = delay;

            Debug.WriteLine("FabricDropped: ");
            await callstats.FabricDropped(fdd);
        }

        private async Task SendFabricAction()
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
            await callstats.FabricAction(fad);
        }
        #endregion

        #region Stats Submission
        private async Task ConferenceStatsSubmission()
        {
            conferenceStatsSubmissionData.localID = _localID;
            conferenceStatsSubmissionData.originID = _originID;
            conferenceStatsSubmissionData.deviceID = _deviceID;
            conferenceStatsSubmissionData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            conferenceStatsSubmissionData.connectionID = _connectionID;
            conferenceStatsSubmissionData.remoteID = _remoteID;
            conferenceStatsSubmissionData.stats = statsObjects;

            Debug.WriteLine("ConferenceStatsSubmission: ");
            await callstats.ConferenceStatsSubmission(conferenceStatsSubmissionData);
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
            ird.currIceConnectionState = "new";
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
            ifd.iceCandidatePairs = _iceCandidatePairsList;
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
            iad.iceCandidatePairs = _iceCandidatePairsList;
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

        #region Special Events
        public void SSRCMapDataSetup(string sdp, string streamType, string reportType)
        {
            var dict = ParseSdp(sdp, "a=ssrc:");

            foreach (var d in dict)
            {
                SSRCData ssrcData = new SSRCData();

                ssrcData.ssrc = d.Key;

                foreach (var k in d.Value)
                {
                    if (k.Key == "cname") ssrcData.cname = k.Value.Replace("\r", "");
                    if (k.Key == "msid") ssrcData.msid = k.Value.Replace("\r", "");
                    if (k.Key == "mslabel") ssrcData.mslabel = k.Value.Replace("\r", "");
                    if (k.Key == "label")
                    {
                        ssrcData.label = k.Value.Replace("\r", "");

                        if (k.Value.Contains("audio")) ssrcData.mediaType = "audio";
                        if (k.Value.Contains("video")) ssrcData.mediaType = "video";
                        if (k.Value.Contains("screen")) ssrcData.mediaType = "screen";
                    }
                }

                ssrcData.streamType = streamType;
                ssrcData.reportType = reportType;
                ssrcData.userID = _userID;
                ssrcData.localStartTime = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                ssrcDataList.Add(ssrcData);
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
            ssrcMapData.localID = _localID;
            ssrcMapData.originID = _originID;
            ssrcMapData.deviceID = _deviceID;
            ssrcMapData.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
            ssrcMapData.connectionID = _connectionID;
            ssrcMapData.remoteID = _remoteID;
            ssrcMapData.ssrcData = ssrcDataList;

            Debug.WriteLine("SSRCMap: ");
            await callstats.SSRCMap(ssrcMapData);
        }

        public async Task SendSDP(string localSDP, string remoteSDP)
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
            await callstats.SDPEvent(sdpEventData);
        }
        #endregion

        #region Stats OnIceConnectionStateChange
        public async Task StatsOnIceConnectionStateChange(RTCPeerConnection pc)
        {
            if (pc.IceConnectionState.ToString() == "Checking")
            {
                if (_newIceConnectionState != "checking")
                {
                    await SetIceConnectionStates(pc);
                }

                if (_prevIceConnectionState == "connected" || _prevIceConnectionState == "completed"
                    || _prevIceConnectionState == "failed" || _prevIceConnectionState == "disconnected"
                    || _prevIceConnectionState == "closed")
                {
                    await SendIceRestart();
                }

                if (_prevIceConnectionState == "disconnected")
                {
                    _prevIceCandidatePairObj = _currIceCandidatePairObj;

                    await GetAllStats(pc);

                    _currIceCandidatePairObj = GetIceCandidatePairData();

                    await SendIceDisruptionEnd();

                    await SendIceConnectionDisruptionEnd();
                }
            }

            if (pc.IceConnectionState.ToString() == "Connected")
            {
                _connectivityClock.Stop();

                _connectivityDelayMiliseconds = _connectivityClock.Elapsed.Milliseconds;
                _totalSetupDelay = _setupClock.Elapsed.Milliseconds;

                if (_newIceConnectionState != "connected")
                {
                    await SetIceConnectionStates(pc);
                }

                await GetAllStats(pc);

                //fabricSetup must be sent whenever iceConnectionState changes from "checking" to "connected" state.
                await SendFabricSetup(_gatheringDelayMiliseconds, _connectivityDelayMiliseconds, _totalSetupDelay);

                if (_prevIceConnectionState == "completed")
                {
                    await GetAllStats(pc);

                    _currIceCandidatePairObj = GetIceCandidatePairData();
                    
                    await SendFabricTransportChange();

                    _prevIceCandidatePairObj = _currIceCandidatePairObj;
                }

                if (_prevIceConnectionState == "disconnected")
                {
                    _prevIceCandidatePairObj = _currIceCandidatePairObj;

                    await GetAllStats(pc);

                    _currIceCandidatePairObj = GetIceCandidatePairData();

                    await SendIceDisruptionEnd();
                }
                    
                System.Timers.Timer timer = new System.Timers.Timer(10000);
                timer.Elapsed += async (sender, e) =>
                {
                    await GetAllStats(pc);

                    Debug.WriteLine("ConferenceStatsSubmission: ");
                    await ConferenceStatsSubmission();
                };
                timer.Start();
            }

            if (pc.IceConnectionState.ToString() == "Completed")
            {
                if (_newIceConnectionState != "completed")
                {
                    await SetIceConnectionStates(pc);
                }

                if (_prevIceConnectionState == "connected")
                {
                    await GetAllStats(pc);

                    _currIceCandidatePairObj = GetIceCandidatePairData();

                    await SendFabricTransportChange();

                    _prevIceCandidatePairObj = _currIceCandidatePairObj;
                }

                if (_prevIceConnectionState == "disconnected")
                {
                    _prevIceCandidatePairObj = _currIceCandidatePairObj;

                    await GetAllStats(pc);

                    _currIceCandidatePairObj = GetIceCandidatePairData();

                    await SendIceDisruptionEnd();
                }
            }

            if (pc.IceConnectionState.ToString() == "Failed")
            {
                if (_newIceConnectionState != "failed")
                {
                    await SetIceConnectionStates(pc);
                }

                if (_prevIceConnectionState == "checking" || _prevIceConnectionState == "disconnected")
                {
                    await SendIceFailed();
                }

                await GetAllStats(pc);

                await SendFabricDropped(_newIceConnectionState, _prevIceConnectionState, 0);

                await SendFabricSetupFailed("IceConnectionFailure", string.Empty, string.Empty, string.Empty);
            }

            if (pc.IceConnectionState.ToString() == "Disconnected")
            {
                if (_newIceConnectionState != "disconnected")
                {
                    await SetIceConnectionStates(pc);
                }

                if (_prevIceConnectionState == "connected" || _prevIceConnectionState == "completed")
                {
                    await GetAllStats(pc);

                    _currIceCandidatePairObj = GetIceCandidatePairData();

                    await SendIceDisruptionStart();
                }

                if (_prevIceConnectionState == "checking")
                {
                    await SendIceConnectionDisruptionStart();
                }
            }

            if (pc.IceConnectionState.ToString() == "Closed")
            {
                if (_newIceConnectionState != "closed")
                {
                    await SetIceConnectionStates(pc);
                }

                await SendFabricSetupTerminated();

                if (_prevIceConnectionState == "checking" || _prevIceConnectionState == "new")
                {
                    await SendIceAborted();
                }

                if (_prevIceConnectionState == "connected" || _prevIceConnectionState == "completed" 
                    || _prevIceConnectionState == "failed" || _prevIceConnectionState == "disconnected")
                {
                    await SendIceTerminated();
                }
            }
        }

        private async Task SetIceConnectionStates(RTCPeerConnection pc)
        {
            if (_prevIceConnectionState == null || _newIceConnectionState == null)
            {
                _prevIceConnectionState = "new";
                _newIceConnectionState = pc.IceConnectionState.ToString().ToLower();
            }
            else
            {
                _prevIceConnectionState = _newIceConnectionState;
                _newIceConnectionState = _newIceConnectionState = pc.IceConnectionState.ToString().ToLower(); ;
            }

            await SendFabricStateChange(_prevIceConnectionState, _newIceConnectionState, "iceConnectionState");
        }
        #endregion

        #region Stats OnIceGatheringStateChange
        public async Task StatsOnIceGatheringStateChange(RTCPeerConnection pc)
        {
            if (pc.IceGatheringState.ToString() == "Gathering")
            {
                if (_newIceGatheringState != "gathering")
                {
                    if (_prevIceGatheringState == null || _newIceGatheringState == null)
                    {
                        _prevIceGatheringState = "complete";
                        _newIceGatheringState = pc.IceGatheringState.ToString().ToLower();
                    }
                    else
                    {
                        _prevIceGatheringState = _newIceGatheringState;
                        _newIceGatheringState = pc.IceGatheringState.ToString().ToLower();
                    }

                    await SendFabricStateChange(_prevIceGatheringState, _newIceGatheringState, "iceGatheringState");
                }
            }

            if (pc.IceGatheringState.ToString() == "Complete")
            {
                _gatheringClock.Stop();

                _gatheringDelayMiliseconds = _gatheringClock.Elapsed.Milliseconds;

                if (_newIceGatheringState != "complete")
                {
                    if (_prevIceGatheringState == null || _newIceGatheringState == null)
                    {
                        _prevIceGatheringState = "complete";
                        _newIceGatheringState = pc.IceGatheringState.ToString().ToLower();
                    }
                    else
                    {
                        _prevIceGatheringState = _newIceGatheringState;
                        _newIceGatheringState = pc.IceGatheringState.ToString().ToLower();
                    }

                    await SendFabricStateChange(_prevIceGatheringState, _newIceGatheringState, "iceGatheringState");
                }
            }
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
                        RTCIceCandidateStats iceCandidateStats;

                        iceCandidateStats = RTCIceCandidateStats.Cast(rtcStats);

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

                        iceCandidateStatsList.Add(ics);

                        statsObjects.Add(ics);
                    }
                }

                if (statsType == RTCStatsType.Codec)
                {
                    RTCCodecStats codecStats;

                    codecStats = RTCCodecStats.Cast(rtcStats);

                    Debug.WriteLine($"codec: {codecStats}");

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

                    statsObjects.Add(cs);
                }

                if (statsType == RTCStatsType.InboundRtp)
                {
                    RTCInboundRtpStreamStats inboundRtpStats;

                    inboundRtpStats = RTCInboundRtpStreamStats.Cast(rtcStats);

                    Debug.WriteLine($"inboundRtp: {inboundRtpStats}");

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

                    statsObjects.Add(irss);
                }

                if (statsType == RTCStatsType.OutboundRtp)
                {
                    RTCOutboundRtpStreamStats outboundRtpStats;

                    outboundRtpStats = RTCOutboundRtpStreamStats.Cast(rtcStats);

                    Debug.WriteLine($"outboundRtp: {outboundRtpStats}");

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

                    statsObjects.Add(orss);
                }

                if (statsType == RTCStatsType.RemoteInboundRtp)
                {
                    RTCRemoteInboundRtpStreamStats remoteInboundRtpStats;

                    remoteInboundRtpStats = RTCRemoteInboundRtpStreamStats.Cast(rtcStats);

                    Debug.WriteLine($"remoteInboundRtp: {remoteInboundRtpStats}");

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

                    statsObjects.Add(rirss);
                }

                if (statsType == RTCStatsType.RemoteOutboundRtp)
                {
                    RTCRemoteOutboundRtpStreamStats remoteOutboundRtpStats;

                    remoteOutboundRtpStats = RTCRemoteOutboundRtpStreamStats.Cast(rtcStats);

                    Debug.WriteLine($"remoteOutboundRtp: {remoteOutboundRtpStats}");

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

                    statsObjects.Add(rorss);
                }

                if (statsType == RTCStatsType.Csrc)
                {
                    RTCRtpContributingSourceStats csrcStats;

                    csrcStats = RTCRtpContributingSourceStats.Cast(rtcStats);

                    Debug.WriteLine($"csrc: {csrcStats}");

                    RtpContributingSourceStats rcss = new RtpContributingSourceStats();
                    rcss.audioLevel = csrcStats.AudioLevel;
                    rcss.contributorSsrc = csrcStats.ContributorSsrc;
                    rcss.id = csrcStats.Id;
                    rcss.inboundRtpStreamId = csrcStats.InboundRtpStreamId;
                    rcss.packetsContributedTo = csrcStats.PacketsContributedTo;
                    rcss.type = csrcStats.StatsType.ToString().ToLower();
                    rcss.statsTypeOther = csrcStats.StatsTypeOther;
                    rcss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                    statsObjects.Add(rcss);
                }

                if (statsType == RTCStatsType.PeerConnection)
                {
                    RTCPeerConnectionStats peerConnectionStats;

                    peerConnectionStats = RTCPeerConnectionStats.Cast(rtcStats);

                    Debug.WriteLine($"peerConnectionStats: {peerConnectionStats.ToString()}");

                    PeerConnectionStats pcs = new PeerConnectionStats();
                    pcs.dataChannelsAccepted = peerConnectionStats.DataChannelsAccepted;
                    pcs.dataChannelsClosed = peerConnectionStats.DataChannelsClosed;
                    pcs.dataChannelsOpened = peerConnectionStats.DataChannelsOpened;
                    pcs.dataChannelsRequested = peerConnectionStats.DataChannelsRequested;
                    pcs.id = peerConnectionStats.Id;
                    pcs.type = "peer-connection";
                    pcs.statsTypeOther = peerConnectionStats.StatsTypeOther;
                    pcs.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                    statsObjects.Add(pcs);
                }

                if (statsType == RTCStatsType.DataChannel)
                {
                    RTCDataChannelStats dataChannelStats;

                    dataChannelStats = RTCDataChannelStats.Cast(rtcStats);

                    Debug.WriteLine($"dataChannel: {dataChannelStats}");

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

                    statsObjects.Add(dc);
                }

                if (statsType == RTCStatsType.Stream)
                {
                    RTCMediaStreamStats mediaStreamStats;

                    mediaStreamStats = RTCMediaStreamStats.Cast(rtcStats);

                    Debug.WriteLine($"mediaStream: {mediaStreamStats.ToString()}");

                    MediaStreamStats mss = new MediaStreamStats();
                    mss.id = mediaStreamStats.Id;
                    mss.type = mediaStreamStats.StatsType.ToString().ToLower();
                    mss.statsTypeOther = mediaStreamStats.StatsTypeOther;
                    mss.streamIdentifier = mediaStreamStats.StreamIdentifier;
                    mss.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();
                    mss.trackIds = mediaStreamStats.TrackIds.ToList();

                    statsObjects.Add(mss);
                }

                if (statsType == RTCStatsType.Track)
                {
                    if (rtcStats.Id == "RTCMediaStreamTrack_sender_1")
                    {
                        RTCSenderVideoTrackAttachmentStats videoTrackStats;

                        videoTrackStats = RTCSenderVideoTrackAttachmentStats.Cast(rtcStats);

                        Debug.WriteLine($"videoTrack: {videoTrackStats}");

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

                        statsObjects.Add(svtas);
                    }

                    if (rtcStats.Id == "RTCMediaStreamTrack_sender_2")
                    {
                        RTCSenderAudioTrackAttachmentStats audioTrackStats;

                        audioTrackStats = RTCSenderAudioTrackAttachmentStats.Cast(rtcStats);

                        Debug.WriteLine($"audioTrack: {audioTrackStats}");

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

                        statsObjects.Add(satas);
                    }
                }
                //TODO: if?
                if (statsType == RTCStatsType.Sender)
                {
                    RTCAudioSenderStats audioSenderStats;

                    audioSenderStats = RTCAudioSenderStats.Cast(rtcStats);

                    Debug.WriteLine($"audioSender: {audioSenderStats}");

                    RTCVideoSenderStats videoSenderStats;

                    videoSenderStats = RTCVideoSenderStats.Cast(rtcStats);

                    Debug.WriteLine($"videoSender: {videoSenderStats}");
                }

                if (statsType == RTCStatsType.Receiver)
                {
                    RTCAudioReceiverStats audioReceiverStats;

                    audioReceiverStats = RTCAudioReceiverStats.Cast(rtcStats);

                    Debug.WriteLine($"audioReceiver: {audioReceiverStats}");

                    RTCVideoReceiverStats videoReceiverStats;

                    videoReceiverStats = RTCVideoReceiverStats.Cast(rtcStats);

                    Debug.WriteLine($"videoReceiver: {videoReceiverStats}");
                }

                if (statsType == RTCStatsType.Transport)
                {
                    RTCTransportStats transportStats;

                    transportStats = RTCTransportStats.Cast(rtcStats);

                    Debug.WriteLine($"transport: {transportStats}");

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

                    statsObjects.Add(ts);
                }

                if (statsType == RTCStatsType.CandidatePair)
                {
                    RTCIceCandidatePairStats candidatePairStats;

                    candidatePairStats = RTCIceCandidatePairStats.Cast(rtcStats);

                    Debug.WriteLine($"candidatePair: {candidatePairStats}");

                    IceCandidatePairStats icp = new IceCandidatePairStats();
                    //icp.availableIncomingBitrate = candidatePairStats.AvailableIncomingBitrate;
                    //icp.availableOutgoingBitrate = candidatePairStats.AvailableOutgoingBitrate;
                    icp.bytesReceived = candidatePairStats.BytesReceived;
                    icp.bytesSent = candidatePairStats.BytesSent;
                    //icp.circuitBreakerTriggerCount = candidatePairStats.CircuitBreakerTriggerCount;
                    //icp.consentExpiredTimestamp = candidatePairStats.ConsentExpiredTimestamp;
                    icp.consentRequestsSent = candidatePairStats.ConsentRequestsSent;
                    //icp.currentRoundTripTime = candidatePairStats.CurrentRoundTripTime;
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
                    icp.totalRoundTripTime = candidatePairStats.TotalRoundTripTime;
                    icp.transportId = candidatePairStats.TransportId;

                    if (!candidatePairsDict.ContainsKey(candidatePairStats.LocalCandidateId))
                        candidatePairsDict.Add(candidatePairStats.LocalCandidateId, "local-candidate");

                    if (!candidatePairsDict.ContainsKey(candidatePairStats.RemoteCandidateId))
                        candidatePairsDict.Add(candidatePairStats.RemoteCandidateId, "remote-candidate");

                    iceCandidatePairs.Add(icp);

                    _currIceCandidatePair = candidatePairStats;
                }

                if (statsType == RTCStatsType.Certificate)
                {
                    RTCCertificateStats certificateStats;

                    certificateStats = RTCCertificateStats.Cast(rtcStats);

                    Debug.WriteLine($"certificate: {certificateStats}");

                    CertificateStats cs = new CertificateStats();
                    cs.base64Certificate = certificateStats.Base64Certificate;
                    cs.fingerprint = certificateStats.Fingerprint;
                    cs.fingerprintAlgorithm = certificateStats.FingerprintAlgorithm;
                    cs.id = certificateStats.Id;
                    cs.issuerCertificateId = certificateStats.IssuerCertificateId;
                    cs.type = certificateStats.StatsType.ToString().ToLower();
                    cs.statsTypeOther = certificateStats.StatsTypeOther;
                    cs.timestamp = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

                    statsObjects.Add(cs);
                }
            }
        }
        #endregion

        #region Ice Candidates Data
        private void AddToIceCandidatePairsList()
        {
            for (int i = 0; i < iceCandidatePairs.Count; i++)
            {
                IceCandidatePairStats icps = iceCandidatePairs[i];

                IceCandidatePair iceCandidatePair = new IceCandidatePair();

                iceCandidatePair.id = icps.id;
                iceCandidatePair.localCandidateId = icps.localCandidateId;
                iceCandidatePair.remoteCandidateId = icps.remoteCandidateId;
                iceCandidatePair.state = icps.state;
                iceCandidatePair.priority = 1;
                iceCandidatePair.nominated = icps.nominated;

                _iceCandidatePairsList.Add(iceCandidatePair);
            }
        }

        private void IceCandidateStatsData()
        {
            for (int i = 0; i < iceCandidateStatsList.Count; i++)
            {
                IceCandidateStats ics = iceCandidateStatsList[i];

                IceCandidate iceCandidate = new IceCandidate();

                iceCandidate.id = ics.id;
                iceCandidate.type = ics.type;
                iceCandidate.ip = ics.ip;
                iceCandidate.port = ics.port;
                iceCandidate.candidateType = ics.candidateType;
                iceCandidate.transport = ics.protocol;

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
            icp.state = _currIceCandidatePair.State.ToString();
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
