using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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

using DiscordRPC;

using BetterLiveScreen.Clients;
using BetterLiveScreen.Extensions;
using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Interfaces.Security;
using BetterLiveScreen.Recording;
using BetterLiveScreen.Recording.Types;
using BetterLiveScreen.Recording.Video;
using BetterLiveScreen.Rooms;
using BetterLiveScreen.Users;

using BetterLiveScreen.BetterShare;

using Path = System.IO.Path;
using CvSize = OpenCvSharp.Size;
using BitmapConverter = BetterLiveScreen.Extensions.BitmapConverter;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace BetterLiveScreen
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        public static UserInfo User { get; set; }
        internal static string UserToken { get; } = Guid.NewGuid().ToString();
        public static List<UserInfo> Users { get; set; } = new List<UserInfo>();

        public static ClientOne Client { get; set; }

        public static BetterShareWindow ShareWindow { get; private set; } = new BetterShareWindow();

        public static bool IsDevMode { get; private set; } = false;

        public static Dispatcher CurrentDispatcher { get; private set; } = null;

        public MainWindow()
        {
            InitializeComponent();
            CurrentDispatcher = this.Dispatcher;

            InitializeClient();
            Rescreen.Initialize();

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

                    Users.Add(User);

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

        private void InitializeClient()
        {
            Client = new ClientOne();
            Client.Connected += (s, e) =>
            {
                MessageBox.Show("Connected!", "BetterLiveScreen", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            Client.Disconnected += (s, e) =>
            {
                MessageBox.Show("Disconnected", "BetterLiveScreen", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            Client.HostConnected += (s, e) =>
            {
                MessageBox.Show("[Debug] Connected");
            };
            Client.HostDisconnected += (s, e) =>
            {
                MessageBox.Show("[Debug] Disconnected to ( " + e + " )");
            };
            Client.Start();

            DiscordHelper.Initialize();
        }

        public void InitializeUI()
        {
            name1.Content = string.Empty;
            name2.Content = string.Empty;
            name3.Content = string.Empty;
            name4.Content = string.Empty;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            Client?.Close();
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
            MessageBox.Show(ex.ToString(), "BetterLiveScreen: Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void goLive_Click(object sender, RoutedEventArgs e)
        {
            Rescreen.Start();
        }

        private void stopLive_Click(object sender, RoutedEventArgs e)
        {
            Rescreen.Stop();
        }

        private async void serverIpConnect_Click(object sender, RoutedEventArgs e)
        {
            //await RecordingTest.RecordTestAsync(
            //    videoType: CaptureVideoType.DD,
            //    audioType: CaptureAudioType.WinCaptureAudio,
            //    milliseconds: 10000,
            //    monitor: RescreenSettings.PrimaryMonitor,
            //    fps: 60,
            //    isHalf: false,
            //    nvencEncoding: true
            //    );
            //return;

            if (string.IsNullOrWhiteSpace(serverIp.Text))
            {
                MessageBox.Show("Address is empty", "BetterLiveScreen : Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var room = await RoomManager.GetRoomInfoAsync(serverIp.Text);
            
            if (room != null)
            {
                RoomManager.CurrentRoom = room;

                string password = string.Empty;

                if (room.PasswordRequired)
                {
                    var textWindow = new ChooseText();

                    textWindow.Title = "Input Room's Password";
                    textWindow.Show();
                    password = SHA512.Hash(await textWindow.WaitAsyncUntilOK());
                }

                var connectedInfo = await RoomManager.ConnectAsync(password);

                switch (connectedInfo.ResponseCode)
                {
                    case ResponseCodes.OK:
                        RoomManager.Password = password;

                        string json = ClientOne.Decode(connectedInfo.Buffer);
                        var jsonRaw = JObject.Parse(json);

                        var users = jsonRaw["users"]?.Values();
                        string id = jsonRaw["id"].ToString();

                        foreach (var user in users)
                        {
                            string fullName = user?["full_name"]?.ToString();
                            string avatarUrl = user?["avatar_url"]?.ToString() ?? string.Empty;
                            bool? isLived = user?["is_lived"]?.ToObject<bool>();

                            if (users != null && fullName != null && isLived.HasValue)
                            {
                                Users.Add(new UserInfo(fullName, avatarUrl, isLived.Value));
                            }
                            else
                            {
                                Debug.WriteLine("[Error] Something went wrong when connecting.");

                                var info = connectedInfo.GetFailed(ResponseCodes.Failed);
                                Client.SendBufferToHost(info);

                                return;
                            }
                        }
                        RoomManager.CurrentRoomId = Guid.Parse(id);

                        Client.SendBufferToHost(connectedInfo.GetOK());
                        RoomManager.IsConnected = true;

                        UpdateUserUI();
                        DiscordHelper.SetPresenceIfJoined();

                        MessageBox.Show($"Connected to {RoomManager.CurrentRoom.Name}!", "BetterLiveScreen", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;

                    case ResponseCodes.AccessDenied:
                        MessageBox.Show("Wrong password!", "BetterLiveScreen : Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;

                    case ResponseCodes.TooManyUsers:
                        MessageBox.Show("The number of user is exceeded.", "BetterLiveScreen : Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
            else
            {
                MessageBox.Show("Failed to get room's information from the address.", "BetterLiveScreen : Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void serverCreate_Click(object sender, RoutedEventArgs e)
        {
            //if (!string.IsNullOrWhiteSpace(serverIp.Text)) RecordingTest.TestName = serverIp.Text;
            //var rt = new RecordingTest();
            //rt.Show();
            //return;

            RoomManager.Create("BLSS", $"{User.NameInfo.Name}'s Server");
            MessageBox.Show("Created!", "BetterLiveScreen", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void serverBetterShare_Click(object sender, RoutedEventArgs e)
        {
            if (ShareWindow.IsClosed) ShareWindow = new BetterShareWindow();
            ShareWindow.Show();
        }

        public void UpdateUserUI() {
            string[] names = new string[4] { string.Empty, string.Empty, string.Empty, string.Empty };
            int index = 0;

            for (int i = 0; i < Users.Count; i++)
            {
                if (Users[i].Equals(User)) continue;
                names[index++] = Users[i].NameInfo.Name;
            }

            name1.Content = names[0];
            name2.Content = names[1];
            name3.Content = names[2];
            name4.Content = names[3];
        }
    }
}
