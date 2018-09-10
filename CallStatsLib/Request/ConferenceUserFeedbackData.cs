namespace CallStatsLib.Request
{
    public class ConferenceUserFeedbackData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public string remoteID { get; set; }
        public Feedback feedback { get; set; }
    }

    public class Feedback
    {
        public int overallRating { get; set; }
        public int videoQualityRating { get; set; }
        public int audioQualityRating { get; set; }
        public string comments { get; set; }
    }
}
