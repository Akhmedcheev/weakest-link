using System.Windows;
using System.Windows.Input;

namespace WeakestLink.Views
{
    public partial class WinWinner : Window
    {
        public WinWinner(string winnerName, int winnings)
        {
            InitializeComponent();
            TxtWinnerName.Text = winnerName.ToUpper();
            TxtWinnings.Text = winnings.ToString("N0") + " ₽";
            
            this.KeyDown += (s, e) => {
                if (e.Key == System.Windows.Input.Key.Escape) this.Close();
            };
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}
