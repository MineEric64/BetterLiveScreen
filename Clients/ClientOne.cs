using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using MessagePack;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Interfaces.Security;
using BetterLiveScreen.Rooms;

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
            //for serialize optimization
            //for details, see at https://github.com/neuecc/MessagePack-CSharp/issues/1350
            _ = MessagePackSerializer.Serialize(BitmapInfo.Empty);

            IPEP = new IPEndPoint(IPAddress.Parse(ip), PORT_NUMBER);
            Client = new UdpClient();

            IsReady = true;
        }

        public async Task SendBufferAsync(ReceiveInfo info)
        {
            byte[] buffer = MessagePackSerializer.Serialize(info);
            await Client.SendAsync(buffer, buffer.Length, IPEP);
        }

        public async Task<ReceiveInfo> ReceiveBufferAsync()
        {
            var result = await Client.ReceiveAsync();
            return MessagePackSerializer.Deserialize<ReceiveInfo>(result.Buffer);
        }

        public async Task<ReceiveInfo> CreateRoomAsync(RoomInfo room, string password = "")
        {
            var json = new JObject
            {
                { "id", room.Id },
                { "password", !string.IsNullOrEmpty(password) ? SHA512.Hash(password) : string.Empty },
                { "user", MainWindow.User.ToString() },
                { "user_url", MainWindow.User.AvatarURL },
                { "name", room.Name },
                { "description", room.Description }
            };

            byte[] buffer = MessagePackSerializer.Serialize(json.ToString(), LZ4_OPTIONS);
            var request = new ReceiveInfo(SendTypes.Created, 0, buffer);

            await SendBufferAsync(request);
            return await ReceiveBufferAsync();
        }

        public async Task<ReceiveInfo> ConnectAsync(string id, string password = "")
        {
            var json = new JObject
            {
                { "id", id },
                { "password", !string.IsNullOrEmpty(password) ? SHA512.Hash(password) : string.Empty },
                { "user", MainWindow.User.ToString() },
                { "user_url", MainWindow.User.AvatarURL }
            };
            byte[] buffer = MessagePackSerializer.Serialize(json.ToString(), LZ4_OPTIONS);

            var info = new ReceiveInfo(SendTypes.Connected, 0, buffer);
            await SendBufferAsync(info);

            return await ReceiveBufferAsync();
        }

        public async Task<ReceiveInfo> DisconnectAsync()
        {
            var json = new JObject
            {
                { "id", RoomManager.CurrentRoom.Id }
                { "user", MainWindow.User.ToString() }
            };
            byte[] buffer = MessagePackSerializer.Serialize(json.ToString(), LZ4_OPTIONS);

            var info = new ReceiveInfo(SendTypes.Disconnected, 0, buffer);
            await SendBufferAsync(info);

            return await ReceiveBufferAsync();
        }

        public async Task<ReceiveInfo> GetRoomInfoAsync(string id)
        {
            var json = new JObject
            {
                { "id", id }
            };
            byte[] buffer = MessagePackSerializer.Serialize(json.ToString(), LZ4_OPTIONS);

            var info = new ReceiveInfo(SendTypes.Requested, 0, buffer);
            await SendBufferAsync(info);

            return await ReceiveBufferAsync();
        }

        public async Task CloseAsync()
        {
            IsReady = false;

            if (RoomManager.IsConnected) await DisconnectAsync();
            Client.Close();
        }
    }
}
