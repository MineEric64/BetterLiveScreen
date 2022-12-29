using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Extensions
{
    public class ProcessSimpleInfo
    {
        public string ProcessName { get; set; }
        public int Id { get; set; }
        public IntPtr Handle { get; set; }
        public string MainWindowTitle { get; set; }
        public IntPtr MainWindowHandle { get; set; }

        public ProcessSimpleInfo(string processName, int id, IntPtr handle, string mainWindowTitle, IntPtr mainWindowHandle)
        {
            ProcessName = processName;
            Id = id;
            Handle = handle;
            MainWindowTitle = mainWindowTitle;
            MainWindowHandle = mainWindowHandle;
        }

        public static ProcessSimpleInfo FromProcess(Process p)
        {
            IntPtr handle = IntPtr.Zero;
            string[] exclude = { "taskmgr" };
            bool wtf = false;

            foreach (var ex in exclude)
            {
                if (p.ProcessName.ToLower() == ex)
                {
                    wtf = true;
                    break;
                }
            }

            if (!wtf)
            {
                try
                {
                    handle = p.Handle;
                }
                catch
                {
                    handle = IntPtr.Zero;
                }
            }

            return new ProcessSimpleInfo(p.ProcessName, p.Id, handle, p.MainWindowTitle, p.MainWindowHandle);
        }

        public override string ToString()
        {
            return $"Process Name : {ProcessName}, Id : {Id}, Main Window Title : {MainWindowTitle}";
        }
    }
}
