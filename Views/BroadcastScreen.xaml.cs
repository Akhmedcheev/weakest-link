using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace WeakestLink.Views
{
    /// <summary>
    /// BroadcastScreen - компонент для отображения денежной цепочки и таймера
    /// Основной экран трансляции игры "Слабое звено"
    /// </summary>
    public partial class BroadcastScreen : UserControl, IDisposable
    {
        // Ключевые параметры настройки
        private const double OverlapOffset = 25.0;      // Толщина нахлеста плашек в стопке
        private const double StandardSpacing = 50.0;    // Расстояние между плашками неиграемых уровней
        private const int AnimationDurationMs = 500;    // Длительность анимации падения плашки
        
        // Денежная цепочка - можно легко добавить новые уровни
        private readonly int[] amounts = { 0, 1000, 2000, 5000, 10000, 20000, 30000, 40000, 50000 };
        
        // Данные для элементов цепочки
        private readonly List<ChainItem> chainItems = new List<ChainItem>();
        
        // Таймер
        private DispatcherTimer _timer;
        private int _secondsRemaining = 150; // По умолчанию 150 секунд для первого раунда
        
        // Текущий уровень
        private int _currentLevel = 0;
        
        public BroadcastScreen()
        {
            InitializeComponent();
            InitializeChain();
            InitializeTimer();
        }
        
        #region Денежная цепочка
        
        /// <summary>
        /// Инициализация денежной цепочки
        /// </summary>
        private void InitializeChain()
        {
            MoneyChainCanvas.Children.Clear();
            chainItems.Clear();
            
            // Создаем элементы для каждого уровня
            for (int i = 0; i < amounts.Length; i++)
            {
                var chainItem = CreateChainItem(i, amounts[i]);
                chainItems.Add(chainItem);
                MoneyChainCanvas.Children.Add(chainItem.Container);
            }
            
            // Обновляем расположение
            UpdateChainLayout();
        }
        
        /// <summary>
        /// Создание элемента денежной цепочки
        /// </summary>
        private ChainItem CreateChainItem(int index, int amount)
        {
            var grid = new Grid
            {
                Width = 300,
                Height = 45,
                Tag = index
            };
            
            // Фоновое изображение
            var image = new Image
            {
                Width = 300,
                Height = 45,
                Stretch = Stretch.Uniform,
                Source = LoadImage(index == 0 ? "BANK.png" : "moneytree_blue.png")
            };
            
            // Текст с суммой
            var text = new TextBlock
            {
                Text = amount == 0 ? "БАНК" : $"{amount:N0} ₽",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Arial Black"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            grid.Children.Add(image);
            grid.Children.Add(text);
            
            return new ChainItem
            {
                Index = index,
                Amount = amount,
                Container = grid,
                Text = text,
                Image = image
            };
        }
        
        /// <summary>
        /// Ключевой метод расчета координат для всех элементов цепочки
        /// </summary>
        private void UpdateChainLayout()
        {
            double canvasHeight = MoneyChainCanvas.ActualHeight > 0 ? MoneyChainCanvas.ActualHeight : 800;
            
            foreach (var item in chainItems)
            {
                double bottom;
                string imageName;
                
                if (item.Index <= _currentLevel)
                {
                    // Пройденные уровни - в стопке снизу с нахлестом
                    bottom = item.Index * OverlapOffset;
                    imageName = item.Index == _currentLevel && item.Index > 0 ? "moneytree_red.png" : "moneytree_blue.png";
                }
                else
                {
                    // Будущие уровни - равномерно распределены сверху
                    double availableHeight = canvasHeight - 150; // Оставляем место внизу
                    double totalSpacing = (amounts.Length - _currentLevel - 1) * StandardSpacing;
                    double startTop = (availableHeight - totalSpacing) / 2;
                    
                    bottom = canvasHeight - (startTop + (item.Index - _currentLevel) * StandardSpacing + 45);
                    imageName = "moneytree_blue.png";
                }
                
                // Устанавливаем позицию
                Canvas.SetLeft(item.Container, 50);
                Canvas.SetBottom(item.Container, bottom);
                
                // Обновляем Z-Index (красный текущий уровень поверх всех)
                Canvas.SetZIndex(item.Container, item.Index == _currentLevel && item.Index > 0 ? 1000 : item.Index);
                
                // Обновляем изображение
                item.Image.Source = LoadImage(imageName);
                
                // Анимация для текущего уровня (красная плашка)
                if (item.Index == _currentLevel && item.Index > 0)
                {
                    AnimateCurrentLevel(item);
                }
            }
        }
        
        /// <summary>
        /// Анимация для текущего уровня (пульсация)
        /// </summary>
        private void AnimateCurrentLevel(ChainItem item)
        {
            var scaleTransform = new ScaleTransform(1.0, 1.0);
            item.Container.RenderTransform = scaleTransform;
            item.Container.RenderTransformOrigin = new Point(0.5, 0.5);
            
            var animation = new DoubleAnimation
            {
                From = 1.0,
                To = 1.05,
                Duration = TimeSpan.FromMilliseconds(1000),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
        
        /// <summary>
        /// Загрузка изображения из ресурсов
        /// </summary>
        private ImageSource LoadImage(string fileName)
        {
            try
            {
                var uri = new Uri($"/Assets/{fileName}", UriKind.Relative);
                return new BitmapImage(uri);
            }
            catch
            {
                // Если изображение не загрузилось, возвращаем простую заглушку
                return null;
            }
        }
        
        #endregion
        
        #region Таймер
        
        /// <summary>
        /// Инициализация таймера
        /// </summary>
        private void InitializeTimer()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            UpdateTimerDisplay();
        }
        
        /// <summary>
        /// Обработчик тика таймера
        /// </summary>
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_secondsRemaining > 0)
            {
                _secondsRemaining--;
                UpdateTimerDisplay();
                
                // Пульсация на последних 10 секундах
                if (_secondsRemaining <= 10)
                {
                    AnimateTimerPulse();
                }
            }
            else
            {
                _timer.Stop();
                TimerExpired?.Invoke(this, EventArgs.Empty);
            }
        }
        
        /// <summary>
        /// Обновление отображения таймера
        /// </summary>
        private void UpdateTimerDisplay()
        {
            var minutes = _secondsRemaining / 60;
            var seconds = _secondsRemaining % 60;
            TimerText.Text = $"{minutes}:{seconds:D2}";
            
            // Изменение цвета на последних 10 секундах
            if (_secondsRemaining <= 10)
            {
                TimerText.Foreground = Brushes.Red;
            }
            else
            {
                TimerText.Foreground = Brushes.White;
            }
        }
        
        /// <summary>
        /// Анимация пульсации таймера
        /// </summary>
        private void AnimateTimerPulse()
        {
            var animation = new DoubleAnimation
            {
                From = 1.0,
                To = 1.1,
                Duration = TimeSpan.FromMilliseconds(500),
                AutoReverse = true
            };
            
            TimerText.BeginAnimation(TextBlock.FontSizeProperty, animation);
        }
        
        #endregion
        
        #region Публичный API
        
        /// <summary>
        /// Установить текущий уровень денежной цепочки
        /// </summary>
        public void SetLevel(int level)
        {
            if (level < 0 || level >= amounts.Length)
                throw new ArgumentOutOfRangeException(nameof(level));
            
            _currentLevel = level;
            UpdateChainLayout();
        }
        
        /// <summary>
        /// Переход на следующий уровень
        /// </summary>
        public void NextLevel()
        {
            if (_currentLevel < amounts.Length - 1)
            {
                SetLevel(_currentLevel + 1);
                AnimateLevelTransition(_currentLevel);
            }
        }
        
        /// <summary>
        /// Переход на предыдущий уровень
        /// </summary>
        public void PreviousLevel()
        {
            if (_currentLevel > 0)
            {
                SetLevel(_currentLevel - 1);
            }
        }
        
        /// <summary>
        /// Получить текущий уровень
        /// </summary>
        public int GetCurrentLevel()
        {
            return _currentLevel;
        }
        
        /// <summary>
        /// Запустить таймер
        /// </summary>
        public void StartTimer()
        {
            _timer.Start();
        }
        
        /// <summary>
        /// Остановить таймер
        /// </summary>
        public void StopTimer()
        {
            _timer.Stop();
        }
        
        /// <summary>
        /// Установить время таймера в секундах
        /// </summary>
        public int SecondsRemaining
        {
            get { return _secondsRemaining; }
            set
            {
                _secondsRemaining = Math.Max(0, value);
                UpdateTimerDisplay();
            }
        }
        
        /// <summary>
        /// Обновить информацию об игре
        /// </summary>
        public void UpdateGameInfo(int round, int bank, string currentPlayer)
        {
            RoundInfoText.Text = $"РАУНД {round}";
            BankInfoText.Text = $"БАНК: {bank:N0} ₽";
            CurrentPlayerText.Text = currentPlayer;
        }
        
        /// <summary>
        /// Показать вопрос
        /// </summary>
        public void ShowQuestion(string question)
        {
            QuestionText.Text = question;
            HideHostMessage();
        }
        
        /// <summary>
        /// Показать сообщение ведущему (суфлёр)
        /// </summary>
        public void ShowHostMessage(string message)
        {
            HostMessageText.Text = message;
            HostMessageOverlay.Visibility = Visibility.Visible;
        }
        
        /// <summary>
        /// Скрыть сообщение ведущему
        /// </summary>
        public void HideHostMessage()
        {
            HostMessageOverlay.Visibility = Visibility.Collapsed;
        }
        
        /// <summary>
        /// Переключить отображение логотипа
        /// </summary>
        public void ToggleLogoScreen()
        {
            if (LogoOverlay.Visibility == Visibility.Visible)
            {
                LogoOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                ShowLogo();
            }
        }

        /// <summary>
        /// Показать логотип
        /// </summary>
        public void ShowLogo()
        {
            LogoOverlay.Visibility = Visibility.Visible;
            var logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", "logotipka.png");
            if (System.IO.File.Exists(logoPath))
            {
                ImgLogo.Source = new BitmapImage(new Uri(logoPath));
            }
        }

        #endregion
        
        #region Анимации
        
        /// <summary>
        /// Анимация перехода уровня (падение плашки)
        /// </summary>
        private void AnimateLevelTransition(int level)
        {
            if (level <= 0 || level >= chainItems.Count)
                return;
            
            var item = chainItems[level];
            var targetBottom = level * OverlapOffset;
            
            var animation = new DoubleAnimation
            {
                From = Canvas.GetBottom(item.Container),
                To = targetBottom,
                Duration = TimeSpan.FromMilliseconds(AnimationDurationMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            item.Container.BeginAnimation(Canvas.BottomProperty, animation);
        }
        
        #endregion
        
        #region События
        
        /// <summary>
        /// Событие истечения таймера
        /// </summary>
        public event EventHandler? TimerExpired;
        
        #endregion
        
        #region IDisposable
        
        /// <summary>
        /// Очистка ресурсов
        /// </summary>
        public void Dispose()
        {
            _timer?.Stop();
            _timer = null;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Элемент денежной цепочки
    /// </summary>
    internal class ChainItem
    {
        public int Index { get; set; }
        public int Amount { get; set; }
        public Grid Container { get; set; } = null!;
        public TextBlock Text { get; set; } = null!;
        public Image Image { get; set; } = null!;
    }
}
