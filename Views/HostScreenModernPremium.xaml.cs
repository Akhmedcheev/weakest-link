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
    public partial class HostScreenModernPremium : Window
    {
        private GameEngine _engine;
        private GameClient _client;
        private int _lastQuestionNumber = 0;
        private System.Windows.Shapes.Ellipse[] _p1Indicators;
        private System.Windows.Shapes.Ellipse[] _p2Indicators;

        public HostScreenModernPremium(GameEngine engine)
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
                            if (_engine != null && _engine.CurrentState != newState)
                            {
                                try { _engine.TransitionTo(newState); } catch { }
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

        public void ClearQuestionAndTimer()
        {
            Dispatcher.Invoke(() =>
            {
                TxtQuestion.Text = "";
                TxtQuestion.Foreground = Brushes.White;
                TxtQuestion.FontSize = 58;
                TxtAnswer.Text = "";
                TxtAnswerAlt.Text = "";
                TxtSystemLine.Text = "";
                TxtHostTimer.Text = "0:00";
                TxtCurrentPlayerName.Text = "";
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
                TxtQuestion.Text = "БАНК СОБРАН!";
                TxtAnswer.Text = $"ИТОГО: {totalBank:N0} ₽";
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
                    FinalDuelPanel.Visibility = Visibility.Visible;
                    TxtHostQuestionInfo.Text = "ФИНАЛЬНАЯ ДУЭЛЬ";
                }
                else
                {
                    FinalDuelPanel.Visibility = Visibility.Collapsed;
                    if (state == GameState.RoundSummary || state == GameState.Voting || state == GameState.Discussion || state == GameState.Reveal || state == GameState.Elimination || state == GameState.RoundReady)
                    {
                        if (state != GameState.RoundSummary && state != GameState.Reveal)
                            ClearQuestionAndTimer();

                        if (state == GameState.RoundSummary) TxtHostQuestionInfo.Text = $"РАУНД {_engine.CurrentRound}";
                        else if (state == GameState.Voting) TxtHostQuestionInfo.Text = "ГОЛОСОВАНИЕ";
                        else if (state == GameState.Discussion) TxtHostQuestionInfo.Text = "КТО СЛАБОЕ ЗВЕНО?";
                        else if (state == GameState.Reveal) TxtHostQuestionInfo.Text = "ВСКРЫТИЕ ГОЛОСОВ";
                        else if (state == GameState.Elimination) TxtHostQuestionInfo.Text = "ВЫБЫВАНИЕ";
                        else TxtHostQuestionInfo.Text = "МЕЖДУРАУНДЬЕ";
                    }
                    else
                        UpdateHeaderInfo();
                }
            });
        }

        public void SetDuelDisplay(string p1, string p2, string s1, string s2)
        {
            UpdateDuelDisplay(p1, p2, s1, s2);
        }

        private void UpdateDuelDisplay(string p1, string p2, string s1, string s2)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                FinalDuelPanel.Visibility = Visibility.Visible;
                TxtHostQuestionInfo.Text = "ФИНАЛЬНАЯ ДУЭЛЬ";

                if (!string.IsNullOrWhiteSpace(p1)) TxtFinalist1Name.Text = p1.Trim().ToUpper();
                if (!string.IsNullOrWhiteSpace(p2)) TxtFinalist2Name.Text = p2.Trim().ToUpper();

                var p1Scores = (s1 ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var p2Scores = (s2 ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                UpdateIndicators(_p1Indicators, p1Scores);
                UpdateIndicators(_p2Indicators, p2Scores);
            });
        }

        private void UpdateIndicators(System.Windows.Shapes.Ellipse[] indicators, string[] scores)
        {
            var gray = new SolidColorBrush(Color.FromRgb(26, 26, 26)); // #1A1A1A
            for (int i = 0; i < indicators.Length; i++)
            {
                string val = (i < scores.Length) ? (scores[i] ?? "").Trim() : "-1";
                if (val == "1") indicators[i].Fill = Brushes.Green;
                else if (val == "0") indicators[i].Fill = Brushes.Red;
                else indicators[i].Fill = gray;
            }
        }

        private void ShowElimination(string name)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                TxtEliminatedName.Text = name;
                EliminationOverlay.Visibility = Visibility.Visible;
            });
        }

        private void ClearElimination()
        {
            Dispatcher.Invoke(() => EliminationOverlay.Visibility = Visibility.Collapsed);
        }

        private void ShowHostRules(string text)
        {
            Dispatcher.Invoke(() =>
            {
                if (text == "CLEAR") { HostRulesOverlay.Visibility = Visibility.Collapsed; return; }
                LogoOverlay.Visibility = Visibility.Collapsed;
                TxtHostRules.Text = text;
                HostRulesOverlay.Visibility = Visibility.Visible;
            });
        }

        public void UpdateQuestion(string question, string answer, int count)
        {
            _lastQuestionNumber = count;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                ApplyGameState(_engine.CurrentState); 
                HostRulesOverlay.Visibility = Visibility.Collapsed;

                bool questionChanged = TxtQuestion.Text != question;
                bool answerChanged = (TxtAnswer.Text + TxtAnswerAlt.Text + TxtSystemLine.Text) != answer;
                
                // Обработка расширенного формата ответов (Modern: Ответ|Альтернатива|Системное)
                var answerParts = (answer ?? "").Split('|');
                TxtAnswer.Text = answerParts.Length > 0 ? answerParts[0].Trim() : "";
                TxtAnswerAlt.Text = answerParts.Length > 1 ? answerParts[1].Trim() : "";
                TxtSystemLine.Text = answerParts.Length > 2 ? answerParts[2].Trim() : "";

                TxtQuestion.Text = question;

                if (questionChanged && !string.IsNullOrEmpty(question))
                {
                    var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["QuestionEntrance"];
                    sb?.Begin();
                }

                if (answerChanged && !string.IsNullOrEmpty(answer))
                {
                    var sb = (System.Windows.Media.Animation.Storyboard)this.Resources["BarEntrance"];
                    sb?.Begin();
                }

                UpdateHeaderInfo();
            }));
        }

        private void UpdateHeaderInfo()
        {
            if (_engine.CurrentState == GameState.FinalDuel) return;

            int currentChainIdx = _engine.CurrentChainIndex;
            int nextSum = (currentChainIdx < _engine.BankChain.Length) 
                          ? _engine.BankChain[currentChainIdx] 
                          : _engine.BankChain.Last();

            string countStr = _lastQuestionNumber > 0 ? _lastQuestionNumber.ToString() : "---";
            TxtHostQuestionInfo.Text = $"РАУНД {_engine.CurrentRound}  •  ВОПРОС {countStr}";
            TxtPlayingFor.Text = $"₽{nextSum:N0}";
        }

        public void UpdateBank(int currentChainIndex, int bankedAmount)
        {
            Dispatcher.Invoke(() =>
            {
                TxtHostBanked.Text = bankedAmount.ToString("N0");
                UpdateHeaderInfo();
            });
        }

        public void UpdateTimer(string timeString)
        {
            Dispatcher.Invoke(() => TxtHostTimer.Text = timeString);
        }

        public void ShowVotingTimer(string timeStr, int secondsLeft)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                TxtHostQuestionInfo.Text = "ГОЛОСОВАНИЕ";
                TxtQuestion.Text = timeStr;
                TxtQuestion.FontSize = 180;
                TxtHostTimer.Text = timeStr;
                TxtQuestion.Foreground = (secondsLeft <= 10) ? Brushes.Red : Brushes.White;
            });
        }

        private System.Windows.Media.Animation.Storyboard? _pulseStoryboard;
        private void StartPulseAnimation(UIElement target)
        {
            StopPulseAnimation(target);
            var anim = new System.Windows.Media.Animation.DoubleAnimation { From = 1.0, To = 0.4, Duration = TimeSpan.FromMilliseconds(500), AutoReverse = true, RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
            _pulseStoryboard = new System.Windows.Media.Animation.Storyboard();
            System.Windows.Media.Animation.Storyboard.SetTarget(anim, target);
            System.Windows.Media.Animation.Storyboard.SetTargetProperty(anim, new PropertyPath(OpacityProperty));
            _pulseStoryboard.Children.Add(anim);
            _pulseStoryboard.Begin();
        }
        private void StopPulseAnimation(UIElement target) { _pulseStoryboard?.Stop(); _pulseStoryboard = null; target.Opacity = 1.0; }

        public void UpdateCurrentPlayer(string playerName)
        {
            Dispatcher.Invoke(() => TxtCurrentPlayerName.Text = (playerName ?? "").ToUpper());
        }

        public void ClearScreen()
        {
            ShowLogo();
        }

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

        private bool _votingUrgent = false;

        public void SetVotingTimerUrgent(bool urgent)
        {
            Dispatcher.Invoke(() =>
            {
                _votingUrgent = urgent;
                if (urgent)
                {
                    StartPulseAnimation(TxtQuestion);
                }
                else
                {
                    StopPulseAnimation(TxtQuestion);
                }
            });
        }

        public void ShowDiscussion()
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                FinalDuelPanel.Visibility = Visibility.Collapsed;
                StopPulseAnimation(TxtQuestion);
                TxtHostQuestionInfo.Text = "КТО СЛАБОЕ ЗВЕНО?";
                TxtQuestion.Text = "";
                TxtAnswer.Text = "";
                TxtAnswerAlt.Text = "";
                TxtSystemLine.Text = "";
                TxtHostTimer.Text = "0:00";
            });
        }

        public void ShowReveal(int roundBank)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                FinalDuelPanel.Visibility = Visibility.Collapsed;
                StopPulseAnimation(TxtQuestion);
                TxtHostQuestionInfo.Text = "ВСКРЫТИЕ ГОЛОСОВ";
                TxtQuestion.Text = "";
                TxtQuestion.Foreground = Brushes.White;
                TxtAnswer.Text = "";
                TxtAnswerAlt.Text = "";
                TxtSystemLine.Text = "";
                TxtHostTimer.Text = "";
                TxtHostBanked.Text = roundBank.ToString("N0");
            });
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && WindowStyle == WindowStyle.None) this.DragMove();
        }

        private void ToggleChrome_Click(object sender, RoutedEventArgs e)
        {
            if (WindowStyle == WindowStyle.None) { WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize; }
            else { WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            if (WindowStyle == WindowStyle.None) { WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize; WindowState = WindowState.Normal; }
            else { WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowState = WindowState.Maximized; }
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.F11 || ((e.Key == Key.Enter || e.SystemKey == Key.Enter) && Keyboard.Modifiers == ModifierKeys.Alt))
            {
                if (WindowStyle == WindowStyle.None) { WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize; WindowState = WindowState.Normal; }
                else { WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowState = WindowState.Maximized; }
                e.Handled = true;
            }
        }
    }
}
