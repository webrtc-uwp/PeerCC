using System.Collections.Generic;

namespace CallStatsLib.Request
{
    public class ConferenceStatsSubmissionData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public string connectionID { get; set; }
        public long timestamp { get; set; }
        public string remoteID { get; set; }
        public List<Stats> stats { get; set; }
    }

    public class Stats
    {
        public string tracks { get; set; }
        public string candidatePairs { get; set; } 
        public long timestamp { get; set; }
    }
}
