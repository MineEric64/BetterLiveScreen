using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using Windows.Foundation.Metadata;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Effects;

using Composition.WindowsRuntimeHelpers_NETStd;

using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

using BetterLiveScreen.Recording.Types;
using BetterLiveScreen.Recording.Video.NvEncoder;

using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Encoder = BetterLiveScreen.Recording.Video.NvEncoder.Encoder;
using static System.Collections.Specialized.BitVector32;

namespace BetterLiveScreen.Recording.Video.WGC
{
    public class WGCHelper
    {
        private static List<FindProcess> _processes = new List<FindProcess>();
        private static List<FindProcess> _monitors = new List<FindProcess>();

        private static GraphicsCaptureItem _item = null;
        private static IDirect3DDevice _device = Direct3D11Helper.CreateDevice();
        private static Device _sharpDevice = Direct3D11Helper.CreateSharpDXDevice(_device);
        private static Direct3D11CaptureFramePool _framePool = null;
        private static GraphicsCaptureSession _session = null;

        private static int _width;
        private static int _height;
        private static int _aw;
        private static int _ah;
        private static Texture2D _frameTexture;
        private static Texture2D _smallerTexture;
        private static ShaderResourceView _smallerTextureView;
        private static Encoder _encoder = null;
        private static int _frameCount = 0;

        public static bool IsInitialized { get; private set; } = false;
        public static bool IsBorderRequired { get; set; } = false;

        public static ReadOnlyCollection<FindProcess> Processes => _processes.AsReadOnly();
        public static ReadOnlyCollection<FindProcess> Monitors => _monitors.AsReadOnly();
        public static event EventHandler<byte[]> ScreenRefreshed;

        public static bool Initialize()
        {
            if (GraphicsCaptureSession.IsSupported())
            {
                IsInitialized = true;
            }
            return IsInitialized;
        }

        public static bool Refresh()
        {
            _processes.Clear();
            _monitors.Clear();

            if (ApiInformation.IsApiContractPresent(typeof(UniversalApiContract).FullName, 8))
            {
                //Monitors
                var monitors = MonitorEnumerationHelper.GetMonitors();
                _monitors.AddRange(monitors.OrderBy(p => p.DeviceName).Select(monitor => new FindProcess(monitor.Hmon, -1, monitor.DeviceName)));

                //Processes
                string[] notProcess = new string[] { "계산기", "NVIDIA GeForce Overlay", "Microsoft Text Input Application", "설정" };

                var processesWithWindows = from p in Process.GetProcesses()
                                           where !string.IsNullOrWhiteSpace(p.MainWindowTitle) && WindowEnumerationHelper.IsWindowValidForCapture(p.MainWindowHandle)
                                           select p;

                _processes.AddRange(processesWithWindows.Where(process => !notProcess.Contains(process.MainWindowTitle)).Select(process => new FindProcess(process.MainWindowHandle, process.Id, process.MainWindowTitle)));

                return true;
            }
            return false;
        }

        public static void StartHwndCapture(IntPtr hwnd)
        {
            GraphicsCaptureItem item = CaptureHelper.CreateItemForWindow(hwnd);
            if (item != null) StartCaptureInternal(item);
        }

        public static void StartHmonCapture(IntPtr hmon)
        {
            GraphicsCaptureItem item = CaptureHelper.CreateItemForMonitor(hmon);
            if (item != null) StartCaptureInternal(item);
        }

        public static void StartPrimaryMonitorCapture()
        {
            MonitorInfo monitor = (from m in MonitorEnumerationHelper.GetMonitors()
                                   where m.IsPrimary
                                   select m).First();
            StartHmonCapture(monitor.Hmon);
        }

        public static void StopCapture()
        {
            _session?.Dispose();
            _framePool?.Dispose();
            _item = null;
            _session = null;
            _framePool = null;

            _frameTexture?.Dispose();
            _smallerTexture?.Dispose();
            _smallerTextureView?.Dispose();
        }

        private static void StartCaptureInternal(GraphicsCaptureItem item)
        {
            _item = item;
            
            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                _item.Size
                );
            _framePool.FrameArrived += (s, a) =>
            {
                Rescreen._deltaResSw.Reset();
                Rescreen._deltaResSw.Start();

                using (var frame = _framePool.TryGetNextFrame())
                {
                    ProcessFrame(frame);
                }

                Rescreen._delayPerFrameSw.Stop();
                Rescreen._delayPerFrame.Add((int)Rescreen._delayPerFrameSw.ElapsedMilliseconds);

                Rescreen._delayPerFrameSw.Reset();
                Rescreen._delayPerFrameSw.Start();
            };

