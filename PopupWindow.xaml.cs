using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

using SharpDX.Direct3D9;

using RawRectangle = SharpDX.Mathematics.Interop.RawRectangle;
using My = BetterLiveScreen.MainWindow;

namespace BetterLiveScreen
{
    /// <summary>
    /// PopupWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PopupWindow : Window
    {
        private static PopupWindow _instance = null;
        private Device _d3dDevice = null;

        public static bool IsOpened { get; private set; } = false;
        public static PopupWindow Instance
        {
            get
            {
                if (_instance == null || !IsOpened) _instance = new PopupWindow();
                return _instance;
            }
        }

        public PopupWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeD3D();
            IsOpened = true;

            My.PreviewMap[My.User.FullName] = 11; //for Test
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            IsOpened = false;
        }

        private void InitializeD3D()
        {
            //Create the d3d interface.
            Direct3D d3d = new Direct3D();

            //Get a handle to the WPF window. This is required to create a device.
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;

            //Create a device. Using standard creation param. 
            //Width and height have been set to 1 because we wont be using the backbuffer.
            //Adapter 0 = default adapter.
            PresentParameters presentationParams = new PresentParameters(1, 1);
            _d3dDevice = new Device(d3d, 0, DeviceType.Hardware, windowHandle, CreateFlags.HardwareVertexProcessing, presentationParams);
        }

        public void ScreenPreview(BitmapSource source)
        {
            int bytePerPixel = source.Format.BitsPerPixel / 8;
            int stride = source.PixelWidth * bytePerPixel;
            int pitch = bytePerPixel == 3 ? (source.PixelWidth * (24 >> 3) + 3) & ~3 : stride;
            byte[] buffer = new byte[stride * source.PixelHeight];

            source.CopyPixels(buffer, stride, 0);
            
            if (_d3dDevice == null)
            {
                return;
            }

            //Create an empty offscreen surface. Use SystemMemory to allow for surface copying.
            Surface imageSurface = Surface.CreateOffscreenPlain(_d3dDevice, source.PixelWidth, source.PixelHeight, Format.A8R8G8B8, Pool.SystemMemory);
            
            //Fill the surface with the image data.
            Surface.FromMemory(imageSurface, buffer, Filter.None, 0, bytePerPixel == 3 ? Format.R8G8B8 : Format.A8B8G8R8, pitch, new RawRectangle(0, 0, source.PixelWidth, source.PixelHeight));
            //Surface.FromFile(imageSurface, @"C:\Users\erics\Downloads\sample.png", Filter.None, 0);

            //Create the surface that will act as the render target.
            //Set as lockable (required for D3DImage)
            Surface target = Surface.CreateRenderTarget(_d3dDevice, source.PixelWidth, source.PixelHeight, Format.A8R8G8B8, MultisampleType.None, 0, true);

            //Copy the image surface contents into the target surface.
            _d3dDevice.UpdateSurface(imageSurface, target);

            d3dImage.Lock();
            d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, target.NativePointer);
            d3dImage.AddDirtyRect(new Int32Rect(0, 0, d3dImage.PixelWidth, d3dImage.PixelHeight));
            d3dImage.Unlock();
        }
    }
}
