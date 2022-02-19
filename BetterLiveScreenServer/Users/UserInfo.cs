using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Server.Users
{
    internal class UserInfo
    {
        public string Name { get; }
        public string Discriminator { get; }
        public string Token { get; }
        public IPEndPoint Ip { get; }
        public DateTime ConnectedTime { get; }

        public UserInfo(string name, string discriminator, string token, IPEndPoint ip)
        {
            Name = name;
            Discriminator = discriminator;
            Token = token;
            Ip = ip;
            ConnectedTime = DateTime.Now;
        }
    }
}
