namespace CallStatsLib.Request
{
    public class IceCandidate
    {
        public string id { get; set; }
        public string type { get; set; }
        public string ip { get; set; }
        public long port { get; set; }
        public string candidateType { get; set; }
        public string transport { get; set; }
    }
}
