using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

using OpenCvSharp;

using TurboJpegWrapper;

using BetterLiveScreen.Extensions;
using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Recording;
using BetterLiveScreen.Recording.Audio;
using BetterLiveScreen.Recording.Audio.Wasapi;
using BetterLiveScreen.Recording.Audio.WinCaptureAudio;
using BetterLiveScreen.Recording.Types;
using BetterLiveScreen.Recording.Video;
using BetterLiveScreen.Recording.Video.WGC;

using Size = System.Drawing.Size;
using WasapiCapture = BetterLiveScreen.Recording.Audio.Wasapi.WasapiCapture;

namespace BetterLiveScreen.Recording.Video
{
    public class Rescreen
    {
        private static FScreen _raw = new FScreen();
        public static Mixer SessionMixer { get; private set; } = new Mixer();

        internal static List<int> _deltaRess = new List<int>();
        internal static List<int> _delayPerFrame = new List<int>();

        private static Stopwatch _flow = new Stopwatch(); //actual recording time
        internal static Stopwatch _deltaResSw = new Stopwatch(); //Resolution Per Frame Time
        internal static Stopwatch _delayPerFrameSw = new Stopwatch(); //Delay Per Frame Time

        public static Dictionary<string, VideoLike> VideoStreams { get; set; } = new Dictionary<string, VideoLike>();
        public static VideoLike MyVideoStream => VideoStreams[MainWindow.User.ToString()];
        public static Dictionary<string, BitrateInfo> BitrateInfos { get; } = new Dictionary<string, BitrateInfo>();

        public static RescreenSettings Settings { get; private set; } = RescreenSettings.Default;
        public static CaptureSupports Supports { get; private set; } = CaptureSupports.Default;

        public static Size ScreenActualSize => Settings.IsHalf ? Settings.SelectedMonitor.ScreenSize.DivideBy(2) : Settings.SelectedMonitor.ScreenSize;
        public static int DelayPerFrame => 1000 / Settings.Fps;
        public static int FpsIfUnfixed30 => Settings.Fps > 0 ? Settings.Fps : 30;
        public static int FpsIfUnfixed60 => Settings.Fps > 0 ? Settings.Fps : 60;

        public static bool IsRecording { get; private set; } = false;
        public static TimeSpan Elapsed => _flow.Elapsed;

        public static void Initialize()
        {
            BitrateInfos.Add("1440@60", new BitrateInfo(9, 12, 18));
            BitrateInfos.Add("1440@30", new BitrateInfo(6, 9, 13));
            BitrateInfos.Add("1080@60", new BitrateInfo(4, 6, 9));
            BitrateInfos.Add("1080@30", new BitrateInfo(3, 4, 6));
            BitrateInfos.Add("720@60", new BitrateInfo(2, 4, 6));
            BitrateInfos.Add("720@30", new BitrateInfo(1, 2, 4));
        }

        public static void MakeSettings(RescreenSettings settings)
        {
            Settings = settings;
        }

