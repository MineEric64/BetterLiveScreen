using System;
using System.Collections.Generic;
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

namespace BetterLiveScreen.BetterShare
{
    /// <summary>
    /// Interaction logic for BetterShareWindow.xaml
    /// </summary>
    public partial class BetterShareWindow : Window
    {
        public bool IsClosed { get; private set; } = false;

        public BetterShareWindow()
        {
            InitializeComponent();
        }

        private void BetterShareWindow_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void BetterShareWindow_Closed(object sender, EventArgs e)
        {
            IsClosed = true;
        }

        private void btnMore_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
