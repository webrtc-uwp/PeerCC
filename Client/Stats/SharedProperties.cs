using CallStatsLib.Request;
using Org.WebRtc;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Networking;
using Windows.Networking.Connectivity;

namespace PeerConnectionClient.Stats
{
    public class SharedProperties
    {
        public static readonly string newConnection = RTCIceConnectionState.New.ToString().ToLower();
        public static readonly string checking = RTCIceConnectionState.Checking.ToString().ToLower();
        public static readonly string connected = RTCIceConnectionState.Connected.ToString().ToLower();
        public static readonly string completed = RTCIceConnectionState.Completed.ToString().ToLower();
        public static readonly string failed = RTCIceConnectionState.Failed.ToString().ToLower();
        public static readonly string disconnected = RTCIceConnectionState.Disconnected.ToString().ToLower();
        public static readonly string closed = RTCIceConnectionState.Closed.ToString().ToLower();

        public static readonly string newGathering = RTCIceGatheringState.New.ToString().ToLower();
        public static readonly string gathering = RTCIceGatheringState.Gathering.ToString().ToLower();
        public static readonly string complete = RTCIceGatheringState.Complete.ToString().ToLower();

        public static string userID = GetLocalPeerName();
        public static string localID = GetLocalPeerName();
        public static string appID = (string)Config.localSettings.Values["appID"];
        public static string keyID = (string)Config.localSettings.Values["keyID"];
        public static readonly string jti = new Func<string>(() =>
        {
            Random random = new Random();
            const string chars = "abcdefghijklmnopqrstuvxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const int length = 10;
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        })();

        public static string confID = Config.localSettings.Values["confID"].ToString();

        public static string originID = null;
        public static string deviceID = "desktop";
        public static string connectionID = $"{GetLocalPeerName()}-{confID}";
        public static string remoteID = "RemotePeer";

        public static string GetLocalPeerName()
        {
            var hostname = NetworkInformation.GetHostNames().FirstOrDefault(h => h.Type == HostNameType.DomainName);
            string ret = hostname?.CanonicalName ?? "<unknown host>";
            return ret.ToLower();
        }

        public static long gateheringTimeStart;
        public static long gateheringTimeStop;

        public static long connectingTimeStart;
        public static long connectingTimeStop;
    
        public static long totalSetupTimeStart;
        public static long totalSetupTimeStop;

        public static List<IceCandidate> localIceCandidates = new List<IceCandidate>();
        public static List<IceCandidate> remoteIceCandidates = new List<IceCandidate>();

        public static List<IceCandidatePair> iceCandidatePairList = new List<IceCandidatePair>();

        public static List<IceCandidateStats> iceCandidateStatsList = new List<IceCandidateStats>();
        public static List<IceCandidatePairStats> iceCandidatePairStatsList = new List<IceCandidatePairStats>();

        public static IceCandidatePair currIceCandidatePairObj;
        public static IceCandidatePair prevIceCandidatePairObj;

        public static string prevIceConnectionState;
        public static string newIceConnectionState;

        public static string prevIceGatheringState;
        public static string newIceGatheringState; 

        public static RTCIceCandidatePairStats currIceCandidatePair;

        public static long sec = 0;

        public static List<SSRCData> ssrcDataList = new List<SSRCData>();

        public static CallStatsClient callStatsClient = new CallStatsClient();

        public static List<object> statsObjects = new List<object>();

        public static string prevSelectedCandidateId;
        public static string currSelectedCandidateId;
    }

    public static class Parsing
    {
        public static Dictionary<string, Dictionary<string, string>> ParseSdp(string sdp, string searchFirstStr)
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
