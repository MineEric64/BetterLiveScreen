using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Recording.Video.NvColorSpace
{
    public class NvColorSpace
    {
        public const string DLL_NAME = "NvColorSpace.dll";

        // ---

        [DllImport(DLL_NAME, EntryPoint = "RGBA32ToBGRA32")]
        public static extern int RGBA32ToBGRA32(IntPtr rgba, IntPtr bgra, int width, int height);

        [DllImport(DLL_NAME, EntryPoint = "RGBA32ToBGR24")]
        public static extern int RGBA32ToBGR24(IntPtr rgba, IntPtr bgr, int width, int height);
    }
}
