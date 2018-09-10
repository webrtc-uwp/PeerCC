using System.Collections.Generic;

namespace CallStatsLib.Request
{
    public class SSRCMapData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public string connectionID { get; set; }
        public string remoteID { get; set; }
        public List<SSRCData> ssrcData { get; set; }
    }

    public class SSRCData
    {
        public string ssrc { get; set; }
        public string cname { get; set; }
        public string streamType { get; set; }
        public string reportType { get; set; }
        public string mediaType { get; set; }
        public string userID { get; set; }
        public string msid { get; set; }
        public string mslabel { get; set; }
        public string label { get; set; }
        public long localStartTime { get; set; }
    }
}
