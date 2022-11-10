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

using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using MessagePack;

using TurboJpegWrapper;

using OpenH264Lib;

using BetterLiveScreen.Clients;
using BetterLiveScreen.Extensions;
using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Interfaces.Security;
using BetterLiveScreen.Recording;
using BetterLiveScreen.Recording.Audio.Wasapi;
using BetterLiveScreen.Recording.Types;
using BetterLiveScreen.Recording.Video;
using BetterLiveScreen.Recording.Video.NvPipe;
using BetterLiveScreen.Rooms;
using BetterLiveScreen.Users;

using BetterLiveScreen.BetterShare;

using Path = System.IO.Path;
using CvSize = OpenCvSharp.Size;
using BitmapConverter = BetterLiveScreen.Extensions.BitmapConverter;
using Window = System.Windows.Window;
using NvDecoder = BetterLiveScreen.Recording.Video.NvPipe.Decoder;
using H264Encoder = OpenH264Lib.Encoder;
using H264Decoder = OpenH264Lib.Decoder;
using System.Drawing.Drawing2D;

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

        public static MainWindow Me { get; private set; } = null;
        public static Dispatcher CurrentDispatcher { get; private set; } = null;

        public MainWindow()
        {
            InitializeComponent();
            Me = this;
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

            RenderOptions.SetBitmapScalingMode(screen_main, BitmapScalingMode.LowQuality);
            
        }

        private void InitializeClient()
        {
            Client = new ClientOne();
            #region Client Events
            #region Peer
            Client.Connected += (s, e) =>
            {
                MessageBox.Show("Connected!", "Better Live Screen", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            #endregion
            #region Room
            Client.Disconnected += (s, isForced) =>
            {
                Users.Clear();
                Rescreen.Stop();

                Dispatcher.Invoke(InitializeUI);

                if (isForced) MessageBox.Show("Host disconnected the connection.", "Better Live Screen", MessageBoxButton.OK, MessageBoxImage.Information);
                Debug.WriteLine("[Info] Disconnected");
            };

            Client.HostConnected += (s, e) =>
            {
                Debug.WriteLine("[Info] Connected");
            };
            #endregion
            #region User
            Client.UserConnected += (s, userInfo) =>
            {
                Users.Add(userInfo);
                Dispatcher.Invoke(UpdateUserUI);

                Debug.WriteLine($"[Info] {userInfo} Joined");
            };
            Client.UserDisconnected += (s, userFullName) =>
            {
                if (!string.IsNullOrEmpty(userFullName))
                {
                    Users.Remove(Users.Where(x => x.Equals(userFullName)).First());
                    Dispatcher.Invoke(UpdateUserUI);
                }

                Debug.WriteLine($"[Info] {userFullName} Left");
            };
            #endregion
            #region Streaming
            Client.StreamStarted += (s, e) =>
            {
                var userInfo = Users.Where(x => x.Equals(e.Item1)).FirstOrDefault();

                if (userInfo == null) return;
                if (Rescreen.VideoStreams.TryGetValue(e.Item1, out var videoStream)) videoStream.Info = e.Item2;
                else Rescreen.VideoStreams.Add(e.Item1, new VideoLike(e.Item2));
                userInfo.IsLived = true;
            };
            Client.StreamEnded += (s, userName) =>
            {
                var userInfo = Users.Where(x => x.Equals(userName)).FirstOrDefault();

                if (userInfo == null) return;
                userInfo.IsLived = false;
            };

            #region Watch
            #endregion
            #region Video
            Client.VideoBufferReceived += (s, e) =>
            {
                if (!Rescreen.VideoStreams.TryGetValue(e.Item2, out var _)) Rescreen.VideoStreams.Add(e.Item2, new VideoLike(BitmapInfo.Empty));
                Rescreen.VideoStreams[e.Item2].ScreenQueue.Enqueue(e.Item1);
                Debug.WriteLine($"[{DateTime.Now}] {e.Item2}'s Screen Received! ({e.Item1.Length})");
            };
            #endregion
            #region Audio
            Client.AudioBufferReceived += (s, e) =>
            {
                if (!Rescreen.VideoStreams.TryGetValue(e.Item2, out var _)) Rescreen.VideoStreams.Add(e.Item2, new VideoLike(BitmapInfo.Empty));
                Rescreen.VideoStreams[e.Item2].AudioQueue.Enqueue(e.Item1);
                Debug.WriteLine($"[{DateTime.Now}] {e.Item2}'s Audio Received! ({e.Item1.Length})");
            };
            #endregion
            #endregion
            #endregion
            Client.Start();

            DiscordHelper.Initialize();
        }

        public void InitializeUI()
        {
            serverIpConnect.Content = "Connect";
            serverCreate.Content = "Create Room";

            name1.Content = string.Empty;
            name2.Content = string.Empty;
            name3.Content = string.Empty;
            name4.Content = string.Empty;

            userConnected.Content = $"0 / {RoomManager.MAX_USER_COUNT} Users Connected";
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            Client?.Disconnect();
            Client?.Stop();

            //Application.Current.Shutdown();
            Process.GetCurrentProcess().Kill();
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
            //TODO: Change Settings by User
            Rescreen.MakeSettings(new RescreenSettings()
            {
                VideoType = CaptureVideoType.DD,
                AudioType = CaptureAudioType.WinCaptureAudio,
                SelectedMonitor = RescreenSettings.PrimaryMonitor,
                Fps = 30,
                IsHalf = true,
                Encoding = EncodingType.OpenH264
            });
            Rescreen.Settings.Bitrate = BitrateInfo.GetBitrateFromMbps(Rescreen.GetBitrateInfoBySize(Rescreen.ScreenActualSize.Height, Rescreen.FpsIfUnfixed60).MbpsAverage);

            Rescreen.Start();
            Task.Run(RescreenRefreshed);

            User.IsLived = true;
            var info = new ReceiveInfo(SendTypes.StreamStarted, ClientOne.Encode(User.ToString()), BufferTypes.String, MessagePackSerializer.Serialize(Rescreen.MyVideoStream.Info));

            if (!RoomManager.IsHost) Client.SendBufferToHost(info);
            else Client.SendBufferToAll(info);
        }

        private void stopLive_Click(object sender, RoutedEventArgs e)
        {
            Rescreen.Stop();

            User.IsLived = false;
            var info = new ReceiveInfo(SendTypes.StreamEnded, ClientOne.Encode(User.ToString()), BufferTypes.String);

            if (!RoomManager.IsHost) Client.SendBufferToHost(info);
            else Client.SendBufferToAll(info);
        }

        private void RescreenRefreshed()
        {
            void SendScreenBuffer(byte[] buffer)
            {
                var infos = ClientOne.DivideInfo(SendTypes.Video, buffer);
                var json = new JObject()
                        {
                            { "buffer_length", buffer.Length },
                            { "user", User.ToString() },
                            { "checksum", Checksum.ComputeAddition(buffer) }
                        };

                foreach (var info in infos)
                {
                    info.ExtraBuffer = ClientOne.Encode(json.ToString());

                    if (!RoomManager.IsHost) Client.SendBufferToHost(info);
                    else Client.SendBufferToAll(info); //test (need to add watch feature)
                }
            }

            NvDecoder decoder = null;
            H264Encoder encoder = null;

            const int PREVIEW_DPF = 33; //preview referesh delay per frame
            var sw = new Stopwatch();

            //initialize encoder & decoder
            switch (Rescreen.Settings.Encoding)
            {
                case EncodingType.Nvenc:
                    //Format : RGBA
                    decoder = new NvDecoder(Rescreen.ScreenActualSize.Width, Rescreen.ScreenActualSize.Height, Codec.H264, Format.RGBA32);
                    decoder.onDecoded += (s, e) =>
                    {
                        if (sw.ElapsedMilliseconds > PREVIEW_DPF)
                        {
                            Mat mat = new Mat(decoder.height, decoder.width, MatType.CV_8UC4);
                            Kernel32.CopyMemory(mat.Data, e.Item1, (uint)e.Item2);

                            Mat mat2 = new Mat();
                            Mat mat3 = new Mat();

                            Cv2.CvtColor(mat, mat2, ColorConversionCodes.RGBA2BGR);
                            Cv2.Resize(mat2, mat3, new CvSize(900, 500), 0, 0, InterpolationFlags.Nearest);

                            ScreenPreview(mat3);

                            mat.Dispose();
                            mat2.Dispose();

                            sw.Restart();
                        }
                    };

                    break;

                case EncodingType.OpenH264:
                    float keyFrameInterval = Rescreen.DelayPerFrame;

                    encoder = new H264Encoder("openh264-2.3.1-win64.dll");
                    encoder.Setup(Rescreen.ScreenActualSize.Width, Rescreen.ScreenActualSize.Height, Rescreen.Settings.Bitrate, Rescreen.FpsIfUnfixed60, keyFrameInterval,
                        (data, length, frameType) =>
                        {
                            byte[] buffer = data.Compress();
                            SendScreenBuffer(buffer);
                        });

                    break;
            }

            sw.Start();

            while (Rescreen.IsRecording)
            {
                while (Rescreen.MyVideoStream.ScreenQueue.Count > 0 || Rescreen.MyVideoStream.AudioQueue.Count > 0)
                {
                    if (Rescreen.MyVideoStream.ScreenQueue.TryDequeue(out byte[] buffer)) //compressed
                    {
                        byte[] preview = buffer.Decompress();
                        if ((buffer?.Length ?? 0) == 0) continue;

                        //TODO: Send Screen Buffer
                        switch (Rescreen.Settings.Encoding)
                        {
                            case EncodingType.Nvenc:
                            case EncodingType.CompressOnly:
                                SendScreenBuffer(buffer);
                                break;

                            case EncodingType.OpenH264:
                                Mat mat = new Mat(Rescreen.ScreenActualSize.Height, Rescreen.ScreenActualSize.Width, MatType.CV_8UC4);
                                int length = Rescreen.ScreenActualSize.Width * Rescreen.ScreenActualSize.Height * 4;

                                Marshal.Copy(preview, 0, mat.Data, length);
                                Mat mat2 = mat.CvtColor(ColorConversionCodes.BGRA2RGB);

                                unsafe
                                {
                                    encoder.Encode(mat2.DataPointer);
                                }
                                //mat2.Dispose();
                                mat.Dispose();

                                break;
                        }

                        //Live Preview
                        switch (Rescreen.Settings.Encoding)
                        {
                            case EncodingType.Nvenc:
                                var handle = GCHandle.Alloc(preview, GCHandleType.Pinned);
                                var ptr = handle.AddrOfPinnedObject();

                                decoder.Decode(ptr, preview.Length);
                                handle.Free();

                                break;

                            case EncodingType.OpenH264:
                            case EncodingType.CompressOnly:
                                if (sw.ElapsedMilliseconds > PREVIEW_DPF)
                                {
                                    var mat = new Mat(Rescreen.ScreenActualSize.Height, Rescreen.ScreenActualSize.Width, MatType.CV_8UC4);
                                    int length = Rescreen.ScreenActualSize.Width * Rescreen.ScreenActualSize.Height * 4; // or src.Height * src.Step;

                                    Marshal.Copy(preview, 0, mat.Data, length);
                                    var mat2 = mat.Resize(new CvSize(900, 500), 0, 0, InterpolationFlags.Nearest);

                                    ScreenPreview(mat2);
                                    mat.Dispose();

                                    sw.Restart();
                                }

                                break;
                        }
                    }
                    if (Rescreen.MyVideoStream.AudioQueue.TryDequeue(out byte[] buffer2)) //compressed
                    {
                        //TODO: Send Audio Buffer
                        var infos = ClientOne.DivideInfo(SendTypes.Audio, buffer2);
                        var json = new JObject()
                        {
                            { "buffer_length", buffer2.Length },
                            { "user", User.ToString() },
                            { "checksum", Checksum.ComputeAddition(buffer2) },
                            { "audio_sample_rate", WasapiCapture.DeviceWaveFormat.SampleRate },
                            { "audio_bits_per_sample", WasapiCapture.DeviceWaveFormat.BitsPerSample },
                            { "audio_channel", WasapiCapture.DeviceWaveFormat.Channels },
                        };

                        foreach (var info in infos)
                        {
                            info.ExtraBuffer = ClientOne.Encode(json.ToString());

                            if (!RoomManager.IsHost) Client.SendBufferToHost(info);
                            else Client.SendBufferToAll(info); //test (need to add watch feature)
                        }
                    }
                }

                Thread.Sleep(10);
            }
            sw.Stop();

            decoder?.Close();
            encoder?.Dispose();
        }

        private void ClientBufferRefreshed()
        {
            H264Decoder h264Decoder = null;
            Dictionary<string, NvDecoder> decoderMap = new Dictionary<string, NvDecoder>();

            while (RoomManager.IsConnected) //need to change user watching
            {
                //max 1 (need to support)
                var livedUser = Users.Where(x => x.IsLived).FirstOrDefault();

                if (livedUser != null)
                {
                    var videoStream = Rescreen.VideoStreams[livedUser.ToString()];
                    Enum.TryParse(videoStream.Info.Encoding, out EncodingType encoding);

                    try
                    {
                        while (videoStream.ScreenQueue.Count > 0 || videoStream.AudioQueue.Count > 0)
                        {
                            if (videoStream.ScreenQueue.TryDequeue(out byte[] buffer)) //compressed
                            {
                                byte[] preview = buffer.Decompress();

                                switch (encoding)
                                {
                                    case EncodingType.Nvenc:
                                        NvDecoder nvDecoder = null;

                                        if (!decoderMap.TryGetValue(livedUser.ToString(), out nvDecoder))
                                        {
                                            nvDecoder = new NvDecoder(videoStream.Info.Width, videoStream.Info.Height, Codec.H264, Format.RGBA32);
                                            nvDecoder.onDecoded += (s, e) =>
                                            {
                                                Mat mat1 = new Mat(nvDecoder.height, nvDecoder.width, MatType.CV_8UC4);
                                                Kernel32.CopyMemory(mat1.Data, e.Item1, (uint)e.Item2);

                                                Mat mat12 = new Mat();
                                                Mat mat13 = new Mat();

                                                Cv2.CvtColor(mat1, mat12, ColorConversionCodes.RGBA2BGR);
                                                Cv2.Resize(mat12, mat13, new CvSize(900, 500), 0, 0, InterpolationFlags.Nearest);

                                                ScreenPreview(mat13);

                                                mat1.Dispose();
                                                mat12.Dispose();
                                            };

                                            decoderMap.Add(livedUser.ToString(), nvDecoder);
                                        }

                                        var handle = GCHandle.Alloc(preview, GCHandleType.Pinned);
                                        var ptr = handle.AddrOfPinnedObject();

                                        nvDecoder.Decode(ptr, preview.Length);
                                        handle.Free();

                                        break;

                                    case EncodingType.OpenH264:
                                        if (h264Decoder == null) h264Decoder = new H264Decoder("openh264-2.3.1-win64.dll");

                                        var bitmap = h264Decoder.Decode(preview, preview.Length);

                                        if (bitmap == null)
                                        {
                                            Debug.WriteLine("[Warning] Decoded bitmap returned null. skipping to next frame.");
                                            continue;
                                        }
                                        ScreenPreview(bitmap);

                                        break;

                                    case EncodingType.CompressOnly:
                                        var mat2 = new Mat(videoStream.Info.Height, videoStream.Info.Width, MatType.CV_8UC4);
                                        int length = videoStream.Info.Width * videoStream.Info.Height * 4; // or src.Height * src.Step;

                                        Marshal.Copy(preview, 0, mat2.Data, length);
                                        var mat22 = mat2.Resize(new CvSize(900, 500), 0, 0, InterpolationFlags.Nearest);

                                        ScreenPreview(mat22);
                                        mat2.Dispose();

                                        break;
                                }
                            }

                            if (videoStream.AudioQueue.TryDequeue(out byte[] buffer2)) //compressed
                            {
                                byte[] decompressed = buffer2.Decompress();

                                if (!WasapiRealtimePlay.IsInitialized) WasapiRealtimePlay.Initialize();
                                if (!WasapiRealtimePlay.BufferMap.ContainsKey(livedUser.ToString())) WasapiRealtimePlay.AddToBufferMap(livedUser.ToString(), videoStream.AudioFormat);

                                WasapiRealtimePlay.AddData(livedUser.ToString(), decompressed);
                                WasapiRealtimePlay.Play();
                            }
                        }
                    }
                    catch (MessagePackSerializationException)
                    {
                        continue; //udp packet loss
                    }
                }
                Thread.Sleep(10);
            }

            foreach (var decoder in decoderMap.Values) decoder.Close();
            h264Decoder?.Dispose();

            WasapiRealtimePlay.Stop();
        }

        private void ScreenPreview(Mat mat)
        {
            Dispatcher.BeginInvoke(new Action(() => {
                var source = mat.ToWriteableBitmap();

                screen_main.Source = source;
                mat.Dispose();
            }), DispatcherPriority.Render);
        }

        private void ScreenPreview(Bitmap bitmap)
        {
            Dispatcher.BeginInvoke(new Action(() => {
                var source = bitmap.ToImage();

                screen_main.Source = source;
                bitmap.Dispose();
            }), DispatcherPriority.Render);
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

            if (RoomManager.IsConnected) //need to Disconnect
            {
                RoomManager.Disconnect();
                serverIpConnect.Content = "Connect";

                return;
            }

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

                        var users = jsonRaw["users"]?.ToArray();
                        string id = jsonRaw["room_id"]?.ToString();

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
                        RoomManager.IsConnected = true;

                        serverIpConnect.Content = "Disconnect";
                        UpdateUserUI();
                        DiscordHelper.SetPresenceIfJoined();

                        Task.Run(ClientBufferRefreshed); //test (need to add watch feature)

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

            if (RoomManager.IsConnected) //need to delete room
            {
                RoomManager.Delete();
                serverCreate.Content = "Create Room";
                MessageBox.Show("Deleted the Room.\nUsers will be disconnected forcely.", "Better Live Screen", MessageBoxButton.OK, MessageBoxImage.Information);

                return;
            }

            RoomManager.Create("BLSS", $"{User.NameInfo.Name}'s Server");
            DiscordHelper.SetPresenceIfJoined();

            userConnected.Content = $"1 / {RoomManager.MAX_USER_COUNT} Users Connected";
            serverCreate.Content = "Delete Room";

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

            userConnected.Content = $"{Users.Count} / {RoomManager.MAX_USER_COUNT} Users Connected";
        }
    }
}
