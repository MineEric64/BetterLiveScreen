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

using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

using DiscordRPC;

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
        public static List<UserInfo> Users { get; set; } = new List<UserInfo>();

        public MainWindow()
        {
            InitializeComponent();
            DiscordClient.Initialize();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var startPage = new StartPage();

            startPage.Closing += (s, ee) =>
            {
                name1.Content = User.Name;
                name1.ToolTip = $"#{User.Discriminator}";

                this.IsEnabled = true;
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

        private void goLive_Click(object sender, RoutedEventArgs e)
        {
            Rescreen.Start();
        }

        private void stopLive_Click(object sender, RoutedEventArgs e)
        {
            Rescreen.Stop();
        }

        public void SendScreen()
        {
            while (Rescreen.IsRecording)
            {

            }
        }

        public void ReceiveScreen()
        {
            while (RoomManager.IsConnected)
            {

            }
        }

        public void DescribeScreen()
        {
            while (RoomManager.IsConnected)
            {

            }
        }
    }
}
