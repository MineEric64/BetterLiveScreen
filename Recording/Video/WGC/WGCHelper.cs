using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
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

using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using NAudio.CoreAudioApi;
using System.Windows.Media.Media3D;

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

        public static ReadOnlyCollection<FindProcess> Processes => _processes.AsReadOnly();
        public static ReadOnlyCollection<FindProcess> Monitors => _monitors.AsReadOnly();
        public static event EventHandler<byte[]> ScreenRefreshed;

        public static bool Initialize()
        {
            if (!GraphicsCaptureSession.IsSupported())
            {
                return false;
            }
            return true;
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

        public static void StartHwndCapture(IntPtr hwnd, bool isHalf = false)
        {
            GraphicsCaptureItem item = CaptureHelper.CreateItemForWindow(hwnd);
            if (item != null) StartCaptureInternal(item, isHalf);
        }

        public static void StartHmonCapture(IntPtr hmon, bool isHalf = false)
        {
            GraphicsCaptureItem item = CaptureHelper.CreateItemForMonitor(hmon);
            if (item != null) StartCaptureInternal(item, isHalf);
        }

        public static void StartPrimaryMonitorCapture(bool isHalf = false)
        {
            MonitorInfo monitor = (from m in MonitorEnumerationHelper.GetMonitors()
                                   where m.IsPrimary
                                   select m).First();
            StartHmonCapture(monitor.Hmon, isHalf);
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

        private static void StartCaptureInternal(GraphicsCaptureItem item, bool isHalf)
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
                    ProcessFrame(frame, isHalf);
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
            _aw = isHalf ? _width / 2 : _width;
            _ah = isHalf ? _height / 2 : _height;

            // Create Staging texture CPU-accessible
            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
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
                Format = Format.B8G8R8A8_UNorm,
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

            _session = _framePool.CreateCaptureSession(_item);
            _session.StartCapture();

            Rescreen._delayPerFrameSw.Start();
        }

        private static void ProcessFrame(Direct3D11CaptureFrame frame, bool isHalf)
        {
            using (Texture2D surfaceTexture = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface))
            {
                if (isHalf) _sharpDevice.ImmediateContext.CopySubresourceRegion(surfaceTexture, 0, null, _smallerTexture, 0);
                else _sharpDevice.ImmediateContext.CopyResource(surfaceTexture, _frameTexture);
            }
            if (isHalf)
            {
                _sharpDevice.ImmediateContext.GenerateMips(_smallerTextureView);
                _sharpDevice.ImmediateContext.CopySubresourceRegion(_smallerTexture, 1, null, _frameTexture, 0);
            }

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

            Rescreen._deltaResSw.Stop();
            Rescreen._deltaRess.Add((int)Rescreen._deltaResSw.ElapsedMilliseconds);
        }
    }
}
