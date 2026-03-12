using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using WeakestLink.Core;
using WeakestLink.Network;

namespace WeakestLink.Views
{
    /// <summary>
    /// AudienceScreen — экран для зала (проектор).
    /// Показывает: раунд, банк, цепочку, имя текущего игрока, таймер, фазу игры.
    /// Вопрос НЕ показывается (спойлер для игроков).
    /// </summary>
    public partial class AudienceScreen : Window
    {
        private static readonly string AssetDir =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images");

        // ── Параметры цепочки (адаптивные к разрешению экрана) ─────────────────
        // Эталон: 1080p. Все размеры масштабируются пропорционально высоте канваса.
        private double ScaleFactor => Math.Max(0.5, ChainCanvas.ActualHeight / 1080.0);
        private double PlankWidth     => 240.0 * ScaleFactor;
        private double PlankHeight    => 70.0  * ScaleFactor;
        private double BankHeight     => 115.0 * ScaleFactor;
        private double StackedOverlap => 28.0  * ScaleFactor;
        private double BottomMargin   => 28.0  * ScaleFactor;
        private const double LeftPad  = 6.0;
        private double FontSize       => Math.Max(12, 24 * ScaleFactor);

        // Высота «слота» для каждой плашки выше стопки (активная + будущие).
        private double CalcSlotHeight()
        {
            double canvasH = ChainCanvas.ActualHeight;
            if (canvasH < 100) canvasH = 1080;
            double bankTop = BottomMargin + BankHeight + 8 * ScaleFactor;
            int passedCount = Math.Max(0, _activeIndex - 1);
            double stackTop = bankTop + passedCount * StackedOverlap;
            int futureCount = Math.Max(0, _amounts.Length - 1 - _activeIndex);
            int totalAboveStack = 1 + futureCount;
            double topLimit = canvasH * 0.92;
            double available = topLimit - stackTop;
            double slot = available / Math.Max(1, totalAboveStack);
            return Math.Clamp(slot, PlankHeight + 10 * ScaleFactor, PlankHeight * 1.6);
        }

        private readonly int[] _amounts = { 0, 1000, 2000, 5000, 10000, 20000, 30000, 40000, 50000 };
        private int _activeIndex = 1;
        private int _bankedAmount = 0;

        private readonly System.Collections.Generic.List<AudienceChainSlot> _slots = new();

        private readonly GameEngine _engine;
        private GameClient? _client;


        public AudienceScreen(GameEngine engine)
        {
            InitializeComponent();
            _engine = engine;

            _engine.BankChanged  += (s, e) => Dispatcher.BeginInvoke(() => UpdateBank(e.CurrentChainIndex, e.RoundBank));
            _engine.StateChanged += (s, e) => Dispatcher.BeginInvoke(() => ApplyState(e.NewState));

            Loaded      += (_, _) => BuildChain();
            SizeChanged += (_, _) => LayoutChain();

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
        // ЦЕПОЧКА
        // ════════════════════════════════════════════════════════════════════════

        private void BuildChain()
        {
            ChainCanvas.Children.Clear();
            _slots.Clear();

            for (int i = 0; i < _amounts.Length; i++)
            {
                bool isBank = (i == 0);
                double h = isBank ? BankHeight : PlankHeight;

                var grid = new Grid { Width = PlankWidth, Height = h };

                var img = new Image { Stretch = Stretch.Fill, Width = PlankWidth, Height = h };
                grid.Children.Add(img);

                var tb = new TextBlock
                {
                    FontFamily  = new FontFamily("Arial Black"),
                    FontWeight  = FontWeights.ExtraBold,
                    Foreground  = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                    TextAlignment       = TextAlignment.Center,
                    FontSize = isBank ? 22 : 24,
                    Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 3, ShadowDepth = 1, Opacity = 0.9 }
                };
                tb.Text = isBank ? "" : FormatAmount(_amounts[i]);
                grid.Children.Add(tb);

                Canvas.SetLeft(grid, LeftPad);
                ChainCanvas.Children.Add(grid);

                _slots.Add(new AudienceChainSlot { Index = i, Grid = grid, Image = img, Label = tb });
            }

            LayoutChain();
        }

        private void LayoutChain()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];

                bool isBank   = (i == 0);
                bool isPassed = (i > 0 && i < _activeIndex);
                bool isActive = (i == _activeIndex);

                // Все плашки одного размера (без масштабирования)
                double scaledW = PlankWidth;
                double scaledH = isBank ? BankHeight : PlankHeight;

                // Вертикальная позиция
                double bottom;
                string asset;

                double bankTop = BottomMargin + BankHeight + 8 * ScaleFactor;
                int passedCount = Math.Max(0, _activeIndex - 1);
                double stackTop = bankTop + passedCount * StackedOverlap;
                double slotH = CalcSlotHeight();

                if (isBank)
                {
                    bottom = BottomMargin;
                    asset  = "BANK.png";
                    slot.Image.Opacity = 0.80;
                    slot.Label.Text = _bankedAmount > 0 ? _bankedAmount.ToString() : "0";
                }
                else if (isPassed)
                {
                    bottom = bankTop + (i - 1) * StackedOverlap;
                    asset  = "moneytree_blue.png";
                    slot.Image.Opacity = 1.0;
                }
                else if (isActive)
                {
                    // Активная — первый слот над стопкой (slot 0)
                    bottom = stackTop + (slotH - PlankHeight) * 0.5;
                    asset  = "moneytree_red.png";
                    slot.Image.Opacity = 1.0;
                }
                else
                {
                    // Будущие — слоты 1, 2, 3... над активной
                    int slotIndex = i - _activeIndex;
                    bottom = stackTop + slotIndex * slotH + (slotH - PlankHeight) * 0.5;
                    asset  = "moneytree_blue.png";
                    slot.Image.Opacity = 1.0;
                }

                // Применяем
                slot.Grid.Width = scaledW;
                slot.Grid.Height = scaledH;
                slot.Image.Width = scaledW;
                slot.Image.Height = scaledH;
                slot.Image.Source = LoadAsset(asset);

                Canvas.SetBottom(slot.Grid, bottom);
                Canvas.SetLeft(slot.Grid, LeftPad);
                Canvas.SetZIndex(slot.Grid, isActive ? 100 : (isPassed ? i + 1 : i + 50));

                slot.Label.FontSize = FontSize;

                slot.Grid.RenderTransformOrigin = new Point(0.5, 0.5);
                if (isActive && _activeIndex > 0)
                {
                    var anim = new DoubleAnimation(1.0, 1.06, TimeSpan.FromMilliseconds(900))
                        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                    var st = new ScaleTransform(1, 1);
                    slot.Grid.RenderTransform = st;
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                }
                else
                {
                    slot.Grid.RenderTransform = new ScaleTransform(1, 1);
                }
            }
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
                TxtBank.Text = $"{bankedAmount:N0} ₽";
                LayoutChain();
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
                {
                    TxtPlayerName.Text  = "—";
                }
                else
                {
                    TxtPlayerName.Text  = name.ToUpper();
                }
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
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // ВСПОМОГАТЕЛЬНЫЕ
        // ════════════════════════════════════════════════════════════════════════

        private System.Windows.Media.ImageSource? LoadAsset(string file)
        {
            var path = System.IO.Path.Combine(AssetDir, file);
            return System.IO.File.Exists(path) ? new BitmapImage(new Uri(path)) : null;
        }

        private string FormatAmount(int v) =>
            v.ToString();

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
