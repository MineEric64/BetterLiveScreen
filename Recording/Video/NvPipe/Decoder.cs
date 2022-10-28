using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Recording.Video.NvPipe
{
    public class Decoder
    {
        public int width = 1920;
        public int height = 1080;
        public Codec codec = Codec.H264;
        public Format format = Format.RGBA32;

        public int id { get; private set; } = -1;

        public bool isValid
        {
            get { return Lib.IsDecoderValid(id); }
        }

        public IntPtr decodedData
        {
            get { return Lib.GetDecoderDecodedData(id); }
        }

        public int decodedSize
        {
            get { return Lib.GetDecoderDecodedSize(id); }
        }

        public event EventHandler<(IntPtr, int)> onDecoded;

        bool isDecoding_ = false;
        bool isDecoded_ = false;

        public Decoder(int width, int height, Codec codec, Format format)
        {
            this.width = width;
            this.height = height;
            this.codec = codec;
            this.format = format;

            id = Lib.CreateDecoder();

            Lib.SetDecoderWidth(id, width);
            Lib.SetDecoderHeight(id, height);
            Lib.SetDecoderCodec(id, codec);
            Lib.SetDecoderFormat(id, format);

            if (!Lib.InitializeDecoder(id))
            {
                Debug.WriteLine(Lib.DecoderGetError(id));
                return;
            }
        }

        public void Close()
        {
            if (isValid)
            {
                Lib.DeleteDecoder(id);
            }
        }

        void _Decode(IntPtr data, int size)
        {
            if (!isValid || isDecoding_) return;

            isDecoding_ = true;
            isDecoded_ = Lib.DecoderDecode(id, data, size);
            isDecoding_ = false;

            if (!isDecoded_)
            {
                Debug.WriteLine(Lib.DecoderGetError(id));
            }
        }

        public void Decode(IntPtr data, int size)
        {
            _Decode(data, size);

            if (isDecoded_)
            {
                InvokeCallback();
            }
        }

        void InvokeCallback()
        {
            if (onDecoded == null) return;

            var ptr = Lib.GetDecoderDecodedData(id);
            var size = Lib.GetDecoderDecodedSize(id);
            onDecoded?.Invoke(null, (ptr, size));
        }
    }
}
