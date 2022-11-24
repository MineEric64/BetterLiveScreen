using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using DiscordRPC;

using BetterLiveScreen.Clients;
using BetterLiveScreen.Extensions;
using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Interfaces.Users;
using BetterLiveScreen.Users;

namespace BetterLiveScreen
{
    /// <summary>
    /// StartPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class StartPage : Window
    {
        public bool IsAccepted { get; set; } = false;

        public StartPage()
        {
            InitializeComponent();
            this.Loaded += StartPage_Loaded;
        }

        private void StartPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!TryDiscord())
            {
                MainWindow.User = UserInfo.GuestUser;
            }
            ApplyUserInfo();

            Task.Run(WaitForLogin);
        }

        public void ApplyUserInfo()
        {
            username.Text = MainWindow.User.NameInfo.Name;
            userauth.Content = $"#{MainWindow.User.NameInfo.Discriminator}";
            usericon.Fill = BitmapConverter.CreateImageBrush(MainWindow.User.GetAvatarImage());
        }

        public bool TryDiscord()
        {
            User user = DiscordHelper.Client.CurrentUser;

            if (user != null)
            {
                MainWindow.User = new UserInfo(user.Username, user.Discriminator.ToString(), user.GetAvatarURL(User.AvatarFormat.JPEG));
                return true;
            }

            return false;
        }

        private void loginFromDiscord_Click(object sender, RoutedEventArgs e)
        {
            if (TryDiscord())
            {
                ApplyUserInfo();  
            }
            else
            {
                MessageBox.Show("Can't find the discord application.", this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ok_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.User.IsGuest && username.Text != MainWindow.User.NameInfo.Name)
            {
                MainWindow.User = UserInfo.GetGuestFromName(username.Text);
            }
            else if (!MainWindow.User.IsGuest && username.Text != MainWindow.User.NameInfo.Name)
            {
                MainWindow.User.NameInfo.Name = username.Text;
            }

            IsAccepted = true;
            this.Close();
        }

        private void WaitForLogin()
        {
            for (int i = 0; i < 100; i++)
            {
                if (TryDiscord())
                {
                    Dispatcher.Invoke(ApplyUserInfo);
                    break;
                }
                Thread.Sleep(100);
            }
        }

        private void StartPage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ok_Click(null, null);
            }
        }
    }
}
