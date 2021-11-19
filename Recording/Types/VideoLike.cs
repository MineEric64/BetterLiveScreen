using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Recording.Types
{
    public class VideoLike
    {
        public Queue<byte[]> ScreenQueue { get; set; }
        public Queue<byte[]> AudioQueue { get; set; }

        public VideoLike()
        {
            ScreenQueue = new Queue<byte[]>();
            AudioQueue = new Queue<byte[]>();
        }
    }
}
