using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using MessagePack;

using BetterLiveScreen.Server.Extensions;
using BetterLiveScreen.Server.Users;
using BetterLiveScreen.Interfaces;

namespace BetterLiveScreen.Server
{
    public class Program
    {
        public static ServerOne One { get; set; } = new ServerOne();
        internal static Dictionary<string, UserInfo> Users { get; set; } = new Dictionary<string, UserInfo>();

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(Program_ProcessExited);
            WriteServerInfo();

            while (One.IsReady)
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                ReceiveInfo info = ReceiveInfo.Empty;

               
            }
        }

        private static void Program_ProcessExited(object? sender, EventArgs e)
        {
            One.Close();
        }

        static void WriteServerInfo()
        {
            IPAddress ip = NetworkManager.GetExternal();
            Console.WriteLine($"Server Address : {ip}");
        }
    }
}