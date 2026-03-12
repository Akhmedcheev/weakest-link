using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WeakestLink.Core;
using WeakestLink.Network;

namespace WeakestLink.Views
{
    /// <summary>
    /// BroadcastWindow — оверлей для TV-эфира.
    /// Чёрный фон (хромакей). Цепочка слева (BANK.png для банка).
    /// Таймер внизу-справа (TIMER.png фон).
    /// Финальная дуэль: 2 ряда × 5 индикаторов из GOOD/BAD/NORMAL спрайтов.
    /// Информационная панель (раунд, банк, вопрос, игрок) — по F10.
    /// </summary>
    public partial class BroadcastWindow : Window
    {
        private static readonly string AssetDir =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images");

        // ── Параметры цепочки (адаптивные к разрешению экрана) ────────────────
        // Эталон: 1080p. Все размеры масштабируются пропорционально высоте канваса.
        private double ScaleFactor => Math.Max(0.5, ChainCanvas.ActualHeight / 1080.0);
        private double _widthMult = 0.9;   // чуть уже для пузатости
        private double _heightMult = 1.15;  // чуть выше для объёма
        private double _textTopOffset = -8; // смещение текста вверх (компенсация 3D-ободка)
        private double PlankWidth     => 250.0 * ScaleFactor * _widthMult;
        private double PlankHeight    => 75.0  * ScaleFactor * _heightMult;
        private double BankHeight     => 120.0 * ScaleFactor * Math.Min(_heightMult, 1.3);
        private double StackedOverlap => 30.0  * ScaleFactor;
        private double BottomMargin   => 30.0  * ScaleFactor;
        private const double LeftPadding = 6.0;
        private double FontSize       => Math.Max(14, 26 * ScaleFactor);

        /// <summary>Динамический FutureGap: заполняет ~75% высоты окна.</summary>
        // Высота «слота» для каждой плашки выше стопки (активная + будущие).
        // На ТВ все они распределены равномерно.
        private double CalcSlotHeight()
        {
            double canvasH = ChainCanvas.ActualHeight;
            if (canvasH < 100) canvasH = 1080;
            // Верхняя граница: оставляем место для верхней плашки
            double bankTop = BottomMargin + BankHeight + 8;
            int passedCount = Math.Max(0, _activeIndex - 1);
            double stackTop = bankTop + passedCount * StackedOverlap;
            int futureCount = Math.Max(0, _amounts.Length - 1 - _activeIndex);
            int totalAboveStack = 1 + futureCount;
            double topLimit = canvasH - PlankHeight;
            double available = topLimit - stackTop;
            double slot = available / Math.Max(1, totalAboveStack);
            return Math.Max(slot, PlankHeight * 0.5);
        }

        // ── Данные ──────────────────────────────────────────────────────────────
        private readonly int[] _amounts = { 0, 1000, 2000, 5000, 10000, 20000, 30000, 40000, 50000 };
        private int _activeIndex = 1;
        private int _bankedTotal = 0;
        private readonly List<ChainSlot> _slots = new();

        // ── Бейджи финала (отдельные PNG файлы) ───────────────────────────────
        private ImageSource? _badgeBlue;
        private ImageSource? _badgeGreen;
        private ImageSource? _badgeRed;
        private ImageSource? _nameplateImage;
        private double BadgeSize => 64.0 * _widthMult;

        // ── Текущий набор ассетов (по умолчанию NEW, переключается CLASSIC/NEW) ──
        private string _assetBank  = "FIXED_NEW_BANK.png";
        private string _assetBlue  = "FIXED_NEW_moneytree_blue.png";
        private string _assetRed   = "FIXED_NEW_moneytree_red.png";
        private string _assetTimer = "FIXED_NEW_TIMER.png";
        private string _assetNameplate = "FIXED_NEW_GREY_UNUSED.PNG";

        // ── Движок / сеть ────────────────────────────────────────────────────────
        private readonly GameEngine _engine;
        private GameClient? _client;
        private bool _timerCritical = false;

