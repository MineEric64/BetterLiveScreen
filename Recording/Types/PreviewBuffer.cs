using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace BetterLiveScreen.Recording.Types
{
    public class PreviewBuffer
    {
        public byte[] Buffer { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public PixelFormat Format { get; set; }
        public int Stride { get; set; }

        public PreviewBuffer(byte[] buffer, int width, int height, PixelFormat format, int stride)
        {
            Buffer = buffer;
            Width = width;
            Height = height;
            Format = format;
            Stride = stride;
        }
    }
}
