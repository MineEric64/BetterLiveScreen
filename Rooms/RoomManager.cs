using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BetterLiveScreen.Interfaces;

using MessagePack;

namespace BetterLiveScreen.Rooms
{
    public class RoomManager
    {
        public static bool IsConnected { get; private set; } = false;
        public static RoomInfo CurrentRoom { get; private set; }

        public const int MAX_USER_COUNT = 5;

        public static async Task<bool> ConnectAsync(string id, string password = "")
        {
            var a = await MainWindow.Client.ConnectAsync(id, password);
            Debug.WriteLine(a.SendType.ToString());
            return true;

            var info = await GetRoomInfoAsync(id);

            if (info == null || (info.PasswordRequired && string.IsNullOrEmpty(password)))
            {
                return false;
            }

            var response = await MainWindow.Client.ConnectAsync(id, password);

            if (response.SendType == SendTypes.OK)
            {
                CurrentRoom = info;
                IsConnected = true;

                return true;
            }

            return false;
        }
        
        public static async Task<bool> DisconnectAsync()
        {
            var response = await MainWindow.Client.DisconnectAsync();

            if (response.SendType == SendTypes.OK)
            {
                CurrentRoom = null;
                IsConnected = false;

                return true;
            }

            return false;
        }

        public static async Task<bool> CreateAsync(string id, string name, string description = "", string password = "")
        {
            var info = new RoomInfo(name, description, id, string.IsNullOrWhiteSpace(password), 0, MAX_USER_COUNT);
            var response = await MainWindow.Client.CreateRoomAsync(info, password);

            if (response.SendType == SendTypes.OK)
            {
                await ConnectAsync(info.Id);
                return true;
            }

            return false;
        }

        public static void CreateForDebug()
        {
            //this function is for debugging only.
            //this function will deleted soon, so don't use this method.
            CurrentRoom = new RoomInfo("blss", "for debugging", GetRandomId(), false, 1, MAX_USER_COUNT);
        }

        public static async Task<RoomInfo> GetRoomInfoAsync(string id)
        {
            var response = await MainWindow.Client.GetRoomInfoAsync(id);
            string json = MessagePackSerializer.Deserialize<string>(response.Buffer);

            return null;
        }

        public static string GetRandomId()
        {
            return Guid.NewGuid().ToString().Substring(0, 6);
        }

        public static string GetInviteSecret()
        {
            //var response = await MainWindow.Client.GetInviteSecret(CurrentRoom.Id, MainWindow.User.ToString());
            return DiscordRPC.Secrets.CreateFriendlySecret(new Random());
        }
    }
}
