using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace BetterLiveScreen.Recording.Audio.Wasapi
{
    public class WasapiCapture
    {
        //일부 코드는 https://github.com/Luigi38/ProjectReinforced/blob/main/Recording.Audio.cs 에서 가져왔습니다.

        internal static MMDevice DefaultMMDevice //Speaker
        {
            get
            {
                try
                {
                    return WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice();
                }
                catch
                {
                    Debug.WriteLine("[Error] Can't get DefaultMMDevice");
                    return null;
                }
            }
        }
        internal static WaveFormat DeviceMixFormat => DefaultMMDevice?.AudioClient?.MixFormat;
        internal static WaveFormat DeviceWaveFormat => DeviceMixFormat?.AsStandardWaveFormat();
        internal static WaveFormat WaveFormat => _capture?.WaveFormat;

        /// <summary>
        /// 소리 녹음용 (스피커)
        /// </summary>
        private static WasapiLoopbackCapture _capture;
        private static string _prevDeviceId = string.Empty;
        /// <summary>
        /// DataAvailable에서 소리가 없을 경우 사용
        /// </summary>
        private static Stopwatch _sw = new Stopwatch();

        public static event EventHandler<byte[]> DataAvailable;

        public static bool IsInitialized { get; private set; } = false;

        /// <summary>
        /// 출력 캡처 장치를 초기화 합니다.
        /// </summary>
        public static void Initialize()
        {
            MMDevice device = DefaultMMDevice;

            if (device != null)
            {
                _capture = new WasapiLoopbackCapture(device);
                _prevDeviceId = device.ID;

                IsInitialized = true;
            }
        }

        public static bool Record()
        {
            MMDevice device = DefaultMMDevice;

            if (!IsInitialized || device == null)
            {
                return false;
            }

            if (_prevDeviceId != device.ID)
            {
                Initialize();
            }

            _capture.DataAvailable += WhenDataAvailable;
            _capture.StartRecording();
            _sw.Start();

            return true;
        }

        public static void Stop()
        {
            if (!IsInitialized) return;

            _capture.StopRecording();
            _capture.DataAvailable -= WhenDataAvailable;
            _capture.Dispose();
            
            IsInitialized = false;

            _sw.Stop();
            _sw.Reset();
        }

        private static void WhenDataAvailable(object sender, WaveInEventArgs e)
        {
            _sw.Stop();

            byte[] buffer = new byte[e.BytesRecorded];

            if (e.BytesRecorded == 0)
            {
                int bytesPerMillisecond = WasapiCapture.WaveFormat.AverageBytesPerSecond / 1000;
                int bytesRecorded = (int)_sw.ElapsedMilliseconds * bytesPerMillisecond;

                buffer = new byte[bytesRecorded];
            }
            else
            {
                Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
            }
            DataAvailable?.Invoke(sender, buffer);

            _sw.Restart();
        }
    }
}
