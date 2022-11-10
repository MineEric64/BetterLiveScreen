using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;

using BetterLiveScreen.Recording.Audio.Wasapi;

namespace BetterLiveScreen.Recording.Audio.WinCaptureAudio
{
    public class MixHandler
    {
        public AudioCaptureHelper Helper { get; set; }
        public Process CurrentProcess { get; set; }

        public ConcurrentQueue<byte[]> Buffer { get; set; }
        public BufferedWaveProvider BufferedWave { get; set; }

        public MixHandler(AudioCaptureHelper helper, Process currentProcess)
        {
            Helper = helper;
            CurrentProcess = currentProcess;

            Buffer = new ConcurrentQueue<byte[]>();
            BufferedWave = new BufferedWaveProvider(WasapiCapture.DeviceWaveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(1),
                DiscardOnBufferOverflow = true
            };
        }
    }
}
