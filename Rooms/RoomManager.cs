using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using MessagePack;

using BetterLiveScreen.Clients;
using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Interfaces.Security;
using BetterLiveScreen.Users;

using My = BetterLiveScreen.MainWindow;
using Newtonsoft.Json;

namespace BetterLiveScreen.Rooms
{
    public class RoomManager
    {
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

        #region User Methods
        public static async Task<ReceiveInfo> ConnectAsync(string password = "")
        {
            var json = new JObject
            {
                { "password", !string.IsNullOrEmpty(password) ? SHA512.Hash(password) : string.Empty },
                { "user", My.User.NameInfo.ToString() },
                { "user_avatar_url", My.User.AvatarURL }
            };
            byte[] buffer = ClientOne.Encode(json.ToString());
            var info = new ReceiveInfo(SendTypes.RoomConnectRequested, buffer, BufferTypes.JsonString);

            My.Client.SendBufferToHost(info);
            return await My.Client.ReceiveBufferAsync(info);
        }
        
        public static void Disconnect()
        {
            var json = new JObject
            {
                { "user", My.User.NameInfo.ToString() }
            };
            byte[] buffer = ClientOne.Encode(json.ToString());
            var info = new ReceiveInfo(SendTypes.UserDisconnected, buffer, BufferTypes.JsonString);

            My.Client.SendBufferToHost(info);
        }

        public static async Task<RoomInfo> GetRoomInfoAsync(string address)
        {
            var ipep = await ClientOne.GetIPEPAsync(address);
            My.Client.Connect(ipep);

            var info = new ReceiveInfo(SendTypes.RoomInfoRequested);
            My.Client.SendBufferToHost(info);

            var received = await My.Client.ReceiveBufferAsync(info);

            if (received.ResponseCode == ResponseCodes.OK)
            {
                string json = ClientOne.Decode(received.Buffer);
                return JsonConvert.DeserializeObject<RoomInfo>(json);
            }

            return null;
        }
        #endregion
        #region Admin Methods
        public static void Create(string name, string description = "", string password = "")
        {
            CurrentRoom = new RoomInfo(name, description, My.User, string.IsNullOrWhiteSpace(password), 1);
            CurrentRoomId = Guid.NewGuid();
            Password = SHA512.Hash(password);

            IsConnected = true;
        }

        public static void Delete()
        {
            var info = new ReceiveInfo(SendTypes.RoomDeleted);

            My.Client.SendBufferToAll(info);
            My.Client.Close();
        }
        #endregion
        public static string GetInviteSecret()
        {
            //var response = await MainWindow.Client.GetInviteSecret(CurrentRoom.Id, MainWindow.User.ToString());
            return DiscordRPC.Secrets.CreateFriendlySecret(new Random());
        }
    }
}
