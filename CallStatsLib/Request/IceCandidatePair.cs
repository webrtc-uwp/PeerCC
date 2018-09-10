namespace CallStatsLib.Request
{
    public class IceCandidatePair
    {
        public string id { get; set; }
        public string localCandidateId { get; set; }
        public string remoteCandidateId { get; set; }
        public string state { get; set; }
        public int priority { get; set; }
        public bool nominated { get; set; }
    }
}
