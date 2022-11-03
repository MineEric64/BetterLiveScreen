using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Extensions
{
    public class Ole32
    {
        public enum COINIT : uint //tagCOINIT
        {
            COINIT_MULTITHREADED = 0x0, //Initializes the thread for multi-threaded object concurrency.
            COINIT_APARTMENTTHREADED = 0x2, //Initializes the thread for apartment-threaded object concurrency
            COINIT_DISABLE_OLE1DDE = 0x4, //Disables DDE for OLE1 support
            COINIT_SPEED_OVER_MEMORY = 0x8, //Trade memory for speed
        }

        [Flags]
        public enum CLSCTX : uint
        {
            CLSCTX_INPROC_SERVER = 0x1,
            CLSCTX_INPROC_HANDLER = 0x2,
            CLSCTX_LOCAL_SERVER = 0x4,
            CLSCTX_INPROC_SERVER16 = 0x8,
            CLSCTX_REMOTE_SERVER = 0x10,
            CLSCTX_INPROC_HANDLER16 = 0x20,
            CLSCTX_RESERVED1 = 0x40,
            CLSCTX_RESERVED2 = 0x80,
            CLSCTX_RESERVED3 = 0x100,
            CLSCTX_RESERVED4 = 0x200,
            CLSCTX_NO_CODE_DOWNLOAD = 0x400,
            CLSCTX_RESERVED5 = 0x800,
            CLSCTX_NO_CUSTOM_MARSHAL = 0x1000,
            CLSCTX_ENABLE_CODE_DOWNLOAD = 0x2000,
            CLSCTX_NO_FAILURE_LOG = 0x4000,
            CLSCTX_DISABLE_AAA = 0x8000,
            CLSCTX_ENABLE_AAA = 0x10000,
            CLSCTX_FROM_DEFAULT_CONTEXT = 0x20000,
            CLSCTX_ACTIVATE_32_BIT_SERVER = 0x40000,
            CLSCTX_ACTIVATE_64_BIT_SERVER = 0x80000,
            CLSCTX_INPROC = CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER,
            CLSCTX_SERVER = CLSCTX_INPROC_SERVER | CLSCTX_LOCAL_SERVER | CLSCTX_REMOTE_SERVER,
            CLSCTX_ALL = CLSCTX_SERVER | CLSCTX_INPROC_HANDLER
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MULTI_QI
        {
            [MarshalAs(UnmanagedType.LPStruct)] public Guid pIID;
            [MarshalAs(UnmanagedType.Interface)] public object pItf;
            public int hr;
        }

        /// <returns>If function succeeds, it returns 0(S_OK). Otherwise, it returns an error code.</returns>
        [DllImport("ole32.dll", CharSet = CharSet.Auto, SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        public static extern int CoInitializeEx(
            [In, Optional] IntPtr pvReserved,
            [In] COINIT dwCoInit //DWORD
            );

        [DllImport("ole32.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CoUninitialize();

        [DllImport("ole32.dll")]
        internal static extern int CoCreateInstance(
            [MarshalAs(UnmanagedType.LPStruct)] ref Guid rclsid,
            IntPtr pUnkOuter,
            CLSCTX dwClsCtx,
            [MarshalAs(UnmanagedType.LPStruct)] ref Guid riid,
            out object ppv);
    }
}
