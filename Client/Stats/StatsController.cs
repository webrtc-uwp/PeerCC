using CallStatsLib.Request;
using Jose;
using Org.WebRtc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;

namespace PeerConnectionClient.Stats
{
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

        private StatsController() {}

        
        //{
        //    return IPGlobalProperties.GetIPGlobalProperties().HostName?.ToLower() ?? "<unknown host>";

        //    //HostName hostname =
        //    //    NetworkInformation.GetHostNames().FirstOrDefault(h => h.Type == HostNameType.DomainName);

        //    //return hostname?.CanonicalName.ToLower() ?? "<unknown host>";
        //}

        public long gateheringTimeStart;
        public long gateheringTimeStop;

        public long connectingTimeStart;
        public long connectingTimeStop;

        public long totalSetupTimeStart;
        public long totalSetupTimeStop;

        public List<IceCandidate> localIceCandidates = new List<IceCandidate>();
        public List<IceCandidate> remoteIceCandidates = new List<IceCandidate>();

        public List<IceCandidatePair> iceCandidatePairList = new List<IceCandidatePair>();

        public List<IceCandidateStats> iceCandidateStatsList = new List<IceCandidateStats>();
        public List<IceCandidatePairStats> iceCandidatePairStatsList = new List<IceCandidatePairStats>();

        public IceCandidatePair currIceCandidatePairObj;
        public IceCandidatePair prevIceCandidatePairObj;

        public string prevIceConnectionState;
        public string newIceConnectionState;

        public string prevIceGatheringState;
        public string newIceGatheringState;

        public RTCIceCandidatePairStats currIceCandidatePair;

        public long sec = 0;

        public List<SSRCData> ssrcDataList = new List<SSRCData>();

        public CallStatsClient callStatsClient = new CallStatsClient();

        public List<object> statsObjects = new List<object>();

        public string prevSelectedCandidateId;
        public string currSelectedCandidateId;

        #region Generate JWT
        public string GenerateJWT()
        {
            var header = new Dictionary<string, object>()
            {
                { "typ", "JWT" },
                { "alg", "ES256" }
            };

            var payload = new Dictionary<string, object>()
            {
                { "userID", Settings.localID},
                { "appID", Settings.appID},
                { "keyID", Settings.keyID },
                { "iat", DateTime.UtcNow.ToUnixTimeStampSeconds() },
                { "nbf", DateTime.UtcNow.AddMinutes(-5).ToUnixTimeStampSeconds() },
                { "exp", DateTime.UtcNow.AddHours(1).ToUnixTimeStampSeconds() },
                { "jti", Settings.jti }
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
    }

    public static class Settings
    {
        public static string userID = GetLocalPeerName();
        public static string localID = GetLocalPeerName();
        public static string appID = (string)Config.localSettings.Values["appID"];
        public static string keyID = (string)Config.localSettings.Values["keyID"];
        public static string confID = Config.localSettings.Values["confID"].ToString();

        public static readonly string jti = new Func<string>(() =>
        {
            Random random = new Random();
            const string chars = "abcdefghijklmnopqrstuvxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const int length = 10;
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        })();

        public static string originID = null;
        public static string deviceID = "desktop";
        public static string connectionID = $"{GetLocalPeerName()}-{confID}";
        public static string remoteID = "RemotePeer";

        public static string GetLocalPeerName() =>
            IPGlobalProperties.GetIPGlobalProperties().HostName?.ToLower() ?? "<unknown host>";
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
