using System.Collections.Generic;

namespace CallStatsLib.Response
{
    class AuthenticationResponse
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public List<AuthIceServer> iceServers { get; set; }
    }

    class AuthIceServer
    {
        public List<string> urls { get; set; }
        public string username { get; set; }
        public string credential { get; set; }
    }
}
