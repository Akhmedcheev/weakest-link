using System;
using System.Windows;
using System.Windows.Media;
using WeakestLink.Core.Services;

namespace WeakestLink.Views
{
    public partial class ServiceScreen : Window
    {
        public ServiceScreen()
        {
            InitializeComponent();
        }

        private async void BtnProgramPad_Click(object sender, RoutedEventArgs e)
        {
            BtnProgramPad.IsEnabled = false;
            TxtStatus.Text = "Подключение к макропаду...";
            TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xf1, 0xc4, 0x0f)); // Желтый

            var result = await MacroPadService.ProgramMacroPadAsync();

            TxtStatus.Text = result.Message;
            if (result.Success)
            {
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x2e, 0xcc, 0x71)); // Зеленый
            }
            else
            {
                TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xe7, 0x4c, 0x3c)); // Красный
            }

            BtnProgramPad.IsEnabled = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
