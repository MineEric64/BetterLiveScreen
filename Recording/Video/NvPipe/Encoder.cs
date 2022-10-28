using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using SharpDX.Direct3D11;

namespace BetterLiveScreen.Recording.Video.NvPipe
{
    public class Encoder
    {
        public int width = 1920;
        public int height = 1080;
        public float bitrateMbps = 32f;
        public int fps = 60;
        public Codec codec = Codec.H264;
        public Format format = Format.RGBA32;
        public Compression compression = Compression.LOSSY;

        public int id { get; private set; } = -1;

        public bool isValid
        {
            get { return Lib.IsEncoderValid(id); }
        }

        public event EventHandler<(IntPtr, int)> onEncoded;

        bool isEncoding_ = false;
        bool isEncoded_ = false;

        void OnEnable()
        {
            id = Lib.CreateEncoder();

            Lib.SetEncoderWidth(id, width);
            Lib.SetEncoderHeight(id, height);
            Lib.SetEncoderBitrate(id, bitrateMbps);
            Lib.SetEncoderFps(id, fps);
            Lib.SetEncoderCodec(id, codec);
            Lib.SetEncoderFormat(id, format);
            Lib.SetEncoderCompression(id, compression);

            if (!Lib.InitializeEncoder(id))
            {
                Debug.WriteLine(Lib.EncoderGetError(id));
                return;
            }
        }

        void OnDisable()
        {
            Lib.DeleteEncoder(id);
        }

        void _Encode(IntPtr data, bool forceIframe)
        {
            if (!isValid || isEncoding_) return;

            isEncoding_ = true;
            isEncoded_ = Lib.EncoderEncode(id, data, forceIframe);
            isEncoding_ = false;

            if (!isEncoded_)
            {
                Debug.WriteLine(Lib.EncoderGetError(id));
            }
        }

        public void Encode(System.IntPtr data, bool forceIframe = false)
        {
            _Encode(data, forceIframe);

            if (isEncoded_)
            {
                InvokeCallback();
            }
        }

        //public void Encode(Texture2D texture, bool forceIframe = false)
        //{
        //    var pixels = texture.GetPixels32();
        //    var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        //    var pointer = handle.AddrOfPinnedObject();

        //    Encode(pointer, forceIframe);

        //    handle.Free();
        //}

        void InvokeCallback()
        {
            if (onEncoded == null) return;

            var ptr = Lib.GetEncoderEncodedData(id);
            var size = Lib.GetEncoderEncodedSize(id);
            onEncoded?.Invoke(null, (ptr, size));
        }
    }
}
