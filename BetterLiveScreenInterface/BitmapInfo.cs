using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MessagePack;

namespace BetterLiveScreen.Interfaces
{
    [MessagePackObject]
    public class BitmapInfo
    {
        [Key(0)]
        public int Width { get; set; }

        [Key(1)]
        public int Height { get; set; }

        [Key(2)]
        public bool NvencEncoding { get; set; }

        [IgnoreMember]
        public static BitmapInfo Empty => new BitmapInfo(0, 0, false);

        public BitmapInfo(int width, int height, bool nvencEncoding)
        {
            Width = width;
            Height = height;
            NvencEncoding = nvencEncoding;
        }
    }
}
