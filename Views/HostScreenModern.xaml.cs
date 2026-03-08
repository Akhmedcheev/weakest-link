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
    public partial class HostScreenModern : Window
    {
        private GameEngine _engine;
        private GameClient _client;
        private int _lastQuestionNumber = 0;
        private System.Windows.Shapes.Ellipse[] _p1Indicators;
        private System.Windows.Shapes.Ellipse[] _p2Indicators;

        private static readonly SolidColorBrush BrushGreen = new(Color.FromRgb(0x00, 0xCC, 0x00));
        private static readonly SolidColorBrush BrushYellow = new(Color.FromRgb(0xCC, 0xCC, 0x00));
        private static readonly SolidColorBrush BrushRed = new(Color.FromRgb(0xCC, 0x00, 0x00));
        private static readonly SolidColorBrush BrushDuelP1 = new(Color.FromRgb(0x00, 0x99, 0xFF));
        private static readonly SolidColorBrush BrushDuelP2 = new(Color.FromRgb(0xFF, 0x66, 0x00));
        private static readonly SolidColorBrush BrushDuelCorrect = new(Color.FromRgb(0x00, 0xCC, 0x00));
        private static readonly SolidColorBrush BrushDuelWrong = new(Color.FromRgb(0xCC, 0x00, 0x00));
        private static readonly SolidColorBrush BrushGray = new(Color.FromRgb(0x1A, 0x1A, 0x1A));

        public HostScreenModern(GameEngine engine)
        {
            InitializeComponent();
            _engine = engine;

            _p1Indicators = new System.Windows.Shapes.Ellipse[] { P1_I1, P1_I2, P1_I3, P1_I4, P1_I5 };
            _p2Indicators = new System.Windows.Shapes.Ellipse[] { P2_I1, P2_I2, P2_I3, P2_I4, P2_I5 };

            _client = new GameClient("127.0.0.1", 8888);
            _client.MessageReceived += OnMessageReceived;
            _client.Start();

            if (_engine != null)
            {
                _engine.BankChanged += (s, e) => UpdateBank(e.CurrentChainIndex, e.RoundBank);
                _engine.StateChanged += (s, e) => ApplyGameState(e.NewState);
                ApplyGameState(_engine.CurrentState);
            }

            UpdateBank(0, 0);
        }

        #region Парсинг ответа

        /// <summary>
        /// Разбирает строку ответа на основную часть и допустимые варианты в скобках.
        /// "Париж (Paris)" → main="Париж", alt="(Paris)"
        /// </summary>
        private static (string main, string alt) ParseAnswer(string answer)
        {
            if (string.IsNullOrEmpty(answer))
                return ("", "");

            int parenStart = answer.IndexOf('(');
            if (parenStart < 0)
                return (answer.Trim(), "");

            string main = answer.Substring(0, parenStart).Trim();
            string alt = answer.Substring(parenStart).Trim();
            return (main, alt);
        }

        #endregion

        #region Синхронное обновление дисплея

        /// <summary>
        /// Мгновенно обновляет все элементы экрана ведущего: вопрос, светофор, верхнюю панель.
        /// Вызывается синхронно через Dispatcher.Invoke — без задержек.
        /// </summary>
        public void SetDisplayData(string question, string answer, int questionNumber = 0, string? systemMessage = null)
        {
            _lastQuestionNumber = questionNumber;
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                HostRulesOverlay.Visibility = Visibility.Collapsed;
                EliminationOverlay.Visibility = Visibility.Collapsed;

                if (_engine.CurrentState != GameState.FinalDuel)
                    FinalDuelPanel.Visibility = Visibility.Collapsed;

                TxtQuestion.Text = question;

                var (main, alt) = ParseAnswer(answer);
                TxtAnswer.Text = main;
                TxtAnswerAlt.Text = alt;

                TxtSystemLine.Text = systemMessage ?? "";

                ResetBarColors();
                UpdatePlayingForInfo();

                string countStr = _lastQuestionNumber > 0 ? _lastQuestionNumber.ToString() : "";
                TxtHostQuestionInfo.Text = countStr.Length > 0 ? $"ВОПРОС {countStr}" : "";
            });
        }

        /// <summary>Возвращает плашки к стандартным цветам светофора.</summary>
        private void ResetBarColors()
        {
            GreenBar.Background = BrushGreen;
            YellowBar.Background = BrushYellow;
            RedBar.Background = BrushRed;
            TxtAnswer.Foreground = Brushes.White;
            TxtAnswerAlt.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x00));
            TxtSystemLine.Foreground = Brushes.White;
        }

        #endregion

        #region Сетевой приём

        private void OnMessageReceived(string message)
        {
            var parts = message.Split('|');
            if (parts.Length == 0) return;

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

        #endregion

        #region Публичные методы (API для OperatorPanel)

        public void ClearQuestionAndTimer()
        {
            Dispatcher.Invoke(() =>
            {
                TxtQuestion.Text = "";
                TxtQuestion.Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
                TxtAnswer.Text = "";
                TxtAnswer.Foreground = Brushes.White;
                TxtAnswerAlt.Text = "";
                TxtSystemLine.Text = "";
                TxtSystemLine.FontSize = 24;
                TxtHostTimer.Text = "0:00";
                ResetBarColors();
                StopPulseAnimation(RedBar);
            });
        }

        public void ShowRoundSummary(int roundBank)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                FinalDuelPanel.Visibility = Visibility.Collapsed;
                TxtHostQuestionInfo.Text = $"РАУНД {_engine.CurrentRound}";
                TxtQuestion.Text = "ИТОГИ РАУНДА";

                ResetBarColors();
                TxtAnswer.Text = $"В БАНКЕ: {roundBank:N0} ₽";
                TxtAnswerAlt.Text = "";
                TxtSystemLine.Text = "";
            });
        }

        public void ShowFullBank(int totalBank)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                FinalDuelPanel.Visibility = Visibility.Collapsed;
                var gold = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
                TxtHostQuestionInfo.Text = $"РАУНД {_engine.CurrentRound}";
                TxtQuestion.Text = "БАНК СОБРАН!";
                TxtQuestion.Foreground = gold;

                GreenBar.Background = gold;
                TxtAnswer.Text = $"ИТОГО: {totalBank:N0} ₽";
                TxtAnswer.Foreground = Brushes.Black;
                TxtAnswerAlt.Text = "";
                TxtSystemLine.Text = "";
            });
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

        /// <summary>
        /// Совместимый метод — вызывается из OperatorPanel через _hostScreen?.UpdateQuestion(...).
        /// Внутри делегирует в SetDisplayData с синхронным Dispatcher.Invoke.
        /// </summary>
        public void UpdateQuestion(string question, string answer, int count)
        {
            SetDisplayData(question, answer, count);
        }

        public void UpdateBank(int currentChainIndex, int bankedAmount)
        {
            Dispatcher.Invoke(() =>
            {
                TxtHostBanked.Text = bankedAmount.ToString("N0");
                UpdatePlayingForInfo();
            });
        }

        public void UpdateTimer(string timeString)
        {
            Dispatcher.Invoke(() =>
            {
                if (HostRulesOverlay.Visibility == Visibility.Visible)
                    HostRulesOverlay.Visibility = Visibility.Collapsed;

                TxtHostTimer.Text = timeString;
            });
        }

        private bool _votingUrgent = false;
        private System.Windows.Media.Animation.Storyboard? _pulseStoryboard;

        public void ShowVotingTimer(string timeStr, int secondsLeft)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                FinalDuelPanel.Visibility = Visibility.Collapsed;
                TxtHostQuestionInfo.Text = "ГОЛОСОВАНИЕ";
                TxtQuestion.Text = "";

                ResetBarColors();
                TxtAnswer.Text = "";
                TxtAnswerAlt.Text = "";

                RedBar.Background = (secondsLeft <= 10 && secondsLeft > 0) ? Brushes.Red : BrushRed;
                TxtSystemLine.Text = timeStr;
                TxtSystemLine.FontSize = 64;

                TxtHostTimer.Text = timeStr;
            });
        }

        public void SetVotingTimerUrgent(bool urgent)
        {
            Dispatcher.Invoke(() =>
            {
                _votingUrgent = urgent;
                if (urgent)
                {
                    StartPulseAnimation(RedBar);
                }
                else
                {
                    StopPulseAnimation(RedBar);
                    TxtSystemLine.FontSize = 24;
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

        public void UpdateCurrentPlayer(string playerName)
        {
            Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(playerName) && playerName != "-")
                    TxtCurrentPlayerName.Text = playerName.ToUpper();
                else
                    TxtCurrentPlayerName.Text = "";
            });
        }

        public void ShowDiscussion()
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                FinalDuelPanel.Visibility = Visibility.Collapsed;
                StopPulseAnimation(RedBar);
                TxtSystemLine.FontSize = 24;
                ResetBarColors();
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
                StopPulseAnimation(RedBar);
                ResetBarColors();
                TxtHostQuestionInfo.Text = "ВСКРЫТИЕ ГОЛОСОВ";
                TxtQuestion.Text = "";
                TxtQuestion.Foreground = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
                TxtAnswer.Text = "";
                TxtAnswerAlt.Text = "";
                TxtSystemLine.FontSize = 24;
                TxtSystemLine.Text = "";
                TxtHostTimer.Text = "";
                TxtHostBanked.Text = roundBank.ToString("N0");
            });
        }

        public void SetDuelDisplay(string p1, string p2, string s1, string s2)
        {
            UpdateDuelDisplay(p1, p2, s1, s2);
        }

        #endregion

        #region Состояния

        private void ApplyGameState(GameState state)
        {
            Dispatcher.Invoke(() =>
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
                EliminationOverlay.Visibility = Visibility.Collapsed;
                if (state == GameState.Playing || state == GameState.FinalDuel)
                    HostRulesOverlay.Visibility = Visibility.Collapsed;

                switch (state)
                {
                    case GameState.FinalDuel:
                        FinalDuelPanel.Visibility = Visibility.Visible;
                        TxtHostQuestionInfo.Text = "ФИНАЛЬНАЯ ДУЭЛЬ";

                        ApplyFinalDuelBarColors();
                        TxtSystemLine.Text = "ФИНАЛЬНАЯ ДУЭЛЬ";
                        break;

                    case GameState.RoundSummary:
                        FinalDuelPanel.Visibility = Visibility.Collapsed;
                        TxtHostQuestionInfo.Text = $"РАУНД {_engine.CurrentRound}";
                        TxtQuestion.Text = "ИТОГИ РАУНДА";
                        ResetBarColors();
                        TxtSystemLine.Text = "";
                        break;

                    case GameState.Voting:
                        FinalDuelPanel.Visibility = Visibility.Collapsed;
                        ClearQuestionAndTimer();
                        TxtHostQuestionInfo.Text = "ГОЛОСОВАНИЕ";
                        TxtSystemLine.Text = "ГОЛОСОВАНИЕ";
                        break;

                    case GameState.Discussion:
                        FinalDuelPanel.Visibility = Visibility.Collapsed;
                        StopPulseAnimation(RedBar);
                        TxtSystemLine.FontSize = 24;
                        ResetBarColors();
                        ClearQuestionAndTimer();
                        TxtHostQuestionInfo.Text = "КТО СЛАБОЕ ЗВЕНО?";
                        TxtSystemLine.Text = "";
                        break;

                    case GameState.Reveal:
                        FinalDuelPanel.Visibility = Visibility.Collapsed;
                        StopPulseAnimation(RedBar);
                        ResetBarColors();
                        TxtHostQuestionInfo.Text = "ВСКРЫТИЕ ГОЛОСОВ";
                        TxtSystemLine.FontSize = 24;
                        TxtSystemLine.Text = "";
                        TxtHostTimer.Text = "";
                        break;

                    case GameState.Elimination:
                        FinalDuelPanel.Visibility = Visibility.Collapsed;
                        ClearQuestionAndTimer();
                        TxtHostQuestionInfo.Text = "ВЫБЫВАНИЕ";
                        TxtSystemLine.Text = "";
                        break;

                    case GameState.RoundReady:
                        FinalDuelPanel.Visibility = Visibility.Collapsed;
                        ClearQuestionAndTimer();
                        TxtHostQuestionInfo.Text = $"РАУНД {_engine.CurrentRound}";
                        TxtSystemLine.Text = "ПОДГОТОВКА К РАУНДУ";
                        break;

                    default:
                        FinalDuelPanel.Visibility = Visibility.Collapsed;
                        ResetBarColors();
                        TxtSystemLine.Text = "";
                        UpdatePlayingForInfo();
                        break;
                }
            });
        }

        /// <summary>
        /// При финальной дуэли окрашивает плашки в цвета игроков:
        /// зелёная → цвет игрока 1 (синий), жёлтая → цвет игрока 2 (оранжевый),
        /// красная остаётся для результата.
        /// </summary>
        private void ApplyFinalDuelBarColors()
        {
            GreenBar.Background = BrushDuelP1;
            YellowBar.Background = BrushDuelP2;
            RedBar.Background = BrushRed;

            TxtAnswer.Foreground = Brushes.White;
            TxtAnswerAlt.Foreground = Brushes.White;
            TxtSystemLine.Foreground = Brushes.White;

            if (_engine != null)
            {
                string p1Name = _engine.ActivePlayers.Count > 0 ? _engine.ActivePlayers[0] : "Игрок 1";
                string p2Name = _engine.ActivePlayers.Count > 1 ? _engine.ActivePlayers[1] : "Игрок 2";
                TxtAnswer.Text = p1Name.ToUpper();
                TxtAnswerAlt.Text = p2Name.ToUpper();
            }
        }

        /// <summary>
        /// Обновляет плашки при ответе финалиста: подсвечивает зелёным (верно) или красным (неверно).
        /// isPlayer1 — true для первого финалиста, false для второго.
        /// </summary>
        public void SetDuelAnswerResult(bool isPlayer1, bool isCorrect)
        {
            Dispatcher.Invoke(() =>
            {
                var targetBar = isPlayer1 ? GreenBar : YellowBar;
                targetBar.Background = isCorrect ? BrushDuelCorrect : BrushDuelWrong;
            });
        }

        #endregion

        #region Дуэль

        private void UpdateDuelDisplay(string p1, string p2, string s1, string s2)
        {
            Dispatcher.Invoke(() =>
            {
                try
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

                    ApplyFinalDuelBarColors();

                    int p1Correct = p1Scores.Count(v => v.Trim() == "1");
                    int p2Correct = p2Scores.Count(v => v.Trim() == "1");
                    TxtAnswer.Text = $"{TxtFinalist1Name.Text}: {p1Correct}";
                    TxtAnswerAlt.Text = $"{TxtFinalist2Name.Text}: {p2Correct}";
                    TxtSystemLine.Text = "ФИНАЛЬНАЯ ДУЭЛЬ";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"HostScreenModern DUEL_UPDATE error: {ex.Message}");
                }
            });
        }

        private void UpdateIndicators(System.Windows.Shapes.Ellipse[] indicators, string[] scores)
        {
            for (int i = 0; i < indicators.Length; i++)
            {
                string val = (i < scores.Length) ? (scores[i] ?? "").Trim() : "-1";
                if (val == "1")
                    indicators[i].Fill = BrushDuelCorrect;
                else if (val == "0")
                    indicators[i].Fill = BrushDuelWrong;
                else
                    indicators[i].Fill = BrushGray;
            }
        }

        #endregion

        #region Оверлеи

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
            Dispatcher.Invoke(() =>
            {
                EliminationOverlay.Visibility = Visibility.Collapsed;
            });
        }

        private void ShowHostRules(string text)
        {
            Dispatcher.Invoke(() =>
            {
                if (text == "CLEAR")
                {
                    HostRulesOverlay.Visibility = Visibility.Collapsed;
                    return;
                }
                LogoOverlay.Visibility = Visibility.Collapsed;
                TxtHostRules.Text = text;
                HostRulesOverlay.Visibility = Visibility.Visible;
            });
        }

        #endregion

        #region Вспомогательные

        private void UpdatePlayingForInfo()
        {
            if (_engine == null) return;

            int currentChainIdx = _engine.CurrentChainIndex;

            int nextSum = 1000;
            if (currentChainIdx < _engine.BankChain.Length)
                nextSum = _engine.BankChain[currentChainIdx];
            else
                nextSum = _engine.BankChain.Last();

            TxtPlayingFor.Text = $"₽{nextSum:N0}";
        }

        #endregion

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
