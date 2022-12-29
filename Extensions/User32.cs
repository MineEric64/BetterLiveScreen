using Config.Net;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Extensions
{
    public class User32
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string lclassName, string windowTitle);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        /// <summary>
        /// 윈도우 열거하기
        /// </summary>
        /// <param name="enumWindowsProc">윈도우 열거하기 대리자</param>
        /// <param name="longParameter">LONG 매개 변수</param>
        /// <returns>처리 결과</returns>
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumWindowsProc, int longParameter);

        /// <summary>
        /// 윈도우 열거하기 대리자
        /// </summary>
        /// <param name="windowHandle">윈도우 핸들</param>
        /// <param name="longParameter">LONG 매개 변수</param>
        /// <returns>처리 결과</returns>
        public delegate bool EnumWindowsProc(IntPtr windowHandle, int longParameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;      // x position of lower-right corner
            public int Bottom;  // y position of lower-right corner
        }

        [DllImport("user32.dll")]
        public static extern int GetParent(int hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowText(int hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        public static extern long GetWindowLong(int hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr GetClassLong(IntPtr hwnd, int nIndex);
    }
}
