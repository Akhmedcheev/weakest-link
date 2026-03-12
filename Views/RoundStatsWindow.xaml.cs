using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WeakestLink.Core;
using WeakestLink.Core.Analytics;

namespace WeakestLink.Views
{
    public partial class RoundStatsWindow : Window
    {
        private readonly GameEngine _engine;
        private readonly StatsAnalyzer _analyzer;
        private readonly int _roundDurationSeconds;
        private readonly int _savedRoundBank;
        private Dictionary<string, string> _votes = new();
        
        private StatsAnalyzer.RoundAnalytics? _currentAnalytics;

        public RoundStatsWindow(GameEngine engine, int roundDurationSeconds, Dictionary<string, string>? votes = null, int savedRoundBank = -1)
        {
            InitializeComponent();
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _roundDurationSeconds = roundDurationSeconds;
            _savedRoundBank = savedRoundBank;
            _analyzer = new StatsAnalyzer(_engine);
            if (votes != null) _votes = votes;
            
            LoadAnalytics();
        }

        private void LoadAnalytics()
        {
            try
            {
                _currentAnalytics = _analyzer.AnalyzeRound(_roundDurationSeconds, _savedRoundBank);
                var analytics = _currentAnalytics;
                
                // Обновляем DataGrid со статистикой игроков
                var sortedStats = analytics.PlayerStats
                    .OrderByDescending(p => p.SuccessPercentage)  // Сначала по успешности
                    .ThenBy(p => p.TotalMistakes)                 // Затем по ошибкам (чем меньше, тем лучше)
                    .ThenByDescending(p => p.BankedMoney)         // Затем по деньгам в банк
                    .ToList();
                
                PlayersDataGrid.ItemsSource = null;
                PlayersDataGrid.ItemsSource = sortedStats;
                
                // Прогноз выбывания (красный акцент)
                PredictionText.Text = string.IsNullOrEmpty(analytics.EliminationPrediction) 
                    ? "НЕ ОПРЕДЕЛЕНО" 
                    : analytics.EliminationPrediction.ToUpper();
                
                // Добавляем объяснение
                if (!string.IsNullOrEmpty(analytics.EliminationReason))
                {
                    ExplanationText.Text = analytics.EliminationReason;
                    ExplanationText.Visibility = Visibility.Visible;
                }
                else
                {
                    ExplanationText.Visibility = Visibility.Collapsed;
                }
                
                // Метрики (Cards)
                TotalBankedText.Text = $"{analytics.TotalBanked:N0} ₽";
                TotalBurnedText.Text = $"{analytics.TotalBurned:N0} ₽";
                EfficiencyText.Text = $"{analytics.EfficiencyPercentage:F1}%";
                StrongestLinkText.Text = string.IsNullOrEmpty(analytics.StrongestLink) ? "—" : analytics.StrongestLink;

                if (!string.IsNullOrEmpty(analytics.StrongestLink))
                    _engine.LastStrongestLinkName = analytics.StrongestLink;
                
                PanicBankerText.Text = string.IsNullOrEmpty(analytics.PanicBanker) ? "—" : analytics.PanicBanker;
                
                // Плашка ничьей
                bool isTie = _analyzer.IsTieDetected(_votes);
                if (isTie)
                {
                    TieBanner.Visibility = Visibility.Visible;
                    TieBannerText.Text = $"⚖️ НИЧЬЯ (РЕШАЕТ {analytics.StrongestLink})";
                }
                else
                {
                    TieBanner.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                WeakestLink.Views.DarkMessageBox.Show($"Ошибка при загрузке аналитики: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GridPlayerStats_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is StatsAnalyzer.PlayerRoundStats playerStat && _currentAnalytics != null)
            {
                bool isStrongest = playerStat.Name == _currentAnalytics.StrongestLink;
                bool isWeakest = playerStat.Name == _currentAnalytics.WeakestLink;
                bool isPanicBanker = playerStat.Name == _currentAnalytics.PanicBanker;

                // Цвета фона строки
                if (isStrongest)
                {
                    // Мягкий зеленый оттенок (Opacity 0.1): #1A00FF00
                    e.Row.Background = new SolidColorBrush(Color.FromArgb(25, 0, 255, 0));
                }
                else if (isWeakest)
                {
                    // Мягкий красный оттенок (#da373c, Opacity ~0.15): #26da373c
                    e.Row.Background = new SolidColorBrush(Color.FromArgb(38, 218, 55, 60));
                }
                else
                {
                    e.Row.Background = Brushes.Transparent;
                }

                // Цвета рамок (Panic Banker получает фиолетовый акцент слева)
                if (isPanicBanker)
                {
                    e.Row.BorderThickness = new Thickness(4, 0, 0, 1);
                    e.Row.BorderBrush = new SolidColorBrush(Color.FromRgb(122, 42, 209)); // #7a2ad1
                }
                else
                {
                    e.Row.BorderThickness = new Thickness(0, 0, 0, 1);
                    e.Row.BorderBrush = new SolidColorBrush(Color.FromRgb(38, 38, 38)); // #262626
                }
            }
        }

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var players = PlayersDataGrid.ItemsSource as IEnumerable<StatsAnalyzer.PlayerRoundStats>;
                if (players == null || !players.Any())
                {
                    WeakestLink.Views.DarkMessageBox.Show("Нет данных для экспорта.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("Имя;Верно;Неверно;Пасы;Ошибки;В банк;Средний банк;Успешность");

                foreach (var p in players)
                {
                    string avg = p.BankPressCount > 0 ? $"{p.AverageBankAmount:N0}" : "—";
                    string success = p.TotalQuestions > 0 ? $"{p.SuccessPercentage:F1}%" : "—";
                    sb.AppendLine($"{p.Name};{p.CorrectAnswers};{p.IncorrectAnswers};{p.Passes};{p.TotalMistakes};{p.BankedMoney:N0} ₽;{avg} ₽;{success}");
                }

                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine("=== ИТОГИ РАУНДА ===");
                sb.AppendLine($"Собрано в банк;{TotalBankedText.Text}");
                sb.AppendLine($"Сгорело денег;{TotalBurnedText.Text}");
                sb.AppendLine($"Эффективность;{EfficiencyText.Text}");
                sb.AppendLine($"Сильное звено;{StrongestLinkText.Text}");
                sb.AppendLine($"Слабое звено;{_currentAnalytics?.WeakestLink}");
                sb.AppendLine($"Главный перестраховщик;{PanicBankerText.Text}");
                sb.AppendLine($"Прогноз на выбывание;{PredictionText.Text}");

                string defaultName = $"WeakestLink_RoundStats_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";

                var dialog = new SaveFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv",
                    FileName = defaultName,
                    Title = "Экспорт статистики раунда"
                };

                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
                    WeakestLink.Views.DarkMessageBox.Show("Статистика успешно выгружена!", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                WeakestLink.Views.DarkMessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        public void RefreshAnalytics()
        {
            LoadAnalytics();
        }

        public static void ShowAnalytics(GameEngine engine, int roundDurationSeconds, Window? owner = null, Dictionary<string, string>? votes = null)
        {
            var window = new RoundStatsWindow(engine, roundDurationSeconds, votes);
            if (owner != null)
            {
                window.Owner = window;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            window.ShowDialog();
        }
    }
}
