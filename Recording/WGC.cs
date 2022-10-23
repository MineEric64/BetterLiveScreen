using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using Windows.Graphics.Capture;
using Windows.Foundation.Metadata;
using Windows.Foundation;

using Composition.WindowsRuntimeHelpers_NETStd;

using BetterLiveScreen.Recording.Types;

namespace BetterLiveScreen.Recording
{
    public class WGC
    {
        private static List<FindProcess> _processes = new List<FindProcess>();
        private static List<FindProcess> _monitors = new List<FindProcess>();

        public static bool Refresh()
        {
            _processes.Clear();
            _monitors.Clear();

            if (ApiInformation.IsApiContractPresent(typeof(UniversalApiContract).FullName, 8))
            {
                //Monitors
                var monitors = MonitorEnumerationHelper.GetMonitors();
                _monitors.AddRange(monitors.OrderBy(p => p.DeviceName).Select(monitor => new FindProcess(monitor.Hmon, -1, monitor.DeviceName)));

                //Processes
                string[] notProcess = new string[] { "계산기", "NVIDIA GeForce Overlay", "Microsoft Text Input Application", "설정" };

                var processesWithWindows = from p in Process.GetProcesses()
                                           where !string.IsNullOrWhiteSpace(p.MainWindowTitle) && WindowEnumerationHelper.IsWindowValidForCapture(p.MainWindowHandle)
                                           select p;

                _processes.AddRange(processesWithWindows.Where(process => !notProcess.Contains(process.MainWindowTitle)).Select(process => new FindProcess(process.MainWindowHandle, process.Id, process.MainWindowTitle)));

                return true;
            }
            return false;
        }

        private void StartHwndCapture(IntPtr hwnd)
        {
            GraphicsCaptureItem item = CaptureHelper.CreateItemForWindow(hwnd);
            if (item != null)
            {
                //sample.StartCaptureFromItem(item);
            }
        }

        private void StartHmonCapture(IntPtr hmon)
        {
            GraphicsCaptureItem item = CaptureHelper.CreateItemForMonitor(hmon);
            if (item != null)
            {
                //sample.StartCaptureFromItem(item);
            }
        }

        private void StartPrimaryMonitorCapture()
        {
            MonitorInfo monitor = (from m in MonitorEnumerationHelper.GetMonitors()
                                   where m.IsPrimary
                                   select m).First();
            StartHmonCapture(monitor.Hmon);
        }

        private void StopCapture()
        {
            //sample.StopCapture();
        }
    }
}
