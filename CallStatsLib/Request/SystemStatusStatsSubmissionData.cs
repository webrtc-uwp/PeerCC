namespace CallStatsLib.Request
{
    public class SystemStatusStatsSubmissionData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public int cpuLevel { get; set; }
        public int batteryLevel { get; set; }
        public int memoryUsage { get; set; }
        public int memoryAvailable { get; set; }
        public int threadCount { get; set; }
    }
}
