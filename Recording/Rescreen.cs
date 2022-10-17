using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Diagnostics;

using BetterLiveScreen.Recording.Types;
using BetterLiveScreen.Extensions;

using Size = System.Drawing.Size;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using OpenCvSharp;

namespace BetterLiveScreen.Recording
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

        public static void Start()
        {
            if (!VideoStreams.ContainsKey(MainWindow.User.ToString()))
            {
                VideoStreams.Add(MainWindow.User.ToString(), new VideoLike());
            }

            int dpf = 1000 / Fps; //delay per frame

            _raw.ScreenRefreshed += (object sender, IntPtr e) =>
            {
                var buffer = new byte[1280 * 720 * 4];

                Marshal.Copy(e, buffer, 0, buffer.Length);
                var compressed = buffer.Compress(); //byte[] -> compressed byte[]

                VideoStreams[MainWindow.User.ToString()].ScreenQueue.Enqueue(compressed);
            };
            _raw.Start();
            _flow.Start();

            IsRecording = true;
        }

        public static void Stop()
        {
            _raw.Stop();
            _flow.Stop();

            MessageBox.Show((VideoStreams[MainWindow.User.ToString()].ScreenQueue.Count / _flow.Elapsed.TotalSeconds).ToString());

            _flow.Reset();
            IsRecording = false;
        }
    }
}
