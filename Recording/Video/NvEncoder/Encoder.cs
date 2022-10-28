using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpDX.Direct3D11;

namespace BetterLiveScreen.Recording.Video.NvEncoder
{
    [Serializable]
    public class Encoder
    {
        
        public event EventHandler<(IntPtr, int)> onEncoded;
        public bool outputError = true;

        public int id { get; private set; } = -1;

        public bool isValid
        {
            get { return Lib.IsValid(id); }
        }

        public int width
        {
            get { return Lib.GetWidth(id); }
        }

        public int height
        {
            get { return Lib.GetHeight(id); }
        }

        public Format format
        {
            get { return Lib.GetFormat(id); }
        }

        public int frameRate
        {
            get { return Lib.GetFrameRate(id); }
        }

        public string error
        {
            get
            {
                if (!Lib.HasError(id)) return "";

                var str = Lib.GetError(id);
                Lib.ClearError(id);
                return str;
            }
        }

        public void Create(EncoderDesc desc, Device device)
        {
            id = Lib.Create(desc, device);

            if (!isValid)
            {
                Debug.WriteLine(error);
            }
        }

        public void Destroy()
        {
            Lib.Destroy(id);
        }

        public void Reconfigure(EncoderDesc desc, Device device)
        {
            // Lib.Reconfigure(id, desc);
            Destroy();
            Create(desc, device);
        }

        public void Update()
        {
            if (!isValid) return;

            Lib.CopyEncodedData(id);

            int n = Lib.GetEncodedDataCount(id);
            for (int i = 0; i < n; ++i)
            {
                var size = Lib.GetEncodedDataSize(id, i);
                var data = Lib.GetEncodedDataBuffer(id, i);
                onEncoded?.Invoke(null, (data, size));
            }
        }

        public bool Encode(Texture2D texture, bool forceIdrFrame)
        {
            if (texture == null)
            {
                Debug.WriteLine("The given texture is invalid.");
                return false;
            }

            var ptr = texture.NativePointer;
            if (!Encode(ptr, forceIdrFrame))
            {
                var msg = error;
                if (outputError && !string.IsNullOrEmpty(msg))
                {
                    Debug.WriteLine(msg);
                }
                return false;
            }

            return true;
        }

        public bool Encode(IntPtr ptr, bool forceIdrFrame)
        {
            if (ptr == IntPtr.Zero)
            {
                Debug.WriteLine("The given texture pointer is invalid.");
                return false;
            }

            if (!isValid)
            {
                Debug.WriteLine("uNvEncoder has not been initialized yet.");
                return false;
            }

            var result = Lib.Encode(id, ptr, forceIdrFrame);
            if (outputError && !result)
            {
                Debug.WriteLine(error);
            }

            return result;
        }

        public bool EncodeSharedHandle(IntPtr sharedHandle, bool forceIdrFrame)
        {
            if (sharedHandle == IntPtr.Zero)
            {
                Debug.WriteLine("The given handle is invalid.");
                return false;
            }

            if (!isValid)
            {
                Debug.WriteLine("uNvEncoder has not been initialized yet.");
                return false;
            }

            var result = Lib.EncodeSharedHandle(id, sharedHandle, forceIdrFrame);
            if (!result)
            {
                Debug.WriteLine(error);
            }

            return result;
        }
    }
}
