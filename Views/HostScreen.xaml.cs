using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Linq;
using WeakestLink.Core;
using WeakestLink.Network;

namespace WeakestLink.Views
{
    public partial class HostScreen : Window
    {
        private GameEngine _engine;
        private GameClient _client;
        private int _lastQuestionNumber = 0;
        private System.Windows.Shapes.Ellipse[] _p1Indicators;
        private System.Windows.Shapes.Ellipse[] _p2Indicators;

        // Вспомогательный класс для отображения цепочки (аналогично OperatorPanel)
        public class BankChainItem
        {
            public int Value { get; set; }
            public bool IsActive { get; set; }
            public int Index { get; set; }
        }

        public HostScreen(GameEngine engine)
        {
            InitializeComponent();
            _engine = engine;

            // Инициализация массивов индикаторов для финала
            _p1Indicators = new System.Windows.Shapes.Ellipse[] { P1_I1, P1_I2, P1_I3, P1_I4, P1_I5 };
            _p2Indicators = new System.Windows.Shapes.Ellipse[] { P2_I1, P2_I2, P2_I3, P2_I4, P2_I5 };

            // Настройка сетевого клиента
            _client = new GameClient("127.0.0.1", 8888);
            _client.MessageReceived += OnMessageReceived;
            _client.Start();

            // Подписка на события ядра для автоматического обновления (локальная)
            if (_engine != null)
            {
                _engine.BankChanged += (s, e) => UpdateBank(e.CurrentChainIndex, e.RoundBank);
                _engine.StateChanged += (s, e) => ApplyGameState(e.NewState);
                
                // ПРИНУДИТЕЛЬНАЯ УСТАНОВКА СОСТОЯНИЯ ПРИ ОТКРЫТИИ
                ApplyGameState(_engine.CurrentState);
            }
            
            UpdateBank(0, 0);
        }

        private void OnMessageReceived(string message)
        {
            var parts = message.Split('|');
            if (parts.Length > 0)
            {
                switch (parts[0])
                {
                    case "STATE" when parts.Length >= 2:
                    case "SET_STATE" when parts.Length >= 2:
                        if (Enum.TryParse<GameState>(parts[1], out var newState))
                        {
                            // Синхронизируем локальный движок, если он в другом штате
                            if (_engine != null && _engine.CurrentState != newState)
                            {
                                try { _engine.TransitionTo(newState); } catch { /* Игнорируем ошибки перехода при синхронизации */ }
                            }
                            ApplyGameState(newState);
                        }
                        break;

                    case "DUEL_STATUS" when parts.Length >= 5:
                    case "DUEL_UPDATE" when parts.Length >= 5:
                        UpdateDuelDisplay(parts[1], parts[2], parts[3], parts[4]);
                        break;

                    case "CURRENT_PLAYER" when parts.Length >= 2:
                        UpdateCurrentPlayer(parts[1]);
                        break;

                    case "QUESTION" when parts.Length >= 4:
                        UpdateQuestion(parts[1], parts[2], int.Parse(parts[3]));
                        break;
                    case "QUESTION" when parts.Length >= 3:
                        UpdateQuestion(parts[1], parts[2], 0);
                        break;
                    case "ELIMINATE" when parts.Length >= 2:
                        ShowElimination(parts[1]);
                        break;
                    case "CLEAR_ELIMINATION":
                        ClearElimination();
                        break;
                    case "UPDATE_BANK" when parts.Length >= 3:
                        UpdateBank(int.Parse(parts[1]), int.Parse(parts[2]));
                        break;
                    case "HOST_MESSAGE" when parts.Length >= 2:
                        ShowHostRules(parts[1]);
                        break;
                    case "CLEAR_QUESTION":
                        ClearQuestionAndTimer();
                        break;
                }
            }
        }

