using System;
using Windows.Storage;

namespace PeerConnectionClient
{
    public static class Config
    {
        public static ApplicationDataContainer localSettings =
                ApplicationData.Current.LocalSettings;

        public static void AppSettings()
        {
            localSettings.Values["appID"] = "";
            localSettings.Values["keyID"] = "";

            Random rnd = new Random();
            int rndnum = rnd.Next();
            localSettings.Values["confID"] = rndnum.ToString();

            // secret string
            localSettings.Values["secret"] = "";
        }
    }
}
