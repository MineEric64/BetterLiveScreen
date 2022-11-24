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

using log4net;

using AutoUpdaterDotNET;

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
using System.Web;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace BetterLiveScreen
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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

        public static Dictionary<string, int> PreviewMap { get; set; } = new Dictionary<string, int>(); //(userName, preview index)
        public static ConcurrentDictionary<int, string> Watches { get; private set; } = new ConcurrentDictionary<int, string>();

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
                    PreviewMap.Add(User.ToString(), 0);

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
            UnwatchAll();

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

            var info = new ReceiveInfo(SendTypes.StreamStarted, ClientOne.Encode(User.ToString()), BufferTypes.String, MessagePackSerializer.Serialize(Rescreen.MyVideoStream.Info));
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
                if ((isLived && !Watches.ContainsValue(userFullName)) || (User.Equals(userFullName) && !isLived)) //Go Lived but unwatching or the user is not go lived
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

            ScreenBlacked(User.ToString(), User.IsLived, ref screen_main);
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

            var info = new ReceiveInfo(SendTypes.StreamEnded, ClientOne.Encode(User.ToString()), BufferTypes.String);
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
                            { "user", User.ToString() },
                            { "checksum", Checksum.ComputeAddition(buffer) },
                            { "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds()}
                        };

                foreach (var info in infos)
                {
                    info.ExtraBuffer = ClientOne.Encode(json.ToString());
                    SendBufferFinal(info, User.ToString());
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
                            Cv2.Resize(mat2, mat3, new CvSize(900, 500), 0, 0, InterpolationFlags.Nearest); //for preview

                            ScreenPreview(mat3, PreviewMap[User.ToString()]);

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

                                break;
                        }
                        #endregion

                        //Live Preview
                        #region Live Preview
                        if (IsEnabledLivePreview)
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

                                        ScreenPreview(mat2, PreviewMap[User.ToString()]);
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
                            { "user", User.ToString() },
                            { "checksum", Checksum.ComputeAddition(audio.Item1) },
                            { "audio_sample_rate", WasapiCapture.DeviceWaveFormat.SampleRate },
                            { "audio_channel", WasapiCapture.DeviceWaveFormat.Channels },
                            { "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds() }
                        };

                        foreach (var info in infos)
                        {
                            info.ExtraBuffer = ClientOne.Encode(json.ToString());
                            SendBufferFinal(info, User.ToString());
                        }
                    }
                    Thread.Sleep(1);
                }
                Thread.Sleep(10);
            }
        }
        #endregion
        #region Receive Screen & Audio
        private void ClientBufferRefreshedVideo()
        {
            //Nvenc
            Dictionary<string, NvDecoder> decoderMap = new Dictionary<string, NvDecoder>();

            //OpenH264
            H264Decoder h264Decoder = null;
            Dictionary<string, (bool, DateTime)> wasNullMap = new Dictionary<string, (bool, DateTime)>();

            void ClearBuffer(VideoLike stream, EncodingType encoding)
            {
                int MAX_COUNT = 90;

                if (stream.ScreenQueue.Count > MAX_COUNT)
                {
                    while (stream.ScreenQueue.Count > 0)
                    {
                        if (stream.ScreenQueue.TryDequeue(out var screen2))
                        {
                            switch (encoding)
                            {
                                case EncodingType.OpenH264:
                                    byte[] preview = screen2.Item1.Decompress();

                                    if (h264Decoder == null) h264Decoder = new H264Decoder("openh264-2.3.1-win64.dll");
                                    h264Decoder.Decode(preview, preview.Length);
                                    break;
                            }
                        }
                    }
                }
            }

            while (Watches.Count > 0)
            {
                foreach (var livedUserName in Watches.Values)
                {
                    var livedUser = Users.Where(x => x.Equals(livedUserName)).FirstOrDefault();

                    if (livedUser != null && Rescreen.VideoStreams.TryGetValue(livedUserName, out var videoStream))
                    {
                        Enum.TryParse(videoStream.Info.Encoding, out EncodingType encoding);

                        //ClearBuffer(videoStream, encoding);
                        try
                        {
                            while (videoStream.ScreenQueue.Count > 0)
                            {
                                if (videoStream.ScreenQueue.TryDequeue(out var screen)) //compressed
                                {
                                    byte[] preview = screen.Item1.Decompress();

                                    switch (encoding)
                                    {
                                        #region Nvenc
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

                                                    ScreenPreview(mat13, PreviewMap[livedUserName]);

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
                                        #endregion
                                        #region OpenH264
                                        case EncodingType.OpenH264:
                                            if (h264Decoder == null) h264Decoder = new H264Decoder("openh264-2.3.1-win64.dll");

                                            var bitmap = h264Decoder.Decode(preview, preview.Length);

                                            if (bitmap == null)
                                            {
                                                if (!wasNullMap.ContainsKey(livedUserName)) wasNullMap.Add(livedUserName, (false, DateTime.Now));
                                                if (!wasNullMap[livedUserName].Item1)
                                                {
                                                    log.Warn("Decoded bitmap returned null. skipping to next frame.");
                                                    wasNullMap[livedUserName] = (true, DateTime.Now);
                                                }
                                                continue;
                                            }
                                            else
                                            {
                                                if (wasNullMap.TryGetValue(livedUserName, out var wasNull))
                                                {
                                                    if (wasNull.Item1)
                                                    {
                                                        var delta = DateTime.Now - wasNull.Item2;
                                                        log.Info($"Decoded bitmap now returns properly. skipped duration: {delta.TotalSeconds:0.##}");
                                                    }
                                                    wasNullMap[livedUserName] = (false, DateTime.Now);
                                                }
                                                else wasNullMap.Add(livedUserName, (false, DateTime.Now));
                                            }

                                            if (PreviewMap.TryGetValue(livedUserName, out int index))
                                            {
                                                ScreenPreview(bitmap, index);
                                            }
                                            break;
                                        #endregion
                                        #region CompressOnly
                                        case EncodingType.CompressOnly:
                                            var mat2 = new Mat(videoStream.Info.Height, videoStream.Info.Width, MatType.CV_8UC4);
                                            int length = videoStream.Info.Width * videoStream.Info.Height * 4; // or src.Height * src.Step;

                                            Marshal.Copy(preview, 0, mat2.Data, length);
                                            var mat22 = mat2.Resize(new CvSize(900, 500), 0, 0, InterpolationFlags.Nearest);

                                            ScreenPreview(mat22, PreviewMap[livedUserName]);
                                            mat2.Dispose();

                                            break;
                                            #endregion
                                    }
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
            foreach (var decoder in decoderMap.Values) decoder.Close();
            h264Decoder?.Dispose();
        }

        private void ClientBufferRefreshedAudio()
        {
            void ClearBuffer(VideoLike stream)
            {
                int MAX_COUNT = 90;

                if (stream.AudioQueue.Count > MAX_COUNT)
                {
                    while (stream.AudioQueue.Count > 0)
                    {
                        stream.AudioQueue.TryDequeue(out _);
                    }
                }
            }

            while (Watches.Count > 0)
            {
                foreach (var livedUserName in Watches.Values)
                {
                    var livedUser = Users.Where(x => x.Equals(livedUserName)).FirstOrDefault();

                    if (livedUser != null && Rescreen.VideoStreams.TryGetValue(livedUserName, out var videoStream))
                    {
                        //ClearBuffer(videoStream);
                        try
                        {
                            while (videoStream.AudioQueue.Count > 0)
                            {
                                if (videoStream.AudioQueue.TryDequeue(out var audio)) //compressed
                                {
                                    byte[] decompressed = audio.Item1.Decompress();

                                    if (!WasapiRealtimePlay.IsInitialized) WasapiRealtimePlay.Initialize();
                                    if (!WasapiRealtimePlay.BufferMap.ContainsKey(livedUser.ToString())) WasapiRealtimePlay.AddToBufferMap(livedUser.ToString(), videoStream.AudioFormat);

                                    WasapiRealtimePlay.AddData(livedUser.ToString(), decompressed);
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

        private void ScreenPreview(BitmapSource source, int index)
        {
            System.Windows.Controls.Image[] thumbnails = new System.Windows.Controls.Image[5] { screen_main, thumbnail1, thumbnail2, thumbnail3, thumbnail4 };
            var thumbnail = thumbnails[index];

            thumbnail.Source = source;
        }

        private void ScreenPreview(Mat mat, int index)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var source = mat.ToWriteableBitmap();

                ScreenPreview(source, index);
                mat.Dispose();
            }), DispatcherPriority.Render);
        }

        private void ScreenPreview(Bitmap bitmap, int index)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var source = bitmap.ToImage();

                ScreenPreview(source, index);
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
                RoomManager.Disconnect();
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

        public async Task WatchAsync(string user)
        {
            if (User.Equals(user)) return; //can't watch my own streaming

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

                SendBufferFinal(info, User.ToString());
                Watches.TryAdd(Watches.Count, user);

                if (prevWatchesCount == 0 && Watches.Count > 0)
                {
                    _ = Task.Run(ClientBufferRefreshedVideo);
                    _ = Task.Run(ClientBufferRefreshedAudio);
                }
                #endregion
            }
            else
            {
                log.Error("can't watch stream because the stream's info can't be received.");
                MessageBox.Show("You can't watch the stream because the stream's info can't be received.", "Better Live Screen : Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            #endregion
        }

        public void Unwatch(string user)
        {
            byte[] buffer = ClientOne.Encode(user);
            var info = new ReceiveInfo(SendTypes.WatchEnded, buffer, BufferTypes.String);

            SendBufferFinal(info, User.ToString());
            if (Watches.ContainsValue(user)) Watches.TryRemove(Watches.GetKeyByValue(user), out _);
        }

        public void UnwatchAll()
        {
            for (int i = 0; i < Watches.Count; i++)
            {
                string user = Watches[i];
                Unwatch(user);
            }
        }

        /// <summary>
        /// Sends the buffer to users if the current user is host. if not, sends the buffer to host.
        /// </summary>
        /// <param name="info"></param>
        public static void SendBufferFinal(ReceiveInfo info, string userName = "")
        {
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

            if (!Watches.ContainsValue(key)) //The user wants to watch
            {
                await WatchAsync(key);
                watch.Content = "Unwatch";
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
    }
}
