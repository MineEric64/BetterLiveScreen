using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using MessagePack;

using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Server.Extensions;

namespace BetterLiveScreen.Server
{
    public class Program
    {
        public static ServerOne One { get; set; } = new ServerOne();
        public static List<IPEndPoint> EndPoints { get; set; } = new List<IPEndPoint>();

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(Program_ProcessExited);
            WriteServerInfo();

            while (One.IsReady)
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                ReceiveInfo info = ReceiveInfo.Empty;

                byte[] buffer = One.Server.Receive(ref remoteEP);
                if (!EndPoints.Contains(remoteEP)) EndPoints.Add(remoteEP);

                foreach (IPEndPoint ep in EndPoints) if (ep != remoteEP) One.Server.Send(buffer, buffer.Length, ep);
            }
        }

        private static void Program_ProcessExited(object? sender, EventArgs e)
        {
            One.Close();
        }

        static void WriteServerInfo()
        {
            IPAddress ip = NetworkManager.GetExternal();
            Console.WriteLine($"Server Address : {ip}:{ServerOne.PORT_NUMBER}");
        }
    }
}