namespace CallStatsLib.Request
{
    public class CreateConferenceData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public EndpointInfo endpointInfo { get; set; }
    }

    public class EndpointInfo
    {
        public string type { get; set; }
        public string os { get; set; }
        public string osVersion { get; set; }
        public string buildName { get; set; }
        public string buildVersion { get; set; }
        public string appVersion { get; set; }
    }
}
