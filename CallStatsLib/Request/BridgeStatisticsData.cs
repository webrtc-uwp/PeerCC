namespace CallStatsLib.Request
{
    public class BridgeStatisticsData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public int cpuUsage { get; set; }
        public int batteryLevel { get; set; }
        public int memoryUsage { get; set; }
        public int totalMemory { get; set; }
        public int threadCount { get; set; }
        public int intervalSentBytes { get; set; }
        public int intervalReceivedBytes { get; set; }
        public int intervalRtpFractionLoss { get; set; }
        public int totalRtpLostPackets { get; set; }
        public int intervalAverageRtt { get; set; }
        public int intervalAverageJitter { get; set; }
        public int intervalDownloadBitrate { get; set; }
        public int intervalUploadBitrate { get; set; }
        public int audioFabricCount { get; set; }
        public int videoFabricCount { get; set; }
        public int conferenceCount { get; set; }
        public int participantCount { get; set; }
    }
}
