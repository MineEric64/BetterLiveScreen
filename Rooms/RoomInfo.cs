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
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("host_full_name")]
        public string HostName { get; set; }

        [JsonProperty("host_avatar_url")]
        public string HostAvatarUrl { get; set; }

        [JsonProperty("current_user_count")]
        public int CurrentUserCount { get; set; }
        [JsonProperty("password_required")]
        public bool PasswordRequired { get; set; }

        [JsonIgnore]
        public UserInfo Host => new UserInfo(HostName, HostAvatarUrl);

        public RoomInfo(string name, string description, UserInfo host, bool passwordRequired, int currentUserCount)
        {
            Name = name;
            Description = description;

            HostName = host.ToString();
            HostAvatarUrl = host.AvatarURL;

            PasswordRequired = passwordRequired;
            CurrentUserCount = currentUserCount;
        }
    }
}
