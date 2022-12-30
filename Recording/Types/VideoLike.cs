using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;

using BetterLiveScreen.Interfaces;
using BetterLiveScreen.Recording.Video;
using System.Collections.Concurrent;

namespace BetterLiveScreen.Recording.Types
{
    public class VideoLike
    {
        //(buffer, timestamp)
        public ConcurrentQueue<(byte[], long)> ScreenQueue { get; set; }
        public ConcurrentQueue<(byte[], long)> AudioQueue { get; set; }

        public BitmapInfo Info { get; set; }
        public WaveFormat AudioFormat { get; set; }

        public VideoLike(BitmapInfo info)
        {
            ScreenQueue = new ConcurrentQueue<(byte[], long)>();
            AudioQueue = new ConcurrentQueue<(byte[], long)>();

            Info = info;
        }

        public void ChangeFormat(WaveFormat format)
        {
            if (format.SampleRate != AudioFormat?.SampleRate || format.Channels != AudioFormat?.Channels) AudioFormat = format;
        }

        public static BitmapInfo FromRescreenSettings()
        {
            return new BitmapInfo(Rescreen.ScreenActualSize.Width, Rescreen.ScreenActualSize.Height, Rescreen.Settings.Encoding.ToString());
        }
    }
}
