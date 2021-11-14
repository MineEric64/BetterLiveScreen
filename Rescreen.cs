using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using MessagePack;

using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace BetterLiveScreen
{
    public class Rescreen
    {
        private static FScreen _raw = new FScreen();
        public static EventHandler<Bitmap> Refreshed { get; set; }
        public static ConcurrentQueue<Bitmap> ImageQueue { get; set; } = new ConcurrentQueue<Bitmap>();

        public static void Start()
        {
            _raw.ScreenRefreshed += (object sender, Bitmap e) =>
            {
                Refreshed?.Invoke(null, e);
            };
            _raw.Start();
        }

        public static void Stop()
        {
            _raw.Stop();
        }
    }
}
