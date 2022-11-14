using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BetterLiveScreen.Recording.Audio.Wasapi
{
    public class WasapiRealtimePlay
    {
        private static WasapiOut _wasapiOut = null;
        private static string _prevDeviceId = string.Empty;
        private static MixingSampleProvider _mixer;
        public static Dictionary<string, BufferedWaveProvider> BufferMap { get; private set; } = new Dictionary<string, BufferedWaveProvider>();

        public static WaveFormat StandardFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        public static bool IsInitialized { get; private set; } = false;
        public static bool IsPlaying { get; private set; } = false;

        public static void Initialize()
        {
            MMDevice device = WasapiCapture.DefaultMMDevice;

            if (device != null)
            {
                _wasapiOut = new WasapiOut(device, AudioClientShareMode.Shared, false, 10);
                _wasapiOut.PlaybackStopped += (s, e) =>
                {
                    IsPlaying = false;
                };
                _prevDeviceId = device.ID;
                _mixer = new MixingSampleProvider(StandardFormat);

                IsInitialized = true;
            }
        }

        public static void Play()
        {
            if (!IsInitialized) Initialize();
            if (!IsInitialized || IsPlaying) return;

            _wasapiOut.Init(_mixer);
            _wasapiOut.Play();

            IsPlaying = true;
        }

        public static void Stop()
        {
            if (!IsInitialized || !IsPlaying) return;

            if (_wasapiOut.PlaybackState != PlaybackState.Paused) _wasapiOut.Pause();

            IsInitialized = false;
            IsPlaying = false;
        }

        public static void AddToBufferMap(string userName, WaveFormat format)
        {
            if (BufferMap.ContainsKey(userName)) BufferMap.Remove(userName);

            var buffered = new BufferedWaveProvider(format);
            var converted = new WdlResamplingSampleProvider(buffered.ToSampleProvider(), 44100);
            //var converted = new WaveFormatConversionProvider(StandardFormat, buffered);

            BufferMap.Add(userName, buffered);
            _mixer.AddMixerInput(converted);
        }

        public static bool AddData(string userName, byte[] buffer)
        {
            if (!BufferMap.TryGetValue(userName, out var buffered)) return false;

            buffered.AddSamples(buffer, 0, buffer.Length);
            return true;
        }
    }
}
