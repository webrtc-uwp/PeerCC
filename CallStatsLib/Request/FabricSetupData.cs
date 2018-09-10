using System.Collections.Generic;

namespace CallStatsLib.Request
{
    public class FabricSetupData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public string remoteID { get; set; }
        public int delay { get; set; }
        public string connectionID { get; set; }
        public int iceGatheringDelay { get; set; }
        public int iceConnectivityDelay { get; set; }
        public string fabricTransmissionDirection { get; set; }
        public string remoteEndpointType { get; set; }
        public List<IceCandidate> localIceCandidates { get; set; }
        public List<IceCandidate> remoteIceCandidates { get; set; }
        public List<IceCandidatePair> iceCandidatePairs { get; set; }
    }
}
