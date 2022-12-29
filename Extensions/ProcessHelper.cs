using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using BetterLiveScreen.Recording.Audio;
using BetterLiveScreen.Recording.Video.WGC;

namespace BetterLiveScreen.Extensions
{
    public class ProcessHelper
    {
        private static List<ProcessSimpleInfo> _processes = new List<ProcessSimpleInfo>(); //for V2

        public static ProcessSimpleInfo[] GetValidProcesses()
        {
            string[] notProcess = new string[] { "계산기", "NVIDIA GeForce Overlay", "Microsoft Text Input Application", "설정" };
            var validProcesses = new List<ProcessSimpleInfo>();
            var processes = Process.GetProcesses();

            for (int i = 0; i < processes.Length; i++)
            {
                var p = processes[i];

                if (!string.IsNullOrWhiteSpace(p.MainWindowTitle))
                {
                    if (WindowEnumerationHelper.IsWindowValidForCapture(p.MainWindowHandle))
                    {
                        if (!notProcess.Contains(p.MainWindowTitle))
                        {
                            var info = ProcessSimpleInfo.FromProcess(p);
                            validProcesses.Add(info);
                        }
                    }
                }
            }
            var validProcessesWithoutUwp = new List<ProcessSimpleInfo>();

            foreach(var process in validProcesses)
            {
                if (process.Handle == IntPtr.Zero) continue;

                if (IsUwpWindow(process))
                {
                    var child = GetUwpActualWindow(process);
                    if (child != null) validProcessesWithoutUwp.Add(process);
                }
                else
                {
                    validProcessesWithoutUwp.Add(process);
                }
            }

            return validProcessesWithoutUwp.ToArray();
        }

        public static ProcessSimpleInfo[] GetValidProcessesV2()
        {
            _processes.Clear();

            User32.EnumWindowsProc callback = new User32.EnumWindowsProc(EnumWindowsProc2);
            User32.EnumWindows(callback, 0);

            return _processes.ToArray();
        }

        private static bool EnumWindowsProc2(IntPtr hWnd, int lParam)
        {
            const int GCL_HMODULE = -16;
            int hWndInt = hWnd.ToInt32();

            //윈도우 핸들로 그 윈도우의 스타일을 얻어옴
            uint style = (uint)User32.GetWindowLong(hWndInt, GCL_HMODULE);
            //해당 윈도우의 캡션이 존재하는지 확인
            if ((style & 0x10000000L) == 0x10000000L && (style & 0x00C00000L) == 0x00C00000L)
            {
                //부모가 바탕화면인지 확인
                if (User32.GetParent(hWndInt) == 0)
                {
                    StringBuilder Buf = new StringBuilder(256);
                    //응용프로그램의 이름을 얻어온다
                    if (User32.GetWindowText(hWndInt, Buf, 256) > 0)
                    {
                        string mainWindowTitle = Buf.ToString();
                        string processName;
                        Buf.Clear();

                        User32.GetWindowThreadProcessId(hWnd, out uint id);

                        IntPtr hProcess = Kernel32.OpenProcess((DLLInjector.ProcessAccessFlags)0x1000, false, (int)id);
                        int size = Buf.Capacity;
                        bool ad = Kernel32.QueryFullProcessImageName(hProcess, 0, Buf, out size);
                        processName = Buf.ToString();

                        if (ad)
                        {
                            processName = Path.GetFileNameWithoutExtension(processName);
                        }

                        _processes.Add(new ProcessSimpleInfo(processName, (int)id, hWnd, mainWindowTitle, hWnd));
                    }
                }
            }
            return true;
        }

        public static string GetProcessInfo(ProcessSimpleInfo p)
        {
            string info =
                $"Name : {p.ProcessName}\n" +
                $"Main Window Title : {p.MainWindowTitle}\n" +
                $"Id : {p.Id}";
            return info;
        }

        public static string GetProcessInfo(Process p)
        {
            var info = ProcessSimpleInfo.FromProcess(p);
            return GetProcessInfo(info);
        }

        public static void DebugInfo()
        {
            foreach (var p in GetValidProcesses())
            {
                MessageBox.Show(GetProcessInfo(p), "BetterLiveScreen", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public static void DebugAudioSessionInfo()
        {
            foreach (var p in AudioSessionManagerV2.GetAllPlayingSessions())
            {
                MessageBox.Show(GetProcessInfo(p), "BetterLiveScreen", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public static bool IsUwpWindow(ProcessSimpleInfo window)
        {
            string[] uwpNames = new string[] { "ApplicationFrameWindow", "ApplicationFrameHost" };
            return uwpNames.Contains(window.ProcessName);
        }

        public static Process GetUwpActualWindow(ProcessSimpleInfo parent)
        {
            IntPtr child = User32.FindWindowEx(parent.Handle, IntPtr.Zero, null, null);

            while (child != IntPtr.Zero)
            {
                User32.GetWindowThreadProcessId(child, out var childId);

                if (parent.Id != childId) return Process.GetProcessById(Kernel32.GetProcessId(child));
                child = User32.FindWindowEx(parent.Handle, child, null, null);
            }

            return null;
        }
    }
}
