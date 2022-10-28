using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Rooms
{
    public class RoomInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int CurrentUserCount { get; set; }
        public int MaxUserCount { get; set; }
        public string Id { get; }
        public bool PasswordRequired { get; set; }

        public RoomInfo(string name, string description, string id, bool passwordRequired, int currentUserCount, int maxUserCount)
        {
            Name = name;
            Description = description;
            Id = id;
            PasswordRequired = passwordRequired;
            CurrentUserCount = currentUserCount;
            MaxUserCount = maxUserCount;
        }
    }
}
