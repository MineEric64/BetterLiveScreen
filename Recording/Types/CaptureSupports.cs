using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetterLiveScreen.Recording.Types
{
    public class CaptureSupports
    {
        //Video - Capture
        public bool DesktopDuplication { get; set; }
        public bool WGC { get; set; }

        //Video - Encoding
        public bool Nvenc { get; set; }

        //Audio
        public bool WinCaptureAudio { get; set; }

        public static CaptureSupports Default => new CaptureSupports()
        {
            DesktopDuplication = true,
            WGC = true,
            Nvenc = true,
            WinCaptureAudio = true
        };
    }
}
