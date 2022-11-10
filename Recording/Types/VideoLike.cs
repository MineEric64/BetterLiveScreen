using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;

using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Recording.Video;

namespace BetterLiveScreen.Recording.Types
{
    public class VideoLike
    {
        public Queue<byte[]> ScreenQueue { get; set; }
        public Queue<byte[]> AudioQueue { get; set; }

        public BitmapInfo Info { get; set; }
        public WaveFormat AudioFormat { get; set; }

        public VideoLike(BitmapInfo info)
        {
            ScreenQueue = new Queue<byte[]>();
            AudioQueue = new Queue<byte[]>();

            Info = info;
        }

        public void ChangeFormat(WaveFormat format)
        {
            if (format.SampleRate != AudioFormat?.SampleRate || format.Channels != AudioFormat?.SampleRate) AudioFormat = format;
        }

        public static BitmapInfo FromRescreenSettings()
        {
            return new BitmapInfo(Rescreen.ScreenActualSize.Width, Rescreen.ScreenActualSize.Height, Rescreen.Settings.Encoding.ToString());
        }
    }
}
