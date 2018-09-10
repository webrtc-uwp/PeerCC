namespace CallStatsLib.Request
{
    public class FabricStateChangeData
    {
        public string localID { get; set; }
        public string originID { get; set; }
        public string deviceID { get; set; }
        public long timestamp { get; set; }
        public string remoteID { get; set; }
        public string connectionID { get; set; }
        public string prevState { get; set; }
        public string newState { get; set; }
        public string changedState { get; set; }
    }
}