            _item.Closed += (s, a) =>
            {
                StopCapture();
            };

            _width = item.Size.Width;
            _height = item.Size.Height;
            _aw = Rescreen.Settings.IsHalf ? _width / 2 : _width;
            _ah = Rescreen.Settings.IsHalf ? _height / 2 : _height;

            // Create Staging texture CPU-accessible
            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Width = _aw,
                Height = _ah,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            _frameTexture = new Texture2D(_sharpDevice, textureDesc);

            // Create Staging texture CPU-accessible
            var smallerTextureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.None,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Width = _width,
                Height = _height,
                OptionFlags = ResourceOptionFlags.GenerateMipMaps,
                MipLevels = 4,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Default
            };
            _smallerTexture = new Texture2D(_sharpDevice, smallerTextureDesc);
            _smallerTextureView = new ShaderResourceView(_sharpDevice, _smallerTexture);

            if (Rescreen.Settings.NvencEncoding)
            {
                _encoder = new Encoder();

                EncoderDesc setting = new EncoderDesc()
                {
                    width = _aw,
                    height = _ah,
                    frameRate = Rescreen.FpsIfUnfixed60,
                    format = NvEncoder.Format.B8G8R8A8_UNORM,
                    bitRate = Rescreen.Settings.Bitrate
                };
                setting.maxFrameSize = setting.bitRate / setting.frameRate;

                _encoder.Create(setting, _sharpDevice);
                _encoder.onEncoded += (s, e) =>
                {
                    byte[] buffer = new byte[e.Item2];
                    Marshal.Copy(e.Item1, buffer, 0, e.Item2);

                    ScreenRefreshed?.Invoke(null, buffer);
                };

                if (!_encoder.isValid)
                {
                    Rescreen.Settings.NvencEncoding = false;
                }
            }

            _frameCount = 0;

            _session = _framePool.CreateCaptureSession(_item);
            
            if (!IsBorderRequired)
            {
                var pUnk = Marshal.GetIUnknownForObject(_session);
                var session3 = Marshal.GetObjectForIUnknown(pUnk) as IGraphicsCaptureSession3;
                session3.IsBorderRequired = false;
            }
            _session.StartCapture();

            Rescreen._delayPerFrameSw.Start();
        }

        private static void ProcessFrame(Direct3D11CaptureFrame frame)
        {
            using (Texture2D surfaceTexture = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface))
            {
                if (Rescreen.Settings.IsHalf) _sharpDevice.ImmediateContext.CopySubresourceRegion(surfaceTexture, 0, null, _smallerTexture, 0);
                else _sharpDevice.ImmediateContext.CopyResource(surfaceTexture, _frameTexture);
            }
            if (Rescreen.Settings.IsHalf)
            {
                _sharpDevice.ImmediateContext.GenerateMips(_smallerTextureView);
                _sharpDevice.ImmediateContext.CopySubresourceRegion(_smallerTexture, 1, null, _frameTexture, 0);
            }

            if (Rescreen.Settings.NvencEncoding)
            {
                bool idr = Rescreen.Settings.Fps > 0 ? _frameCount++ % Rescreen.Settings.Fps == 0 : false;

                if (_encoder.Encode(_frameTexture, idr))
                {
                    _encoder.Update();
                }
            }
            else
            {
                DataBox mappedMemory =
                   _sharpDevice.ImmediateContext.MapSubresource(_frameTexture, 0, MapMode.Read, MapFlags.None);

                IntPtr sourcePointer = mappedMemory.DataPointer;
                int sourceStride = mappedMemory.RowPitch;
                int destinationStride = _aw * 4;

                byte[] frameBytes = new byte[_aw * _ah * 4]; // 4 bytes / pixel (High Mem. Allocation)

                unsafe
                {
                    fixed (byte* frameBytesPointer = frameBytes)
                    {
                        IntPtr destinationPointer = (IntPtr)frameBytesPointer;
                        FScreen.CopyMemory(
                            false, // Should run in parallel or not.
                            0,
                            _ah,
                            sourcePointer,
                            destinationPointer,
                            sourceStride,
                            destinationStride
                            );
                    }
                }

                _sharpDevice.ImmediateContext.UnmapSubresource(_frameTexture, 0);
                ScreenRefreshed?.Invoke(null, frameBytes);
            }

            Rescreen._deltaResSw.Stop();
            Rescreen._deltaRess.Add((int)Rescreen._deltaResSw.ElapsedMilliseconds);
        }
    }
}
