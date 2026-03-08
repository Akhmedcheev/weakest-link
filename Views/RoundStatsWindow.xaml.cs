using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
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
                var analytics = _analyzer.AnalyzeRound(_roundDurationSeconds, _savedRoundBank);
                
                // Обновляем DataGrid со статистикой игроков
                var sortedStats = analytics.PlayerStats
                    .OrderByDescending(p => p.SuccessPercentage)  // Сначала по успешности
                    .ThenBy(p => p.TotalMistakes)                 // Затем по ошибкам (чем меньше, тем лучше)
                    .ThenByDescending(p => p.BankedMoney)         // Затем по деньгам в банк
                    .ToList();
                
                PlayersDataGrid.ItemsSource = null;  // Сбрасываем привязку
                PlayersDataGrid.ItemsSource = sortedStats;  // Устанавливаем новую коллекцию
                PlayersDataGrid.Items.Refresh();  // Принудительно обновляем
                
                // Обновляем прогноз выбывания с объяснением
                PredictionText.Text = $"Прогноз на выбывание: {analytics.EliminationPrediction}";
                
                // Добавляем объяснение прогноза
                if (!string.IsNullOrEmpty(analytics.EliminationReason))
                {
                    ExplanationText.Text = $"Причина: {analytics.EliminationReason}";
                    ExplanationText.Visibility = Visibility.Visible;
                }
                else
                {
                    ExplanationText.Visibility = Visibility.Collapsed;
                }
                
                // Обновляем общую статистику раунда
                TotalBankedText.Text = $"{analytics.TotalBanked:N0} ₽";
                TotalBurnedText.Text = $"{analytics.TotalBurned:N0} ₽";
                
                // Форматируем длительность раунда
                int minutes = _roundDurationSeconds / 60;
                int seconds = _roundDurationSeconds % 60;
                RoundDurationText.Text = $"{minutes}:{seconds:D2}";
                
                TotalQuestionsText.Text = analytics.TotalQuestionsAsked.ToString();
                AverageTimeText.Text = $"{analytics.AverageAnswerTime:F1} сек";
                
                StrongestLinkText.Text = analytics.StrongestLink;
                WeakestLinkText.Text = analytics.WeakestLink;

                if (!string.IsNullOrEmpty(analytics.StrongestLink))
                    _engine.LastStrongestLinkName = analytics.StrongestLink;
                
                // Обновляем главного перестраховщика (средний банк)
                if (!string.IsNullOrEmpty(analytics.PanicBanker))
                {
                    var panicPlayer = analytics.PlayerStats.FirstOrDefault(p => p.Name == analytics.PanicBanker);
                    PanicBankerText.Text = panicPlayer != null
                        ? $"{analytics.PanicBanker} (Средний банк: {panicPlayer.AverageBankAmount:N0} ₽)"
                        : analytics.PanicBanker;
                }
                else
                {
                    PanicBankerText.Text = "—";
                }
                
                // Обновляем эффективность
                EfficiencyText.Text = $"{analytics.EfficiencyPercentage:F1}%";
                
                // Проверяем ничью и показываем баннер
                bool isTie = _analyzer.IsTieDetected(_votes);
                if (isTie)
                {
                    TieBanner.Visibility = Visibility.Visible;
                    TieBannerText.Text = $"⚖️ НИЧЬЯ! Решает Сильное звено: {analytics.StrongestLink}";
                }
                else
                {
                    TieBanner.Visibility = Visibility.Collapsed;
                }

                // Выделяем сильное и слабое звено в таблице цветом
                HighlightSpecialPlayers(analytics, isTie);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке аналитики: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HighlightSpecialPlayers(StatsAnalyzer.RoundAnalytics analytics, bool isTie = false)
        {
            if (PlayersDataGrid.ItemsSource == null) return;

            var items = PlayersDataGrid.Items.Cast<StatsAnalyzer.PlayerRoundStats>().ToList();
            
            foreach (var item in items)
            {
                var row = PlayersDataGrid.ItemContainerGenerator.ContainerFromItem(item) as System.Windows.Controls.DataGridRow;
                if (row != null)
                {
                    if (item.Name == analytics.StrongestLink)
                    {
                        if (isTie)
                        {
                            row.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 80, 0));
                            row.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0));
                            row.BorderThickness = new Thickness(3);
                        }
                        else
                        {
                            row.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 80, 0));
                        }
                    }
                    else if (item.Name == analytics.WeakestLink)
                    {
                        row.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 0, 0));
                    }
                    else if (item.Name == analytics.PanicBanker)
                    {
                        row.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 0, 80));
                    }
                    else if (item.Name == analytics.EliminationPrediction && 
                             item.Name != analytics.WeakestLink)
                    {
                        row.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0));
                        row.BorderThickness = new Thickness(3);
                    }
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
                    MessageBox.Show("Нет данных для экспорта.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
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
                sb.AppendLine($"Длительность раунда;{RoundDurationText.Text}");
                sb.AppendLine($"Эффективность;{EfficiencyText.Text}");
                sb.AppendLine($"Сильное звено;{StrongestLinkText.Text}");
                sb.AppendLine($"Слабое звено;{WeakestLinkText.Text}");
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
                    MessageBox.Show("Статистика успешно выгружена!", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Обновляет аналитику (вызывается извне при изменении данных)
        /// </summary>
        public void RefreshAnalytics()
        {
            LoadAnalytics();
        }

        /// <summary>
        /// Показывает окно аналитики в модальном режиме
        /// </summary>
        public static void ShowAnalytics(GameEngine engine, int roundDurationSeconds, Window? owner = null, Dictionary<string, string>? votes = null)
        {
            var window = new RoundStatsWindow(engine, roundDurationSeconds, votes);
            if (owner != null)
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            window.ShowDialog();
        }
    }
}
