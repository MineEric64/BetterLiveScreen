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
        public string Discriminator { get; set; }

        public static UserNameInfo GuestUser => new UserNameInfo(GetGuestUserName(), "Guest");

        public UserNameInfo(string user)
        {
            string[] info = user.Split('#');

            if (info.Length < 2)
            {
                var guest = GuestUser;

                Name = guest.Name;
                Discriminator = guest.Discriminator;

                return;
            }

            Name = info[0];
            Discriminator = info[1];
        }

        public UserNameInfo(string name, string discriminator)
        {
            Name = name;
            Discriminator = discriminator;
        }

        public override string ToString()
        {
            return string.Join("#", Name, Discriminator);
        }

        private static string GetGuestUserName()
        {
            return string.Join(string.Empty, "Guest", Guid.NewGuid().ToString().Substring(0, 4).ToUpper());
        }
    }
}
