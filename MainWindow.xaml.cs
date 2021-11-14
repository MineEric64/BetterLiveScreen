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
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

using DiscordRPC;

namespace BetterLiveScreen
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public static DiscordRpcClient DiscordClient { get; set; } = new DiscordRpcClient("909043000053760012");
        public static UserInfo User { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            DiscordClient.Initialize();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Rescreen.Refreshed += (object s, Bitmap bitmap) =>
            {
                var bitmapSource = FScreen.ToImage(bitmap);
                bitmapSource.Freeze();

                Dispatcher.BeginInvoke(new Action(() => screen_main.Source = bitmapSource));
            };

            var startPage = new StartPage();

            startPage.Closing += (s, ee) =>
            {
                this.IsEnabled = true;
            };

            startPage.Show();
            this.IsEnabled = false;
        }



        private void goLive_Click(object sender, RoutedEventArgs e)
        {
            Rescreen.Start();
        }

        private void stopLive_Click(object sender, RoutedEventArgs e)
        {
            Rescreen.Stop();
        }
    }
}
