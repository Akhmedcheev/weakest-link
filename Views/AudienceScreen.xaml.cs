using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WeakestLink.Core;
using WeakestLink.Network;

namespace WeakestLink.Views
{
    /// <summary>
    /// AudienceScreen — экран для игроков (проектор/телевизор в студии).
    /// Показывает: цепочку сумм, таймер, банк, финальную дуэль.
    /// Формат: российская версия "Слабое звено".
    /// </summary>
    public partial class AudienceScreen : Window
    {
        private static readonly string AssetDir =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images");

        private readonly int[] _amounts = { 0, 1000, 2000, 5000, 10000, 20000, 30000, 40000, 50000 };
        private int _activeIndex = 1;
        private int _bankedAmount = 0;

        private TextBlock[] _chainLabels = Array.Empty<TextBlock>();

        private readonly GameEngine _engine;
        private GameClient? _client;

        // Бейджи финала
        private ImageSource? _badgeBlue;
        private ImageSource? _badgeGreen;
        private ImageSource? _badgeRed;
        private ImageSource? _nameplateImage;
        private const double BadgeSize = 64.0;

        // Для совместимости
        private readonly List<AudienceChainSlot> _slots = new();

        public AudienceScreen(GameEngine engine)
        {
            InitializeComponent();
            _engine = engine;

            _engine.BankChanged  += (s, e) => Dispatcher.BeginInvoke(() => UpdateBank(e.CurrentChainIndex, e.RoundBank));
            _engine.StateChanged += (s, e) => Dispatcher.BeginInvoke(() => ApplyState(e.NewState));

            Loaded += (_, _) => { BuildChain(); LoadFinalAssets(); };

            try
            {
                _client = new GameClient("127.0.0.1", 8888);
                _client.MessageReceived += OnMessage;
                _client.Start();
            }
            catch { }

            ApplyState(_engine.CurrentState);
        }

        // ════════════════════════════════════════════════════════════════════════
        // ЗАГРУЗКА РЕСУРСОВ ДЛЯ ФИНАЛА
        // ════════════════════════════════════════════════════════════════════════

        private void LoadFinalAssets()
        {
            _badgeBlue      = LoadAsset("FIXED final_blue.png")  ?? LoadAsset("final_blue.png");
            _badgeGreen     = LoadAsset("FIXED final_green.png") ?? LoadAsset("final_green.png");
            _badgeRed       = LoadAsset("FIXED final_red.png")   ?? LoadAsset("final_red.png");
            _nameplateImage = LoadAsset("FIXED_NEW_GREY_UNUSED.PNG") ?? LoadAsset("final_nameplate.png");
        }

        // ════════════════════════════════════════════════════════════════════════
        // ЦЕПОЧКА — простые текстовые числа
        // ════════════════════════════════════════════════════════════════════════

        private void BuildChain()
        {
            ChainPanel.Children.Clear();
            _chainLabels = new TextBlock[_amounts.Length];

            for (int i = _amounts.Length - 1; i >= 0; i--)
            {
                var tb = new TextBlock
                {
                    Text = FormatAmount(_amounts[i]),
                    FontFamily = new FontFamily("Arial Black"),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 80,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 4),
                    HorizontalAlignment = HorizontalAlignment.Right,
                };

                _chainLabels[i] = tb;
                ChainPanel.Children.Add(tb);
            }

            UpdateChainColors();
        }

