using System;
using System.ComponentModel;
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
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Windows.UI.Xaml.Documents;

using SharpDX.DXGI;

using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.WpfExtensions;

using DiscordRPC;

using BetterLiveScreen.Clients;
using BetterLiveScreen.Extensions;
using BetterLiveScreen.Recording;
using BetterLiveScreen.Rooms;
using BetterLiveScreen.Users;

using BetterLiveScreen.BetterShare;

using Path = System.IO.Path;
using CvSize = OpenCvSharp.Size;
using BitmapConverter = BetterLiveScreen.Extensions.BitmapConverter;

namespace BetterLiveScreen
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public const string DISCORD_APPLICATION_ID = "909043000053760012";
        public static DiscordRpcClient DiscordClient { get; set; } = new DiscordRpcClient(DISCORD_APPLICATION_ID);

        public static UserInfo User { get; set; }
        internal static string UserToken { get; } = Guid.NewGuid().ToString();
        public static List<UserInfo> Users { get; set; } = new List<UserInfo>();

        public static ClientOne Client { get; set; } = new ClientOne();

        public static BetterShareWindow ShareWindow { get; private set; } = new BetterShareWindow();

        public static bool IsDevMode { get; private set; } = false;

        public MainWindow()
        {
            InitializeComponent();
            DiscordClient.Initialize();

            this.Closing += MainWindow_Closing;
            AppDomain.CurrentDomain.UnhandledException += MainWindow_UnhandledException;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var startPage = new StartPage();

            startPage.Closing += (s, ee) =>
            {
                if (startPage.IsAccepted)
                {
                    usericon.Fill = BitmapConverter.CreateImageBrush(User.GetAvatarImage());
                    username.Content = User.NameInfo.Name;
                    username.ToolTip = $"#{User.NameInfo.Discriminator}";

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

        private void InitializeOption()
        {
            var factory = new Factory1();

            foreach (Adapter1 adapter in factory.Adapters1)
            {
                for (int i = 0; i < adapter.Outputs.Length; i++)
                {
                    var output = adapter.Outputs[i];
                    Console.WriteLine($"[{i}] {output.Description.DeviceName} : ({output.Description.DesktopBounds.Right}x{output.Description.DesktopBounds.Bottom})");
                }
            }

            //var adapter = factory.GetAdapter1(0);
            //var output = adapter.GetOutput(0);

            //int width = output.Description.DesktopBounds.Right;
            //int height = output.Description.DesktopBounds.Bottom;
        }

        private async void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            await Client?.CloseAsync();
            Application.Current.Shutdown();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //Developer Mode
            if (Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.LeftShift) && Keyboard.IsKeyDown(Key.LeftAlt) && Keyboard.IsKeyDown(Key.Y))
            {
                IsDevMode = !IsDevMode;
                credit.Content = string.Concat("[ Dev Moded ]", IsDevMode ? " !" : " ?");
            }
        }

        private void MainWindow_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            MessageBox.Show($"{ex.Message}\n{ex.Source}\n{ex.StackTrace}", "BetterLiveScreen: Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            Rescreen.Start(true);
            await Task.Delay(10000);
            Rescreen.Stop();

            var writer = new VideoWriter();

            writer.Open(@"C:\Users\erics\Downloads\cv.mp4", FourCC.H264, Rescreen.Fps, Rescreen.ScreenSize.ToCvSize());
            if (writer.IsOpened()) MessageBox.Show("hurary!");
            else MessageBox.Show("tlsqkf");
            
            while (Rescreen.VideoStreams[User.ToString()].ScreenQueue.Count > 0)
            {
                byte[] buffer = Rescreen.VideoStreams[User.ToString()].ScreenQueue.Dequeue();
                byte[] raw = buffer.Decompress();

                var src = new Mat(Rescreen.ScreenSize.Height, Rescreen.ScreenSize.Width, MatType.CV_8UC4);
                int length = Rescreen.ScreenSize.Width * Rescreen.ScreenSize.Height * 4; // or src.Height * src.Step;
                Marshal.Copy(raw, 0, src.Data, length);

                writer.Write(src);
                src.Dispose();
            }

            writer.Dispose();

            MessageBox.Show("Done");
            return;

            string[] info = serverIp.Text.Trim().Split(':');

            if (info.Length == 0)
            {
                return;
            }

            if (!Client.IsReady)
            {
                await Client.ApplyEndPointAsync(info[0]);
            }

            if (IsDevMode)
            {
                await ConnectAsync("y oshi");
                return;
            }

            if (info.Length > 1)
            {
                await ConnectAsync(info[1]);
            }
            else
            {

            }
            
            async Task ConnectAsync(string id)
            {
                if (await RoomManager.ConnectAsync(id))
                {
                    MessageBox.Show("OK");
                }
                else
                {

                }
            }
        }

        private async void serverCreate_Click(object sender, RoutedEventArgs e)
        {
            if (!Client.IsReady)
            {
                await Client.ApplyEndPointAsync(serverIp.Text.Trim());
            }

            if (await RoomManager.CreateAsync("test", $"{User.NameInfo.Name}'s Server"))
            {

            }
            else
            {

            }
        }

        private void serverBetterShare_Click(object sender, RoutedEventArgs e)
        {
            if (ShareWindow.IsClosed) ShareWindow = new BetterShareWindow();
            ShareWindow.Show();
        }
    }
}
