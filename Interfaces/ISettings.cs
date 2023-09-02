using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Config;
using Config.Net;

namespace BetterLiveScreen.Interfaces
{
    public interface ISettings
    {
        [Option(Alias = "clear_buffer_enabled.video", DefaultValue = false)]
        bool ClearVideoBufferEnabled { get; }

        [Option(Alias = "clear_buffer_enabled.audio", DefaultValue = true)]
        bool ClearAudioBufferEnabled { get; }

        [Option(Alias = "win_capture_audio_detect_silent", DefaultValue = "zero")]  //[none, zero, db, hybrid] | *hybrid: not recommended, inefficient
       string WinCaptureAudioDetectSilent { get; }
    }
}