        public BroadcastWindow(GameEngine engine)
        {
            InitializeComponent();
            _engine = engine;

            _engine.BankChanged  += (s, e) => Dispatcher.BeginInvoke(() => UpdateBank(e.CurrentChainIndex, e.RoundBank));
            _engine.StateChanged += (s, e) => Dispatcher.BeginInvoke(() => ApplyState(e.NewState));

            Loaded      += (_, _) => { BuildChain(); LoadExtras(); };
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
        // ЗАГРУЗКА РЕСУРСОВ
        // ════════════════════════════════════════════════════════════════════════

        private void LoadExtras()
        {
            var timerSrc = LoadAsset(_assetTimer);
            if (timerSrc != null)
            {
                ImgTimerBg.Source = timerSrc;
            }
            else
            {
                // Фолбэк: если файл нет — рисуем фон программно
                TimerBorder.Background = new System.Windows.Media.LinearGradientBrush(
                    Color.FromRgb(0x00, 0x1a, 0x3a), Color.FromRgb(0x00, 0x2a, 0x5c),
                    new Point(0, 0), new Point(0, 1));
            }

            // Таймер: размер и смещение текста
            TxtTimer.Margin = new Thickness(0, 4, 0, 0);
            TimerBorder.Width  = 250 * _widthMult;
            TimerBorder.Height = 130 * Math.Min(_heightMult, 1.3);

            _badgeBlue      = LoadAsset("FIXED final_blue.png")  ?? LoadAsset("final_blue.png");
            _badgeGreen     = LoadAsset("FIXED final_green.png") ?? LoadAsset("final_green.png");
            _badgeRed       = LoadAsset("FIXED final_red.png")   ?? LoadAsset("final_red.png");
            _nameplateImage = LoadAsset(_assetNameplate);

            var bankSrc = LoadAsset("overlay_bank.png");
            if (bankSrc != null) ImgBankOverlay.Source = bankSrc;
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
                    FontFamily = new FontFamily("Arial Black"),
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                    TextAlignment       = TextAlignment.Center,
                    FontSize = isBank ? 26 : 28,
                    Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 2, Opacity = 0.9 }
                };
                tb.Text = isBank ? "" : FormatAmount(_amounts[i]);
                grid.Children.Add(tb);

