﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using SharpDX.DXGI;

using BetterLiveScreen.Recording.Types;

namespace BetterLiveScreen.Recording.Video.WGC
{
    public class MonitorInfo
    {
        public bool IsPrimary { get; set; }
        public Vector2 ScreenSizeVec2 { get; set; }
        public Size ScreenSize => new Size((int)ScreenSizeVec2.X, (int)ScreenSizeVec2.Y);
        public Rectangle MonitorArea { get; set; }
        public Rectangle WorkArea { get; set; }
        public string DeviceName { get; set; }
        public IntPtr Hmon { get; set; }

        public GPUSelect GPU { get; set; }
    }

    public static class MonitorEnumerationHelper
    {
        delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private const int CCHDEVICENAME = 32;
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct MonitorInfoEx
        {
            public int Size;
            public RECT Monitor;
            public RECT WorkArea;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string DeviceName;
        }

        [DllImport("user32.dll")]
        static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

        public static IEnumerable<MonitorInfo> GetMonitors()
        {
            var result = new List<MonitorInfo>();
            var _factory = new Factory1();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
                {
                    MonitorInfoEx mi = new MonitorInfoEx();
                    mi.Size = Marshal.SizeOf(mi);
                    bool success = GetMonitorInfo(hMonitor, ref mi);
                    if (success)
                    {
                        var info = new MonitorInfo
                        {
                            ScreenSizeVec2 = new Vector2(mi.Monitor.right - mi.Monitor.left, mi.Monitor.bottom - mi.Monitor.top),
                            MonitorArea = new Rectangle(mi.Monitor.left, mi.Monitor.top, mi.Monitor.right - mi.Monitor.left, mi.Monitor.bottom - mi.Monitor.top),
                            WorkArea = new Rectangle(mi.WorkArea.left, mi.WorkArea.top, mi.WorkArea.right - mi.WorkArea.left, mi.WorkArea.bottom - mi.WorkArea.top),
                            IsPrimary = mi.Flags > 0,
                            Hmon = hMonitor,
                            DeviceName = mi.DeviceName,
                            GPU = GetGraphics(mi.DeviceName, _factory)
                        };
                        result.Add(info);
                    }
                    return true;
                }, IntPtr.Zero);
            return result;
        }

        private static GPUSelect GetGraphics(string monitorDeviceName, Factory1 factory)
        {
            string name = FScreen.GetAdapterName(monitorDeviceName, factory).ToUpper();

            if (name.Contains("INTEL")) return GPUSelect.Intel;
            if (name.Contains("AMD")) return GPUSelect.AMD;
            if (name.Contains("NVIDIA")) return GPUSelect.Nvidia;

            return GPUSelect.None; //???
        }
    }
}
