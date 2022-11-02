using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Recording.Audio.WinCaptureAudio
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS
    {
        public uint TargetProcessId;
        public PROCESS_LOOPBACK_MODE ProcessLoopbackMode;
    }
}
