using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Recording.Types
{
    public class FindProcess
    {
        public IntPtr Handle;
        public int Pid;
        public string ProcessName;

        public FindProcess(IntPtr handle, int pid, string processName)
        {
            Handle = handle;
            Pid = pid;
            ProcessName = processName;
        }

        public override string ToString() => $"{ProcessName}";
    }
}
