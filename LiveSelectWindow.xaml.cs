using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

using BetterLiveScreen.Recording.Types;
using BetterLiveScreen.Recording.Video;
using BetterLiveScreen.Recording.Video.WGC;

namespace BetterLiveScreen
{
    /// <summary>
    /// LiveSelectWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LiveSelectWindow : Window
    {
        public bool IsAccepted { get; private set; } = false;
        public bool IsClosed { get; private set; } = false;

        public static List<MonitorInfo> Monitors { get; private set; } = new List<MonitorInfo>();

        public LiveSelectWindow()
        {
            InitializeComponent();

            this.Loaded += LiveSelectWindow_Loaded;
            this.Closing += LiveSelectWindow_Closing;
        }

        private void LiveSelectWindow_Loaded(object sender, RoutedEventArgs e)
        {
            DispatcherTimer timer = new DispatcherTimer();

            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += timer_Tick;
            timer.Start();


        }

        private void timer_Tick(object sender, EventArgs e)
        {
            //Monitor Refresh
            var monitors = MonitorEnumerationHelper.GetMonitors();
            var primary = RescreenSettings.PrimaryMonitor;

            if (Monitors.Count > 0 && Monitors[0] != primary) Monitors.Clear();
            if (Monitors.Count == 0) Monitors.Add(primary);

            foreach (var monitor in monitors)
            {
                if (monitor.DeviceName == primary.DeviceName) continue;
                if (!Monitors.Contains(monitor)) Monitors.Add(monitor);
            }

            if (Monitors.Count > 1)
            {
                monitor2Group.IsEnabled = true;
            }
            else if (Monitors.Count == 1)
            {
                monitor2Group.IsEnabled = false;
            }

            //Screen Refresh

        }

        private void LiveSelectWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsClosed = true;
        }

        private void cancel_Click(object sender, RoutedEventArgs e)
        {
            IsAccepted = false;
            IsClosed = true;

            this.Close();
        }

        private void goLive_Click(object sender, RoutedEventArgs e)
        {
            ComboBoxItem comboItem;
            string itemTag;

            //Monitor (max: 2, need to support above 3)
            if (monitor1Select.IsChecked.HasValue && monitor1Select.IsChecked.Value) //Monitor 1 (Primary)
            {
                Rescreen.Settings.SelectedMonitor = Monitors[0];
            }
            else if (monitor2Select.IsChecked.HasValue && monitor2Select.IsChecked.Value) //Monitor 2
            {
                Rescreen.Settings.SelectedMonitor = Monitors[1];
            }

            //Video
            //- Method
            comboItem = (ComboBoxItem)videoMethod.SelectedItem;
            itemTag = (string)comboItem.Tag;

            switch (itemTag)
            {
                case "Auto":
                    CaptureSupports.SupportsDesktopDuplication();
                    CaptureSupports.SupportsWGC();

                    if (!Rescreen.Supports.DesktopDuplication)
                    {
                        if (!Rescreen.Supports.WGC)
                        {
                            Debug.WriteLine("[Error] Desktop Duplication & WGC can't be started.");
                            MessageBox.Show("Go Live can't be started because any capture method doesn't supports on this computer.",
                                "Better Live Screen : Error", MessageBoxButton.OK, MessageBoxImage.Error);

                            return;
                        }
                        Rescreen.Settings.VideoType = CaptureVideoType.WGC;
                        Debug.WriteLine("[Info] Set to WGC instead of DD.");
                    }
                    else
                    {
                        Rescreen.Settings.VideoType = CaptureVideoType.DD;
                    }
                    break;

                default:
                    if (Enum.TryParse(itemTag, out CaptureVideoType video))
                        Rescreen.Settings.VideoType = video;
                    break;
            }

            //- Encoding
            comboItem = (ComboBoxItem)videoEncoding.SelectedItem;
            itemTag = (string)comboItem.Tag;

            switch (itemTag)
            {
                case "Auto":
                    CaptureSupports.SupportsNvenc();

                    if (!Rescreen.Supports.Nvenc)
                    {
                        Rescreen.Settings.Encoding = EncodingType.OpenH264;
                        Debug.WriteLine("[Info] Set to OpenH264 instead of NVENC.");
                    }
                    break;

                default:
                    if (Enum.TryParse(itemTag, out EncodingType encoding))
                        Rescreen.Settings.Encoding = encoding;
                    break;
            }

            // - Fps
            if (videoFps30.IsChecked.HasValue && videoFps30.IsChecked.Value)
            {
                Rescreen.Settings.Fps = 30;
            }
            else if (videoFps60.IsChecked.HasValue && videoFps60.IsChecked.Value)
            {
                Rescreen.Settings.Fps = 60;
            }

            // - Low Resolution
            Rescreen.Settings.IsHalf = videoLowResolution.IsChecked.HasValue && videoLowResolution.IsChecked.Value;

            // - Quality (same as Bitrate)
            comboItem = (ComboBoxItem)videoQuality.SelectedItem;
            itemTag = (string)comboItem.Tag;

            var bitrateInfo = Rescreen.GetBitrateInfoBySize(Rescreen.ScreenActualSize.Height, Rescreen.FpsIfUnfixed60);
            int mbps = bitrateInfo.MbpsAverage;

            switch (itemTag)
            {
                case "Low":
                    mbps = bitrateInfo.MbpsMin;
                    break;

                case "Medium":
                    mbps = bitrateInfo.MbpsAverage;
                    break;

                case "High":
                    mbps = bitrateInfo.MbpsMax;
                    break;
            }

            Rescreen.Settings.Bitrate = BitrateInfo.GetBitrateFromMbps(mbps);

            //Audio
            comboItem = (ComboBoxItem)audioMethod.SelectedItem;
            itemTag = (string)comboItem.Tag;

            if (Enum.TryParse(itemTag, out CaptureAudioType audio))
            {
                Rescreen.Settings.AudioType = audio;
            }

            IsAccepted = true;
            IsClosed = true;

            this.Close();
        }

        public async Task<bool> WaitAsyncUntilOK()
        {
            while (!IsClosed)
            {
                await Task.Delay(30);
            }
            return IsAccepted;
        }

        private void monitor1Select_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            monitor2Select.IsChecked = false;
        }

        private void monitor2Select_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            if (Monitors.Count < 2)
            {
                MessageBox.Show("The computer has only 1 monitor. you can select only primary monitor.", "Better Live Screen", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                monitor1Select.IsChecked = true;

                return;
            }

            monitor1Select.IsChecked = false;
        }
    }
}
