using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BetterLiveScreen.Recording.Audio
{
    public class WasapiPlay
    {
        //일부 코드는 ProjectReinforced (https://github.com/Luigi38/ProjectReinforced/blob/main/Recording.Audio.cs) 그리고
        //ProjectUnitor에서 가져왔습니다.

        private static WasapiOut _wasapiOut = null;
        private static string _prevDeviceId = string.Empty;
        private static Mp3FileReader _reader = null;

        public static bool IsInitialized { get; private set; } = false;
        public static bool IsRead { get; private set; } = false;
        public static bool IsReady { get; private set; } = false;
        public static bool IsFinalReady => IsInitialized && IsRead && IsReady;

        public static bool IsPlaying { get; private set; } = false;
        public static bool IsPaused { get; private set; } = false;

        public static void Initialize() {
            MMDevice device = WasapiCapture.DefaultMMDevice;

            if (device != null)
            {
                _wasapiOut = new WasapiOut(device, AudioClientShareMode.Shared, false, 10);
                _wasapiOut.PlaybackStopped += (s, e) =>
                {
                    IsPlaying = false;
                };
                _prevDeviceId = device.ID;

                IsInitialized = true;
            }
        }
       
        public static void Read(string path)
        {
            if (!File.Exists(path)) return;

            try
            {
                _reader = new Mp3FileReader(path);
                IsRead = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "BetterLiveScreen : Error", MessageBoxButton.OK, MessageBoxImage.Error);
                IsRead = false;
            }
        }

        public static void Ready()
        {
            if (!IsInitialized || !IsRead) return;
            _wasapiOut.Init(_reader);
            IsReady = true;
        }

        public static void SetTime(TimeSpan position)
        {
            if (!IsRead) return;
            _reader.CurrentTime = position;
        }

        public static bool Play()
        {
            MMDevice device = WasapiCapture.DefaultMMDevice;

            if (!IsFinalReady) return false;
            if (_prevDeviceId != device.ID) Initialize();

            _wasapiOut.Play();
            IsPlaying = true;
            IsPaused = false;

            return true;
        }

        public static void Pause()
        {
            if (!IsFinalReady) return;
            if (_wasapiOut.PlaybackState == PlaybackState.Paused) return;

            _wasapiOut.Pause();
            IsPlaying = false;
            IsPaused = true;
        }

        public static void Stop()
        {
            if (!IsFinalReady) return;
            if (_wasapiOut.PlaybackState != PlaybackState.Paused) _wasapiOut.Pause();

            _reader.Position = 0;
            IsPlaying = false;
            IsPaused = true;
        }

        public static void Close()
        {
            _wasapiOut?.Dispose();
            _reader?.Dispose();

            IsPlaying = false;
            IsPaused = false;
            IsInitialized = false;
            IsRead = false;
            IsReady = false;
        }
    }
}
