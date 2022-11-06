using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.ApplicationModel.Chat;

using BetterLiveScreen.Interfaces.Users;
using BetterLiveScreen.Extensions;

namespace BetterLiveScreen.Users
{
    public class UserInfo
    {
        public UserNameInfo NameInfo { get; set; }
        public string AvatarURL { get; set; }

        public static UserInfo GuestUser => new UserInfo(UserNameInfo.GuestUser, string.Empty);

        public UserInfo(string user, string avatarUrl)
        {
            NameInfo = new UserNameInfo(user);
            AvatarURL = avatarUrl;
        }

        public UserInfo(string name, string discriminator, string avatarUrl)
        {
            NameInfo = new UserNameInfo(name, discriminator);
            AvatarURL = avatarUrl;
        }

        public UserInfo(UserNameInfo nameInfo, string avatarUrl)
        {
            NameInfo = nameInfo;
            AvatarURL = avatarUrl;
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

        public override string ToString()
        {
            return NameInfo.ToString();
        }

        public bool Equals(UserInfo a)
        {
            return ToString() == a.ToString();
        }
    }
}
