using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Rooms
{
    public class RoomManager
    {
        public static bool IsConnected { get; private set; } = false;
        public static RoomInfo CurrentRoom { get; private set; }

        public static void Connect(string id, string password = null)
        {
            var info = GetRoomInfo(id);

            if (info != null || (info.PasswordRequired && string.IsNullOrEmpty(password)))
            {
                return;
            }

            IsConnected = true;
        }
        
        public static void Disconnect()
        {


            CurrentRoom = null;
            IsConnected = false;
        }

        public static void Create(string name, string description = null, string password = null)
        {
            var info = new RoomInfo(name, description, GetRandomId(), string.IsNullOrEmpty(password));

            Connect(info.Id);
        }

        public static RoomInfo GetRoomInfo(string id)
        {
            return null;
        }

        public static string GetRandomId()
        {
            return Guid.NewGuid().ToString().Substring(0, 6);
        }
    }
}
