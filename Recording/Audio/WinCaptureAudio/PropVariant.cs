using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.Storage.Search;

namespace BetterLiveScreen.Recording.Audio.WinCaptureAudio
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct PROPVARIANT
    {
        [FieldOffset(0)]
        public tag_inner_PROPVARIANT inner;
        [FieldOffset(0)]
        public decimal decVal;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct tag_inner_PROPVARIANT
    {
        [FieldOffset(0)]
        public ushort vt;
        [FieldOffset(2)]
        public ushort wReserved1;
        [FieldOffset(4)]
        public ushort wReserved2;
        [FieldOffset(6)]
        public ushort wReserved3;
        [FieldOffset(8)]
        public BLOB blob;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BLOB
    {
        public ulong cbSize;
        public IntPtr pBlobData;
    }
}
