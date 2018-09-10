using System.Collections.Generic;

namespace CallStatsLib.Request
{
    public class IceAbortedData
    {
        public string eventType { get; set; }
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public string remoteID { get; set; }
        public string connectionID { get; set; }
        public List<IceCandidate> localIceCandidates { get; set; }
        public List<IceCandidate> remoteIceCandidates { get; set; }
        public List<IceCandidatePair> iceCandidatePairs { get; set; }
        public string currIceConnectionState { get; set; }
        public string prevIceConnectionState { get; set; }
        public int delay { get; set; }
    }
}
