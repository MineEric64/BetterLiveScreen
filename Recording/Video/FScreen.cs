using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;

using OpenCvSharp;

using BetterLiveScreen.Extensions;
using BetterLiveScreen.Recording.Video.NvEncoder;
using BetterLiveScreen.Recording.Video.NvPipe;
using BetterLiveScreen.Recording.Video.WGC;

using Encoder = BetterLiveScreen.Recording.Video.NvEncoder.Encoder;

namespace BetterLiveScreen.Recording.Video
{
    /// <summary>
    /// some codes from https://github.com/Luigi38/ProjectReinforced/
    ///                                   https://github.com/TheBlackPlague/DynoSharp/blob/main/DynoSharp/FramePool.cs
    /// </summary>
    internal class FScreen
    {
        private bool _run, _init;

        public int Size { get; private set; }
        public FScreen()
        {

        }

        public void Start()
        {
            _run = true;
            var factory = new Factory1();
            var deviceFromMonitor = GetAdapterOutput(Rescreen.Settings.SelectedMonitor, factory);

            //Get first adapter
            var adapter = factory.GetAdapter1(deviceFromMonitor.Item1);
            //Get device from adapter
            var device = new SharpDX.Direct3D11.Device(adapter);
            //Get front buffer of the adapter
            var output = adapter.GetOutput(deviceFromMonitor.Item2);
            var output1 = output.QueryInterface<Output1>();

            // Width/Height of desktop to capture
            int width = output.Description.DesktopBounds.Right;
            int height = output.Description.DesktopBounds.Bottom;

            int aw = Rescreen.Settings.IsHalf ? width / 2 : width;
            int ah = Rescreen.Settings.IsHalf ? height / 2 : height;
            int frameCount = 0;
            
            // Create Staging texture CPU-accessible
            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Width = aw,
                Height = ah,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };
            
            var stagingTexture = new Texture2D(device, textureDesc);

            // Create Staging texture CPU-accessible
            var smallerTextureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.None,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Width = width,
                Height = height,
                OptionFlags = ResourceOptionFlags.GenerateMipMaps,
                MipLevels = 4,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Default
            };
            var smallerTexture = new Texture2D(device, smallerTextureDesc);
            var smallerTextureView = new ShaderResourceView(device, smallerTexture);

            Encoder encoder = null;

            if (Rescreen.Settings.NvencEncoding)
            {
                encoder = new Encoder();

                EncoderDesc setting = new EncoderDesc()
                {
                    width = aw,
                    height = ah,
                    frameRate = Rescreen.FpsIfUnfixed60,
                    format = NvEncoder.Format.B8G8R8A8_UNORM,
                    bitRate = Rescreen.Settings.Bitrate
                };
                setting.maxFrameSize = setting.bitRate / setting.frameRate;

                encoder.Create(setting, device);
                encoder.onEncoded += (s, e) =>
                {
                    byte[] buffer = new byte[e.Item2];
                    Marshal.Copy(e.Item1, buffer, 0, e.Item2);

                    ScreenRefreshed?.Invoke(null, buffer);
                };

                if (!encoder.isValid)
                {
                    Rescreen.Settings.NvencEncoding = false;
                }
            }

