using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using CoreAudioApi;

using NAudio.Wave;

using BetterLiveScreen.Extensions;
using BetterLiveScreen.Recording.Audio.Wasapi;

namespace BetterLiveScreen.Recording.Audio.AudioRender
{
    [Obsolete("'AudioRenderer' Class is deprecated, because non-init issue can't be fixed.")]
    //non-init issue이란? : AuxSense에서 Detours로 후킹을 하지만, 이미 IAudioClient::Initialize() 함수를 실행한 상태에서 DLL을 Inject하여 후킹을 시작하면 WASAPI가 dll도 아니여서 주소로 후킹을 해야한다.
    //그래서 IAudioClient::Initialize()의 주소도 알 수 없으므로 프로세스를 실행한 상태 직후 DLL을 Inject하지 않으면 IAudioRenderClient::GetBuffer도 후킹할 수 없게 된다.
    public class AudioRenderer
    {
        [DllImport("AuxSense.dll", EntryPoint = "GetQueueSize")]
        private static extern int GetQueueSize();
        [DllImport("AuxSense.dll", EntryPoint = "GetQueueBuffer")]
        private static extern IntPtr GetQueueBuffer(out IntPtr ptr, out int size, out int duration);
        [DllImport("AuxSense.dll", EntryPoint = "FreeBuffer")]
        private static extern void FreeBuffer(ref IntPtr ptr);
        [DllImport("AuxSense.dll", EntryPoint = "SetEnabled")]
        private static extern void SetEnabled(int enabled);
        [DllImport("AuxSense.dll", EntryPoint = "GetWaveFormat")]
        private static extern IntPtr GetWaveFormatEx();
        [DllImport("AuxSense.dll", EntryPoint = "GetAudioPadding")]
        private static extern uint GetAudioPadding();

        private static int _enabled = 0;
        private static int _callCount = 0;
        public static Queue<byte[]> bytes = new Queue<byte[]>();
        private static WAVEFORMATEXTENSIBLE _waveFormatEx = null;
        public static event EventHandler<(IntPtr, int)> OnBufferFilled;

        public static void Initialize(int pid)
        {
            SetEnabled(0);
            DLLInjector.Inject(pid, "AuxSense.dll");
        }

        private static void BufferFilled(object sender, (IntPtr, int) e) 
        {
            byte[] buffer = new byte[e.Item2];

            Marshal.Copy(e.Item1, buffer, 0, e.Item2);
            bytes.Enqueue(buffer);
        }

        private static byte[] FillBlankBuffer(int duration) //milliseconds
        {
            WaveFormat wf = GetWaveFormat();
            int bytesPerMillisecond = wf.AverageBytesPerSecond / 1000;
            int bytesRecorded = duration * bytesPerMillisecond;

            byte[] buffer = new byte[bytesRecorded];
            return buffer;
        }

        private static int BufferLengthToDuration(int length)
        {
            WaveFormat wf = GetWaveFormat();
            int bytesPerMillisecond = wf.AverageBytesPerSecond / 1000;

            return length / bytesPerMillisecond;
        }

        public static void Start()
        {
            SetEnabled(1);
            _enabled = 1;
            _callCount = 0;

            Task.Run(StartInternal);
        }

        private static void StartInternal()
        {
            while (_enabled != 0)
            {
                if (GetQueueSize() > 0)
                {
                    GetQueueBuffer(out IntPtr ptr, out int size, out int duration);

                    if (ptr == IntPtr.Zero || size == 0) continue;

                    //if (duration > 0)
                    //{
                    //    byte[] buffer = FillBlankBuffer(duration);
                    //    bytes.Enqueue(buffer);
                    //}

                    OnBufferFilled?.Invoke(null, (ptr, size));
                    BufferFilled(null, (ptr, size));

                    FreeBuffer(ref ptr);
                    _callCount++;
                }
                Thread.Sleep(5);
            }
        }

        public static void Stop()
        {
            SetEnabled(0);
            _enabled = 0;
        }

        public static WaveFormat GetWaveFormat()
        {
            if (_waveFormatEx == null)
            {
                IntPtr ptr = GetWaveFormatEx();
                WAVEFORMATEXTENSIBLE wf = new WAVEFORMATEXTENSIBLE();

                Marshal.PtrToStructure(ptr, wf);
                _waveFormatEx = wf;
            }
            if (_waveFormatEx == null || _waveFormatEx.wFormatTag == WaveFormatTag.WAVE_FORMAT_UNKNOWN) return WasapiCapture.DefaultMMDevice?.AudioClient?.MixFormat;
            return new WaveFormat((int)_waveFormatEx.nSamplesPerSec, _waveFormatEx.nChannels);
        }
    }
}
