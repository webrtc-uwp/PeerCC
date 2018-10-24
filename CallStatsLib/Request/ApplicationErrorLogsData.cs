namespace CallStatsLib.Request
{
    public class ApplicationErrorLogsData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public string connectionID { get; set; }
        public string level { get; set; }
        public string message { get; set; }
        public string messageType { get; set; }
    }
}
