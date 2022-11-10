using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Recording.Video;

namespace BetterLiveScreen.Recording.Types
{
    public class VideoLike
    {
        public Queue<byte[]> ScreenQueue { get; set; }
        public Queue<byte[]> AudioQueue { get; set; }

        public BitmapInfo Info { get; set; }

        public VideoLike(BitmapInfo info)
        {
            ScreenQueue = new Queue<byte[]>();
            AudioQueue = new Queue<byte[]>();

            Info = info;
        }

        public static BitmapInfo FromRescreenSettings()
        {
            return new BitmapInfo(Rescreen.ScreenActualSize.Width, Rescreen.ScreenActualSize.Height, Rescreen.Settings.NvencEncoding);
        }
    }
}
