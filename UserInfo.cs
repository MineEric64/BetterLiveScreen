using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BetterLiveScreen
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

        public static string GetGuestUserName()
        {
            return string.Join(string.Empty, "Guest", Guid.NewGuid().ToString().Substring(0, 4).ToUpper());
        }

        public ImageSource GetAvatarImage()
        {
            if (string.IsNullOrWhiteSpace(AvatarURL))
            {
                var bmp = new BitmapImage();

                bmp.BeginInit();
                bmp.UriSource = new Uri("/Resources/black_screen.png", UriKind.Relative);
                bmp.EndInit();

                return bmp;
            }

            var bitmapImage = new BitmapImage();

            bitmapImage.BeginInit();
            bitmapImage.UriSource = new Uri(AvatarURL);
            bitmapImage.EndInit();

            return bitmapImage;
        }
    }
}
