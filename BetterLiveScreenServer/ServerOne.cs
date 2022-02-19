using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using MessagePack;

using BetterLiveScreen.Server.Extensions;
using BetterLiveScreen.Interfaces;

namespace BetterLiveScreen.Server
{
    public class ServerOne
    {
        public const int PORT_NUMBER = 4089;
        public const int MAXIMUM_BUFFER_SIZE = 65507;

        /// <summary>
        /// MessagePack을 위한 LZ4 압축 옵션
        /// </summary>
        public static MessagePackSerializerOptions LZ4_OPTIONS => MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

        public UdpClient Server { get; set; }
        public bool IsReady { get; set; } = false;

        public ServerOne()
        {
            byte[] buffer = new byte[] { 0, 0, 0 };
            byte[] compressed = MessagePackSerializer.Serialize(buffer);

            Server = new UdpClient(PORT_NUMBER);
            _ = MessagePackSerializer.Deserialize<byte[]>(compressed);

            IsReady = true;
        }

        public async Task SendBufferAsync(ReceiveInfo info, IPEndPoint remoteEP)
        {
            byte[] buffer = MessagePackSerializer.Serialize(info);
            await Server.SendAsync(buffer, buffer.Length, remoteEP);
        }

        public async Task<(ReceiveInfo, IPEndPoint)> ReceiveBufferAsync()
        {
            var response = await Server.ReceiveAsync();
            var info = MessagePackSerializer.Deserialize<ReceiveInfo>(response.Buffer);

            return (info, response.RemoteEndPoint);
        }

        public void Close()
        {
            IsReady = false;

            Server.Close();
        }
    }
}
