using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Extensions
{
    public class Kernel32
    {
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        [DllImport("kernel32.dll")]
        public static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll")]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        public static extern uint WaitForMultipleObjects(uint nCount, IntPtr[] lpHandles, [MarshalAs(UnmanagedType.Bool)] bool bWaitAll, uint dwMilliseconds);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetEvent(IntPtr hEvent);

        [DllImport("kernel32.dll")]
        public static extern int GetProcessId(IntPtr handle);
    }
}
