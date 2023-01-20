using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.UI.Xaml.Documents;

using ConcurrentCollections;

using Config;
using Config.Net;

using log4net;

using AutoUpdaterDotNET;

using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using MessagePack;

using OpenH264Lib;

using BetterLiveScreen.Clients;
using BetterLiveScreen.Extensions;
using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Interfaces.Security;
using BetterLiveScreen.Recording;
using BetterLiveScreen.Recording.Audio.Wasapi;
using BetterLiveScreen.Recording.Types;
using BetterLiveScreen.Recording.Video;
using BetterLiveScreen.Recording.Video.NvColorSpace;
using BetterLiveScreen.Recording.Video.NvPipe;
using BetterLiveScreen.Rooms;
using BetterLiveScreen.Users;

using BetterLiveScreen.BetterShare;

using Path = System.IO.Path;
using CvSize = OpenCvSharp.Size;
using BitmapConverter = BetterLiveScreen.Extensions.BitmapConverter;
using Window = System.Windows.Window;
using NvDecoder = BetterLiveScreen.Recording.Video.NvPipe.Decoder;
using NvPipeFormat = BetterLiveScreen.Recording.Video.NvPipe.Format;
using NvPipeCodec = BetterLiveScreen.Recording.Video.NvPipe.Codec;
using H264Encoder = OpenH264Lib.Encoder;
using H264Decoder = OpenH264Lib.Decoder;
using Debugger = BetterLiveScreen.Extensions.Debugger;