        public static void Start()
        {
            VideoStreams[MainWindow.User.ToString()] = new VideoLike(VideoLike.FromRescreenSettings());
            _flow.Reset();

            switch (Settings.VideoType)
            {
                case CaptureVideoType.DD:
                    CaptureSupports.SupportsDesktopDuplication();

                    if (!Supports.DesktopDuplication)
                    {
                        MessageBox.Show("The screen can't be captured when using DXGI Desktop Duplication.\nPlease use another capture method.",
                        "Better Live Screen : Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    _raw.ScreenRefreshed += ScreenRefreshed;
                    _raw.Start();

                    break;

                case CaptureVideoType.WGC:
                    CaptureSupports.SupportsWGC();

                    if (!Supports.WGC)
                    {
                        MessageBox.Show("The screen can't be captured when using Windows.Graphics.Capture.\nPlease use another capture method.",
                        "Better Live Screen : Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    WGCHelper.ScreenRefreshed += ScreenRefreshed;
                    WGCHelper.StartSelectedMonitorCapture();

                    break;
            }
            switch (Settings.AudioType)
            {
                case CaptureAudioType.WasapiLoopback:
                    if (!WasapiCapture.IsInitialized) WasapiCapture.Initialize();

                    WasapiCapture.DataAvailable += AudioRefreshed;
                    WasapiCapture.Record();

                    break;

                case CaptureAudioType.WinCaptureAudio:
                    SessionMixer.DataAvailable += AudioRefreshed;
                    SessionMixer.Start();

                    break;
            }

            _flow.Start();
            IsRecording = true;
        }

        public static void Stop()
        {
            if (!IsRecording) return;

            switch (Settings.VideoType)
            {
                case CaptureVideoType.DD:
                    _raw.Stop();
                    _raw.ScreenRefreshed -= ScreenRefreshed;

                    break;

                case CaptureVideoType.WGC:
                    WGCHelper.StopCapture();
                    WGCHelper.ScreenRefreshed -= ScreenRefreshed;

                    break;
            }
            switch (Settings.AudioType)
            {
                case CaptureAudioType.WasapiLoopback:
                    WasapiCapture.Stop();
                    WasapiCapture.DataAvailable -= AudioRefreshed;

                    break;

                case CaptureAudioType.WinCaptureAudio:
                    SessionMixer.Stop();
                    SessionMixer.DataAvailable -= AudioRefreshed;

                    break;
            }
            _flow.Stop();

            _deltaResSw.Stop();
            _deltaResSw.Reset();

            _delayPerFrameSw.Stop();
            _delayPerFrameSw.Reset();

            Debug.WriteLine($"[Info] Recorded,\n{GetRecordedInfo()}");
            
            IsRecording = false;
        }

        private static void ScreenRefreshed(object sender, byte[] buffer)
        {
            byte[] compressed = buffer.Compress(); //byte[] -> compressed byte[]
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            MyVideoStream.ScreenQueue.Enqueue((compressed, timestamp));
        }

        private static void AudioRefreshed(object sender, byte[] buffer)
        {
            const bool DISCARD_IF_EMPTY = true;

            if (!DISCARD_IF_EMPTY || buffer.Any(x => x != 0))
            {
                byte[] compressed = buffer.Compress(); //byte[] -> compressed byte[]
                long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                MyVideoStream.AudioQueue.Enqueue((compressed, timestamp));
            }
        }

        public static BitrateInfo GetBitrateInfoBySize(int height, int fps)
        {
            string GetKey(int height2, int fps2) => string.Join("@", height2, fps2);
            BitrateInfo info = null;

            if (fps != 30 || fps != 60) fps = 60; //fixed size
            if (BitrateInfos.TryGetValue(GetKey(height, fps), out info)) return info;
            
            //Custom Resolution (Not Permanent)
            if (height > 1440) info = BitrateInfos[GetKey(1440, fps)];
            else if (height > 1080 && height < 1440) info = BitrateInfos[GetKey(1080, fps)];
            else info = BitrateInfos[GetKey(720, fps)];

            return info;
        }

        //For Debugging Methods for Rescreen!!!
        //For Debugging Methods for Rescreen!!!
        //For Debugging Methods for Rescreen!!!

        public static double GetFps(int frameCount, double elapsedSeconds)
        {
            return frameCount / elapsedSeconds;
        }

        public static double GetAverage(List<int> list)
        {
            var cloned = list.ToList();
            return cloned.Count > 0 ? cloned.Average() : 0;
        }

        public static double GetAverageMbps(double lengthPerSecond)
        {
            const double MB_PER_BYTE = 9.537 * 0.0000001;
            double averageMb = lengthPerSecond * MB_PER_BYTE;

            return averageMb * 8;
        }

        public static double GetAverageMbps(ConcurrentQueue<(byte[], long)> screenQueue, double fps)
        {
            var lengthList = new List<int>();
            var screenList = screenQueue.ToList();
            int length = 0;
            int position = 0;

            foreach (var screen in screenList)
            {
                if (position++ > fps)
                {
                    position = 0;
                    lengthList.Add(length);
                    length = 0;
                }
                length += screen.Item1.Length;
            }

            if (lengthList.Count == 0) return 0.0;
            return GetAverageMbps(lengthList.Average());
        }

        public static string GetRecordedInfo()
        {
            double fps = GetFps(MyVideoStream.ScreenQueue.Count, Elapsed.TotalSeconds);
            double rpf = GetAverage(_deltaRess);
            double dpf = GetAverage(_delayPerFrame);
            string info =
                "Resolution Per Frame : " + rpf.ToString("0.##") + "ms\n" +
                "Delay Per Frame : " + dpf.ToString("0.##") + "ms\n" +
                "Fps : " + fps.ToString("0.##") + "\n" +
                "Fps (DPF) : " + (1000 / dpf).ToString("0.##") + "\n" +
                "Mbps : " + GetAverageMbps(MyVideoStream.ScreenQueue, fps).ToString("0.##");

            return info;
        }
    }
}