                Canvas.SetLeft(grid, LeftPadding);
                ChainCanvas.Children.Add(grid);
                _slots.Add(new ChainSlot { Index = i, Grid = grid, Image = img, Label = tb });
            }
            LayoutChain();
        }

        private void LayoutChain()
        {
            // Эфирный стиль цепочки (как на ТВ):
            // • Все плашки одинакового размера (без перспективы)
            // • Пройденные — плотная стопка «монет» полного размера
            // • Активная — красная, чётко отделена сверху от стопки
            // • Будущие — равномерно распределены над активной

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];

                bool isBank   = (i == 0);
                bool isPassed = (i > 0 && i < _activeIndex);
                bool isActive = (i == _activeIndex);

                // Размеры плашки (50000 — крупнее как на ТВ)
                bool isTopPrize = (i == _amounts.Length - 1);
                double topScale = isTopPrize ? 1.2 : 1.0;
                double scaledW = PlankWidth * topScale;
                double scaledH = (isBank ? BankHeight : PlankHeight) * topScale;

                // ── Вертикальная позиция (bottom-up) ──
                double bottom;
                string asset;
                Color glow;

                // Высота стопки пройденных (банк + пройденные уровни)
                double bankTop = BottomMargin + BankHeight + 8;
                int passedCount = Math.Max(0, _activeIndex - 1);
                double stackTop = bankTop + passedCount * StackedOverlap;
                double slotH = CalcSlotHeight();

                if (isBank)
                {
                    bottom = BottomMargin;
                    asset  = _assetBank;
                    glow   = Color.FromRgb(100, 100, 120);
                    slot.Image.Opacity = 0.80;
                    slot.Label.Text = _bankedTotal > 0 ? _bankedTotal.ToString() : "0";
                }
                else if (isPassed)
                {
                    bottom = bankTop + (i - 1) * StackedOverlap;
                    asset  = _assetBlue;
                    glow   = Color.FromRgb(0, 60, 140);
                    slot.Image.Opacity = 1.0;
                }
                else if (isActive)
                {
                    // Активная — первый слот над стопкой (slot 0)
                    bottom = stackTop + (slotH - PlankHeight) * 0.5;
                    asset  = _assetRed;
                    glow   = Color.FromRgb(200, 20, 20);
                    slot.Image.Opacity = 1.0;
                }
                else // future
                {
                    // Будущие — слоты 1, 2, 3... над активной
                    int slotIndex = i - _activeIndex;
                    bottom = stackTop + slotIndex * slotH + (slotH - PlankHeight) * 0.5;
                    asset  = _assetBlue;
                    glow   = Color.FromRgb(0, 80, 160);
                    slot.Image.Opacity = 1.0;
                }

                // ── Применяем ──
                slot.Grid.Width  = scaledW;
                slot.Grid.Height = scaledH;
                slot.Image.Width  = scaledW;
                slot.Image.Height = scaledH;
                slot.Image.Source = LoadAsset(asset);

                Canvas.SetBottom(slot.Grid, bottom);
                // Центрируем все плашки относительно самой широкой (50000)
                double maxW = PlankWidth * (_heightMult > 1.0 ? 1.15 : 1.0);
                double left = LeftPadding + (maxW - scaledW) / 2.0;
                Canvas.SetLeft(slot.Grid, left);
                Canvas.SetZIndex(slot.Grid, isActive ? 100 : (isPassed ? i + 1 : i + 50));

                slot.Label.FontSize = isBank ? FontSize * 1.4 : FontSize;
                slot.Label.Margin = new Thickness(0, isBank ? 0 : _textTopOffset, 0, 0);

                // Свечение
                if (slot.Grid.Effect is DropShadowEffect dse) dse.Color = glow;
                else slot.Grid.Effect = new DropShadowEffect { Color = glow, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.9 };

                // Пульс активной плашки
                slot.Grid.RenderTransformOrigin = new Point(0.5, 0.5);
                if (isActive && _activeIndex > 0) StartPulse(slot.Grid);
                else { StopPulse(slot.Grid); slot.Grid.RenderTransform = new ScaleTransform(1, 1); }
            }
        }

        private void StartPulse(FrameworkElement el)
        {
            var anim = new DoubleAnimation { From = 1.0, To = 1.06, Duration = TimeSpan.FromMilliseconds(900), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            var st = new ScaleTransform(1, 1); el.RenderTransform = st; el.RenderTransformOrigin = new Point(0.5, 0.5);
            st.BeginAnimation(ScaleTransform.ScaleXProperty, anim); st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        private void StopPulse(FrameworkElement el)
        {
            if (el.RenderTransform is ScaleTransform st) { st.BeginAnimation(ScaleTransform.ScaleXProperty, null); st.BeginAnimation(ScaleTransform.ScaleYProperty, null); }
        }

        // ════════════════════════════════════════════════════════════════════════
        // ФИНАЛЬНАЯ ДУЭЛЬ
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>Показать оверлей финальной дуэли с именами игроков.</summary>
        public void ShowFinalDuel(string player1, string player2)
        {
            Dispatcher.BeginInvoke(() =>
            {
                TxtFinalPlayer1.Text = player1.ToUpper();
                TxtFinalPlayer2.Text = player2.ToUpper();

                TxtSuddenDeath.Visibility = Visibility.Collapsed;

                // Установить картинку плашки имени
                if (_nameplateImage != null)
                {
                    ImgNameplate1.Source = _nameplateImage;
                    ImgNameplate2.Source = _nameplateImage;
                }

                // Масштабирование плашек имён для текущего стиля
                double npW = 260 * _widthMult;
                double npH = 120; // пузатые плашки имён
                double fontSize = 22 * _widthMult;
                var np1Grid = (Grid)ImgNameplate1.Parent;
                var np2Grid = (Grid)ImgNameplate2.Parent;
                np1Grid.Width = npW; np1Grid.Height = npH;
                np2Grid.Width = npW; np2Grid.Height = npH;
                TxtFinalPlayer1.FontSize = fontSize;
                TxtFinalPlayer2.FontSize = fontSize;
                TxtFinalPlayer1.Margin = new Thickness(0);
                TxtFinalPlayer2.Margin = new Thickness(0);

                FinalDuelOverlay.Visibility = Visibility.Visible;
                StatusOverlay.Visibility = Visibility.Collapsed;
                ChainCanvas.Visibility = Visibility.Collapsed;
                TimerBorder.Visibility = Visibility.Collapsed;
                RebuildFinalRow(FinalRow1, Enumerable.Repeat<bool?>(null, 5).ToList(), 0);
                RebuildFinalRow(FinalRow2, Enumerable.Repeat<bool?>(null, 5).ToList(), 0);
            });
        }

        /// <summary>Обновить состояние финальной дуэли из движка.</summary>
        public void UpdateFinalDuel()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (FinalDuelOverlay.Visibility != Visibility.Visible) return;

                var p1 = _engine.Player1FinalScores;
                var p2 = _engine.Player2FinalScores;

                // Определяем текущую группу из 5 — единую для обоих рядов
                int maxCount = Math.Max(p1.Count, p2.Count);
                int currentGroup = maxCount > 0 ? (maxCount - 1) / 5 : 0;
                int groupStart = currentGroup * 5;

                // Если текущая группа содержит только null у ОБОИХ —
                // показываем предыдущую (чтобы последний ответ был виден)
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

        // ═══════════════════════════════════════════════════
        // 📺 ГРАФИКА — toggle visibility methods
        // ═══════════════════════════════════════════════════

        public void SetTimerVisibility(bool visible)
        {
            Dispatcher.BeginInvoke(() => TimerBorder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed);
        }

        public void SetChainVisibility(bool visible)
        {
            Dispatcher.BeginInvoke(() => ChainCanvas.Visibility = visible ? Visibility.Visible : Visibility.Collapsed);
        }

        public void ShowBankOverlay(int totalBank)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (BankOverlay == null) return;
                TxtBankOverlayAmount.Text = totalBank.ToString("N0");
                BankOverlay.Visibility = Visibility.Visible;
            });
        }

        public void HideBankOverlay()
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (BankOverlay != null)
                    BankOverlay.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>Переключить набор ассетов (classic / new).</summary>
        public void SwitchStyle(bool useNew)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (useNew)
                {
                    _assetBank  = "FIXED_NEW_BANK.png";
                    _assetBlue  = "FIXED_NEW_moneytree_blue.png";
                    _assetRed   = "FIXED_NEW_moneytree_red.png";
                    _assetTimer = "FIXED_NEW_TIMER.png";
                    _assetNameplate = "FIXED_NEW_GREY_UNUSED.PNG";
                    _widthMult = 0.9;
                    _heightMult = 1.15;
                    _textTopOffset = -8;
                    _badgeBlue  = LoadAsset("FIXED final_blue.png")  ?? _badgeBlue;
                    _badgeGreen = LoadAsset("FIXED final_green.png") ?? _badgeGreen;
                    _badgeRed   = LoadAsset("FIXED final_red.png")   ?? _badgeRed;
                }
                else
                {
                    _assetBank  = "BANK.png";
                    _assetBlue  = "moneytree_blue.png";
                    _assetRed   = "moneytree_red.png";
                    _assetTimer = "TIMER.png";
                    _assetNameplate = "final_nameplate.png";
                    _widthMult = 1.0;
                    _heightMult = 1.0;
                    _textTopOffset = 0;
                    _badgeBlue  = LoadAsset("final_blue.png");
                    _badgeGreen = LoadAsset("final_green.png");
                    _badgeRed   = LoadAsset("final_red.png");
                }

                // Таймер — перезагрузить фон
                var timerSrc = LoadAsset(_assetTimer);
                if (timerSrc != null) ImgTimerBg.Source = timerSrc;
                TxtTimer.Margin = new Thickness(0, 4, 0, 0);
                TimerBorder.Width  = 250 * _widthMult;
                TimerBorder.Height = 130 * Math.Min(_heightMult, 1.3);

                // Nameplate для финала
                _nameplateImage = LoadAsset(_assetNameplate);

                // Перестроить цепочку с новыми ассетами
                LayoutChain();
            });
        }

        /// <summary>Скрыть оверлей финальной дуэли.</summary>
        public void HideFinalDuel()
        {
            Dispatcher.BeginInvoke(() =>
            {
                FinalDuelOverlay.Visibility = Visibility.Collapsed;
                ChainCanvas.Visibility = Visibility.Visible;
                TimerBorder.Visibility = Visibility.Visible;
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

        /// <summary>
        /// Создаёт бейдж финальной дуэли:
        /// null = синяя плашка с номером, true = зелёная ✓, false = красная ✗
        /// </summary>
        private FrameworkElement CreateBadge(bool? correct, int badgeIndex)
        {
            var grid = new Grid { Width = BadgeSize, Height = BadgeSize, Margin = new Thickness(6, 6, 6, 0) };

            // Выбрать нужный PNG
            ImageSource? src = correct == true  ? _badgeGreen
                             : correct == false ? _badgeRed
                             : _badgeBlue;

            if (src != null)
            {
                grid.Children.Add(new Image { Source = src, Stretch = Stretch.Uniform });
            }
            else
            {
                // Фолбэк — рисованный эллипс
                Color c1, c2;
                if (correct == true)       { c1 = Color.FromRgb(0x44, 0xFF, 0x44); c2 = Color.FromRgb(0x00, 0xAA, 0x00); }
                else if (correct == false)  { c1 = Color.FromRgb(0xFF, 0x44, 0x44); c2 = Color.FromRgb(0xCC, 0x00, 0x00); }
                else                        { c1 = Color.FromRgb(0x44, 0x88, 0xFF); c2 = Color.FromRgb(0x00, 0x44, 0xCC); }
                grid.Children.Add(new Ellipse { Width = BadgeSize, Height = BadgeSize, Fill = new RadialGradientBrush(c1, c2) });
            }

            // Номер на синем бейдже (когда ответ ещё не дан)
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
        // ИНФОРМАЦИОННАЯ ПАНЕЛЬ
        // ════════════════════════════════════════════════════════════════════════

        public void ShowQuestion(string text)
        {
            Dispatcher.BeginInvoke(() => { TxtQuestion.Text = text; QuestionBorder.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible; });
        }

        public void UpdateCurrentPlayer(string name)
        {
            Dispatcher.BeginInvoke(() => TxtCurrentPlayer.Text = name?.ToUpper() ?? "");
        }

        public void UpdateRoundInfo(int round, int bank)
        {
            Dispatcher.BeginInvoke(() => { TxtRoundInfo.Text = $"РАУНД {round}"; TxtBankInfo.Text = $"БАНК: {bank:N0} ₽"; });
        }

        public void ToggleInfoPanel()
        {
            Dispatcher.BeginInvoke(() => InfoPanel.Visibility = InfoPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible);
        }

        // ════════════════════════════════════════════════════════════════════════
        // ПУБЛИЧНЫЙ API
        // ════════════════════════════════════════════════════════════════════════

        public void UpdateBank(int chainIndex, int bankedAmount)
        {
            Dispatcher.BeginInvoke(() => { _activeIndex = Math.Max(1, chainIndex + 1); _bankedTotal = bankedAmount; UpdateRoundInfo(_engine.CurrentRound, bankedAmount); LayoutChain(); });
        }

        public void UpdateTimer(string timeStr, int secsLeft)
        {
            Dispatcher.BeginInvoke(() =>
            {
                TxtTimer.Text = timeStr;
                bool critical = secsLeft <= 10 && secsLeft >= 0;
                if (critical != _timerCritical)
                {
                    _timerCritical = critical;
                    TxtTimer.Foreground = critical ? new SolidColorBrush(Color.FromRgb(0xFF,0x33,0x33)) : Brushes.White;
                    if (critical)
                    {
                        var pulse = new DoubleAnimation { From = 1.0, To = 0.5, Duration = TimeSpan.FromMilliseconds(400), AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                        TimerBorder.BeginAnimation(OpacityProperty, pulse);
                    }
                    else { TimerBorder.BeginAnimation(OpacityProperty, null); TimerBorder.Opacity = 1.0; }
                }
            });
        }

        public void ShowLogo()
        {
            Dispatcher.BeginInvoke(() =>
            {
                var path = System.IO.Path.Combine(AssetDir, "logotipka.png");
                if (System.IO.File.Exists(path)) ImgLogo.Source = new BitmapImage(new Uri(path));
                LogoOverlay.Visibility = Visibility.Visible;
            });
        }

        public void HideLogo() => Dispatcher.BeginInvoke(() => LogoOverlay.Visibility = Visibility.Collapsed);

        public void ToggleLogo() => Dispatcher.BeginInvoke(() => { if (LogoOverlay.Visibility == Visibility.Visible) HideLogo(); else ShowLogo(); });

        public void ShowElimination(string name)
        {
            Dispatcher.BeginInvoke(() => { TxtEliminatedName.Text = name.ToUpper(); EliminationOverlay.Visibility = Visibility.Visible; StatusOverlay.Visibility = Visibility.Collapsed; });
        }

        public void ClearElimination() => Dispatcher.BeginInvoke(() => EliminationOverlay.Visibility = Visibility.Collapsed);

        public void ShowStatus(string text)
        {
            Dispatcher.BeginInvoke(() => { TxtStatus.Text = text; StatusOverlay.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible; EliminationOverlay.Visibility = Visibility.Collapsed; });
        }

        public void ClearStatus() => Dispatcher.BeginInvoke(() => StatusOverlay.Visibility = Visibility.Collapsed);

        // ════════════════════════════════════════════════════════════════════════
        // СОСТОЯНИЕ ДВИЖКА
        // ════════════════════════════════════════════════════════════════════════

        private void ApplyState(GameState state)
        {
            switch (state)
            {
                case GameState.Voting:       ShowStatus("ГОЛОСОВАНИЕ"); break;
                case GameState.Discussion:   ShowStatus("КТО СЛАБОЕ ЗВЕНО?"); break;
                case GameState.Reveal:       ShowStatus("ВСКРЫТИЕ ГОЛОСОВ"); break;
                case GameState.Elimination:  ClearStatus(); break;
                case GameState.RoundSummary: ShowStatus($"РАУНД {_engine.CurrentRound} · ИТОГИ"); break;
                case GameState.FinalDuel:
                    ClearStatus();
                    if (_engine.ActivePlayers.Count >= 2)
                        ShowFinalDuel(_engine.ActivePlayers[0], _engine.ActivePlayers[1]);
                    break;
                case GameState.Playing:
                    ClearStatus(); ClearElimination(); HideFinalDuel(); break;
                default:
                    ClearStatus(); break;
            }
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
                    if (int.TryParse(parts[1], out int ci) && int.TryParse(parts[2], out int ba)) UpdateBank(ci, ba);
                    break;
                case "TIMER" when parts.Length >= 2:
                    int secs = 0; if (parts.Length >= 3) int.TryParse(parts[2], out secs); UpdateTimer(parts[1], secs);
                    break;
                case "ELIMINATE" when parts.Length >= 2:
                    ShowElimination(parts[1]); break;
                case "CLEAR_ELIMINATION":
                    ClearElimination(); break;
                case "STATE" when parts.Length >= 2:
                    if (Enum.TryParse<GameState>(parts[1], out var st)) Dispatcher.BeginInvoke(() => ApplyState(st));
                    break;
                case "QUESTION" when parts.Length >= 2:
                    ShowQuestion(parts[1]); break;
                case "CURRENT_PLAYER" when parts.Length >= 2:
                    UpdateCurrentPlayer(parts[1]); break;
                case "FINAL_UPDATE":
                    UpdateFinalDuel(); break;
                case "FINAL_SHOW" when parts.Length >= 3:
                    ShowFinalDuel(parts[1], parts[2]); break;
                case "FINAL_HIDE":
                    HideFinalDuel(); break;
                case "SHOW_INFO":
                    Dispatcher.BeginInvoke(() => InfoPanel.Visibility = Visibility.Visible); break;
                case "HIDE_INFO":
                    Dispatcher.BeginInvoke(() => InfoPanel.Visibility = Visibility.Collapsed); break;
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

        private string FormatAmount(int v) => v == 0 ? "БАНК" : v.ToString();

        private void Window_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && WindowStyle == WindowStyle.None) DragMove();
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
            if (e.Key == Key.F11 || (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt) || (e.SystemKey == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt))
            { ToggleFullscreen(); e.Handled = true; }
            else if (e.Key == Key.Escape && WindowStyle == WindowStyle.None)
            { WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize; WindowState = WindowState.Normal; e.Handled = true; }
            else if (e.Key == Key.F10)
            { ToggleInfoPanel(); e.Handled = true; }
        }

        private void ToggleFullscreen()
        {
            if (WindowStyle == WindowStyle.None)
            { WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize; WindowState = WindowState.Normal; }
            else
            { WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowState = WindowState.Maximized; }
        }

        protected override void OnClosed(EventArgs e) { _client?.Stop(); base.OnClosed(e); }

        private class ChainSlot
        {
            public int Index { get; set; }
            public Grid Grid { get; set; } = null!;
            public Image Image { get; set; } = null!;
            public TextBlock Label { get; set; } = null!;
        }
    }
}
