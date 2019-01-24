using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using Windows.Storage;

namespace PeerConnectionClientCore
{
    public static class Config
    {
        public static ApplicationDataContainer localSettings =
                ApplicationData.Current.LocalSettings;

        public static void AppSettings()
        {
            string config = @"config.txt";
            if (!File.Exists(config))
            {
                localSettings.Values["appID"] = "";
                localSettings.Values["keyID"] = "";

                // secret string
                localSettings.Values["secret"] = "";
            }
            else
            {
                Dictionary<string, string> dict = File.ReadAllLines(config)
                .Select(l => l.Split(new[] { '=' }))
                .ToDictionary(s => s[0].Trim(), s => s[1].Trim());

                localSettings.Values["appID"] = dict["appID"];
                localSettings.Values["keyID"] = dict["keyID"];
                localSettings.Values["secret"] = dict["secret"];
            }

            Random rnd = new Random();
            int rndnum = rnd.Next();
            localSettings.Values["confID"] = rndnum.ToString();

            localSettings.Values["userID"] = GetLocalPeerName();
            localSettings.Values["localID"] = GetLocalPeerName();
        }

        public static string GetLocalPeerName() =>
            IPGlobalProperties.GetIPGlobalProperties().HostName?.ToLower() ?? "<unknown host>";
    }
}
