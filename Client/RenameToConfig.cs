using System;
using Windows.Storage;

namespace PeerConnectionClient
{
    // Rename RenameToConfig class to Config and add values
    public static class RenameToConfig
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
