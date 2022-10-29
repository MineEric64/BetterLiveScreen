using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace BetterLiveScreen.Recording.Audio
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
                    return null;
                }
            }
        }
        internal static WaveFormat WaveFormat => _capture?.WaveFormat;

        /// <summary>
        /// 소리 녹음용 (스피커)
        /// </summary>
        private static WasapiLoopbackCapture _capture;
        private static string _prevDeviceId = string.Empty;
        private static Action<WaveInEventArgs> _whenDataAvailable;

        public static bool IsInitialized { get; private set; } = false;

        /// <summary>
        /// 출력 캡처 장치를 초기화 합니다.
        /// </summary>
        public static void Initialize(Action<WaveInEventArgs> whenDataAvailable)
        {
            MMDevice device = DefaultMMDevice;

            _whenDataAvailable = whenDataAvailable;

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
                Initialize(_whenDataAvailable);
            }

            _capture.DataAvailable += WhenDataAvailable;
            _capture.StartRecording();

            return true;
        }

        public static void Stop()
        {
            if (!IsInitialized) return;

            _capture.StopRecording();
            _capture.DataAvailable -= WhenDataAvailable;
        }

        private static void WhenDataAvailable(object sender, WaveInEventArgs e)
        {
            _whenDataAvailable(e);
        }
    }
}
