namespace CallStatsLib.Request
{
    public class SDPEventData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public string connectionID { get; set; }
        public string remoteID { get; set; }
        public string localSDP { get; set; }
        public string remoteSDP { get; set; }
    }
}
