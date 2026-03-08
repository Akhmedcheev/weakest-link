using System.Windows;
using System.Windows.Input;

namespace WeakestLink.Views
{
    public partial class DarkMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        private DarkMessageBox(string message, string title, MessageBoxButton buttons)
        {
            InitializeComponent();
            TxtMessage.Text = message;
            TxtTitle.Text = title;

            if (buttons == MessageBoxButton.YesNo || buttons == MessageBoxButton.YesNoCancel)
            {
                BtnOk.Content = "ДА";
                BtnCancel.Content = "НЕТ";
                BtnCancel.Visibility = Visibility.Visible;
            }
            else if (buttons == MessageBoxButton.OKCancel)
            {
                BtnCancel.Visibility = Visibility.Visible;
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            DialogResult = false;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            DialogResult = false;
            Close();
        }

        // ═══ Static API — drop-in замена MessageBox.Show() ═══

        public static MessageBoxResult Show(string message, string title = "Уведомление",
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None)
        {
            var owner = Application.Current?.MainWindow;
            var dlg = new DarkMessageBox(message, title, buttons);

            // Цвет кнопки OK зависит от типа сообщения
            switch (icon)
            {
                case MessageBoxImage.Error:
                    dlg.BtnOk.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xda, 0x37, 0x3c)); // красный
                    break;
                case MessageBoxImage.Warning:
                    dlg.BtnOk.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00)); // оранжевый
                    dlg.BtnOk.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0x1a, 0x1a, 0x1a));
                    break;
            }

            if (owner != null && owner.IsLoaded && owner.IsVisible)
            {
                dlg.Owner = owner;
            }

            dlg.ShowDialog();
            return dlg.Result;
        }
    }
}
