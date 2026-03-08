using System;
using System.Windows;

namespace MoneyChain
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            UpdateLevelDisplay();
        }

        // ============== ОБРАБОТЧИКИ СОБЫТИЙ ==============

        private void PrevLevel_Click(object sender, RoutedEventArgs e)
        {
            BroadcastScreenControl.PreviousLevel();
            UpdateLevelDisplay();
        }

        private void NextLevel_Click(object sender, RoutedEventArgs e)
        {
            BroadcastScreenControl.NextLevel();
            UpdateLevelDisplay();
        }

        private void JumpToLevel_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (int.TryParse(btn.Tag.ToString(), out int levelIndex))
            {
                BroadcastScreenControl.SetLevel(levelIndex);
                UpdateLevelDisplay();
            }
        }

        private void ResetTimer_Click(object sender, RoutedEventArgs e)
        {
            BroadcastScreenControl.SecondsRemaining = 30;
            TimerStatusText.Text = "30 сек";
        }

        // ============== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==============

        private void UpdateLevelDisplay()
        {
            int currentLevel = BroadcastScreenControl.GetCurrentLevel();
            CurrentLevelText.Text = $"Уровень: {currentLevel}";
        }

        // ============== ОЧИСТКА РЕСУРСОВ ==============

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            BroadcastScreenControl.Dispose();
        }
    }
}
