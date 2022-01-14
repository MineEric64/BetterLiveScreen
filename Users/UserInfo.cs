using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using BetterLiveScreen.Extensions;

namespace BetterLiveScreen.Users
{
    public class UserInfo
    {
        public string Name { get; set; }
        public string Discriminator { get; set; }
        public string AvatarURL { get; set; }

        public static UserInfo GuestUser => new UserInfo(GetGuestUserName(), "Guest", string.Empty);

        public UserInfo(string name, string discriminator, string avatarUrl)
        {
            Name = name;
            Discriminator = discriminator;
            AvatarURL = avatarUrl;
        }

        public override string ToString()
        {
            return string.Join("#", Name, Discriminator);
        }

        public UserInfo FromString(string str, string avatarUrl = "")
        {
            string[] info = str.Split('#');

            if (info.Length < 2) return GuestUser;
            return new UserInfo(info[0], info[1], (!string.IsNullOrWhiteSpace(avatarUrl) ? avatarUrl : GuestUser.AvatarURL));
        }

        public static string GetGuestUserName()
        {
            return string.Join(string.Empty, "Guest", Guid.NewGuid().ToString().Substring(0, 4).ToUpper());
        }

        public ImageSource GetAvatarImage()
        {
            if (string.IsNullOrWhiteSpace(AvatarURL))
            {
                return BitmapConverter.BLACK_IMAGE;
            }

            var bitmapImage = new BitmapImage();

            bitmapImage.BeginInit();
            bitmapImage.UriSource = new Uri(AvatarURL);
            bitmapImage.EndInit();

            return bitmapImage;
        }
    }
}
