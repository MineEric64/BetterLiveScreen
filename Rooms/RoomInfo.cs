using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using BetterLiveScreen.Users;

namespace BetterLiveScreen.Rooms
{
    public class RoomInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public UserInfo Admin { get; set; }
        public int CurrentUserCount { get; set; }
        public bool PasswordRequired { get; set; }

        public RoomInfo(string name, string description, UserInfo admin, bool passwordRequired, int currentUserCount)
        {
            Name = name;
            Description = description;

            Admin = admin;
            PasswordRequired = passwordRequired;
            CurrentUserCount = currentUserCount;
        }

        public static RoomInfo FromJsonString(string jsonStr)
        {
            var json = JObject.Parse(jsonStr);

            string name = json["name"].ToObject<string>();
            string description = json["description"].ToObject<string>();
            string admin = json["admin"].ToObject<string>();
            string adminAvatarUrl = json["admin_avatar_url"].ToObject<string>();
            bool passwordRequired = json["password_required"].ToObject<bool>();
            int currentUserCount = json["current_user_count"].ToObject<int>();

            return new RoomInfo(name, description, new UserInfo(admin, adminAvatarUrl), passwordRequired, currentUserCount);
        }

        public string ToJsonString()
        {
            var json = new JObject()
            {
                { "name", Name },
                { "description", Description },
                { "admin", Admin.ToString() },
                { "admin_avatar_url", Admin.AvatarURL },
                { "password_required", PasswordRequired },
                { "current_user_count", CurrentUserCount }
            };

            return json.ToString();
        }
    }
}
