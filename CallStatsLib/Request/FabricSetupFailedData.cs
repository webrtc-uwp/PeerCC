namespace CallStatsLib.Request
{
    public class FabricSetupFailedData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public string fabricTransmissionDirection { get; set; }
        public string remoteEndpointType { get; set; }
        public string reason { get; set; }
        public string name { get; set; }
        public string message { get; set; }
        public string stack { get; set; }
    }
}
