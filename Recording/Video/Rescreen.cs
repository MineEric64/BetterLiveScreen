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
using BetterLiveScreen.Extensions;

using Size = System.Drawing.Size;
using WasapiCapture = BetterLiveScreen.Recording.Audio.WasapiCapture;

namespace BetterLiveScreen.Recording.Video
{
    public class Rescreen
    {
        private static FScreen _raw = new FScreen();
        private static Stopwatch _flow = new Stopwatch();
        public static EventHandler<Bitmap> Refreshed { get; set; }
        
        public static Dictionary<string, VideoLike> VideoStreams { get; set; } = new Dictionary<string, VideoLike>();

        public static int Fps { get; set; } = 30;

        public static Size ScreenSize { get; set; } = new Size(1280, 720);

        public static bool IsRecording { get; private set; } = false;

        public static void Start(bool isHalf = false)
        {
            if (!VideoStreams.ContainsKey(MainWindow.User.ToString()))
            {
                VideoStreams.Add(MainWindow.User.ToString(), new VideoLike());
            }

            int dpf = 1000 / Fps; //delay per frame

            _raw.ScreenRefreshed += (object sender, byte[] e) =>
            {
                var compressed = e.Compress(); //byte[] -> compressed byte[]
                VideoStreams[MainWindow.User.ToString()].ScreenQueue.Enqueue(compressed);
            };
            _raw.Start(isHalf);
            WasapiCapture.Record();
            _flow.Start();

            IsRecording = true;
        }

        public static void Stop()
        {
            _raw.Stop();
            WasapiCapture.Stop();
            _flow.Stop();

            MessageBox.Show(_raw.deltaRess.Average().ToString("0.##"));
            MessageBox.Show((VideoStreams[MainWindow.User.ToString()].ScreenQueue.Count / _flow.Elapsed.TotalSeconds).ToString("0.##"));

            _flow.Reset();
            IsRecording = false;
        }
    }
}
