namespace CallStatsLib.Request
{
    public class IceDisruptionStartData
    {
        public string eventType { get; set; }
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public string remoteID { get; set; }
        public string connectionID { get; set; }
        public IceCandidatePair currIceCandidatePair { get; set; }
        public string currIceConnectionState { get; set; }
        public string prevIceConnectionState { get; set; }
    }
}