        /// <summary>Очистка вопроса, ответа и обнуление таймера (цепочка и банк остаются).</summary>
        public void ClearQuestionAndTimer()
        {
            Dispatcher.Invoke(() =>
            {
                TxtQuestion.Text = "";
                TxtQuestion.Foreground = Brushes.White;
                TxtQuestion.FontSize = 88;
                TxtAnswer.Text = "";
                TxtHostTimer.Text = "0:00";
                TxtHostQuestionInfo.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x00));
                StopPulseAnimation(TxtQuestion);
            });
        }

        public void ShowRoundSummary(int roundBank)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                EliminationOverlay.Visibility = Visibility.Collapsed;
                HostRulesOverlay.Visibility = Visibility.Collapsed;
                TxtHostQuestionInfo.Text = $"РАУНД {_engine.CurrentRound}";
                TxtQuestion.Text = "ИТОГИ РАУНДА";
                TxtAnswer.Text = $"В БАНКЕ: {roundBank:N0} ₽";
            });
        }

        public void ShowFullBank(int totalBank)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                EliminationOverlay.Visibility = Visibility.Collapsed;
                HostRulesOverlay.Visibility = Visibility.Collapsed;
                TxtHostQuestionInfo.Text = $"РАУНД {_engine.CurrentRound}";
                TxtHostQuestionInfo.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
                TxtQuestion.Text = "БАНК СОБРАН!";
                TxtQuestion.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
                TxtAnswer.Text = $"ИТОГО: {totalBank:N0} ₽";
            });
        }

        /// <summary>Очистить экран ведущего (показать логотип).</summary>
        public void ClearScreen()
        {
            ShowLogo();
        }

        /// <summary>Переключить отображение логотипа: показать при скрытом, скрыть при показанном.</summary>
        public void ToggleLogoScreen()
        {
            Dispatcher.Invoke(() =>
            {
                if (LogoOverlay.Visibility == Visibility.Visible)
                {
                    LogoOverlay.Visibility = Visibility.Collapsed;
                    if (_engine != null)
                        ApplyGameState(_engine.CurrentState);
                }
                else
                {
                    ShowLogo();
                }
            });
        }

        private void ShowLogo()
        {
            Dispatcher.Invoke(() =>
            {
                ClearQuestionAndTimer();
                TxtHostQuestionInfo.Text = "";
                HostRulesOverlay.Visibility = Visibility.Collapsed;
                EliminationOverlay.Visibility = Visibility.Collapsed;
                LogoOverlay.Visibility = Visibility.Visible;
                var logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "logotipka.png");
                if (System.IO.File.Exists(logoPath))
                {
                    ImgLogo.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(logoPath));
                }
            });
        }

        private void ApplyGameState(GameState state)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                if (state == GameState.Playing || state == GameState.FinalDuel)
                    HostRulesOverlay.Visibility = Visibility.Collapsed;
                if (state == GameState.FinalDuel)
                {
                    MoneyTreeBorder.Visibility = Visibility.Collapsed;
                    MoneyTreeColumn.Width = new GridLength(0);
                    BottomInfoPanel.Visibility = Visibility.Collapsed;
                    FinalDuelPanel.Visibility = Visibility.Visible;
                    TxtHostQuestionInfo.Text = "ФИНАЛЬНАЯ ДУЭЛЬ";
                    TxtHostQuestionInfo.Foreground = System.Windows.Media.Brushes.White;
                    // Имена и кружки НЕ сбрасываем здесь — единственный источник правды: DUEL_UPDATE.
                    // Иначе STATE/SET_STATE, приходящие после DUEL_UPDATE, затирали бы данные с пульта.
                }
                else
                {
                    MoneyTreeBorder.Visibility = Visibility.Visible;
                    MoneyTreeColumn.Width = new GridLength(280);
                    BottomInfoPanel.Visibility = Visibility.Visible;
                    FinalDuelPanel.Visibility = Visibility.Collapsed;
                    // При переходе в Voting/Elimination/RoundReady — очищаем вопрос/ответ и таймер, цепочка и банк остаются
                    if (state == GameState.RoundSummary || state == GameState.Voting || state == GameState.Discussion || state == GameState.Reveal || state == GameState.Elimination || state == GameState.RoundReady)
                    {
                        if (state != GameState.RoundSummary && state != GameState.Reveal)
                            ClearQuestionAndTimer();

                        if (state == GameState.RoundSummary)
                            TxtHostQuestionInfo.Text = $"РАУНД {_engine.CurrentRound}";
                        else if (state == GameState.Voting)
                            TxtHostQuestionInfo.Text = "ГОЛОСОВАНИЕ";
                        else if (state == GameState.Discussion)
                            TxtHostQuestionInfo.Text = "КТО СЛАБОЕ ЗВЕНО?";
                        else if (state == GameState.Reveal)
                            TxtHostQuestionInfo.Text = "ВСКРЫТИЕ ГОЛОСОВ";
                        else if (state == GameState.Elimination)
                            TxtHostQuestionInfo.Text = "ВЫБЫВАНИЕ";
                        else
                            TxtHostQuestionInfo.Text = "ПОДГОТОВКА К РАУНДУ";
                        TxtHostQuestionInfo.Foreground = System.Windows.Media.Brushes.White;
                    }
                    else
                        UpdateHeaderInfo();
                }
            });
        }

        public void ShowDiscussion()
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                EliminationOverlay.Visibility = Visibility.Collapsed;
                HostRulesOverlay.Visibility = Visibility.Collapsed;
                StopPulseAnimation(TxtQuestion);
                TxtQuestion.FontSize = 88;
                TxtQuestion.Foreground = Brushes.White;
                TxtHostQuestionInfo.Text = "КТО СЛАБОЕ ЗВЕНО?";
                TxtQuestion.Text = "";
                TxtAnswer.Text = "";
                TxtHostTimer.Text = "0:00";
            });
        }

        public void ShowReveal(int roundBank)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                EliminationOverlay.Visibility = Visibility.Collapsed;
                HostRulesOverlay.Visibility = Visibility.Collapsed;
                StopPulseAnimation(TxtQuestion);
                TxtQuestion.FontSize = 88;
                TxtQuestion.Foreground = Brushes.White;
                TxtHostQuestionInfo.Text = "ВСКРЫТИЕ ГОЛОСОВ";
                TxtHostQuestionInfo.Foreground = Brushes.White;
                TxtQuestion.Text = "";
                TxtAnswer.Text = "";
                TxtHostBanked.Text = roundBank.ToString("N0");
                TxtHostTimer.Text = "";
            });
        }

        /// <summary>Прямое обновление дуэли (вызов с пульта в том же процессе или при разборе DUEL_UPDATE по TCP).</summary>
        public void SetDuelDisplay(string p1, string p2, string s1, string s2)
        {
            UpdateDuelDisplay(p1, p2, s1, s2);
        }

        private void UpdateDuelDisplay(string p1, string p2, string s1, string s2)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    LogoOverlay.Visibility = Visibility.Collapsed;
                    FinalDuelPanel.Visibility = Visibility.Visible;
                    BottomInfoPanel.Visibility = Visibility.Collapsed;
                    MoneyTreeBorder.Visibility = Visibility.Collapsed;
                    MoneyTreeColumn.Width = new GridLength(0);
                    TxtHostQuestionInfo.Text = "ФИНАЛЬНАЯ ДУЭЛЬ";
                    TxtHostQuestionInfo.Foreground = System.Windows.Media.Brushes.White;

                    if (!string.IsNullOrWhiteSpace(p1)) TxtFinalist1Name.Text = p1.Trim().ToUpper();
                    if (!string.IsNullOrWhiteSpace(p2)) TxtFinalist2Name.Text = p2.Trim().ToUpper();

                    var p1Scores = (s1 ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    var p2Scores = (s2 ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                    UpdateIndicators(_p1Indicators, p1Scores);
                    UpdateIndicators(_p2Indicators, p2Scores);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"HostScreen DUEL_UPDATE error: {ex.Message} | {p1}|{p2}|{s1}|{s2}");
                }
            });
        }

        private void UpdateIndicators(System.Windows.Shapes.Ellipse[] indicators, string[] scores)
        {
            var gray = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 34, 34)); // #222 из XAML
            for (int i = 0; i < indicators.Length; i++)
            {
                string val = (i < scores.Length) ? (scores[i] ?? "").Trim() : "-1";
                if (val == "1")
                    indicators[i].Fill = System.Windows.Media.Brushes.Green;
                else if (val == "0")
                    indicators[i].Fill = System.Windows.Media.Brushes.Red;
                else
                    indicators[i].Fill = gray;
            }
        }

        private void ShowElimination(string name)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                TxtEliminatedName.Text = name;
                EliminationOverlay.Visibility = Visibility.Visible;
            }));
        }

        private void ClearElimination()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                EliminationOverlay.Visibility = Visibility.Collapsed;
            }));
        }

        private void ShowHostRules(string text)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (text == "CLEAR")
                {
                    HostRulesOverlay.Visibility = Visibility.Collapsed;
                    return;
                }
                LogoOverlay.Visibility = Visibility.Collapsed;
                TxtHostRules.Text = text;
                HostRulesOverlay.Visibility = Visibility.Visible;
            }));
        }

        /// <summary>
        /// Обновление текста вопроса и правильного ответа.
        /// </summary>
        public void UpdateQuestion(string question, string answer, int count)
        {
            _lastQuestionNumber = count;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                ApplyGameState(_engine.CurrentState); // Гарантируем правильный режим UI
                HostRulesOverlay.Visibility = Visibility.Collapsed;
                TxtQuestion.Text = question;
                TxtAnswer.Text = answer;
                UpdateHeaderInfo();
            }));
        }

        private void UpdateHeaderInfo()
        {
            if (_engine.CurrentState == GameState.FinalDuel)
            {
                TxtHostQuestionInfo.Text = "ФИНАЛЬНАЯ ДУЭЛЬ";
                TxtHostQuestionInfo.Foreground = System.Windows.Media.Brushes.White;
                return;
            }

            int currentChainIdx = _engine.CurrentChainIndex;
            
            // Сумма, которую можно положить в банк сейчас
            int bankNow = 0;
            if (currentChainIdx > 0 && currentChainIdx <= _engine.BankChain.Length)
                bankNow = _engine.BankChain[currentChainIdx - 1];

            // Сумма, которая будет после следующего верного ответа
            int nextSum = 1000;
            if (currentChainIdx < _engine.BankChain.Length)
                nextSum = _engine.BankChain[currentChainIdx];
            else
                nextSum = _engine.BankChain.Last();

            string countStr = _lastQuestionNumber > 0 ? _lastQuestionNumber.ToString() : "---";
            
            // Заголовок: Сумма за следующий ответ + номер вопроса
            TxtHostQuestionInfo.Text = $"{nextSum:N0} ВОПРОС : {countStr}";
            TxtHostQuestionInfo.Foreground = System.Windows.Media.Brushes.Yellow;
            
            // Панель статуса
            TxtToBank.Text = (bankNow > 0) ? bankNow.ToString("N0") : "XXXX";
            TxtNextSum.Text = nextSum.ToString("N0");
        }

        /// <summary>
        /// Обновление визуализации денежной цепочки и текущего банка.
        /// </summary>
        public void UpdateBank(int currentChainIndex, int bankedAmount)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TxtHostBanked.Text = bankedAmount.ToString("N0");
                UpdateHeaderInfo();

                // Обновляем цепочку
                var items = new List<BankChainItem>();
                // В классическом виде 0 снизу, макс сверху.
                // Добавим 0 в начало списка (пустой шаг)
                items.Add(new BankChainItem { Value = 0, Index = 0, IsActive = (currentChainIndex == 0) });
                
                for (int i = 1; i <= _engine.BankChain.Length; i++)
                {
                    items.Add(new BankChainItem
                    {
                        Value = _engine.BankChain[i - 1],
                        Index = i,
                        IsActive = (currentChainIndex == i)
                    });
                }
                
                // Разворачиваем, чтобы максимум был сверху
                items.Reverse();
                HostBankChainList.ItemsSource = items;
            }));
        }

        /// <summary>
        /// Обновление таймера.
        /// </summary>
        public void UpdateTimer(string timeString)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Скрываем правила, если таймер запущен (началась игра)
                if (HostRulesOverlay.Visibility == Visibility.Visible)
                    HostRulesOverlay.Visibility = Visibility.Collapsed;

                TxtHostTimer.Text = timeString;
            }));
        }

        private bool _votingUrgent = false;
        private System.Windows.Media.Animation.Storyboard? _pulseStoryboard;

        public void ShowVotingTimer(string timeStr, int secondsLeft)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                EliminationOverlay.Visibility = Visibility.Collapsed;
                HostRulesOverlay.Visibility = Visibility.Collapsed;
                TxtHostQuestionInfo.Text = "ГОЛОСОВАНИЕ";
                TxtQuestion.Text = timeStr;
                TxtQuestion.FontSize = 200;
                TxtAnswer.Text = "";
                TxtHostTimer.Text = timeStr;

                if (secondsLeft <= 10 && secondsLeft > 0)
                    TxtQuestion.Foreground = Brushes.Red;
                else
                    TxtQuestion.Foreground = Brushes.White;
            });
        }

        public void SetVotingTimerUrgent(bool urgent)
        {
            Dispatcher.Invoke(() =>
            {
                _votingUrgent = urgent;
                if (urgent)
                {
                    TxtQuestion.Foreground = Brushes.Red;
                    StartPulseAnimation(TxtQuestion);
                }
                else
                {
                    TxtQuestion.Foreground = Brushes.White;
                    TxtQuestion.FontSize = 88;
                    StopPulseAnimation(TxtQuestion);
                }
            });
        }

        private void StartPulseAnimation(System.Windows.UIElement target)
        {
            StopPulseAnimation(target);
            var anim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1.0, To = 0.4,
                Duration = TimeSpan.FromMilliseconds(500),
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
            };
            _pulseStoryboard = new System.Windows.Media.Animation.Storyboard();
            System.Windows.Media.Animation.Storyboard.SetTarget(anim, target);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(anim, new PropertyPath(OpacityProperty));
            _pulseStoryboard.Children.Add(anim);
            _pulseStoryboard.Begin();
        }

        private void StopPulseAnimation(System.Windows.UIElement target)
        {
            _pulseStoryboard?.Stop();
            _pulseStoryboard = null;
            target.Opacity = 1.0;
        }

        /// <summary>
        /// Обновление текущего игрока.
        /// </summary>
        public void UpdateCurrentPlayer(string playerName)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Проверяем, есть ли у нас элемент для отображения текущего игрока
                // Если нет, можно использовать TxtHostQuestionInfo для временного отображения
                if (!string.IsNullOrEmpty(playerName) && playerName != "-")
                {
                    // Временно отображаем текущего игрока в заголовке вопроса
                    TxtHostQuestionInfo.Text = $"ХОД ИГРОКА: {playerName.ToUpper()}";
                    TxtHostQuestionInfo.Foreground = System.Windows.Media.Brushes.Yellow;
                }
                else
                {
                    // Восстанавливаем стандартный заголовок
                    UpdateHeaderInfo();
                }
            }));
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            bool isAltEnter = (e.Key == Key.Enter || e.SystemKey == Key.Enter)
                              && Keyboard.Modifiers == ModifierKeys.Alt;

            if (isAltEnter)
            {
                if (WindowStyle == WindowStyle.None)
                {
                    WindowStyle = WindowStyle.SingleBorderWindow;
                    ResizeMode = ResizeMode.CanResize;
                    WindowState = WindowState.Normal;
                }
                else
                {
                    WindowStyle = WindowStyle.None;
                    ResizeMode = ResizeMode.NoResize;
                    WindowState = WindowState.Maximized;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && WindowStyle == WindowStyle.None)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                WindowState = WindowState.Normal;
                e.Handled = true;
            }
        }
    }
}
