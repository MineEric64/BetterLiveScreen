using BetterLiveScreen.Recording.Video.WGC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BetterLiveScreen.Extensions
{
    public class ProcessHelper
    {
        public static Process[] GetValidProcesses()
        {
            string[] notProcess = new string[] { "계산기", "NVIDIA GeForce Overlay", "Microsoft Text Input Application", "설정" };
            var processesWithWindows = from p in Process.GetProcesses()
                                       where !string.IsNullOrWhiteSpace(p.MainWindowTitle) && WindowEnumerationHelper.IsWindowValidForCapture(p.MainWindowHandle)
                                       select p;

            var validProcesses = processesWithWindows.Where(process => !notProcess.Contains(process.MainWindowTitle));
            var validProcessesWithoutUwp = new List<Process>();

            foreach(var process in validProcesses)
            {
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

        public static string GetProcessInfo(Process p)
        {
            string info =
                $"Name : {p.ProcessName}\n" +
                $"Main Window Title : {p.MainWindowTitle}\n" +
                $"Id : {p.Id}";
            return info;
        }

        public static void DebugInfo()
        {
            foreach (var p in GetValidProcesses())
            {
                MessageBox.Show(GetProcessInfo(p), "BetterLiveScreen", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public static bool IsUwpWindow(Process window)
        {
            string[] uwpNames = new string[] { "ApplicationFrameWindow", "ApplicationFrameHost" };
            return uwpNames.Contains(window.ProcessName);
        }

        public static Process GetUwpActualWindow(Process parent)
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
