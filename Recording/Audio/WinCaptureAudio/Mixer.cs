using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.WebUI;

using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

using BetterLiveScreen.Extensions;

using WasapiCapture = BetterLiveScreen.Recording.Audio.Wasapi.WasapiCapture;

namespace BetterLiveScreen.Recording.Audio.WinCaptureAudio
{
    public class Mixer
    {
        private Dictionary<string, MixHandler> _sessionMap;
        private Thread _mixThread;
        private MixingSampleProvider _mixer;

        public IEnumerable<MixHandler> Sessions => _sessionMap.Values;
        public HashSet<string> ExcludeMixes { get; set; }
        public event EventHandler<byte[]> DataAvailable;

        public bool IsRunning { get; private set; } = false;
        public bool IsEnabled { get; set; } = true;

        public Mixer()
        {
            _sessionMap = new Dictionary<string, MixHandler>();
            
            ExcludeMixes = new HashSet<string>();
            ExcludeMixes.Add("Discord");
            ExcludeMixes.Add("BetterLiveScreen");
        }

        public void Add(Process p)
        {
            if (_sessionMap.TryGetValue(p.ProcessName, out var handler))
            {
                if (p.Id != handler.Helper.Id) //that's weird
                {
                    RemoveInternal(p.ProcessName);
                    AddInternal(p);
                }
            }
            else
            {
                AddInternal(p);
            }
        }

        public void Add(Process[] ps)
        {
            foreach (Process p in ps)
            {
                Add(p);
            }
        }

        public void AddAsAllPlayingSessions()
        {
            Process[] ps = AudioSessionManagerV2.GetAllPlayingSessions();
            Add(ps);
            return;
        }

        private void AddInternal(Process p)
        {
            if (ExcludeMixes.Contains(p.ProcessName)) return;

            var helper = new AudioCaptureHelper(p.Id);
            var handler = new MixHandler(helper, p);

            helper.HandleMix(handler);
            _sessionMap.Add(p.ProcessName, handler);
            p.EnableRaisingEvents = true;
            p.Exited += (s, e) =>
            {
                RemoveInternal(p.ProcessName);
            };
        }

        public void Remove(string processName)
        {
            RemoveInternal(processName);
        }

        public void Remove(string[] processNames)
        {
            foreach (string processName in processNames)
            {
                Remove(processName);
            }
        }

        public void Remove(Process p)
        {
            Remove(p.ProcessName);
        }

        public void Remove(Process[] ps)
        {
            foreach (var p in ps)
            {
                Remove(p.ProcessName);
            }
        }

        private void RemoveInternal(string processName)
        {
            var handler = _sessionMap[processName];

            if (IsRunning) handler.Helper.Stop();
            handler.Helper.Dispose();
            while (!handler.Buffer.IsEmpty) handler.Buffer.TryDequeue(out _);
            handler.BufferedWave.ClearBuffer();

            _sessionMap.Remove(processName);
        }

        public void Start()
        {
            if (WasapiCapture.DefaultMMDevice == null) return; //Doesn't exists any playing device

            _mixThread = new Thread(Mix);
            IsRunning = true;

            AddAsAllPlayingSessions();
            StartAllHelper();
            _mixThread.Start();
        }

        private void Mix()
        {
            _mixer = new MixingSampleProvider(WasapiCapture.DeviceWaveFormat);
            int waitToRefresh = 0;

            while (IsRunning)
            {
                if (!IsEnabled)
                {
                    Thread.Sleep(100);
                    continue;
                }

                float volume = 1.0f / _sessionMap.Count;
                int bufferLength = 0;

                foreach (var session in _sessionMap.Values)
                {
                    if (!session.Buffer.TryDequeue(out byte[] buffer)) continue;

                    VolumeSampleProvider volumed;

                    session.BufferedWave.ClearBuffer();
                    session.BufferedWave.AddSamples(buffer, 0, buffer.Length);
                    bufferLength = Math.Max(bufferLength, session.BufferedWave.BufferedBytes);

                    volumed = new VolumeSampleProvider(session.BufferedWave.ToSampleProvider())
                    {
                        Volume = volume
                    };
                    _mixer.AddMixerInput(volumed);
                }

                if (bufferLength > 0)
                {
                    float[] bufferFloated = new float[bufferLength];
                    byte[] bufferMixed;

                    int read = _mixer.Read(bufferFloated, 0, bufferFloated.Length);
                    bufferMixed = new byte[read];
                    Buffer.BlockCopy(bufferFloated, 0, bufferMixed, 0, read);

                    DataAvailable?.Invoke(null, bufferMixed);
                    _mixer.RemoveAllMixerInputs();
                }
                Thread.Sleep(1);

                if (++waitToRefresh == 10 && IsRunning)
                {
                    AddAsAllPlayingSessions();
                    StartAllHelper();
                    waitToRefresh = 0;
                }
            }
        }

        private void StartAllHelper()
        {
            foreach (var session in _sessionMap.Values)
            {
                if (!session.Helper.IsRunning && IsRunning)
                {
                    session.Helper.Start();
                }
            }
        }

        public void Stop()
        {
            IsRunning = false;

            foreach (var session in _sessionMap.Values)
            {
                if (session.Helper.IsRunning)
                {
                    session.Helper.Stop();
                }
            }
        }
    }
}
