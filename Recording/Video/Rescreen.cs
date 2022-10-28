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

using BetterLiveScreen.Recording;
using BetterLiveScreen.Recording.Audio;
using BetterLiveScreen.Recording.Types;
using BetterLiveScreen.Recording.Video.WGC;
using BetterLiveScreen.Extensions;

using Size = System.Drawing.Size;
using WasapiCapture = BetterLiveScreen.Recording.Audio.WasapiCapture;

namespace BetterLiveScreen.Recording.Video
{
    public class Rescreen
    {
        private static FScreen _raw = new FScreen();
        internal static List<int> _deltaRess = new List<int>();
        internal static List<int> _delayPerFrame = new List<int>();

        private static Stopwatch _flow = new Stopwatch(); //actual recording time
        internal static Stopwatch _deltaResSw = new Stopwatch(); //Resolution Per Frame Time
        internal static Stopwatch _delayPerFrameSw = new Stopwatch(); //Delay Per Frame Time

        public static Dictionary<string, VideoLike> VideoStreams { get; set; } = new Dictionary<string, VideoLike>();

        public static int Fps { get; set; } = 30;
        public static Size ScreenSize { get; set; } = new Size(2560, 1440);
        public static Size ScreenActualSize => IsHalf ? ScreenSize.DivideBy(2) : ScreenSize;
        public static int DelayPerFrame => 1000 / Fps;

        public static bool IsHalf { get; set; } = false;
        public static bool NvencEncoding { get; set; } = true;
        public static int Bitrate { get; set; } = GetBitrateFromMbps(2);

        public static bool IsRecording { get; private set; } = false;
        public static TimeSpan Elapsed => _flow.Elapsed;

        public static CaptureVideoType VideoType { get; set; } = CaptureVideoType.DD;
        public static CaptureAudioType AudioType { get; set; } = CaptureAudioType.Wasapi;

        public static void Start()
        {
            if (!VideoStreams.ContainsKey(MainWindow.User.ToString()))
            {
                VideoStreams.Add(MainWindow.User.ToString(), new VideoLike());
            }
            _flow.Reset();

            switch (VideoType)
            {
                case CaptureVideoType.DD:
                    _raw.ScreenRefreshed += ScreenRefreshed;
                    _raw.Start();

                    break;

                case CaptureVideoType.WGC:
                    WGCHelper.ScreenRefreshed += ScreenRefreshed;
                    WGCHelper.StartPrimaryMonitorCapture();

                    break;
            }
            switch (AudioType)
            {
                case CaptureAudioType.Wasapi:
                    WasapiCapture.Record();
                    break;
            }

            _flow.Start();
            IsRecording = true;
        }

        public static void Stop()
        {
            switch (VideoType)
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
            switch (AudioType)
            {
                case CaptureAudioType.Wasapi:
                    WasapiCapture.Stop();
                    break;
            }
            _flow.Stop();

            _deltaResSw.Stop();
            _deltaResSw.Reset();

            _delayPerFrameSw.Stop();
            _delayPerFrameSw.Reset();

            string info =
                "Resolution Per Frame : " + GetAverageAsString(ref _deltaRess) + "ms\n" +
                "Delay Per Frame : " + GetAverageAsString(ref _delayPerFrame) + "ms\n" +
                "Fps : " + GetFps(VideoStreams[MainWindow.User.ToString()].ScreenQueue.Count, _flow.Elapsed.TotalSeconds).ToString("0.##");
            MessageBox.Show(info, "BetterLiveScreen", MessageBoxButton.OK, MessageBoxImage.Information);
            
            IsRecording = false;
        }

        private static void ScreenRefreshed(object sender, byte[] e)
        {
            var compressed = e.Compress(); //byte[] -> compressed byte[]
            VideoStreams[MainWindow.User.ToString()].ScreenQueue.Enqueue(compressed);
        }

        public static double GetFps(int frameCount, double elapsedSeconds)
        {
            return frameCount / elapsedSeconds;
        }

        public static string GetAverageAsString(ref List<int> list)
        {
            return list.Count > 0 ? list.Average().ToString("0.##") : "0";
        }

        public static int GetBitrateFromMbps(int mbps)
        {
            return mbps * 1000000;
        }
    }
}