        private void UpdateChainColors()
        {
            if (_chainLabels.Length == 0) return;

            for (int i = 0; i < _chainLabels.Length; i++)
            {
                _chainLabels[i].Foreground = (i == _activeIndex)
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0x22, 0x22))
                    : Brushes.White;
            }
        }

        private string FormatAmount(int v)
        {
            if (v == 0) return "0";
            return v.ToString("N0").Replace(",", " ");
        }

        // ════════════════════════════════════════════════════════════════════════
        // ФИНАЛЬНАЯ ДУЭЛЬ
        // ════════════════════════════════════════════════════════════════════════

        public void ShowFinalDuel(string player1, string player2)
        {
            Dispatcher.BeginInvoke(() =>
            {
                TxtFinalPlayer1.Text = player1.ToUpper();
                TxtFinalPlayer2.Text = player2.ToUpper();

                if (_nameplateImage != null)
                {
                    ImgNameplate1.Source = _nameplateImage;
                    ImgNameplate2.Source = _nameplateImage;
                }

                FinalDuelOverlay.Visibility = Visibility.Visible;
                ChainPanel.Visibility = Visibility.Collapsed;
                RoundInfoPanel.Visibility = Visibility.Collapsed;

                RebuildFinalRow(FinalRow1, Enumerable.Repeat<bool?>(null, 5).ToList(), 0);
                RebuildFinalRow(FinalRow2, Enumerable.Repeat<bool?>(null, 5).ToList(), 0);
            });
        }

        public void UpdateFinalDuel()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (FinalDuelOverlay.Visibility != Visibility.Visible) return;

                var p1 = _engine.Player1FinalScores;
                var p2 = _engine.Player2FinalScores;

                int maxCount = Math.Max(p1.Count, p2.Count);
                int currentGroup = maxCount > 0 ? (maxCount - 1) / 5 : 0;
                int groupStart = currentGroup * 5;

                if (currentGroup > 0)
                {
                    bool allNullP1 = true, allNullP2 = true;
                    for (int j = groupStart; j < p1.Count; j++)
                        if (p1[j] != null) { allNullP1 = false; break; }
                    for (int j = groupStart; j < p2.Count; j++)
                        if (p2[j] != null) { allNullP2 = false; break; }
                    if (allNullP1 && allNullP2)
                        groupStart = (currentGroup - 1) * 5;
                }

                RebuildFinalRow(FinalRow1, p1, groupStart);
                RebuildFinalRow(FinalRow2, p2, groupStart);
            });
        }

        public void HideFinalDuel()
        {
            Dispatcher.BeginInvoke(() =>
            {
                FinalDuelOverlay.Visibility = Visibility.Collapsed;
                ChainPanel.Visibility = Visibility.Visible;
                RoundInfoPanel.Visibility = Visibility.Visible;
            });
        }

        private void RebuildFinalRow(StackPanel row, List<bool?> scores, int groupStart)
        {
            row.Children.Clear();
            for (int i = 0; i < 5; i++)
            {
                int idx = groupStart + i;
                bool? val = idx < scores.Count ? scores[idx] : null;
                row.Children.Add(CreateBadge(val, i));
            }
        }

        private FrameworkElement CreateBadge(bool? correct, int badgeIndex)
        {
            var grid = new Grid { Width = BadgeSize, Height = BadgeSize, Margin = new Thickness(6, 6, 6, 0) };

            ImageSource? src = correct == true  ? _badgeGreen
                             : correct == false ? _badgeRed
                             : _badgeBlue;

            if (src != null)
            {
                grid.Children.Add(new Image { Source = src, Stretch = Stretch.Uniform });
            }
            else
            {
                Color c1, c2;
                if (correct == true)       { c1 = Color.FromRgb(0x44, 0xFF, 0x44); c2 = Color.FromRgb(0x00, 0xAA, 0x00); }
                else if (correct == false)  { c1 = Color.FromRgb(0xFF, 0x44, 0x44); c2 = Color.FromRgb(0xCC, 0x00, 0x00); }
                else                        { c1 = Color.FromRgb(0x44, 0x88, 0xFF); c2 = Color.FromRgb(0x00, 0x44, 0xCC); }
                grid.Children.Add(new Ellipse { Width = BadgeSize, Height = BadgeSize, Fill = new RadialGradientBrush(c1, c2) });
            }

            if (!correct.HasValue)
            {
                grid.Children.Add(new TextBlock
                {
                    Text = (badgeIndex + 1).ToString(),
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Arial Black"),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, -10, 0, 0),
                    Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 3, ShadowDepth = 1, Opacity = 0.6 }
                });
            }

            return grid;
        }

        // ════════════════════════════════════════════════════════════════════════
        // ПУБЛИЧНЫЙ API
        // ════════════════════════════════════════════════════════════════════════

        public void UpdateBank(int chainIndex, int bankedAmount)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _activeIndex = Math.Max(1, chainIndex);
                _bankedAmount = bankedAmount;
                TxtBank.Text = bankedAmount.ToString("N0").Replace(",", " ");
                UpdateChainColors();
            });
        }

        public void UpdateTimer(string timeStr, int secsLeft = -1)
        {
            Dispatcher.BeginInvoke(() =>
            {
                TxtTimer.Text = timeStr;
                bool critical = secsLeft >= 0 && secsLeft <= 10;
                TxtTimer.Foreground = critical
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44))
                    : Brushes.White;
            });
        }

        public void UpdateCurrentPlayer(string name)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (string.IsNullOrWhiteSpace(name) || name == "-")
                    TxtPlayerName.Text = "—";
                else
                    TxtPlayerName.Text = name.ToUpper();
            });
        }

        public void UpdateRound(int round)
        {
            Dispatcher.BeginInvoke(() => TxtRound.Text = $"РАУНД {round}");
        }

        public void ShowPhase(string text)
        {
            Dispatcher.BeginInvoke(() =>
            {
                TxtPhase.Text = text;
                TxtPhase.Foreground = Brushes.White;
            });
        }

        public void ShowLogo()
        {
            Dispatcher.BeginInvoke(() =>
            {
                var path = System.IO.Path.Combine(AssetDir, "logotipka.png");
                if (System.IO.File.Exists(path))
                    ImgLogo.Source = new BitmapImage(new Uri(path));
                LogoOverlay.Visibility = Visibility.Visible;
            });
        }

        public void HideLogo()
        {
            Dispatcher.BeginInvoke(() => LogoOverlay.Visibility = Visibility.Collapsed);
        }

        public void ToggleLogo()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (LogoOverlay.Visibility == Visibility.Visible) HideLogo();
                else ShowLogo();
            });
        }

        // ════════════════════════════════════════════════════════════════════════
        // СОСТОЯНИЕ ДВИЖКА
        // ════════════════════════════════════════════════════════════════════════

        private void ApplyState(GameState state)
        {
            if (state == GameState.FinalDuel)
            {
                if (_engine.ActivePlayers.Count >= 2)
                    ShowFinalDuel(_engine.ActivePlayers[0], _engine.ActivePlayers[1]);
            }
            else if (state == GameState.Playing)
            {
                HideFinalDuel();
            }

            string phase = state switch
            {
                GameState.Playing      => $"РАУНД {_engine.CurrentRound}",
                GameState.Voting       => "🗳 ГОЛОСОВАНИЕ",
                GameState.Discussion   => "КТО СЛАБОЕ ЗВЕНО?",
                GameState.Reveal       => "ВСКРЫТИЕ ГОЛОСОВ",
                GameState.Elimination  => "СЛАБОЕ ЗВЕНО ВЫБЫВАЕТ",
                GameState.RoundSummary => "ИТОГИ РАУНДА",
                GameState.FinalDuel    => "⚔ ФИНАЛЬНАЯ ДУЭЛЬ",
                GameState.RoundReady   => "ПОДГОТОВКА К РАУНДУ",
                _                      => ""
            };

            ShowPhase(phase);

            if (state == GameState.Playing)
                UpdateRound(_engine.CurrentRound);
        }

        // ════════════════════════════════════════════════════════════════════════
        // TCP
        // ════════════════════════════════════════════════════════════════════════

        private void OnMessage(string msg)
        {
            var parts = msg.Split('|');
            if (parts.Length < 1) return;

            switch (parts[0])
            {
                case "UPDATE_BANK" when parts.Length >= 3:
                    if (int.TryParse(parts[1], out int ci) && int.TryParse(parts[2], out int ba))
                        UpdateBank(ci, ba);
                    break;
                case "TIMER" when parts.Length >= 2:
                    int secs = -1;
                    if (parts.Length >= 3) int.TryParse(parts[2], out secs);
                    UpdateTimer(parts[1], secs);
                    break;
                case "CURRENT_PLAYER" when parts.Length >= 2:
                    UpdateCurrentPlayer(parts[1]);
                    break;
                case "STATE" when parts.Length >= 2:
                    if (Enum.TryParse<GameState>(parts[1], out var st))
                        Dispatcher.BeginInvoke(() => ApplyState(st));
                    break;
                case "FINAL_UPDATE":
                    UpdateFinalDuel();
                    break;
                case "FINAL_SHOW" when parts.Length >= 3:
                    ShowFinalDuel(parts[1], parts[2]);
                    break;
                case "FINAL_HIDE":
                    HideFinalDuel();
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // ВСПОМОГАТЕЛЬНЫЕ
        // ════════════════════════════════════════════════════════════════════════

        private ImageSource? LoadAsset(string file)
        {
            var path = System.IO.Path.Combine(AssetDir, file);
            return System.IO.File.Exists(path) ? new BitmapImage(new Uri(path)) : null;
        }

        private void Window_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && WindowStyle == WindowStyle.None)
                DragMove();
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);
            ToggleFullscreen();
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.F11 || (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt)
                                 || (e.SystemKey == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt))
            {
                ToggleFullscreen(); e.Handled = true;
            }
            else if (e.Key == Key.Escape && WindowStyle == WindowStyle.None)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode  = ResizeMode.CanResize;
                WindowState = WindowState.Normal;
                e.Handled   = true;
            }
        }

        private void ToggleFullscreen()
        {
            if (WindowStyle == WindowStyle.None)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode  = ResizeMode.CanResize;
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                ResizeMode  = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _client?.Stop();
            base.OnClosed(e);
        }

        private class AudienceChainSlot
        {
            public int Index { get; set; }
            public Grid Grid { get; set; } = null!;
            public Image Image { get; set; } = null!;
            public TextBlock Label { get; set; } = null!;
        }
    }
}
