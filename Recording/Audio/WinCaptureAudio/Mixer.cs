﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.WebUI;

using log4net;

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
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ConcurrentDictionary<string, MixHandler> _sessionMap;
        private Thread _mixThread;
        private MixingSampleProvider _mixer;

        public IEnumerable<MixHandler> Sessions => _sessionMap.Values;
        public HashSet<string> ExcludeMixes { get; set; }
        public event EventHandler<byte[]> DataAvailable;

        public bool IsRunning { get; private set; } = false;
        public bool IsEnabled { get; set; } = true;

        public Mixer()
        {
            _sessionMap = new ConcurrentDictionary<string, MixHandler>();

            var excludes = new string[] {
                //Discord
                "Discord",
                "DiscordPTB",
                "DiscordCanary",
                "DiscordDevelopment",

                //BetterLiveScreen
                "BetterLiveScreen",

                //System
                "Idle",

                //NVIDIA
                "nvcontainer", 

                //Virtual Machine
                "VirtualBoxVM"
                };
            ExcludeMixes = new HashSet<string>();
            foreach (string name in excludes) ExcludeMixes.Add(name.ToLower());
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
            if (ExcludeMixes.Contains(p.ProcessName.ToLower())) return;

            var helper = new AudioCaptureHelper(p.Id);
            var handler = new MixHandler(helper, p);

            helper.HandleMix(handler);
            _sessionMap.TryAdd(p.ProcessName, handler);

            try
            {
                p.EnableRaisingEvents = true;
                p.Exited += (s, e) =>
                {
                    RemoveInternal(p.ProcessName);
                };
            }
            catch (Win32Exception) //Access Denied (when Enable Raising Events)
            {
                RemoveInternal(p.ProcessName);
                ExcludeMixes.Add(p.ProcessName.ToLower()); //Add Exclude Mix Process

                log.Warn($"can't add process in audio because of access denied. Process Name : {p.ProcessName}");
            }
            catch (Exception ex)
            {
                RemoveInternal(p.ProcessName);
                log.Error($"can't add process in audio. Process Name : {p.ProcessName}", ex);
            }
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

            _sessionMap.TryRemove(processName, out _);
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
            var startRefreshed = DateTime.Now;

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
                    if (!session.Buffer.TryDequeue(out byte[] buffer))
                    {
                        Thread.Sleep(30);
                        continue;
                    }

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
                Thread.Sleep(3);
                var delta = DateTime.Now - startRefreshed;

                if (delta.TotalMilliseconds >= 3000.0 && IsRunning)
                {
                    AddAsAllPlayingSessions();
                    StartAllHelper();

                    startRefreshed = DateTime.Now;
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
