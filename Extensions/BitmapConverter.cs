using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
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
        public static BitmapSource BLACK_IMAGE { get; } = CreateBlackImage();

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
                System.Windows.Media.PixelFormats.Bgr24, null,
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

        public static Bitmap ToBitmap(this byte[] data, int width, int height, System.Drawing.Imaging.PixelFormat pixelFormat)
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

        private static BitmapSource CreateBlackImage()
        {
            int width = 1280;
            int height = 720;
            int stride = width / 8;
            byte[] pixels = new byte[height * stride];

            List<System.Windows.Media.Color> colors = new List<System.Windows.Media.Color>();
            colors.Add(System.Windows.Media.Color.FromRgb(0, 0, 0));
            BitmapPalette myPalette = new BitmapPalette(colors);

            BitmapSource image = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Indexed1,
                myPalette,
                pixels,
                stride);

            return image;
        }

        public static ImageBrush CreateImageBrush(ImageSource source)
        {
            ImageBrush brush = new ImageBrush();
            brush.ImageSource = source;

            return brush;
        }

        public static OpenCvSharp.Size ToCvSize(this Size size)
        {
            return new OpenCvSharp.Size(size.Width, size.Height);
        }

        public static Size DivideBy(this Size size, int divideBy)
        {
            return new Size(size.Width / divideBy, size.Height / divideBy);
        }

        /// <summary>
        /// Converts the input BitmapSource to the Pbgra32 format WriteableBitmap which is internally used by the WriteableBitmapEx.
        /// </summary>
        /// <param name="source">The source bitmap.</param>
        /// <returns></returns>
        public static WriteableBitmap ConvertToPbgra32Format(BitmapSource source)
        {
            // Convert to Pbgra32 if it's a different format
            if (source.Format == PixelFormats.Pbgra32)
            {
                return new WriteableBitmap(source);
            }

            var formatedBitmapSource = new FormatConvertedBitmap();
            formatedBitmapSource.BeginInit();
            formatedBitmapSource.Source = source;
            formatedBitmapSource.DestinationFormat = PixelFormats.Pbgra32;
            formatedBitmapSource.EndInit();
            return new WriteableBitmap(formatedBitmapSource);
        }
    }
}
