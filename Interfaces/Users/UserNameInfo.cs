using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Interfaces.Users
{
    public class UserNameInfo
    {
        public string Name { get; set; }
        public string UniqueName { get; set; }

        public static UserNameInfo GuestUser => new UserNameInfo(GetGuestUserName(), string.Empty);

        public UserNameInfo(string name, string uniqueName)
        {
            Name = name;
            UniqueName = uniqueName;

            if (string.IsNullOrWhiteSpace(uniqueName))
            {
                UniqueName = name;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        private static string GetGuestUserName()
        {
            return string.Join(string.Empty, "Guest", Guid.NewGuid().ToString().Substring(0, 4).ToUpper());
        }
    }
}
