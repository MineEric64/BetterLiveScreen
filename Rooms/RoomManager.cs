using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using MessagePack;

using BetterLiveScreen.Clients;
using BetterLiveScreen.Extensions;
using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Interfaces.Security;
using BetterLiveScreen.Recording.Video;
using BetterLiveScreen.Users;

using My = BetterLiveScreen.MainWindow;

namespace BetterLiveScreen.Rooms
{
    public class RoomManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(App));

        public static bool IsConnected { get; set; } = false;
        public static RoomInfo CurrentRoom { get; set; }
        /// <summary>
        /// Room's Unique ID
        /// </summary>
        public static Guid CurrentRoomId { get; set; } = Guid.Empty;
        /// <summary>
        /// this password should be not empty if this user created the room with password
        /// </summary>
        public static string Password { get; set; } = string.Empty;

        public static bool IsHost => CurrentRoom != null && CurrentRoomId != Guid.Empty && CurrentRoom.Host.Equals(My.User);
        public const int MAX_USER_COUNT = 5;
        public const int MAX_PASSWORD_LENGTH = 112; //SHAed

        #region User Methods
        public static async Task<ReceiveInfo> ConnectAsync(string password = "")
        {
            var json = new JObject
            {
                { "password", !string.IsNullOrEmpty(password) ? SHA512.Hash(password).Substring(0, MAX_PASSWORD_LENGTH) : string.Empty },
                { "user", My.User.NameInfo.ToString() },
                { "user_avatar_url", My.User.AvatarURL }
            };
            byte[] buffer = ClientOne.Encode(json.ToString());
            var info = new ReceiveInfo(SendTypes.RoomConnectRequested, buffer, BufferTypes.JsonString);

            My.Client.SendBufferToHost(info);
            return await My.Client.ReceiveBufferAsync(info);
        }
        
        public static void Disconnect(bool roomDeleted = false)
        {
            if (!roomDeleted)
            {
                var info = new ReceiveInfo(SendTypes.UserDisconnected, ClientOne.Encode(My.User.ToString()), BufferTypes.String);

                My.Client.SendBufferToHost(info);
                My.Client.Disconnect();
            }
            DiscordHelper.SetPresenceIfLeft();

            CurrentRoom = null;
            CurrentRoomId = Guid.Empty;
            IsConnected = false;
        }

        public static async Task<RoomInfo> GetRoomInfoAsync(string address)
        {
            ReceiveInfo received;
            var ipep = await ClientOne.GetIPEPAsync(address);
            My.Client.Connect(ipep);

            received = await My.Client.ReceiveBufferAsync(SendTypes.PeerConnected);

            if (received.ResponseCode != ResponseCodes.OK)
            {
                log.Error("Can't connect to host.");
                return null;
            }

            var info = new ReceiveInfo(SendTypes.RoomInfoRequested);
            My.Client.SendBufferToHost(info);

            received = await My.Client.ReceiveBufferAsync(info);

            if (received.ResponseCode == ResponseCodes.OK)
            {
                string json = ClientOne.Decode(received.Buffer);
                return JsonConvert.DeserializeObject<RoomInfo>(json);
            }
            else
            {
                log.Error("Can't get room information from host.");
            }

            return null;
        }
        #endregion
        #region Host Methods
        public static void Create(string name, string description = "", string password = "")
        {
            CurrentRoom = new RoomInfo(name, description, My.User, !string.IsNullOrWhiteSpace(password), 1);
            CurrentRoomId = Guid.NewGuid();
            Password = string.IsNullOrEmpty(password) ? "" : SHA512.Hash(password).Substring(0, MAX_PASSWORD_LENGTH);

            IsConnected = true;
        }

        public static void Delete()
        {
            My.Client.Disconnect();
            Disconnect(true);

            Password = string.Empty;
        }
        #endregion
        public static string GetInviteSecret()
        {
            //return "192.168.0.43;";
            
            if (IsHost) return string.Join(";", NetworkManager.GetPublicIPAddress(), Password);
            return string.Concat(My.Client.Client.FirstPeer.EndPoint.Address.ToString(), ";");
        }
    }
}
