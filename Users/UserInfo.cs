using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using BetterLiveScreen.Interfaces.Users;
using BetterLiveScreen.Extensions;

namespace BetterLiveScreen.Users
{
    public class UserInfo
    {
        public UserNameInfo NameInfo { get; set; }
        public string AvatarURL { get; set; }
        public bool IsLived { get; set; } //Go Live

        public static UserInfo GuestUser => new UserInfo(UserNameInfo.GuestUser, string.Empty);
        public bool IsGuest => NameInfo.Discriminator == "Guest" && string.IsNullOrEmpty(AvatarURL);

        public UserInfo(string user, string avatarUrl, bool isLived = false)
        {
            NameInfo = new UserNameInfo(user);
            AvatarURL = avatarUrl;
            IsLived = isLived;
        }

        public UserInfo(string name, string discriminator, string avatarUrl, bool isLived = false)
        {
            NameInfo = new UserNameInfo(name, discriminator);
            AvatarURL = avatarUrl;
            IsLived = isLived;
        }

        public UserInfo(UserNameInfo nameInfo, string avatarUrl, bool isLived = false)
        {
            NameInfo = nameInfo;
            AvatarURL = avatarUrl;
            IsLived = isLived;
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

        public static UserInfo GetGuestFromName(string name)
        {
            return new UserInfo(new UserNameInfo(name, "Guest"), string.Empty);
        }

        public override string ToString()
        {
            return NameInfo.ToString();
        }

        public bool Equals(UserInfo a)
        {
            return ToString() == a.ToString();
        }

        public bool Equals(string user)
        {
            return ToString() == user;
        }

        public bool Equals(string name, string discriminator)
        {
            return ToString() == string.Join("#", name, discriminator);
        }
    }
}
