using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MessagePack;

namespace BetterLiveScreen.Interface
{
    [MessagePackObject]
    public class BitmapInfo
    {
        [Key(0)]
        public int Width { get; set; }

        [Key(1)]
        public int Height { get; set; }

        [Key(2)]
        public PixelFormat PixelFormat { get; set; }

        [Key(3)]
        public int BufferLength { get; set; }

        public BitmapInfo(int width, int height, PixelFormat pixelFormat, int bufferLength)
        {
            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            BufferLength = bufferLength;
        }
    }
}
