using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using CoreAudioApi;
using CoreAudioApi.Interfaces;

using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wasapi.CoreAudioApi.Interfaces;
using NAudio.Wave;

using BetterLiveScreen.Extensions;
using BetterLiveScreen.Recording.Audio.Wasapi;

using WasapiCapture = BetterLiveScreen.Recording.Audio.Wasapi.WasapiCapture;
using IAudioClient = NAudio.CoreAudioApi.Interfaces.IAudioClient;

namespace BetterLiveScreen.Recording.Audio.WinCaptureAudio
{
    /// <summary>
    /// This code is from https://github.com/bozbez/win-capture-audio
    /// </summary>
    public class AudioCaptureHelper : IDisposable
    {
        private IAudioClient _client;
        private IAudioCaptureClient _captureClient;
        private IntPtr[] _events = new IntPtr[(int)HelperEvents.Count];
        private Thread _captureThread;
        private MixHandler _mixHandler = null;

        public int Id { get; }
        public WaveFormat Format { get; } = null;
        public bool IsRunning { get; private set; } = false;
        public bool IsInitialized { get; private set; } = false;

        public AudioCaptureHelper(int id)
        {
            Id = id;
            Format = WasapiCapture.DeviceMixFormat;

            for (int i = 0; i < _events.Length; i++)
                _events[i] = Kernel32.CreateEvent(IntPtr.Zero, false, false, null);
        }

        public void Start()
        {
            _captureThread = new Thread(CaptureSafe);
            _captureThread.Start();

            IsRunning = true;
        }

        [STAThread]
        private void InitClient()
        {
            int result;
            Guid guid = Guid.Empty;

            var _params = GetParams();
            var propvariant = GetPropvariant(_params, out var paramsPtr);

            CompletionHandler completionHandler = new CompletionHandler();
            result = Mmdevapi.ActivateAudioInterfaceAsync(
                Mmdevapi.VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK,
                typeof(IAudioClient).GUID,
                propvariant,
                completionHandler,
                out _
                );
            Marshal.ThrowExceptionForHR(result);

            Kernel32.WaitForSingleObject(completionHandler.eventFinished, uint.MaxValue);
            Marshal.ThrowExceptionForHR(completionHandler.hr);

            _client = completionHandler.client;
            result = _client.Initialize(
                AudioClientShareMode.Shared,
                AudioClientStreamFlags.Loopback | AudioClientStreamFlags.EventCallback,
                5 * 10000000,
                0,
                Format,
                ref guid);

            Marshal.ThrowExceptionForHR(result);

            result = _client.SetEventHandle(_events[0]);
            Marshal.ThrowExceptionForHR(result);

            //for propvariant
            Marshal.FreeHGlobal(paramsPtr);
            Marshal.FreeHGlobal(propvariant);
        }

        private void InitCapture()
        {
            InitClient();
            int result = _client.GetService(
                typeof(IAudioCaptureClient).GUID,
                out object obj);
            Marshal.ThrowExceptionForHR(result);
            _captureClient = obj as IAudioCaptureClient;

            IsInitialized = true;
        }

        private AUDIOCLIENT_ACTIVATION_PARAMS GetParams()
        {
            var mode = PROCESS_LOOPBACK_MODE.PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE;

            return new AUDIOCLIENT_ACTIVATION_PARAMS()
            {
                ActivationType = AUDIOCLIENT_ACTIVATION_TYPE.AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK,
                ProcessLoopbackParams = new AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS()
                {
                    TargetProcessId = (uint)Id,
                    ProcessLoopbackMode = mode
                }
            };
        }

        private IntPtr GetPropvariant(AUDIOCLIENT_ACTIVATION_PARAMS _params, out IntPtr paramsPtr)
        {
            int size = Marshal.SizeOf<AUDIOCLIENT_ACTIVATION_PARAMS>();
            int propSize = Marshal.SizeOf<PROPVARIANT>();
            IntPtr propPtr;
            PROPVARIANT prop;

            paramsPtr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(_params, paramsPtr, false);
            prop = new PROPVARIANT()
            {
                inner = new tag_inner_PROPVARIANT()
                {
                    vt = (ushort)VarEnum.VT_BLOB,
                    blob = new BLOB()
                    {
                        cbSize = (ulong)size,
                        pBlobData = paramsPtr
                    }
                }
            };
            propPtr = Marshal.AllocHGlobal(propSize);
            Marshal.StructureToPtr(prop, propPtr, false);

            return propPtr;
        }

        public void ForwardPacket()
        {
            int frameSize = Format.BlockAlign;
            uint numFrames = 0;
            int result;

            result = _captureClient.GetNextPacketSize(out numFrames);
            Marshal.ThrowExceptionForHR(result);

            while (numFrames > 0)
            {
                IntPtr newData;
                AudioClientBufferFlags flags;
                ulong qpcPosition;

                result = _captureClient.GetBuffer(out newData, out numFrames, out flags, out var _, out qpcPosition);
                Marshal.ThrowExceptionForHR(result);

                if ((flags & AudioClientBufferFlags.Silent) == AudioClientBufferFlags.None)
                {
                    int bytesPerFrame = Format.Channels * Format.BitsPerSample / 8;
                    byte[] buffer = new byte[numFrames * bytesPerFrame];

                    Marshal.Copy(newData, buffer, 0, buffer.Length);
                    ForwardToMixer(qpcPosition, buffer);
                }

                if ((flags & AudioClientBufferFlags.DataDiscontinuity) != AudioClientBufferFlags.None)
                    Debug.WriteLine("data discontinuity flag set");

                if ((flags & AudioClientBufferFlags.TimestampError) != AudioClientBufferFlags.None)
                    Debug.WriteLine("timestamp error flag set");

                result = _captureClient.ReleaseBuffer(numFrames);
                Marshal.ThrowExceptionForHR(result);

                result = _captureClient.GetNextPacketSize(out numFrames);
                Marshal.ThrowExceptionForHR(result);
            }
        }

        public void ForwardToMixer(ulong timestamp, byte[] buffer)
        {
            if (_mixHandler == null) return;

            _mixHandler.Buffer.Enqueue(buffer);
        }

        private void Capture()
        {
            if (!IsInitialized) InitCapture();
            int result;
            bool shutdown = false;

            result = _client.Start();
            Marshal.ThrowExceptionForHR(result);

            while (!shutdown && IsRunning)
            {
                HelperEvents eventId = (HelperEvents)Kernel32.WaitForMultipleObjects((uint)_events.Length, _events, false, uint.MaxValue);

                switch (eventId)
                {
                    case HelperEvents.PacketReady:
                        ForwardPacket();
                        break;

                    case HelperEvents.Shutdown:
                        shutdown = true;
                        break;

                    default:
                        Debug.WriteLine($"wait failed with result: {eventId}");
                        shutdown = true;
                        break;
                }

                Thread.Sleep(3);
            }

            result = _client.Stop();
            Marshal.ThrowExceptionForHR(result);
        }

        private void CaptureSafe()
        {
            try
            {
                Capture();
            }
            catch (Exception ex)
            {
                Stop();
                MessageBox.Show(ex.ToString(), "BetterLiveScreen : Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Stop()
        {
            Kernel32.SetEvent(_events[(int)HelperEvents.Shutdown]);
            //_captureThread.Join();
            IsRunning = false;
        }

        public void Dispose()
        {
            if (IsRunning) Stop();
        }

        public void HandleMix(MixHandler handler)
        {
            _mixHandler = handler;
        }
    }
}
