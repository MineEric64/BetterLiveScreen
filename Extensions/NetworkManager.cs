using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Extensions
{
    public class NetworkManager
    {
        public static bool IsAvailable => NetworkInterface.GetIsNetworkAvailable();

        public static string GetPublicIPAddress()
        {
            if (!IsAvailable) return string.Empty;
            try
            {
                var web = new WebClient();
                return web.DownloadString("http://icanhazip.com").Replace("\\r\\n", "").Replace("\\n", "").Trim();
            }
            catch
            {
                Debug.WriteLine("[Error] Can't get public ip address.");
            }
            return string.Empty;
        }
    }
}
