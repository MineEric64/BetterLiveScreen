using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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

        public static float Volume { get; set; } = 1.0F;
        public static bool IsInitialized { get; private set; } = false;
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

        public static void Play(string path, int millisecondsToPlay = -1)
        {
            MMDevice device = WasapiCapture.DefaultMMDevice;

            if (IsInitialized && device != null)
            {
                if (_prevDeviceId != device.ID)
                {
                    Initialize();
                    Play(path);
                }
                else
                {
                    if (!IsPaused)
                    {
                        _reader?.Dispose();
                        _reader = new Mp3FileReader(path);

                        ISampleProvider final = _reader.ToSampleProvider();

                        if (Volume != 1.0F)
                        {
                            var volumed = new VolumeSampleProvider(final)
                            {
                                Volume = Volume
                            };
                            final = volumed;
                        }
                        if (millisecondsToPlay != -1)
                        {
                            var trimmed = new OffsetSampleProvider(final);
                            trimmed.SkipOver = TimeSpan.FromMilliseconds(millisecondsToPlay);

                            final = trimmed;
                        }

                        _wasapiOut.Init(final);
                    }
                    _wasapiOut.Play();
                    IsPlaying = true;
                    IsPaused = false;
                }
            }
        }

        public static void Pause()
        {
            if (_wasapiOut.PlaybackState == PlaybackState.Paused) return;

            _wasapiOut.Pause();
            IsPlaying = false;
            IsPaused = true;
        }

        public static void Stop()
        {
            if (_wasapiOut.PlaybackState != PlaybackState.Paused) _wasapiOut.Pause();
            _reader.Position = 0;
            IsPlaying = false;
            IsPaused = true;
        }
    }
}
