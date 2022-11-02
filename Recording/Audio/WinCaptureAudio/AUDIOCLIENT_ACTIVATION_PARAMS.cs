using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Recording.Audio.WinCaptureAudio
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AUDIOCLIENT_ACTIVATION_PARAMS
    {
        public AUDIOCLIENT_ACTIVATION_TYPE ActivationType;
        public AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS ProcessLoopbackParams;
    }
}
