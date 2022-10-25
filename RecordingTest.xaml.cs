using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

using OpenCvSharp;

using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Lame;
using NAudio.Wave;

using BetterLiveScreen.Extensions;
using BetterLiveScreen.Recording;
using BetterLiveScreen.Recording.Audio;
using BetterLiveScreen.Recording.Video;

using Window = System.Windows.Window;
using Path = System.IO.Path;
using Size = System.Drawing.Size;
using WasapiCapture = BetterLiveScreen.Recording.Audio.WasapiCapture;
using Windows.ApplicationModel.Contacts;

namespace BetterLiveScreen
{
    /// <summary>
    /// RecordingTest.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class RecordingTest : Window
    {
        private static Stopwatch _sw = new Stopwatch();
        private static bool _playSliderDragStarted = false;

        public static string TestDirectory { get; set; } = @"C:\Users\erics\Downloads";
        public static string TestName { get; set; } = "cv";
        public static string TestVideoFilePath => Path.Combine(TestDirectory, TestName) + ".mp4";
        public static string TestAudioFilePath => Path.Combine(TestDirectory, TestName) + ".mp3";

        public RecordingTest()
        {
            InitializeComponent();
            if (!WasapiPlay.IsInitialized) WasapiPlay.Initialize();

            Loaded += RecordingTest_Loaded;
            Closing += RecordingTest_Closing;
        }

        private void RecordingTest_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeVideo();
        }

        private void RecordingTest_Closing(object sender, CancelEventArgs e)
        {
            xMedia.Close();
            WasapiPlay.Close();
        }

        public static void InitializeForAudioCaptureTest()
        {
            if (!WasapiCapture.IsInitialized) WasapiCapture.Initialize(new Action<WaveInEventArgs>((e) => AudioDataAvailable(e)));
        }

        public void InitializeVideo()
        {
            //Audio
            WasapiPlay.Read(TestAudioFilePath);
            WasapiPlay.Ready();

            //Video
            void TimerTickHandler(object sender, EventArgs e)
            {
                if (_playSliderDragStarted) return;
                if (xMedia.Source == null || !xMedia.NaturalDuration.HasTimeSpan)
                {
                    xPlayTimeLabel.Content = "No file selected...";
                    return;
                }

                xPlayTimeSlider.Value = xMedia.Position.TotalSeconds;
            }

            xMedia.Source = new Uri(TestVideoFilePath);
            xMedia.SpeedRatio = 1;

            DispatcherTimer timer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += TimerTickHandler;
            timer.Start();

            xMedia.Play();
            WasapiPlay.Play();

            xPlay.Content = "II";
        }

        public static async Task<bool> RecordTestAsync(int milliseconds, int width, int height, int fps, bool isHalf)
        {
            InitializeForAudioCaptureTest();

            Rescreen.ScreenSize = new Size(isHalf ? width / 2 : width, isHalf ? height / 2 : height);
            Rescreen.Fps = fps;
            _sw.Start();
            Rescreen.Start(isHalf);

            await Task.Delay(milliseconds);
            Rescreen.Stop();

            var writer = new VideoWriter();
            writer.Open(TestVideoFilePath, FourCC.H264, Rescreen.Fps, Rescreen.ScreenSize.ToCvSize());

            if (!writer.IsOpened())
            {
                MessageBox.Show("tlsqkf", "BetterLiveScreen : Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            string userName = MainWindow.User.ToString();
            
            //write video file
            while (Rescreen.VideoStreams[userName].ScreenQueue.Count > 0)
            {
                byte[] buffer = Rescreen.VideoStreams[userName].ScreenQueue.Dequeue();
                byte[] raw = buffer.Decompress();

                var src = new Mat(Rescreen.ScreenSize.Height, Rescreen.ScreenSize.Width, MatType.CV_8UC4);
                int length = Rescreen.ScreenSize.Width * Rescreen.ScreenSize.Height * 4; // or src.Height * src.Step;
                Marshal.Copy(raw, 0, src.Data, length);

                writer.Write(src);
                src.Dispose();
            }

            writer.Dispose();

            using (var writer2 = new LameMP3FileWriter(TestAudioFilePath, WasapiCapture.WaveFormat, 128))
            {
                while (Rescreen.VideoStreams[userName].AudioQueue.Count > 0)
                {
                    byte[] buffer = Rescreen.VideoStreams[userName].AudioQueue.Dequeue();
                    writer2.Write(buffer, 0, buffer.Length);
                }
            }

            var result = MessageBox.Show("hurary!\nopen video?", "BetterLiveScreen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var rt = new RecordingTest();
                rt.Show();
            }

            return true;
        }

        private static void AudioDataAvailable(WaveInEventArgs e)
        {
            _sw.Stop();

            byte[] buffer = new byte[e.BytesRecorded];

            if (e.BytesRecorded == 0)
            {
                int bytesPerMillisecond = WasapiCapture.WaveFormat.AverageBytesPerSecond / 1000;
                int bytesRecorded = (int)_sw.ElapsedMilliseconds * bytesPerMillisecond;

                buffer = new byte[bytesRecorded];
            }
            else
            {
                Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);
            }
            Rescreen.VideoStreams[MainWindow.User.ToString()].AudioQueue.Enqueue(buffer);

            _sw.Reset();
            _sw.Start();
        }

        private void xPlay_Click(object sender, RoutedEventArgs e)
        {
            if (WasapiPlay.IsPlaying)
            {
                xPlay.Content = "▶";
                xMedia.Pause();
                WasapiPlay.Pause();
            }
            else
            {
                xPlay.Content = "II";
                xMedia.Play();
                WasapiPlay.Play();
            }
        }

        private void xStop_Click(object sender, RoutedEventArgs e)
        {
            xPlay.Content = "▶";
            xMedia.Stop();
            WasapiPlay.Stop();
        }

        private void xMedia_MediaOpened(object sender, RoutedEventArgs e)
        {
            xPlayTimeSlider.Minimum = 0;
            xPlayTimeSlider.Maximum = xMedia.NaturalDuration.TimeSpan.TotalSeconds;
        }

        private void xMedia_MediaEnded(object sender, RoutedEventArgs e)
        {
            xMedia.Stop();
            WasapiPlay.Stop();

            xPlay.Content = "▶";
        }

        private void xMedia_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show($"Error occured while loading video : {e.ErrorException.Message}");
        }

        private void xPlayTimeSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            _playSliderDragStarted = true;

            xMedia.Pause();
            WasapiPlay.Pause();

            xPlay.Content = "▶";
        }

        private void xPlayTimeSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            TimeSpan position = TimeSpan.FromSeconds(xPlayTimeSlider.Value);

            xMedia.Position = position;
            WasapiPlay.SetTime(position);

            xMedia.Play();
            WasapiPlay.Play();

            xPlay.Content = "II";
            _playSliderDragStarted = false;
        }

        private void xPlayTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            xPlayTimeLabel.Content = string.Format("{0} / {1}", xMedia.Position.ToString(@"mm\:ss"), xMedia.NaturalDuration.TimeSpan.ToString(@"mm\:ss"));
        }
    }
}
