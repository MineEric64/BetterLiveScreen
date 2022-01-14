using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Net;
using System.Net.Sockets;
using System.ComponentModel;

using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

using DiscordRPC;

using BetterLiveScreen.Clients;
using BetterLiveScreen.Extensions;
using BetterLiveScreen.Recording;
using BetterLiveScreen.Rooms;
using BetterLiveScreen.Users;

namespace BetterLiveScreen
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public static DiscordRpcClient DiscordClient { get; set; } = new DiscordRpcClient("909043000053760012");

        public static UserInfo User { get; set; }
        internal static string UserToken { get; } = Guid.NewGuid().ToString();
        public static List<UserInfo> Users { get; set; } = new List<UserInfo>();

        public static ClientOne Client { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            DiscordClient.Initialize();

            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var startPage = new StartPage();

            startPage.Closing += (s, ee) =>
            {
                if (startPage.IsAccepted)
                {
                    usericon.Fill = BitmapConverter.CreateImageBrush(User.GetAvatarImage());
                    username.Content = User.Name;
                    username.ToolTip = $"#{User.Discriminator}";

                    this.IsEnabled = true;
                }
                else
                {
                    this.Close();
                }
            };

            InitializeUI();

            startPage.Show();
            this.IsEnabled = false;
        }

        private void InitializeUI()
        {
            name1.Content = string.Empty;
            name2.Content = string.Empty;
            name3.Content = string.Empty;
            name4.Content = string.Empty;
        }

        private async void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            await Client?.CloseAsync();
        }

        private void goLive_Click(object sender, RoutedEventArgs e)
        {
            Rescreen.Start();
        }

        private void stopLive_Click(object sender, RoutedEventArgs e)
        {
            Rescreen.Stop();
        }

        public void AnalyzeReceive()
        {
            while (true)
            {

            }
        }

        private async void serverIpConnect_Click(object sender, RoutedEventArgs e)
        {
            string id = serverIp.Text;
            
            if(await RoomManager.ConnectAsync(id))
            {

            }
            else
            {

            }
        }

        private async void serverCreate_Click(object sender, RoutedEventArgs e)
        {
            string[] ip = serverIp.Text.Split(':');
            int port = 0;

            if (ip.Length < 2 || !int.TryParse(ip[1], out port))
            {
                return;
            }

            if (await RoomManager.CreateAsync(ip[0], port, serverId.Text, $"{User}'s Server"))
            {

            }
            else
            {

            }
        }
    }
}
