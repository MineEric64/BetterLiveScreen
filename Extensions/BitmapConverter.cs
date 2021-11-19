using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

using MessagePack;

namespace BetterLiveScreen.Extensions
{
    public static class BitmapConverter
    {
        /// <summary>
        /// MessagePack을 위한 LZ4 압축 옵션
        /// </summary>
        public static MessagePackSerializerOptions LZ4_OPTIONS = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

        public static BitmapImage ToImage(this byte[] array)
        {
            using (var ms = new System.IO.MemoryStream(array))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad; // here
                image.StreamSource = ms;
                image.EndInit();
                return image;
            }
        }

        public static BitmapSource ToImage(this Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                System.Windows.Media.PixelFormats.Bgra32, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);

            return bitmapSource;
        }

        public static byte[] ToArray(this Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly,
                bitmap.PixelFormat);
            byte[] byteData = new byte[bitmapData.Stride * bitmap.Height];

            Marshal.Copy(bitmapData.Scan0, byteData, 0, byteData.Length);
            bitmap.UnlockBits(bitmapData);

            return byteData;
        }

        public static Bitmap ToBitmap(this byte[] data, int width, int height, PixelFormat pixelFormat)
        {
            Bitmap bitmap = new Bitmap(width, height, pixelFormat);
            BitmapData bmpData =
                bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, pixelFormat);

            Marshal.Copy(data, 0, bmpData.Scan0, data.Length);
            bitmap.UnlockBits(bmpData);

            return bitmap;
        }

        public static byte[] Compress(this byte[] buffer)
        {
            return MessagePackSerializer.Serialize(buffer, LZ4_OPTIONS);
        }

        public static byte[] Decompress(this byte[] compressed)
        {
            return MessagePackSerializer.Deserialize<byte[]>(compressed, LZ4_OPTIONS);
        }
    }
}
