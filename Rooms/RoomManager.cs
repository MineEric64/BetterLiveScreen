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

namespace BetterLiveScreen.Rooms
{
    public class RoomManager
    {
        public static bool IsConnected { get; private set; } = false;
        public static RoomInfo CurrentRoom { get; private set; }
        /// <summary>
        /// Room's Unique ID
        /// </summary>
        public static Guid CurrentRoomId { get; private set; } = Guid.Empty;
        public static bool IsAdmin => CurrentRoom != null && CurrentRoomId != Guid.Empty && CurrentRoom.Admin.Equals(My.User);
        /// <summary>
        /// this password should be not empty if this user created the room with password
        /// </summary>
        public static string Password { get; private set; } = string.Empty;

        public const int MAX_USER_COUNT = 5;

        #region User Methods
        public static async Task<ReceiveInfo> ConnectAsync(string ip, string password = "")
        {
            var json = new JObject
            {
                { "password", !string.IsNullOrEmpty(password) ? SHA512.Hash(password) : string.Empty },
                { "user", My.User.NameInfo.ToString() },
                { "user_avatar_url", My.User.AvatarURL }
            };
            byte[] buffer = ClientOne.Encode(json.ToString());
            var info = new ReceiveInfo(SendTypes.RoomConnectRequested, buffer, BufferTypes.JsonString);

            await My.Client.SendBufferAsync(info);
            return await My.Client.ReceiveBufferAsync(info);
        }
        
        public static async Task DisconnectAsync()
        {
            var json = new JObject
            {
                { "user", My.User.NameInfo.ToString() }
            };
            byte[] buffer = ClientOne.Encode(json.ToString());
            var info = new ReceiveInfo(SendTypes.RoomDisconnected, buffer, BufferTypes.JsonString);

            await My.Client.SendBufferAsync(info);
        }

        public static async Task<RoomInfo> GetRoomInfoAsync(string address)
        {
            await My.Client.AddAdminAsync(address);
            await My.Client.ConnectAsync();

            var info = new ReceiveInfo(SendTypes.RoomInfoRequested);
            await My.Client.SendBufferAsync(info);

            var received = await My.Client.ReceiveBufferAsync(info);

            if (received.ResponseCode == ResponseCodes.OK)
            {
                string json = ClientOne.Decode(received.Buffer);
                return RoomInfo.FromJsonString(json);
            }

            return null;
        }
        #endregion
        #region Admin Methods
        public static void Create(string name, string description = "", string password = "")
        {
            CurrentRoom = new RoomInfo(name, description, My.User, string.IsNullOrWhiteSpace(password), 1);
            CurrentRoomId = new Guid();
            Password = SHA512.Hash(password);

            IsConnected = true;
        }

        public static async Task DeleteAsync()
        {
            var info = new ReceiveInfo(SendTypes.RoomDeleted);
            await My.Client.SendBufferAsync(info);
        }
        #endregion
        public static string GetInviteSecret()
        {
            //var response = await MainWindow.Client.GetInviteSecret(CurrentRoom.Id, MainWindow.User.ToString());
            return DiscordRPC.Secrets.CreateFriendlySecret(new Random());
        }
    }
}
