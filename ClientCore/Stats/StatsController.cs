using Org.WebRtc.Callstats;
using Org.WebRtc.Callstats.Request;
using Jose;
using Org.WebRtc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace PeerConnectionClientCore.Stats
{
    /// <summary>
    /// A singleton controller for shared properties and methods used to collect stats.
    /// </summary>
    public sealed class StatsController
    {
        private static StatsController instance = null;
        private static readonly object InstanceLock = new object();

        public static StatsController Instance
        {
            get
            {
                lock (InstanceLock)
                {
                    if (instance == null)
                        instance = new StatsController();

                    return instance;
                }
            }
        }

        private StatsController()
        {
            Config.AppSettings();

            localID = (string)Config.localSettings.Values["localID"];
            appID = (string)Config.localSettings.Values["appID"];
            keyID = (string)Config.localSettings.Values["keyID"];
            confID = (string)Config.localSettings.Values["confID"];
            userID = (string)Config.localSettings.Values["userID"];

            if (appID != string.Empty)
                callStatsClient = new CallStatsClient(localID, appID, keyID, confID, userID, "desktop", "RemotePeer");
        }

        #region Properties
        private static string localID;
        private static string appID;
        private static string keyID;
        private static string confID;
        private static string userID;

        public long totalSetupTimeStart;
        public long totalSetupTimeStop;

        public List<IceCandidate> localIceCandidates = new List<IceCandidate>();
        public List<IceCandidate> remoteIceCandidates = new List<IceCandidate>();

        public List<IceCandidatePair> iceCandidatePairList = new List<IceCandidatePair>();

        public List<IceCandidateStats> iceCandidateStatsList = new List<IceCandidateStats>();
        public List<IceCandidatePairStats> iceCandidatePairStatsList = new List<IceCandidatePairStats>();

        public IceCandidatePair currIceCandidatePairObj;
        public IceCandidatePair prevIceCandidatePairObj;

        public RTCIceCandidatePairStats currIceCandidatePair;

        public long timeMilisec = 0;

        public List<SSRCData> ssrcDataList = new List<SSRCData>();

        public CallStatsClient callStatsClient;

        public List<object> statsObjects = new List<object>();

        public string prevSelectedCandidateId;
        public string currSelectedCandidateId;
        #endregion

        #region Generate JWT
        private static readonly string jti = new Func<string>(() =>
        {
            Random random = new Random();
            const string chars = "abcdefghijklmnopqrstuvxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const int length = 10;
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        })();

        public string GenerateJWT()
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

        #region SSRC Data
        public void SSRCMapDataSetup(string sdp, string streamType, string reportType)
        {
            var dict = Instance.ParseSdp(sdp, "a=ssrc:");

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

                Instance.ssrcDataList.Add(ssrcData);
            }
        }

        public Dictionary<string, Dictionary<string, string>> ParseSdp(string sdp, string searchFirstStr)
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
        #endregion

        public async Task FabricSetup(string fabricTransmissionDirection, string remoteEndpointType, long gatheringDelayMiliseconds,
            long connectivityDelayMiliseconds, long totalSetupDelay)
        {
            Instance.IceCandidateStatsData();
            Instance.AddToIceCandidatePairsList();

            await callStatsClient.SendFabricSetup(fabricTransmissionDirection, remoteEndpointType,
                gatheringDelayMiliseconds, connectivityDelayMiliseconds, totalSetupDelay,
                Instance.localIceCandidates, Instance.remoteIceCandidates, Instance.iceCandidatePairList);
        }

        public async Task FabricTransportChange(
            int delay, string relayType, string newIceConnectionState, string prevIceConnectionState)
        {
            Instance.IceCandidateStatsData();

            await callStatsClient.SendFabricTransportChange(delay, relayType, Instance.localIceCandidates,
                Instance.remoteIceCandidates, Instance.currIceCandidatePairObj, Instance.prevIceCandidatePairObj,
                newIceConnectionState, prevIceConnectionState);
        }

        public async Task ConferenceStatsSubmission()
        {
            Instance.timeMilisec = DateTime.UtcNow.ToUnixTimeStampMiliseconds();

            await callStatsClient.SendConferenceStatsSubmission(Instance.statsObjects);
        }

        #region Ice Candidates Data
        public void IceCandidateStatsData()
        {
            for (int i = 0; i < Instance.iceCandidateStatsList.Count; i++)
            {
                IceCandidateStats ics = Instance.iceCandidateStatsList[i];

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
                    Instance.localIceCandidates.Add(iceCandidate);

                if (ics.type.Contains("remote"))
                    Instance.remoteIceCandidates.Add(iceCandidate);
            }
        }

        public void AddToIceCandidatePairsList()
        {
            for (int i = 0; i < Instance.iceCandidatePairStatsList.Count; i++)
            {
                IceCandidatePairStats icps = Instance.iceCandidatePairStatsList[i];

                IceCandidatePair iceCandidatePair = new IceCandidatePair();

                iceCandidatePair.id = icps.id;
                iceCandidatePair.localCandidateId = icps.localCandidateId;
                iceCandidatePair.remoteCandidateId = icps.remoteCandidateId;
                iceCandidatePair.state = icps.state;
                iceCandidatePair.priority = 1;
                iceCandidatePair.nominated = icps.nominated;

                Instance.iceCandidatePairList.Add(iceCandidatePair);
            }
        }

        public IceCandidatePair GetIceCandidatePairData()
        {
            IceCandidatePair icp = new IceCandidatePair();
            icp.id = Instance.currIceCandidatePair.Id;
            icp.localCandidateId = Instance.currIceCandidatePair.LocalCandidateId;
            icp.remoteCandidateId = Instance.currIceCandidatePair.RemoteCandidateId;
            icp.state = Instance.currIceCandidatePair.State.ToString().ToLower();
            icp.priority = 1;
            icp.nominated = Instance.currIceCandidatePair.Nominated;

            return icp;
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