            Task.Factory.StartNew(() =>
            {
                // Duplicate the output
                using (var duplicatedOutput = output1.DuplicateOutput(device))
                {
                    var startDate = DateTime.MinValue;
                    int needElapsed = 0;
                    int deltaRes = 0;

                    Rescreen._delayPerFrameSw.Start();
                    
                    while (_run)
                    {
                        try
                        {
                            SharpDX.DXGI.Resource screenResource;
                            OutputDuplicateFrameInformation duplicateFrameInformation;

                            // Try to get duplicated frame within given time is ms
                            var result = duplicatedOutput.TryAcquireNextFrame(Rescreen.Settings.Fps > 0 ? Rescreen.DelayPerFrame : 5, out duplicateFrameInformation, out screenResource);
                            var delta = DateTime.Now - startDate;

                            if (result.Failure)
                            {
                                continue;
                            }
                            else if (startDate == DateTime.MinValue)
                            {
                                startDate = DateTime.Now;
                            }
                            else if (Rescreen.DelayPerFrame > 0 && needElapsed - deltaRes > (int)delta.TotalMilliseconds)
                            {
                                Thread.Sleep(needElapsed - deltaRes - (int)delta.TotalMilliseconds);
                            }

                            Rescreen._deltaResSw.Reset();
                            Rescreen._deltaResSw.Start();

                            needElapsed += Rescreen.DelayPerFrame;

                            // copy resource into memory that can be accessed by the CPU
                            using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                                if (Rescreen.Settings.IsHalf) device.ImmediateContext.CopySubresourceRegion(screenTexture2D, 0, null, smallerTexture, 0);
                                else device.ImmediateContext.CopyResource(screenTexture2D, stagingTexture);

                            if (Rescreen.Settings.IsHalf)
                            {
                                // Generates the mipmap of the screen
                                device.ImmediateContext.GenerateMips(smallerTextureView);

                                // Copy the mipmap 1 of smallerTexture (size/2) to the staging texture
                                device.ImmediateContext.CopySubresourceRegion(smallerTexture, 1, null, stagingTexture, 0);
                            }

                            if (Rescreen.Settings.NvencEncoding)
                            {
                                bool idr = Rescreen.Settings.Fps > 0 ? frameCount++ % Rescreen.Settings.Fps == 0 : false;
                                
                                if (encoder.Encode(stagingTexture, false))
                                {
                                    encoder.Update();
                                }
                            }
                            else
                            {
                                // Get the desktop capture texture
                                var mapSource = device.ImmediateContext.MapSubresource(stagingTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                                int sourceStride = mapSource.RowPitch;
                                int destStride = aw * 4;

                                var sourcePtr = mapSource.DataPointer;
                                var destRaw = new byte[aw * ah * 4];

                                unsafe
                                {
                                    fixed (byte* destRawPtr = destRaw)
                                    {
                                        IntPtr destPtr = (IntPtr)destRawPtr;
                                        CopyMemory(
                                            false, // Should run in parallel or not.
                                            0,
                                            ah,
                                            sourcePtr,
                                            destPtr,
                                            sourceStride,
                                            destStride
                                            );
                                    }
                                }
                                //for (int y = 0; y < ah; y++)
                                //{
                                //    // Copy a single line
                                //    int offset = y * aw * 4;
                                //    Marshal.Copy(sourcePtr, destRaw, offset, aw * 4);

                                //    // Advance pointers
                                //    sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                                //}

                                device.ImmediateContext.UnmapSubresource(stagingTexture, 0);
                                ScreenRefreshed?.Invoke(this, destRaw);

                                // Create Drawing.Bitmap
                                //using (var bitmap = new Bitmap(width / 2, height / 2, PixelFormat.Format32bppArgb))
                                //{
                                //    var boundsRect = new Rectangle(0, 0, width / 2, height / 2);

                                //    // Copy pixels from screen capture Texture to GDI bitmap
                                //    var mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                                //    var sourcePtr = mapSource.DataPointer;
                                //    var destPtr = mapDest.Scan0;
                                //    for (int y = 0; y < height / 2; y++)
                                //    {
                                //        // Copy a single line 
                                //        Utilities.CopyMemory(destPtr, sourcePtr, width * 4 / 2);

                                //        // Advance pointers
                                //        sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                                //        destPtr = IntPtr.Add(destPtr, mapDest.Stride);
                                //    }

                                //    // Release source and dest locks
                                //    bitmap.UnlockBits(mapDest);
                                //    device.ImmediateContext.UnmapSubresource(stagingTexture, 0);

                                //    bitmap.Save(@"C:\Users\erics\Downloads\fsc.jpg");

                                //    //ScreenRefreshed?.Invoke(this, bitmap);
                                //    _init = true;
                                //}
                            }

                            _init = true;

                            screenResource.Dispose();
                            duplicatedOutput.ReleaseFrame();

                            Rescreen._deltaResSw.Stop();
                            Rescreen._deltaRess.Add((int)Rescreen._deltaResSw.ElapsedMilliseconds);

                            Rescreen._delayPerFrameSw.Stop();
                            Rescreen._delayPerFrame.Add((int)Rescreen._delayPerFrameSw.ElapsedMilliseconds);

                            Rescreen._delayPerFrameSw.Reset();
                            Rescreen._delayPerFrameSw.Start();
                        }
                        catch (SharpDXException e)
                        {
                            if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                            {
                                Trace.TraceError(e.Message);
                                Trace.TraceError(e.StackTrace);
                            }
                        }
                    }

                    stagingTexture.Dispose();
                    smallerTexture.Dispose();
                    smallerTextureView.Dispose();
                }
            });
            while (!_init) ;
        }

        public void Stop()
        {
            _run = false;
        }

        public event EventHandler<byte[]> ScreenRefreshed;

        public static void CopyMemory(
            bool parallel,
            int from,
            int to,
            IntPtr sourcePointer,
            IntPtr destinationPointer,
            int sourceStride,
            int destinationStride)
        {
            //[2560x1440 60fps - Background]
            //Legacy => 19ms
            //Non-Parallel => 18.1ms
            //Parallel => 18.4ms

            //[1280x720 30fps - In Game]
            //Legacy => 23.2ms
            //Non-Parallel => 22.9ms
            //Parallel => 23ms

            if (!parallel)
            {
                for (int i = from; i < to; i++)
                {
                    IntPtr sourceIteratedPointer = IntPtr.Add(sourcePointer, sourceStride * i);
                    IntPtr destinationIteratedPointer = IntPtr.Add(destinationPointer, destinationStride * i);

                    // Memcpy is apparently faster than Buffer.MemoryCopy. 
                    Utilities.CopyMemory(destinationIteratedPointer, sourceIteratedPointer, destinationStride);
                }
                return;
            }

            Parallel.For(from, to, i =>
            {
                IntPtr sourceIteratedPointer = IntPtr.Add(sourcePointer, sourceStride * i);
                IntPtr destinationIteratedPointer = IntPtr.Add(destinationPointer, destinationStride * i);

                // Memcpy is apparently faster than Buffer.MemoryCopy. 
                Utilities.CopyMemory(destinationIteratedPointer, sourceIteratedPointer, destinationStride);
            });
        }

        public static (int, int) GetAdapterOutput(MonitorInfo monitor, Factory1 factory)
        {
            for (int i = 0; i < factory.Adapters1.Length; i++)
            {
                var adapter = factory.Adapters1[i];
                string adapterName = adapter.Description.Description; //ex) NVIDIA GeForce GTX 1050 Ti

                for (int j = 0; j < adapter.Outputs.Length; j++)
                {
                    var output = adapter.Outputs[i];
                    string outputName = output.Description.DeviceName; //ex) \\.\DISPLAY1
                    
                    if (monitor.DeviceName == outputName) return (i, j);
                }
            }

            return (-1, -1);
        }
    }
}
