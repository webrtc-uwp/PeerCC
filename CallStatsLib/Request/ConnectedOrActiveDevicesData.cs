using System.Collections.Generic;

namespace CallStatsLib.Request
{
    public class ConnectedOrActiveDevicesData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public List<MediaDevice> mediaDeviceList { get; set; }
        public string eventType { get; set; }
    }

    public class MediaDevice
    {
        public string mediaDeviceID { get; set; }
        public string kind { get; set; }
        public string label { get; set; }
        public string groupID { get; set; }
    }
}