namespace BetterLiveScreen
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static H264Decoder _h264Decoder = null;

        public static ISettings Settings { get; private set; } = null;

        public static UserInfo User { get; set; }
        internal static string UserToken { get; } = Guid.NewGuid().ToString();
        public static List<UserInfo> Users { get; set; } = new List<UserInfo>();

        public static ClientOne Client { get; set; }

        public static StartPage StartWindow { get; private set; } = new StartPage();
        public static BetterShareWindow ShareWindow { get; private set; } = new BetterShareWindow();

        public static bool IsDevMode { get; private set; } = false;

        public static MainWindow Me { get; private set; } = null;
        public static Dispatcher CurrentDispatcher { get; private set; } = null;

        public static bool IsEnabledVideo { get; set; } = true;
        public static bool IsEnabledAudio { get; set; } = true;
        public static bool IsEnabledLivePreview { get; set; } = true;
        public static bool IsEnabledZOrderOptimization { get; set; } = true;
        public static bool IsEnabledZOrderPreview { get; set; } = true;

        public static Dictionary<string, int> PreviewMap { get; set; } = new Dictionary<string, int>(); //(userName, preview index)
        public static ConcurrentHashSet<string> Watches { get; private set; } = new ConcurrentHashSet<string>();

        public MainWindow()
        {
            InitializeComponent();
            Me = this;
            CurrentDispatcher = this.Dispatcher;

            InitializeClient();
            Rescreen.Initialize();

            if (!File.Exists("settings.json"))
            {
                File.WriteAllText("settings.json", Properties.Resources.DefaultSettings);
            }
            Settings = new ConfigurationBuilder<ISettings>()
            .UseJsonFile("settings.json")
            .Build();

            this.Closing += MainWindow_Closing;
            AppDomain.CurrentDomain.UnhandledException += MainWindow_UnhandledException;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AutoUpdater.Synchronous = true;
            AutoUpdater.ShowSkipButton = false;
            AutoUpdater.ShowRemindLaterButton = false;
            AutoUpdater.Mandatory = true;
            AutoUpdater.UpdateMode = Mode.Forced | Mode.ForcedDownload;
            AutoUpdater.RunUpdateAsAdmin = false;

            AutoUpdater.Start("https://raw.githubusercontent.com/Luigi38/ProjectData/master/BetterLiveScreen/info.xml");

            StartWindow.Closing += (s, ee) =>
            {
                if (StartWindow.IsAccepted)
                {
                    usericon.Fill = BitmapConverter.CreateImageBrush(User.GetAvatarImage());
                    username.Content = User.NameInfo.Name;
                    username.ToolTip = $"#{User.NameInfo.Discriminator}";

                    Users.Add(User);
                    PreviewMap.Add(User.FullName, 0);

                    this.IsEnabled = true;
                }
                else
                {
                    this.Close();
                }
            };

            InitializeUI();

            StartWindow.Show();
            this.IsEnabled = false;

            RenderOptions.SetBitmapScalingMode(screen_main, BitmapScalingMode.LowQuality);
            RenderOptions.SetBitmapScalingMode(thumbnail1, BitmapScalingMode.LowQuality);
            RenderOptions.SetBitmapScalingMode(thumbnail2, BitmapScalingMode.LowQuality);
            RenderOptions.SetBitmapScalingMode(thumbnail3, BitmapScalingMode.LowQuality);
            RenderOptions.SetBitmapScalingMode(thumbnail4, BitmapScalingMode.LowQuality);

            //Z-Order
            Task.Run(() =>
            {
                while (true)
                {
                    if (IsEnabledZOrderOptimization && IsEnabledLivePreview)
                    {
                        ZOrderHelper.Refresh();
                        IsEnabledZOrderPreview = !ZOrderHelper.IsOverlapped();
                    }
                    Thread.Sleep(100);
                }
            });
        }

        private void InitializeClient()
        {
            Client = new ClientOne();
            #region Client Events
            #region Peer
            Client.Connected += (s, e) =>
            {
                log.Info($"Connected to {RoomManager.CurrentRoom.Name}");
                //MessageBox.Show("Connected!", "Better Live Screen", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            #endregion
            #region Room
            Client.Disconnected += (s, isForced) =>
            {
                Users.Clear();
                Users.Add(User);

                Rescreen.Stop();

                Dispatcher.Invoke(InitializeUI);
                DiscordHelper.SetPresenceIfLeft();

                if (isForced) MessageBox.Show("Host disconnected the connection.", "Better Live Screen", MessageBoxButton.OK, MessageBoxImage.Information);
                log.Info("Disconnected");
            };

            Client.HostConnected += (s, e) =>
            {
                log.Info("Connected");
            };
            #endregion
            #region User
            Client.UserConnected += (s, userInfo) =>
            {
                Users.Add(userInfo);
                RoomManager.CurrentRoom.CurrentUserCount = Users.Count;

                Dispatcher.Invoke(UpdateUserUI);

                DiscordHelper.SetPresenceIfUserUpdated();

                log.Info($"{userInfo} Joined");
            };
            Client.UserDisconnected += (s, userFullName) =>
            {
                if (!string.IsNullOrEmpty(userFullName))
                {
                    var userInfo = GetUserByName(userFullName);

                    if (userInfo == null) return;

                    Users.Remove(userInfo);
                    PreviewMap.Remove(userFullName);
                    Unwatch(userFullName);

                    RoomManager.CurrentRoom.CurrentUserCount = Users.Count;

                    Dispatcher.Invoke(UpdateUserUI);

                    DiscordHelper.SetPresenceIfUserUpdated();
                }

                log.Info($"{userFullName} Left");
            };
            #endregion
            #region Streaming
            Client.StreamStarted += (s, userName) =>
            {
                var userInfo = GetUserByName(userName);

                if (userInfo == null) return;

                userInfo.IsLived = true;
                Dispatcher.Invoke(UpdateUserUI);

                log.Info($"{userName} Stream Started");
            };
            Client.StreamEnded += (s, userName) =>
            {
                var userInfo = GetUserByName(userName);

                if (userInfo == null) return;

                userInfo.IsLived = false;
                Dispatcher.Invoke(UpdateUserUI);

                log.Info($"{userName} Stream Ended");
            };
            #region Video
            Client.VideoBufferReceived += (s, e) =>
            {
                if (!Rescreen.VideoStreams.TryGetValue(e.Item2, out var _)) Rescreen.VideoStreams.Add(e.Item2, new VideoLike(BitmapInfo.Empty));
                Rescreen.VideoStreams[e.Item2].ScreenQueue.Enqueue((e.Item1, e.Item3));

                //log.Info($"[{DateTime.Now}] {e.Item2}'s Screen Received! ({e.Item1.Length})");
            };
            #endregion
            #region Audio
            Client.AudioBufferReceived += (s, e) =>
            {
                if (!Rescreen.VideoStreams.TryGetValue(e.Item2, out var _)) Rescreen.VideoStreams.Add(e.Item2, new VideoLike(BitmapInfo.Empty));
                Rescreen.VideoStreams[e.Item2].AudioQueue.Enqueue((e.Item1, e.Item3));

                //log.Info($"{e.Item2}'s Audio Received! ({e.Item1.Length})");
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

            InitializeUserUI();
        }

        public void InitializeUserUI()
        {
            name1.Content = string.Empty;
            name2.Content = string.Empty;
            name3.Content = string.Empty;
            name4.Content = string.Empty;

            icon1.Visibility = Visibility.Hidden;
            icon2.Visibility = Visibility.Hidden;
            icon3.Visibility = Visibility.Hidden;
            icon4.Visibility = Visibility.Hidden;

            thumbnail1.Visibility = Visibility.Hidden;
            thumbnail2.Visibility = Visibility.Hidden;
            thumbnail3.Visibility = Visibility.Hidden;
            thumbnail4.Visibility = Visibility.Hidden;

            watch1.Visibility = Visibility.Hidden;
            watch2.Visibility = Visibility.Hidden;
            watch3.Visibility = Visibility.Hidden;
            watch4.Visibility = Visibility.Hidden;

            userConnected.Content = $"0 / {RoomManager.MAX_USER_COUNT} Users Connected";
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            _h264Decoder?.Dispose();

            UnwatchAll();

            Client?.Disconnect();
            Client?.Stop();

            //Application.Current.Shutdown();
            Process.GetCurrentProcess().Kill();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!RoomManager.IsConnected && !string.IsNullOrWhiteSpace(serverIp.Text) && Keyboard.IsKeyDown(Key.Enter)) //Connect
            {
                serverIpConnect_Click(null, null);
            }
            if (Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.Enter)) //Popup Window
            {
                Popup();
            }

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

            log.Error("Error occured", ex);
            MessageBox.Show(ex.ToCleanString(), "BetterLiveScreen: Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void goLive_Click(object sender, RoutedEventArgs e)
        {
            if (Rescreen.IsRecording) Rescreen.Stop();

            //TODO: Change Settings by User
            var liveSelect = new LiveSelectWindow();

            liveSelect.Show();
            if (!await liveSelect.WaitAsyncUntilOK()) return;

            //Screen Share
            Rescreen.Start();
            _ = Task.Run(RescreenRefreshedVideo);
            _ = Task.Run(RescreenRefreshedAudio);

            User.IsLived = true;

            var info = new ReceiveInfo(SendTypes.StreamStarted, ClientOne.Encode(User.FullName), BufferTypes.String, MessagePackSerializer.Serialize(Rescreen.MyVideoStream.Info));
            SendBufferFinal(info);
        }

        private void UpdateScreen(ref System.Windows.Controls.Image thumbnail, BitmapSource bitmap)
        {
            thumbnail.Source = bitmap;
        }
        private void UpdateScreenToBlack(ref System.Windows.Controls.Image thumbnail)
        {
            UpdateScreen(ref thumbnail, BitmapConverter.BLACK_IMAGE);
        }

        public void UpdateUserUI()
        {
            void ScreenBlacked(string userFullName, bool isLived, ref System.Windows.Controls.Image thumbnail)
            {
                if ((isLived && !Watches.Contains(userFullName)) || (User.Equals(userFullName) && !isLived)) //Go Lived but unwatching or the user is not go lived
                {
                    //the screen should be black
                    UpdateScreenToBlack(ref thumbnail);
                }
            }

            void UpdateInternal(
                ref System.Windows.Controls.Image thumbnail, ref Button watch,
                ref Label name, ref Ellipse icon,
                string userFullName, string userName, string avatarUrl, bool isLived
                )
            {
                if (!string.IsNullOrEmpty(userFullName))
                {
                    icon.Visibility = Visibility.Visible;
                    thumbnail.Visibility = Visibility.Visible;

                    name.Content = userName;
                    icon.Fill = BitmapConverter.CreateImageBrush(UserInfo.GetAvatarImage(avatarUrl));

                    ScreenBlacked(userFullName, isLived, ref thumbnail);

                    if (User.Equals(userFullName))
                    {
                        watch.Visibility = Visibility.Hidden;
                    }
                    else if (!isLived)
                    {
                        UpdateScreenToBlack(ref thumbnail);

                        watch.Content = "Watch";
                        watch.Visibility = Visibility.Hidden;
                    }
                    else
                    {
                        watch.Visibility = Visibility.Visible;
                    }
                }
            }

            InitializeUserUI(); //Reset

            string[] fullNames = new string[5] { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty };
            string[] names = new string[5] { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty };
            string[] urls = new string[5] { string.Empty, string.Empty, string.Empty, string.Empty, string.Empty };
            bool[] lives = new bool[5] { false, false, false, false, false };

            if (!PreviewMap.ContainsValue(0)) //Reset, because some user is left
            {
                PreviewMap.Clear();
            }

            for (int i = 0; i < Users.Count; i++)
            {
                string userName = Users[i].ToString();

                if (!PreviewMap.TryGetValue(userName, out var index))
                {
                    index = i;
                    PreviewMap.Add(userName, index);
                }

                fullNames[index] = userName;
                names[index] = Users[i].NameInfo.Name;
                urls[index] = Users[i].AvatarURL;
                lives[index] = Users[i].IsLived;
            }

            ScreenBlacked(User.FullName, User.IsLived, ref screen_main);
            UpdateInternal(ref thumbnail1, ref watch1, ref name1, ref icon1, fullNames[1], names[1], urls[1], lives[1]);
            UpdateInternal(ref thumbnail2, ref watch2, ref name2, ref icon2, fullNames[2], names[2], urls[2], lives[2]);
            UpdateInternal(ref thumbnail3, ref watch3, ref name3, ref icon3, fullNames[3], names[3], urls[3], lives[3]);
            UpdateInternal(ref thumbnail4, ref watch4, ref name4, ref icon4, fullNames[4], names[4], urls[4], lives[4]);

            userConnected.Content = $"{Users.Count} / {RoomManager.MAX_USER_COUNT} Users Connected";
        }

        private void stopLive_Click(object sender, RoutedEventArgs e)
        {
            Rescreen.Stop();

            User.IsLived = false;

            var info = new ReceiveInfo(SendTypes.StreamEnded, ClientOne.Encode(User.FullName), BufferTypes.String);
            SendBufferFinal(info);
        }

        #region Send Screen & Audio
        private void RescreenRefreshedVideo()
        {
            void SendScreenBuffer(byte[] buffer)
            {
                var infos = ClientOne.DivideInfo(SendTypes.Video, buffer);
                var json = new JObject()
                        {
                            { "buffer_length", buffer.Length },
                            { "user", User.FullName },
                            { "checksum", Checksum.ComputeAddition(buffer) },
                            { "timestamp", Timestamp.Now }
                        };

                foreach (var info in infos)
                {
                    info.ExtraBuffer = ClientOne.Encode(json.ToString());
                    SendBufferFinal(info, User.FullName);
                }
            }

            #region Initializing En/Decoder
            NvDecoder decoder = null;
            H264Encoder encoder = null;

            const int PREVIEW_DPF = 33; //preview referesh delay per frame
            var sw = new Stopwatch();

            //initialize encoder & decoder
            switch (Rescreen.Settings.Encoding)
            {
                case EncodingType.Nvenc:
                    //Format : RGBA
                    decoder = new NvDecoder(Rescreen.ScreenActualSize.Width, Rescreen.ScreenActualSize.Height, NvPipeCodec.H264, NvPipeFormat.RGBA32);
                    decoder.onDecoded += (s, e) =>
                    {
                        if (sw.ElapsedMilliseconds > PREVIEW_DPF)
                        {
                            if (PreviewMap.TryGetValue(User.FullName, out int index))
                            {
                                void FinalPreview()
                                {
                                    Mat ConvertLegacy(int width, int height, IntPtr ptr, int length)
                                    {
                                        Mat mat = new Mat(decoder.height, decoder.width, MatType.CV_8UC4);
                                        Kernel32.CopyMemory(mat.Data, ptr, (uint)length);

                                        Mat mat2 = mat.CvtColor(ColorConversionCodes.RGBA2BGR);
                                        //Mat mat3 = mat2.Resize(new CvSize(900, 500), 0, 0, InterpolationFlags.Nearest); //for preview

                                        mat.Dispose();
                                        //mat2.Dispose();

                                        return mat2;
                                    }
                                    PreviewBuffer ConvertNvColorSpace(int width, int height, IntPtr ptr, int length)
                                    {
                                        int size = width * height * 3;
                                        IntPtr bgr = Marshal.AllocHGlobal(size);
                                        int status = NvColorSpace.RGBA32ToBGR24(ptr, bgr, width, height);
                                        byte[] buffer = new byte[size];

                                        Marshal.Copy(bgr, buffer, 0, size);
                                        Marshal.FreeHGlobal(bgr);

                                        return new PreviewBuffer(buffer, width, height, PixelFormats.Bgr24, width * 3);
                                    }

                                    if (Rescreen.Settings.SelectedMonitor.GPU == GPUSelect.Nvidia)
                                    {
                                        var converted = ConvertNvColorSpace(decoder.width, decoder.height, e.Item1, e.Item2);
                                        ScreenPreview(converted, index);
                                    }
                                    else
                                    {
                                        var converted = ConvertLegacy(decoder.width, decoder.height, e.Item1, e.Item2);
                                        ScreenPreview(converted, index);
                                    }
                                }

                                FinalPreview();
                            }

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
            #endregion

            sw.Start();

            while (Rescreen.IsRecording)
            {
                while (Rescreen.MyVideoStream.ScreenQueue.Count > 0)
                {
                    if (Rescreen.MyVideoStream.ScreenQueue.TryDequeue(out var screen) && IsEnabledVideo) //compressed
                    {
                        byte[] preview = screen.Item1.Decompress();

                        //TODO: Send Screen Buffer
                        #region Send Screen Buffer
                        switch (Rescreen.Settings.Encoding)
                        {
                            case EncodingType.Nvenc:
                            case EncodingType.CompressOnly:
                                SendScreenBuffer(screen.Item1);
                                break;

                            case EncodingType.OpenH264:
                                void FinalEncode()
                                {
                                    void ConvertLegacy()
                                    {
                                        Mat mat = new Mat(Rescreen.ScreenActualSize.Height, Rescreen.ScreenActualSize.Width, MatType.CV_8UC4);
                                        int length = Rescreen.ScreenActualSize.Width * Rescreen.ScreenActualSize.Height * 4;

                                        Marshal.Copy(preview, 0, mat.Data, length);
                                        Mat mat2 = mat.CvtColor(ColorConversionCodes.BGRA2YUV_I420);

                                        unsafe
                                        {
                                            encoder.Encode(mat2.DataPointer);
                                        }

                                        mat2.Dispose();
                                        mat.Dispose();
                                    }
                                    void ConvertNvColorSpace()
                                    {
                                        int size = Rescreen.ScreenActualSize.Width * Rescreen.ScreenActualSize.Height * 3;

                                        GCHandle bgraHandle = GCHandle.Alloc(preview, GCHandleType.Pinned);
                                        IntPtr bgra = bgraHandle.AddrOfPinnedObject();
                                        IntPtr yuv420 = Marshal.AllocHGlobal(size);

                                        int status = NvColorSpace.BGRA32ToYUV420(bgra, yuv420, Rescreen.ScreenActualSize.Width, Rescreen.ScreenActualSize.Height);
                                        //Debug.WriteLine(status);

                                        unsafe
                                        {
                                            byte* ptr = (byte*)yuv420.ToPointer();
                                            encoder.Encode(ptr);
                                        }

                                        bgraHandle.Free();
                                        Marshal.FreeHGlobal(yuv420);
                                    }

                                    ConvertLegacy();
                                    //if (Rescreen.Settings.SelectedMonitor.GPU == GPUSelect.Nvidia)
                                    //{
                                    //    ConvertNvColorSpace();
                                    //}
                                    //else
                                    //{
                                    //    ConvertLegacy();
                                    //}
                                }

                                FinalEncode();
                                break;
                        }
                        #endregion

                        //Live Preview
                        #region Live Preview
                        if (IsEnabledLivePreview && IsEnabledZOrderPreview)
                        {
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

                                        if (PreviewMap.TryGetValue(User.FullName, out int index))
                                        {
                                            ScreenPreview(mat2, index);
                                        }
                                        mat.Dispose();

                                        sw.Restart();
                                    }

                                    break;
                            }
                        }
                        #endregion
                    }
                    Thread.Sleep(1);
                }
                Thread.Sleep(10);
            }
            sw.Stop();

            decoder?.Close();
            encoder?.Dispose();
        }

        private void RescreenRefreshedAudio()
        {
            while (Rescreen.IsRecording)
            {
                while (Rescreen.MyVideoStream.AudioQueue.Count > 0)
                {
                    if (Rescreen.MyVideoStream.AudioQueue.TryDequeue(out var audio) && IsEnabledAudio) //compressed
                    {
                        //TODO: Send Audio Buffer
                        var infos = ClientOne.DivideInfo(SendTypes.Audio, audio.Item1);
                        var json = new JObject()
                        {
                            { "buffer_length", audio.Item1.Length },
                            { "user", User.FullName },
                            { "checksum", Checksum.ComputeAddition(audio.Item1) },
                            { "audio_sample_rate", WasapiCapture.DeviceWaveFormat.SampleRate },
                            { "audio_channel", WasapiCapture.DeviceWaveFormat.Channels },
                            { "timestamp", Timestamp.Now }
                        };

                        foreach (var info in infos)
                        {
                            info.ExtraBuffer = ClientOne.Encode(json.ToString());
                            SendBufferFinal(info, User.FullName);
                        }
                    }
                    Thread.Sleep(1);
                }
                Thread.Sleep(10);
            }
        }
        #endregion
        #region Receive Screen & Audio
        #region Screen
        private void ClientBufferRefreshedVideo(string user)
        {
            //Nvenc
            NvDecoder nvDecoder = null;

            //OpenH264
            bool wasNull = false;
            DateTime wasNullTime = DateTime.Now;

            const double CLEAR_BUFFER_INTERVAL = 5.0; //unit: seconds
            const bool CLEAR_BUFFER_DECODING = true;
            var startCleared = DateTime.Now;

            void ClearBuffer(VideoLike stream2, EncodingType encoding)
            {
                long timestamp = Timestamp.Now;

                while (stream2.ScreenQueue.Count > 0)
                {
                    if (stream2.ScreenQueue.TryPeek(out var video))
                    {
                        if (video.Item2 >= timestamp) break;

                        if (CLEAR_BUFFER_DECODING)
                        {
                            switch (encoding)
                            {
                                case EncodingType.OpenH264:
                                    byte[] preview = video.Item1.Decompress();

                                    if (_h264Decoder == null) _h264Decoder = new H264Decoder("openh264-2.3.1-win64.dll");
                                    _h264Decoder.Decode(preview, preview.Length);
                                    break;
                            }
                        }
                        stream2.ScreenQueue.TryDequeue(out _);
                    }
                }
            }

            while (Watches.Contains(user))
            {
                var livedUser = GetUserByName(user);

                if (livedUser != null && Rescreen.VideoStreams.TryGetValue(user, out var stream))
                {
                    Enum.TryParse(stream.Info.Encoding, out EncodingType encoding);

                    if (Settings.ClearVideoBufferEnabled && (DateTime.Now - startCleared).TotalSeconds >= CLEAR_BUFFER_INTERVAL)
                    {
                        ClearBuffer(stream, encoding);
                        startCleared = DateTime.Now;
                    }

                    try
                    {
                        while (stream.ScreenQueue.Count > 0)
                        {
                            if (stream.ScreenQueue.TryDequeue(out var screen) && IsEnabledZOrderPreview) //compressed
                            {
                                byte[] preview = screen.Item1.Decompress();

                                switch (encoding)
                                {
                                    #region Nvenc
                                    case EncodingType.Nvenc:
                                        if (nvDecoder == null)
                                        {
                                            Mat ConvertLegacy(IntPtr ptr_, int length_)
                                            {
                                                Mat mat1 = new Mat(nvDecoder.height, nvDecoder.width, MatType.CV_8UC4);
                                                Kernel32.CopyMemory(mat1.Data, ptr_, (uint)length_);

                                                Mat mat12 = new Mat();
                                                //Mat mat13 = new Mat();

                                                Cv2.CvtColor(mat1, mat12, ColorConversionCodes.RGBA2BGR);
                                                //Cv2.Resize(mat12, mat13, new CvSize(900, 500), 0, 0, InterpolationFlags.Nearest);

                                                mat1.Dispose();
                                                //mat12.Dispose();

                                                return mat12;
                                            }
                                            PreviewBuffer ConvertNvColorSpace(IntPtr ptr_, int length_)
                                            {
                                                int size = nvDecoder.width * nvDecoder.height * 3;
                                                IntPtr bgr = Marshal.AllocHGlobal(size);
                                                int status = NvColorSpace.RGBA32ToBGR24(ptr_, bgr, nvDecoder.width, nvDecoder.height);
                                                byte[] buffer = new byte[size];

                                                Marshal.Copy(bgr, buffer, 0, size);
                                                Marshal.FreeHGlobal(bgr);

                                                return new PreviewBuffer(buffer, nvDecoder.width, nvDecoder.height, PixelFormats.Bgr24, nvDecoder.width * 3);
                                            }
                                            void FinalPreview(IntPtr ptr_, int length_, int index)
                                            {
                                                if (Rescreen.Settings.SelectedMonitor.GPU == GPUSelect.Nvidia)
                                                {
                                                    var converted = ConvertNvColorSpace(ptr_, length_);
                                                    ScreenPreview(converted, index);
                                                }
                                                else
                                                {
                                                    var converted = ConvertLegacy(ptr_, length_);
                                                    ScreenPreview(converted, index);
                                                }
                                            }

                                            nvDecoder = new NvDecoder(stream.Info.Width, stream.Info.Height, Codec.H264, Format.RGBA32);
                                            nvDecoder.onDecoded += (s, e) =>
                                            {
                                                if (PreviewMap.TryGetValue(user, out int index))
                                                {
                                                    FinalPreview(e.Item1, e.Item2, index);
                                                }
                                            };
                                        }

                                        var handle = GCHandle.Alloc(preview, GCHandleType.Pinned);
                                        var ptr = handle.AddrOfPinnedObject();

                                        nvDecoder.Decode(ptr, preview.Length);
                                        handle.Free();

                                        break;
                                    #endregion
                                    #region OpenH264
                                    case EncodingType.OpenH264:
                                        if (_h264Decoder == null) _h264Decoder = new H264Decoder("openh264-2.3.1-win64.dll");

                                        var bitmap = CurrentDispatcher.Invoke(() => _h264Decoder.Decode(preview, preview.Length));

                                        if (bitmap == null)
                                        {
                                            if (!wasNull)
                                            {
                                                wasNull = true;
                                                wasNullTime = DateTime.Now;

                                                log.Warn("Decoded bitmap returned null. skipping to next frame.");
                                            }
                                            continue;
                                        }
                                        else
                                        {
                                            if (wasNull)
                                            {
                                                var delta = DateTime.Now - wasNullTime;
                                                log.Info($"Decoded bitmap now returns properly. skipped duration: {delta.TotalSeconds:0.##}");
                                            }

                                            wasNull = false;
                                            wasNullTime = DateTime.Now;
                                        }

                                        if (PreviewMap.TryGetValue(user, out int index2))
                                        {
                                            ScreenPreview(bitmap, index2);
                                        }
                                        break;
                                    #endregion
                                    #region CompressOnly
                                    case EncodingType.CompressOnly:
                                        var mat2 = new Mat(stream.Info.Height, stream.Info.Width, MatType.CV_8UC4);
                                        int length = stream.Info.Width * stream.Info.Height * 4; // or src.Height * src.Step;

                                        Marshal.Copy(preview, 0, mat2.Data, length);
                                        //var mat22 = mat2.Resize(new CvSize(900, 500), 0, 0, InterpolationFlags.Nearest);

                                        if (PreviewMap.TryGetValue(user, out int index3))
                                        {
                                            ScreenPreview(mat2, index3);
                                        }
                                        mat2.Dispose();

                                        break;
                                        #endregion
                                }
                            }
                            Thread.Sleep(1);
                        }
                    }
                    catch (MessagePackSerializationException)
                    {
                        return; //udp packet loss
                    }
                }
                Thread.Sleep(1);
            }
        }
        #endregion
        #region Audio
        private void ClientBufferRefreshedAudio()
        {
            const double CLEAR_BUFFER_INTERVAL = 5.0; //unit: seconds
            var startCleared = DateTime.Now;

            void ClearBuffer(VideoLike stream)
            {
                long timestamp = Timestamp.Now;

                while (stream.AudioQueue.Count > 0)
                {
                    if (stream.AudioQueue.TryPeek(out var audio))
                    {
                        if (audio.Item2 >= timestamp) break;
                        stream.AudioQueue.TryDequeue(out _);
                    }
                }
            }

            while (Watches.Count > 0)
            {
                foreach (var livedUserName in Watches)
                {
                    var livedUser = GetUserByName(livedUserName);

                    if (livedUser != null && Rescreen.VideoStreams.TryGetValue(livedUserName, out var videoStream))
                    {
                        if (Settings.ClearAudioBufferEnabled && (DateTime.Now - startCleared).TotalSeconds >= CLEAR_BUFFER_INTERVAL)
                        {
                            ClearBuffer(videoStream);
                            startCleared = DateTime.Now;
                        }

                        try
                        {
                            while (videoStream.AudioQueue.Count > 0)
                            {
                                if (videoStream.AudioQueue.TryDequeue(out var audio)) //compressed
                                {
                                    byte[] decompressed = audio.Item1.Decompress();

                                    if (!WasapiRealtimePlay.IsInitialized) WasapiRealtimePlay.Initialize();
                                    if (!WasapiRealtimePlay.BufferMap.ContainsKey(livedUser.FullName)) WasapiRealtimePlay.AddToBufferMap(livedUser.FullName, videoStream.AudioFormat);

                                    WasapiRealtimePlay.AddData(livedUser.FullName, decompressed);
                                    WasapiRealtimePlay.Play();
                                }
                            }
                            Thread.Sleep(1);
                        }
                        catch (MessagePackSerializationException)
                        {
                            continue; //udp packet loss
                        }
                    }
                }
                Thread.Sleep(10);
            }
            WasapiRealtimePlay.Stop();
        }
        #endregion
        #endregion
        private void ScreenPreviewInternal(BitmapSource source, int index)
        {
            if (index == 11) //Popup
            {
                PopupWindow.Instance.ScreenPreview(source);
                return;
            }

            System.Windows.Controls.Image[] thumbnails = new System.Windows.Controls.Image[5] { screen_main, thumbnail1, thumbnail2, thumbnail3, thumbnail4 };
            var thumbnail = thumbnails[index];

            thumbnail.Source = source;
        }

        private void ScreenPreview(PreviewBuffer e, int index) //byte[] -> BitmapSource -> WriteableBitmap
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                WriteableBitmap wbtmMap = new WriteableBitmap(e.Width, e.Height, 96, 96, e.Format, null);
                wbtmMap.WritePixels(new Int32Rect(0, 0, e.Width, e.Height), e.Buffer, e.Stride, 0);

                ScreenPreviewInternal(wbtmMap, index);
            }), DispatcherPriority.Render);
        }

        private void ScreenPreview(Mat mat, int index)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var source = mat.ToWriteableBitmap();

                ScreenPreviewInternal(source, index);
                mat.Dispose();
            }), DispatcherPriority.Render);
        }

        private void ScreenPreview(Bitmap bitmap, int index)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var source = bitmap.ToImage();

                ScreenPreviewInternal(source, index);
                bitmap.Dispose();
            }), DispatcherPriority.Render);
        }

        private async void serverIpConnect_Click(object sender, RoutedEventArgs e)
        {
            //if (IsDevMode)
            //{
            //    await RecordingTest.RecordTestAsync(
            //    videoType: CaptureVideoType.DD,
            //    audioType: CaptureAudioType.WinCaptureAudio,
            //    milliseconds: 10000,
            //    monitor: RescreenSettings.PrimaryMonitor,
            //    fps: 60,
            //    isHalf: false,
            //    encoding: EncodingType.Nvenc
            //    );
            //    return;
            //}

            if (RoomManager.IsConnected) //need to Disconnect
            {
                //RoomManager.Disconnect();
                RoomManager.Delete();
                serverIpConnect.Content = "Connect";

                return;
            }

            if (string.IsNullOrWhiteSpace(serverIp.Text))
            {
                MessageBox.Show("Address is empty.", "BetterLiveScreen : Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (await ShowRoomInfo(serverIp.Text))
            {
                string password = string.Empty;

                if (RoomManager.CurrentRoom.PasswordRequired)
                {
                    var textWindow = new ChooseText();

                    textWindow.Title = "Input Room's Password";
                    textWindow.Show();
                    password = SHA512.Hash(await textWindow.WaitAsyncUntilOK());
                }

                await ConnectRoom(password);
            }
            else
            {
                MessageBox.Show("Failed to get room's information from the address.", "BetterLiveScreen : Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task<bool> ShowRoomInfo(string address)
        {
            if (Rescreen.IsRecording) Rescreen.Stop();

            var room = await RoomManager.GetRoomInfoAsync(address);

            if (room != null)
            {
                RoomManager.CurrentRoom = room;

                log.Info($"Room Name : {room.Name}");
                log.Info($"Room Description : {room.Description}");
                log.Info($"Room Host : {room.HostName}");
                log.Info($"Room Current User Count : {room.CurrentUserCount}");
                log.Info($"Room Password Required : {room.PasswordRequired}");

                return true;
            }

            return false;
        }

        public async Task ConnectRoom(string password = "")
        {
            var connectedInfo = await RoomManager.ConnectAsync(password);

            switch (connectedInfo.ResponseCode)
            {
                case ResponseCodes.OK:
                    //RoomManager.Password = password;

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
                            log.Error("Something went wrong when connecting.");

                            var info = connectedInfo.GetFailed(ResponseCodes.Failed);
                            Client.SendBufferToHost(info);

                            return;
                        }
                    }
                    RoomManager.CurrentRoom.CurrentUserCount = Users.Count;
                    RoomManager.CurrentRoomId = Guid.Parse(id);
                    RoomManager.IsConnected = true;

                    serverIpConnect.Content = "Disconnect";
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
            DiscordHelper.SetPresenceIfCreated();

            userConnected.Content = $"1 / {RoomManager.MAX_USER_COUNT} Users Connected";
            serverCreate.Content = "Delete Room";

            MessageBox.Show("Created!", "BetterLiveScreen", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void serverBetterShare_Click(object sender, RoutedEventArgs e)
        {
            if (ShareWindow.IsClosed) ShareWindow = new BetterShareWindow();
            ShareWindow.Show();
        }

        private void chk_enableVideo_Checked(object sender, RoutedEventArgs e)
        {
            IsEnabledVideo = true;
        }

        private void chk_enableVideo_Unchecked(object sender, RoutedEventArgs e)
        {
            IsEnabledVideo = false;
        }

        private void chk_enableAudio_Checked(object sender, RoutedEventArgs e)
        {
            IsEnabledAudio = true;
        }

        private void chk_enableAudio_Unchecked(object sender, RoutedEventArgs e)
        {
            IsEnabledAudio = false;
        }

        private void chk_enableLivePreview_Checked(object sender, RoutedEventArgs e)
        {
            IsEnabledLivePreview = true;
        }

        private void chk_enableLivePreview_Unchecked(object sender, RoutedEventArgs e)
        {
            IsEnabledLivePreview = false;
        }

        public static UserInfo GetUserByName(string userFullName)
        {
            return Users.Where(x => x.Equals(userFullName)).FirstOrDefault();
        }

        public async Task<bool> WatchAsync(string user)
        {
            if (User.Equals(user)) return false; //can't watch my own streaming

            int prevWatchesCount = Watches.Count;
            byte[] buffer;
            ReceiveInfo info;

            #region Requesting Stream Info
            buffer = ClientOne.Encode(user);
            info = new ReceiveInfo(SendTypes.StreamInfoRequested, buffer, BufferTypes.String);

            SendBufferFinal(info);
            info = await Client.ReceiveBufferAsync(info);

            if (info.ResponseCode == ResponseCodes.OK)
            {
                string userName = ClientOne.Decode(info.Buffer);
                var streamInfo = MessagePackSerializer.Deserialize<BitmapInfo>(info.ExtraBuffer);

                if (Rescreen.VideoStreams.TryGetValue(userName, out var videoStream)) videoStream.Info = streamInfo;
                else Rescreen.VideoStreams.Add(userName, new VideoLike(streamInfo));

                log.Info($"{userName} Stream Info Received : {streamInfo}");

                #region Watching Started
                buffer = ClientOne.Encode(user);
                info = new ReceiveInfo(SendTypes.WatchStarted, buffer, BufferTypes.String);

                SendBufferFinal(info, User.FullName);
                Watches.Add(user);

                _ = Task.Run(() => ClientBufferRefreshedVideo(user));

                if (prevWatchesCount == 0 && Watches.Count > 0)
                {
                    _ = Task.Run(ClientBufferRefreshedAudio);
                }
                #endregion

                return true;
            }
            else
            {
                log.Error("can't watch stream because the stream's info can't be received.");
                MessageBox.Show("You can't watch the stream because the stream's info can't be received.", "Better Live Screen : Error", MessageBoxButton.OK, MessageBoxImage.Error);

                return false;
            }
            #endregion
        }

        public void Unwatch(string user)
        {
            if (!Watches.Contains(user)) return;

            byte[] buffer = ClientOne.Encode(user);
            var info = new ReceiveInfo(SendTypes.WatchEnded, buffer, BufferTypes.String);

            SendBufferFinal(info, User.FullName);
            Watches.TryRemove(user);
        }

        public void UnwatchAll()
        {
            foreach (string user in Watches)
            {
                Unwatch(user);
            }
        }

        /// <summary>
        /// Sends the buffer to users if the current user is host. if not, sends the buffer to host.
        /// </summary>
        /// <param name="info"></param>
        public static void SendBufferFinal(ReceiveInfo info, string userName = "")
        {
            if (string.IsNullOrEmpty(userName)) userName = User.FullName;

            if (!RoomManager.IsHost)
            {
                Client.SendBufferToHost(info);
            }
            else
            {
                Client.OnReceived4Host(null, 0, info, userName);
            }
        }

        private void credit_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Clipboard.SetText("세연#7997");
            MessageBox.Show("Copied to Clipboard!", "Better Live Screen", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void thumbnail1_MouseUp(object sender, MouseButtonEventArgs e)
        {
            thumbnail_Internal(0, 1, e);
        }

        private void thumbnail2_MouseUp(object sender, MouseButtonEventArgs e)
        {
            thumbnail_Internal(0, 2, e);
        }

        private void thumbnail3_MouseUp(object sender, MouseButtonEventArgs e)
        {
            thumbnail_Internal(0, 3, e);
        }

        private void thumbnail4_MouseUp(object sender, MouseButtonEventArgs e)
        {
            thumbnail_Internal(0, 4, e);
        }

        private void thumbnail_Internal(int index1, int index2, MouseButtonEventArgs e)
        {
            string key1 = PreviewMap.GetKeyByValue(index1);
            string key2 = PreviewMap.GetKeyByValue(index2);

            if (key1 != null && key2 != null) //Swap
            {
                PreviewMap.Swap(key1, key2);
                UpdateUserUI();
            }
        }

        private async void watch1_Click(object sender, RoutedEventArgs e)
        {
            await watch_Internal(1, watch1);
        }

        private async void watch2_Click(object sender, RoutedEventArgs e)
        {
            await watch_Internal(2, watch2);
        }

        private async void watch3_Click(object sender, RoutedEventArgs e)
        {
            await watch_Internal(3, watch3);
        }

        private async void watch4_Click(object sender, RoutedEventArgs e)
        {
            await watch_Internal(4, watch4);
        }

        private async Task watch_Internal(int index, Button watch)
        {
            System.Windows.Controls.Image[] thumbnails = new System.Windows.Controls.Image[4] { thumbnail1, thumbnail2, thumbnail3, thumbnail4 };
            string key = PreviewMap.GetKeyByValue(index);

            if (!Watches.Contains(key)) //The user wants to watch
            {
                if (await WatchAsync(key))
                {
                    watch.Content = "Unwatch";
                }
            }
            else //The user doesn't want to watch
            {
                Unwatch(key);
                UpdateScreenToBlack(ref thumbnails[index - 1]);
                watch.Content = "Watch";
            }
        }

        private void usericon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(User.AvatarURL)) return;

            using (WebClient wc = new WebClient())
            {
                using (Stream s = wc.OpenRead(User.AvatarURL))
                {
                    using (Bitmap bmp = new Bitmap(s))
                    {
                        var source = bmp.ToImage();
                        usericon.Fill = BitmapConverter.CreateImageBrush(source);
                    }
                }
            }
        }

        private void Popup()
        {
            if (!PopupWindow.IsOpened)
            {
                PopupWindow.Instance.Show();
            }
            else
            {
                PopupWindow.Instance.Focus();
            }
        }
    }
}
