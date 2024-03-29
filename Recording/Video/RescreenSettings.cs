﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BetterLiveScreen.Recording.Types;
using BetterLiveScreen.Recording.Video.WGC;

namespace BetterLiveScreen.Recording.Video
{
    public class RescreenSettings
    {
        public int Fps { get; set; }
        public MonitorInfo SelectedMonitor { get; set; }
        public bool IsHalf { get; set; }
        public EncodingType Encoding { get; set; }
        public int Bitrate { get; set; }

        public CaptureVideoType VideoType { get; set; }
        public CaptureAudioType AudioType { get; set; }

        public static MonitorInfo PrimaryMonitor => MonitorEnumerationHelper.GetMonitors().Where((m) => m.IsPrimary).FirstOrDefault();
        public static RescreenSettings Default => new RescreenSettings()
        {
            Fps = 30,
            SelectedMonitor = PrimaryMonitor,
            IsHalf = false,
            Encoding = EncodingType.Nvenc,
            Bitrate = BitrateInfo.GetBitrateFromMbps(4),
            VideoType = CaptureVideoType.DD,
            AudioType = CaptureAudioType.WinCaptureAudio
        };
    }
}
