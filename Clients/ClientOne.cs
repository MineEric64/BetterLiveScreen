using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using MessagePack;

using BetterLiveScreen.Interfaces;

namespace BetterLiveScreen.Clients
{
    public class ClientOne
    {
        public const int PORT_NUMBER = 4089;
        public const int MAXIMUM_BUFFER_SIZE = 65507;

        /// <summary>
        /// MessagePack을 위한 LZ4 압축 옵션
        /// </summary>
        public static MessagePackSerializerOptions LZ4_OPTIONS => MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

        public IPEndPoint IPEP { get; set; }
        public UdpClient Client { get; set; }

        public bool IsReady { get; set; } = false;

        public ClientOne(string ip)
        {
            _ = MessagePackSerializer.Serialize(BitmapInfo.Empty);

            IPEP = new IPEndPoint(IPAddress.Parse(ip), PORT_NUMBER);
            Client = new UdpClient();

            IsReady = true;
        }

        public void Close()
        {
            IsReady = false;

            Client.Close();
        }
    }
}
