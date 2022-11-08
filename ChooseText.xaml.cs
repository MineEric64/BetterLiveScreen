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

namespace BetterLiveScreen
{
    /// <summary>
    /// ChooseText.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ChooseText : Window
    {
        public bool IsOKed { get; private set; } = false;
        public bool IsClosed { get; private set; } = false;

        public string Text { get; private set; } = string.Empty;

        public ChooseText()
        {
            InitializeComponent();

            this.KeyUp += ChooseText_KeyUp;
            this.Closing += ChooseText_Closing;
        }

        private void ChooseText_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsClosed = true;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            IsOKed = true;
            Text = textBox.Text;

            this.Close();
        }

        private void ChooseText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OK_Click(this, null);
            }
        }

        public async Task<string> WaitAsyncUntilOK()
        {
            while (!IsOKed && !IsClosed)
            {
                await Task.Delay(10);
            }
            return Text;
        }
    }
}