using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WeakestLink.Audio;
using WeakestLink.Core;
using WeakestLink.Core.Models;
using WeakestLink.Core.Analytics;
using System.Windows.Media;
using WeakestLink.Core.Services;
using WeakestLink.Network;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using QRCoder;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WeakestLink.Views
{
    public partial class OperatorPanel : Window
    {
        /// Длительность отбивки в начале раундового трека (мс).
        /// Таймер игры стартует ПОСЛЕ этой задержки.
        /// Подстройка: если гонг приходит позже нуля — увеличить, раньше — уменьшить.
        private int TRACK_INTRO_BEAT_MS = 100;

        // Цветовые подсказки для UpdateButtonStates (UI Hygiene)
        private static readonly SolidColorBrush BrushActiveGreen = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
        private static readonly SolidColorBrush BrushActiveRed = new SolidColorBrush(Color.FromRgb(0xD3, 0x2F, 0x2F));
        private static readonly SolidColorBrush BrushBankOrange = new SolidColorBrush(Color.FromRgb(0xCC, 0x88, 0x00));

        // Timer Hub adaptive color brushes (Segmented Fill Style)
        private static readonly Color HubColorNormal   = Color.FromRgb(0x00, 0x4D, 0x60);  // Deep stee-blue
        private static readonly Color HubColorGlow     = Color.FromRgb(0x00, 0xAE, 0xEF);  // Bright Cyan Glow
        private static readonly Color HubColorCritical = Color.FromRgb(0x8B, 0x00, 0x00);  // Deep red
        private static readonly Color HubDotBorder     = Color.FromRgb(0x1A, 0x1A, 0x1A);  // Thin gunmetal
        private static readonly SolidColorBrush TimerBrushNormal   = new(HubColorGlow);
        private static readonly SolidColorBrush TimerBrushCritical = new(HubColorCritical);
        private bool _timerPulseRunning = false;
        private readonly List<System.Windows.Controls.Border> _hubDots = new();
        private int _hubTotalDots = 0;
        private int _hubRoundDuration = 0;
        private static readonly SolidColorBrush BrushPassNeutral = new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B));
        private static readonly SolidColorBrush BrushPlayBlue = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));
        private static readonly SolidColorBrush BrushStartClock = new SolidColorBrush(Color.FromRgb(0x00, 0x88, 0x00));
        private static readonly SolidColorBrush BrushStartClockFg = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x00));
        // Отключённые кнопки: неактивно-серый фон и серый текст (никакого белого)
        private static readonly SolidColorBrush BrushDisabledGray = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));
        private static readonly SolidColorBrush BrushDisabledForeground = new SolidColorBrush(Color.FromRgb(0x70, 0x70, 0x70));

        private GameEngine _engine;
        private QuestionProvider _questionProvider;
        private GameServer _server;
        private AudioManager _audioManager;
        private DispatcherTimer _roundTimer;
        // Voting timer fields removed — voting system pending redesign
        private HostScreen _hostScreen;
        private HostScreenModern _hostModernScreen;
        private HostScreenPremium _hostPremiumScreen;
        private HostScreenModernPremium _hostModernPremiumScreen;
        private BroadcastWindow? _broadcastWindow;
        private AudienceScreen? _audienceScreen;
        private QuestionData? _currentQuestion;
        private int _timeLeftSeconds = 150;
        private int _questionCount = 0;
        private int _lastRoundBankForSummary = 0;
        private bool _fullBankTriggered = false;
        private DispatcherTimer? _votingTimer;
        private int _votingTimeLeft = 0;
        private WebRemoteController? _webRemote;

        // Вспомогательный класс для отображения цепочки в ItemsControl
        public class BankChainItem
        {
            public int Value { get; set; }
            public bool IsActive { get; set; }
            public int Index { get; set; }
            public string ValueDisplay => Value.ToString("N0") + " ₽";
            public string Background => IsActive ? "#FFD700" : "#1e1f22";
            public string TextColor => IsActive ? "#1a1a1a" : "White";
        }
        
        public class EliminationComboItem
        {
            public string Name { get; set; } = "";
            public string TextColor { get; set; } = "#dbdee1";
        }

        // Smart Roster: карточка игрока с разделением игровых и эфирных данных
        public class PlayerSetupItem : System.ComponentModel.INotifyPropertyChanged
        {
            private int _consoleNumber;
            private string _gameName = "";   // Короткое имя на пульте (для движка)
            private string _fullName = "";   // ФИО (для суфлёра)
            private string _cityDesc = "";   // Город/Профессия (для суфлёра)

            public int ConsoleNumber
            {
                get => _consoleNumber;
                set { _consoleNumber = value; OnPropertyChanged(nameof(ConsoleNumber)); OnPropertyChanged(nameof(ConsoleDisplay)); }
            }

            // ═══ ИГРОВЫЕ ДАННЫЕ (→ движок, пульт, аналитика) ═══
            public string GameName
            {
                get => _gameName;
                set { _gameName = value; OnPropertyChanged(nameof(GameName)); }
            }

            // ═══ ОФИЦИАЛЬНЫЕ ДАННЫЕ (→ суфлёр ведущего) ═══
            public string FullName
            {
                get => _fullName;
                set { _fullName = value; OnPropertyChanged(nameof(FullName)); OnPropertyChanged(nameof(PrompterLine)); }
            }

            public string CityDesc
            {
                get => _cityDesc;
                set { _cityDesc = value; OnPropertyChanged(nameof(CityDesc)); OnPropertyChanged(nameof(PrompterLine)); }
            }

            public string ConsoleDisplay => $"Пульт {ConsoleNumber}";

            private bool _isLocked;
            public bool IsLocked
            {
                get => _isLocked;
                set { _isLocked = value; OnPropertyChanged(nameof(IsLocked)); }
            }

            // ═══ АНКЕТА (→ левый сайдбар) ═══
            private string _photoPath = "";
            public string PhotoPath
            {
                get => _photoPath;
                set { _photoPath = value; OnPropertyChanged(nameof(PhotoPath)); OnPropertyChanged(nameof(PhotoSource)); }
            }

            public System.Windows.Media.ImageSource? PhotoSource
            {
                get
                {
                    if (string.IsNullOrEmpty(_photoPath) || !System.IO.File.Exists(_photoPath)) return null;
                    try
                    {
                        var bi = new System.Windows.Media.Imaging.BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = new Uri(_photoPath, UriKind.Absolute);
                        bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bi.DecodePixelWidth = 120;
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
                    }
                    catch { return null; }
                }
            }

            private int _age;
            public int Age
            {
                get => _age;
                set { _age = value; OnPropertyChanged(nameof(Age)); }
            }

            private string _bio = "";
            public string Bio
            {
                get => _bio;
                set { _bio = value; OnPropertyChanged(nameof(Bio)); }
            }

            public string PrompterLine =>
                string.IsNullOrWhiteSpace(CityDesc) ? FullName.Trim()
                                                    : $"{FullName.Trim()}, {CityDesc.Trim()}";

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(prop));
        }

        private readonly ObservableCollection<PlayerSetupItem> _rosterItems = new();
        private PlayerSetupItem? _currentDossierPlayer;



        public class PlayerStatView
        {
            public string Name { get; set; } = "";
            public int Correct { get; set; }
            public int Incorrect { get; set; }
            public int Passes { get; set; }
            public int Banked { get; set; }
            /// <summary>За кого проголосовал (только в режиме тестирования ботов).</summary>
            public string VotedFor { get; set; } = "";
        }

        private bool _isSuddenDeathMusicPlayed = false;
        private System.Windows.Shapes.Ellipse[] _p1Circles;
        private System.Windows.Shapes.Ellipse[] _p2Circles;
        private StatsAnalyzer _statsAnalyzer;
        private DispatcherTimer _autoBotTimer = null!;
        private DispatcherTimer? _timeOutGeneralBedFallbackTimer;
        // Voting dictor timer fields removed — voting system pending redesign
        // _waitingForBeforeVoting removed — voting system pending redesign
        // Voting flags removed — voting system pending redesign
        private Dictionary<string, string> _botVotes = new();
        private string _currentTestModeLabel = "";
        private bool _isAutoTestRunning = false;
        private bool _isSessionStarted = false;
        private GeminiTestPlayer? _aiPlayer;

        public OperatorPanel()
        {
            InitializeComponent();
            SourceInitialized += OnSourceInitialized;
            
            _engine = new GameEngine();
            _questionProvider = new QuestionProvider();
            _statsAnalyzer = new StatsAnalyzer(_engine);

            // Инициализация ссылок на жестко заданные кружочки
            _p1Circles = new[] { P1_C1, P1_C2, P1_C3, P1_C4, P1_C5 };
            _p2Circles = new[] { P2_C1, P2_C2, P2_C3, P2_C4, P2_C5 };
            
            try
            {
                _questionProvider.LoadQuestions("questions.json");
                Log($"Загружено вопросов: {_questionProvider.LoadedCount}");

                // Загрузка финальных вопросов
                List<QuestionData> finalQuestions = new List<QuestionData>();
                if (System.IO.File.Exists("final_questions.json"))
                {
                    try
                    {
                        var finalJson = System.IO.File.ReadAllText("final_questions.json");
                        var loaded = System.Text.Json.JsonSerializer.Deserialize<List<QuestionData>>(finalJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (loaded != null) finalQuestions = loaded;
                    }
                    catch (Exception ex)
                    {
                        Log($"Ошибка парсинга финальных вопросов: {ex.Message}");
                    }
                }
                
                // Даже если список пуст (файл не найден или пустой), вызываем метод, 
                // чтобы движок добавил свой резервный вопрос.
                _engine.LoadFinalQuestions(finalQuestions);
                Log($"Загружено финальных вопросов: {finalQuestions.Count}");
            }
            catch (Exception ex)
            {
                Log($"КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
                DarkMessageBox.Show($"Ошибка при загрузке базы вопросов:\n{ex.Message}", "Ошибка инициализации");
            }

            _autoBotTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _autoBotTimer.Tick += AutoBotTimer_Tick;

            // Инициализация сервера
            _server = new GameServer(8888);
            _server.Start();
            string serverIp = GetLocalIPAddress(); // Получаем IP для логирования
            Log($"TCP Сервер запущен на порту {_server.BoundPort} (IP: {serverIp}).");

            // Инициализация iPad-пульта (Web)
            int preferredPort = 8080;
            try
            {
                string localIp = GetLocalIPAddress();
                
                // Функция для получения текущего состояния (вопрос + таймер + ответ + банк + флаг активности) для пульта
                Func<object> stateProvider = () => {
                    return Dispatcher.Invoke(() => new {
                        question = (HeadToHeadPanel.Visibility == Visibility.Visible) ? FinalQuestionTextBlock.Text : TxtCurrentQuestion.Text,
                        answer = (HeadToHeadPanel.Visibility == Visibility.Visible) ? (_engine.CurrentFinalQuestion?.Answer ?? "-") : TxtCurrentAnswer.Text,
                        timer = TxtTimer.Text,
                        bank = _engine.RoundBank.ToString("N0"),
                        isActive = (_engine.CurrentState == GameState.Playing || _engine.CurrentState == GameState.FinalDuel)
                    });
                };

                // Состояние для экрана ведущего (GET /host): цепочка, банк, таймер, вопрос/ответ, финал дуэли
                Func<object> hostStateProvider = () =>
                {
                    return Dispatcher.Invoke(() =>
                    {
                        int chainIdx = _engine.CurrentChainIndex;
                        int toBankVal = chainIdx > 0 && chainIdx <= _engine.BankChain.Length ? _engine.BankChain[chainIdx - 1] : 0;
                        int nextSumVal = chainIdx < _engine.BankChain.Length ? _engine.BankChain[chainIdx] : ( _engine.BankChain.Length > 0 ? _engine.BankChain[_engine.BankChain.Length - 1] : 1000 );
                        string question = (HeadToHeadPanel.Visibility == Visibility.Visible) ? FinalQuestionTextBlock.Text : TxtCurrentQuestion.Text;
                        string answer = (HeadToHeadPanel.Visibility == Visibility.Visible) ? (_engine.CurrentFinalQuestion?.Answer ?? "-") : TxtCurrentAnswer.Text;
                        int qNum = (HeadToHeadPanel.Visibility == Visibility.Visible) ? 0 : _questionCount;
                        string p1s = string.Join(",", _engine.Player1FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                        string p2s = string.Join(",", _engine.Player2FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                        return new {
                            state = _engine.CurrentState.ToString(),
                            question = question,
                            answer = answer,
                            questionNumber = qNum,
                            toBank = toBankVal,
                            nextSum = nextSumVal,
                            timer = TxtTimer.Text,
                            banked = _engine.RoundBank,
                            bankChain = _engine.BankChain,
                            currentChainIndex = chainIdx,
                            finalist1 = TxtFinalist1.Text,
                            finalist2 = TxtFinalist2.Text,
                            p1Scores = p1s,
                            p2Scores = p2s
                        };
                    });
                };

                _webRemote = new WebRemoteController(localIp, preferredPort, HandleRemoteCommand, msg => Log(msg), stateProvider, hostStateProvider);
                try 
                {
                    _webRemote.Start();
                }
                catch (Exception startEx) when (startEx.Message.Contains("already in use") || startEx.HResult == -2147467259)
                {
                    Log($"Порт {preferredPort} занят. Пробую резервный 8081...");
                    preferredPort = 8081;
                    _webRemote = new WebRemoteController(localIp, preferredPort, HandleRemoteCommand, msg => Log(msg), stateProvider, hostStateProvider);
                    _webRemote.Start();
                }
                
                string remoteUrl = $"http://{localIp}:{preferredPort}";
                RemoteLinkText.Text = remoteUrl;
                
                // Итоговая информация о сетевых настройках
                Log($"=== СЕТЕВЫЕ НАСТРОЙКИ ===");
                Log($"Web Remote URL: {remoteUrl}");
                Log($"TCP Сервер: порт 8888 (IP: {serverIp})");
                Log($"QR-код сгенерирован для: {remoteUrl}");
                Log("========================");
                
                // Генерация QR-кода
                QrCodeImage.Source = GenerateQrCode(remoteUrl);
            }
            catch (Exception ex)
            {
                Log($"ОШИБКА Web-пульта: {ex.Message}");
                System.IO.File.AppendAllText("web_remote_error.txt", $"[{DateTime.Now}] {ex.Message}\n{ex.StackTrace}\n");
                if (ex.Message.Contains("отказано в доступе") || ex.Message.ToLower().Contains("access is denied"))
                {
                    Log("СОВЕТ: Запустите программу от имени Администратора для работы iPad-пульта.");
                }
                RemoteLinkText.Text = "Ошибка запуска";
                RemoteLinkText.Foreground = System.Windows.Media.Brushes.Red;
            }

            // Подписка на события ядра
            _engine.StateChanged += OnEngineStateChanged;
            _engine.BankChanged += OnEngineBankChanged;
            _engine.MaxBankReached += OnMaxBankReached;
            _engine.FinalDuelEnded += OnFinalDuelEnded;

            // Настройка таймера
            _roundTimer = new DispatcherTimer();
            _roundTimer.Interval = TimeSpan.FromSeconds(1);
            _roundTimer.Tick += RoundTimer_Tick;

            // Voting timers removed — voting system pending redesign

            _audioManager = new AudioManager();

            // Начальная установка UI
            UpdateBankChainUI();
            // Инициализация 8 фиксированных слотов пультов
            for (int i = 1; i <= 8; i++)
                _rosterItems.Add(new PlayerSetupItem { ConsoleNumber = i });
            LstPlayers.ItemsSource = _rosterItems;
            ParticipantsGrid.ItemsSource = _rosterItems;
            EliminationComboBox.ItemsSource = _engine.ActivePlayers;
            UpdateButtonStates();
            UpdateOperationalHints();
            Log("Пульт оператора инициализирован. Состояние: Idle.");
            
            SetCentralContext("SETUP");
        }

        /// <summary>
        /// При закрытии окна оператора: предупреждение + закрытие всех экранов ведущего.
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            // Если уже подтверждено (повторный вызов при принудительном закрытии) — пропускаем диалог
            if (_closingConfirmed) return;

            // Показываем кастомный диалог подтверждения
            var result = DarkMessageBox.Show(
                "Вы закрываете пульт оператора.\n\nВсе экраны ведущего также будут закрыты.",
                "Закрыть приложение?",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                // Отменяем закрытие
                e.Cancel = true;
                return;
            }

            // Подтверждено — закрываем все экраны ведущего
            _closingConfirmed = true;
            CloseAllHostScreens();
        }

        private bool _closingConfirmed = false;

        private void CloseAllHostScreens()
        {
            try { if (_hostScreen        != null && _hostScreen.IsLoaded)              _hostScreen.Close(); }              catch { }
            try { if (_hostModernScreen  != null && _hostModernScreen.IsLoaded)        _hostModernScreen.Close(); }        catch { }
            try { if (_hostPremiumScreen != null && _hostPremiumScreen.IsLoaded)       _hostPremiumScreen.Close(); }       catch { }
            try { if (_hostModernPremiumScreen != null && _hostModernPremiumScreen.IsLoaded) _hostModernPremiumScreen.Close(); } catch { }
            try { if (_broadcastWindow != null && _broadcastWindow.IsLoaded) _broadcastWindow.Close(); } catch { }
            try { if (_audienceScreen  != null && _audienceScreen.IsLoaded)  _audienceScreen.Close();  } catch { }
        }

        private void HandleRemoteCommand(string action)
        {
            switch (action)
            {
                case "BANK":
                    if (PlayContext.Visibility == Visibility.Visible)
                        BtnBank_Click(null, null);
                    break;
                case "CORRECT":
                    if (HeadToHeadPanel.Visibility == Visibility.Visible)
                        BtnDuelCorrect_Click(null, null);
                    else if (PlayContext.Visibility == Visibility.Visible)
                        BtnCorrect_Click(null, null);
                    break;
                case "WRONG":
                    if (HeadToHeadPanel.Visibility == Visibility.Visible)
                        BtnDuelWrong_Click(null, null);
                    else if (PlayContext.Visibility == Visibility.Visible)
                        BtnWrong_Click(null, null);
                    break;
                case "PASS":
                    if (PlayContext.Visibility == Visibility.Visible)
                        BtnPass_Click(null, null);
                    break;
                case "NEXT":
                    if (PlayContext.Visibility == Visibility.Visible)
                        BtnNextQuestion_Click(null, null);
                    else if (HeadToHeadPanel.Visibility == Visibility.Visible && BtnStartDuel.Visibility == Visibility.Visible)
                        BtnStartDuel_Click(null, null);
                    break;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _webRemote?.Stop();
            _server?.Stop();
            CloseAnalytics(); // Закрываем окно аналитики при закрытии программы
            base.OnClosed(e);
        }

        private bool IsValidLocalIp(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress)) return false;
            
            // Проверяем, что это не localhost и не APIPA
            return ipAddress != "127.0.0.1" && 
                   !ipAddress.StartsWith("169.254.") &&
                   !ipAddress.StartsWith("0.") &&
                   System.Net.IPAddress.TryParse(ipAddress, out var ip) &&
                   ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }

        private string GetLocalIPAddress()
        {
            try
            {
                Log("=== ПОИСК ЛОКАЛЬНОГО IP-АДРЕСА ===");
                
                // Получаем все активные физические интерфейсы (Ethernet и Wi-Fi)
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                    .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || 
                                 ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    .ToList();

                Log($"Найдено активных интерфейсов: {interfaces.Count}");

                // Расширенный список исключений для виртуальных адаптеров и VPN
                string[] filterWords = { 
                    "virtual", "vpn", "hamachi", "hyper-v", "vmware", "tunnel", "tailscale", 
                    "radmin", "pseudo", "loopback", "zerotier", "openvpn", "wireguard",
                    "tap", "tun", "veth", "docker", "bridge", "kvm", "qemu", "virtualbox"
                };

                foreach (var ni in interfaces)
                {
                    string desc = ni.Description.ToLower();
                    string name = ni.Name.ToLower();
                    
                    Log($"Проверка интерфейса: {name} ({desc})");

                    // Жесткая фильтрация - если в названии есть любое из исключений, пропускаем
                    if (filterWords.Any(word => desc.Contains(word) || name.Contains(word)))
                    {
                        Log($"  -> ИСКЛЮЧЕН (VPN/виртуальный)");
                        continue;
                    }

                    // Дополнительная проверка: исключаем интерфейсы без физического адреса (MAC)
                    if (ni.GetPhysicalAddress().GetAddressBytes().Length == 0)
                    {
                        Log($"  -> ИСКЛЮЧЕН (нет MAC-адреса)");
                        continue;
                    }

                    var ipProps = ni.GetIPProperties();
                    var ipv4Addresses = ipProps.UnicastAddresses
                        .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork &&
                                      !IPAddress.IsLoopback(ua.Address) &&
                                      !ua.Address.ToString().StartsWith("169.254."))
                        .ToList();

                    Log($"  -> Найдено IPv4 адресов: {ipv4Addresses.Count}");
                    
                    foreach (var addr in ipv4Addresses)
                    {
                        Log($"    -> {addr.Address}");
                    }

                    var ipv4Addr = ipv4Addresses.FirstOrDefault();
                    if (ipv4Addr != null)
                    {
                        string selectedIp = ipv4Addr.Address.ToString();
                        if (IsValidLocalIp(selectedIp))
                        {
                            Log($"ВЫБРАН IP: {selectedIp} (интерфейс: {name})");
                            return selectedIp;
                        }
                        else
                        {
                            Log($"  -> IP {selectedIp} не прошел валидацию, ищем дальше...");
                        }
                    }
                }

                // Резервный метод через Dns (только если физические не найдены)
                Log("Физические интерфейсы не найдены, используем DNS-резерв");
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var fallbackIp = host.AddressList
                    .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && 
                                !IPAddress.IsLoopback(ip) &&
                                !ip.ToString().StartsWith("169.254."))
                    .Select(ip => ip.ToString())
                    .FirstOrDefault(IsValidLocalIp) ?? "127.0.0.1";
                
                Log($"РЕЗЕРВНЫЙ IP: {fallbackIp}");
                return fallbackIp;
            }
            catch (Exception ex)
            { 
                Log($"ОШИБКА ПОЛУЧЕНИЯ IP: {ex.Message}");
                return "127.0.0.1";
            }
        }

        private BitmapImage? GenerateQrCode(string url)
        {
            try
            {
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q))
                using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                {
                    byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);
                    using (var ms = new MemoryStream(qrCodeAsPngByteArr))
                    {
                        var imageSource = new BitmapImage();
                        imageSource.BeginInit();
                        imageSource.CacheOption = BitmapCacheOption.OnLoad;
                        imageSource.StreamSource = ms;
                        imageSource.EndInit();
                        return imageSource;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка генерации QR: {ex.Message}");
                return null;
            }
        }

        #region Обработка событий Engine

        private void OnEngineStateChanged(object sender, StateChangedEventArgs e)
        {
            // Обновление UI через Dispatcher для безопасности потоков
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TxtGameState.Text = e.NewState.ToString().ToUpper();
                Log($"Состояние изменено: {e.OldState} -> {e.NewState}");
                
                // Ретранслируем состояние на Host Screen
                _server.Broadcast($"SET_STATE|{e.NewState}");
                _server.Broadcast($"STATE|{e.NewState}");

                // Управление таймером в зависимости от состояния
                if (e.NewState == GameState.RoundReady)
                {
                    // Настройка интерфейса для Префинала (7 раунд)
                    if (_engine.CurrentRound == 7)
                    {
                        VotingBorder.Visibility = Visibility.Collapsed;
                        BtnToFinal.Visibility = Visibility.Collapsed;
                        Log("ВНИМАНИЕ: Префинальный раунд. Голосование отключено, банк будет удвоен.");
                    }
                    else
                    {
                        VotingBorder.Visibility = Visibility.Visible;
                        BtnToFinal.Visibility = Visibility.Collapsed;
                    }

                    int timeSeconds = 150;
                    switch (_engine.CurrentRound)
                    {
                        case 1: timeSeconds = 150; break;
                        case 2: timeSeconds = 140; break;
                        case 3: timeSeconds = 130; break;
                        case 4: timeSeconds = 120; break;
                        case 5: timeSeconds = 110; break;
                        case 6: timeSeconds = 100; break;
                        case 7: timeSeconds = 90;  break;
                        default: timeSeconds = 150; break;
                    }

                    if (ChkTestLast30Sec != null && ChkTestLast30Sec.IsChecked == true)
                        timeSeconds = 30;

                    // Определение стартового игрока
                    string startingPlayer = _engine.GetStartingPlayerForRound(_engine.CurrentRound);
                    StartingPlayerTextBlock.Text = $"Начинает раунд: {startingPlayer}";

                    ResetTimer(timeSeconds);
                    Log($"Подготовка к раунду {_engine.CurrentRound}. Начинает: {startingPlayer}. Время: {timeSeconds}с.");
                }
                else if (e.NewState == GameState.Playing)
                {
                    // Таймер теперь запускается в BtnState_Click после задержки
                    Log("Переход в режим игры...");
                }
                else if (e.NewState == GameState.RoundSummary)
                {
                    _roundTimer.Stop();
                    Log("Таймер раунда остановлен. Показ итогов раунда.");

                    BtnStartVoting.IsEnabled = true;
                    BtnStartVoting.Background = BrushBankOrange;
                    BtnStartVoting.Foreground = Brushes.White;

                    if (!_fullBankTriggered)
                    {
                        _hostScreen?.ShowRoundSummary(_lastRoundBankForSummary);
                        _hostModernScreen?.ShowRoundSummary(_lastRoundBankForSummary);
                        _hostPremiumScreen?.ShowRoundSummary(_lastRoundBankForSummary);
                        _hostModernPremiumScreen?.ShowRoundSummary(_lastRoundBankForSummary);
                    }
                    _fullBankTriggered = false;

                    OpenRoundAnalytics();
                }
                else if (e.NewState == GameState.Voting || e.NewState == GameState.Idle)
                {
                    _roundTimer.Stop();
                    Log("Таймер раунда остановлен.");
                }

                if (e.OldState == GameState.Voting && e.NewState != GameState.Voting)
                {
                    _votingTimer?.Stop();
                    _votingTimer = null;
                    TxtTimerLabel.Text = "ТАЙМЕР";
                    TxtTimer.Foreground = TimerBrushNormal;
                    ApplyTimerGlow(HubColorNormal);
                    StopTimerPulse();
                    StartingPlayerTextBlock.Text = "—";
                    StartingPlayerTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
                }
                if (e.NewState == GameState.Voting)
                {
                    BtnStartVoting.IsEnabled = false;
                    SetButtonDisabled(BtnStartVoting);
                    if (BtnBotTurn.Visibility == Visibility.Visible)
                        SimulateBotVotes();
                }
                if (e.NewState == GameState.Discussion)
                {
                    VotingBorder.Visibility = Visibility.Visible;
                    BtnReveal.IsEnabled = true;
                    BtnReveal.Background = new SolidColorBrush(Color.FromRgb(0x88, 0x00, 0xAA));
                    BtnReveal.Foreground = Brushes.White;

                    _hostScreen?.ShowDiscussion();
                    _hostModernScreen?.ShowDiscussion();
                    _hostPremiumScreen?.ShowDiscussion();
                    _hostModernPremiumScreen?.ShowDiscussion();

                    if (_statsAnalyzer.IsTieDetected(_botVotes))
                    {
                        var tiedNames = _statsAnalyzer.GetTiedPlayerNames(_botVotes);
                        int rd = Math.Max(0, _engine.GetRoundDuration() - _timeLeftSeconds);
                        string sl = _statsAnalyzer.GetStrongestLinkName(rd);
                        _tiedPlayerNames = tiedNames;
                        _tieStrongestLink = sl;

                        if (tiedNames.Contains(sl) && tiedNames.Count == 2)
                        {
                            string autoElim = tiedNames.First(n => n != sl);
                            _server.Broadcast($"HOST_MESSAGE|Ничья. Но {sl} — самое сильное звено.\n{autoElim}, вы выбываете автоматически!");
                            EliminationComboBox.ItemsSource = new List<string> { autoElim };
                            EliminationComboBox.SelectedIndex = 0;
                            Log($"НИЧЬЯ (Сценарий А): СЗ {sl} спасено. Автовыбывание: {autoElim}.");
                        }
                        else
                        {
                            string namesList = string.Join(" или ", tiedNames.Where(n => n != sl));
                            _server.Broadcast($"HOST_MESSAGE|{sl}, голоса разделились поровну.\nВы самое сильное звено. Кто должен покинуть команду: {namesList}?");
                            EliminationComboBox.ItemsSource = tiedNames;
                            Log($"НИЧЬЯ (Сценарий Б): Решает {sl}. Кандидаты: {string.Join(", ", tiedNames)}.");
                        }
                    }
                    else
                    {
                        var sortedPlayers = _statsAnalyzer.GetSortedPlayersByPerformanceDesc(
                            Math.Max(0, _engine.GetRoundDuration() - _timeLeftSeconds)
                        );
                        
                        var analytics = _statsAnalyzer.AnalyzeRound(Math.Max(0, _engine.GetRoundDuration() - _timeLeftSeconds));
                        string predictedElimination = analytics.EliminationPrediction;
                        
                        var items = sortedPlayers.Select(p => new EliminationComboItem 
                        { 
                            Name = p,
                            TextColor = (p == predictedElimination) ? "#da373c" : "#dbdee1"
                        }).ToList();
                        
                        EliminationComboBox.ItemsSource = items;
                        
                        _tiedPlayerNames = null;
                        _tieStrongestLink = null;
                    }
                    Log("Обсуждение: оператор может исключить игрока.");
                }
                if (e.NewState == GameState.Reveal)
                {
                    _hostScreen?.ShowReveal(_lastRoundBankForSummary);
                    _hostModernScreen?.ShowReveal(_lastRoundBankForSummary);
                    _hostPremiumScreen?.ShowReveal(_lastRoundBankForSummary);
                    _hostModernPremiumScreen?.ShowReveal(_lastRoundBankForSummary);
                }
                if (e.OldState == GameState.Reveal && e.NewState != GameState.Reveal)
                {
                    StopRevealTracking();
                    RevealProgressPanel.Visibility = Visibility.Collapsed;
                }
                
                if (e.NewState == GameState.RoundSummary || e.NewState == GameState.Voting || e.NewState == GameState.Discussion || e.NewState == GameState.Reveal || e.NewState == GameState.Elimination)
                {
                    TxtCurrentQuestion.Text = "";
                    TxtCurrentAnswer.Text = "ОТВЕТ: —";
                    _server.Broadcast("CLEAR_QUESTION");
                    if (e.NewState != GameState.RoundSummary)
                    {
                        _hostScreen?.ClearQuestionAndTimer();
                        _hostModernScreen?.ClearQuestionAndTimer();
                        _hostPremiumScreen?.ClearQuestionAndTimer();
                        _hostModernPremiumScreen?.ClearQuestionAndTimer();
                    }
                }
                
                if ((e.OldState == GameState.Discussion || e.OldState == GameState.Voting || e.OldState == GameState.Reveal || e.OldState == GameState.Elimination) 
                    && e.NewState != GameState.Voting && e.NewState != GameState.Discussion && e.NewState != GameState.Reveal && e.NewState != GameState.Elimination)
                {
                    _timeOutGeneralBedFallbackTimer?.Stop();
                    _timeOutGeneralBedFallbackTimer = null;
                    StopVotingTrackCrossfadeWatch();
                    _botVotes.Clear();
                    _tiedPlayerNames = null;
                    _tieStrongestLink = null;
                    CloseAnalytics();
                }
                
                // При выходе из Playing — возврат к RoundReadyState (список игроков и т.п.)
                if (e.OldState == GameState.Playing && e.NewState != GameState.Playing)
                {
                    TxtTimer.Foreground = TimerBrushNormal;
                    ApplyTimerGlow(HubColorNormal);
                    StopTimerPulse();
                }
                
                UpdateStateButtons(e.NewState);
                UpdateButtonStates();
                UpdateOperationalHints();
            }));
        }

        /// <summary>
        /// Обновляет строку состояния: что происходит и что нажать.
        /// </summary>
        private void SetOperatorAction(string action)
        {
            _engine.LastActionText = action;
            TxtStatusAction.Text = action;
        }

        private async void SimulateButtonPress(Button btn)
        {
            var border = VisualTreeHelper.GetChild(btn, 0) as Border;
            if (border == null) return;

            var origThickness = border.BorderThickness;
            var origMargin = border.Margin;
            var origOpacity = border.Opacity;

            border.BorderThickness = new Thickness(0, 2, 0, 0);
            border.Margin = new Thickness(0, 3, 0, -3);
            border.Opacity = 0.82;

            await Task.Delay(120);

            border.BorderThickness = origThickness;
            border.Margin = origMargin;
            border.Opacity = origOpacity;
        }

        private static readonly SolidColorBrush StatusBgDefault     = new(Color.FromRgb(0x2D, 0x2D, 0x2D));
        private static readonly SolidColorBrush StatusBgReady       = new(Color.FromRgb(0x1B, 0x3A, 0x1B));
        private static readonly SolidColorBrush StatusBgPlaying     = new(Color.FromRgb(0x00, 0x2B, 0x55));
        private static readonly SolidColorBrush StatusBgVoting      = new(Color.FromRgb(0x3D, 0x00, 0x4D));
        private static readonly SolidColorBrush StatusBgPreGame      = new(Color.FromRgb(0x0A, 0x1A, 0x3A));
        private static readonly SolidColorBrush StatusBgElimination = new(Color.FromRgb(0x4D, 0x00, 0x00));
        private static readonly SolidColorBrush StatusBgFinal       = new(Color.FromRgb(0x3A, 0x2A, 0x00));

        private void UpdateOperationalHints()
        {
            var state = _engine.CurrentState;
            int r = _engine.CurrentRound;
            int players = _engine.ActivePlayers.Count;
            string status;
            string hint;
            SolidColorBrush bg;



            switch (state)
            {
                case GameState.IntroOpening:
                    bg = StatusBgPreGame;
                    status = "Звучит главная тема. Логотип на экране.";
                    hint = "Когда музыка стихнет, жмите INTRO 1 (Рассказ о шоу).";
                    break;

                case GameState.IntroNarrative:
                    bg = StatusBgPreGame;
                    status = "Рассказ о шоу.";
                    hint = "Жмите INTRO 2 (Представление).";
                    break;

                case GameState.PlayerIntro:
                    bg = StatusBgPreGame;
                    status = "Представление участников.";
                    hint = "Жмите RULES (Правила).";
                    break;

                case GameState.RulesExplanation:
                    bg = StatusBgPreGame;
                    status = "Объяснение механики.";
                    hint = "Жмите READY (Сигнал старта).";
                    break;

                case GameState.Idle:
                    bg = StatusBgDefault;
                    if (players == 2 && r >= 7)
                    {
                        status = "Префинал завершён. Осталось двое.";
                        hint = "Нажмите TO FINAL для финальной дуэли.";
                    }
                    else if (r > 0)
                    {
                        status = "Ожидание. Раунд завершён.";
                        hint = "Нажмите NEXT ROUND для подготовки.";
                    }
                    else
                    {
                        status = "Ожидание. Система готова.";
                        hint = "Добавьте игроков и нажмите READY.";
                    }
                    break;

                case GameState.RoundReady:
                    bg = StatusBgReady;
                    status = "Финальный призыв.";
                    hint = players == 2 && r >= 7
                        ? "Жмите START O'CLOCK (Таймер) или TO FINAL."
                        : $"Жмите START O'CLOCK (Таймер {_timeLeftSeconds / 60}:{(_timeLeftSeconds % 60):D2}).";
                    break;

                case GameState.Playing:
                    bg = StatusBgPlaying;
                    status = $"Идёт раунд {r}. Игроки отвечают.";
                    hint = "Принимайте ответы и BANK. Ждите 0:00.";
                    break;

                case GameState.RoundSummary:
                    bg = StatusBgDefault;
                    status = $"Итоги раунда {r}. Ведущая объявляет результат.";
                    hint = "Нажмите VOTE START по команде ведущей.";
                    break;

                case GameState.Voting:
                    bg = StatusBgVoting;
                    status = "Голосование 45 сек. Участники выбирают жертву.";
                    hint = "Вносите голоса в аналитику. Ждите окончания.";
                    break;

                case GameState.Discussion:
                    bg = StatusBgVoting;
                    if (_tiedPlayerNames != null && _tiedPlayerNames.Count >= 2 && _tieStrongestLink != null)
                    {
                        if (_tiedPlayerNames.Contains(_tieStrongestLink) && _tiedPlayerNames.Count == 2)
                        {
                            string autoElim = _tiedPlayerNames.First(n => n != _tieStrongestLink);
                            status = $"НИЧЬЯ! СЗ {_tieStrongestLink} спасено.";
                            hint = $"Выбывает {autoElim}. Жмите REVEAL, затем ELIMINATE.";
                        }
                        else
                        {
                            string candidates = string.Join(" / ", _tiedPlayerNames.Where(n => n != _tieStrongestLink));
                            status = $"НИЧЬЯ! Решает {_tieStrongestLink} (Сильное звено).";
                            hint = $"Ждите решения {_tieStrongestLink} и удаляйте выбранного: {candidates}.";
                        }
                    }
                    else
                    {
                        status = "Таймер стоп. Пора вскрывать планшеты.";
                        hint = "Нажмите REVEAL для вскрытия голосов.";
                    }
                    break;

                case GameState.Reveal:
                    bg = StatusBgVoting;
                    if (_tiedPlayerNames != null && _tiedPlayerNames.Count >= 2 && _tieStrongestLink != null)
                    {
                        if (_tiedPlayerNames.Contains(_tieStrongestLink) && _tiedPlayerNames.Count == 2)
                        {
                            string autoElim2 = _tiedPlayerNames.First(n => n != _tieStrongestLink);
                            status = $"НИЧЬЯ! СЗ спасено. Выбывает {autoElim2}.";
                            hint = "Нажмите VERDICT, затем ELIMINATE.";
                        }
                        else
                        {
                            status = $"НИЧЬЯ! {_tieStrongestLink} выбирает жертву.";
                            hint = "Нажмите VERDICT. Ждите решения и жмите ELIMINATE.";
                        }
                    }
                    else
                    {
                        status = "Спор и вердикт. Карточки раскрыты.";
                        hint = "Нажмите VERDICT, затем ELIMINATE.";
                    }
                    break;

                case GameState.Elimination:
                    bg = StatusBgElimination;
                    string eliminated = _engine.EliminatedPlayerName;
                    status = string.IsNullOrEmpty(eliminated)
                        ? "Игрок уходит. Walk of Shame."
                        : $"{eliminated} уходит. Walk of Shame.";
                    hint = "Ждите конца трека, затем NEXT ROUND.";
                    break;

                case GameState.FinalDuel:
                    bg = StatusBgFinal;
                    int duelBank = _engine.TotalBank;
                    if (_engine.IsSuddenDeath)
                    {
                        int sd1 = _engine.Player1FinalScores.Count(x => x == true);
                        int sd2 = _engine.Player2FinalScores.Count(x => x == true);
                        status = $"РЕЖИМ SUDDEN DEATH. Ничья {sd1}:{sd2}.";
                        hint = "Играем до первого промаха в паре. Принимайте ответы.";
                    }
                    else if (!string.IsNullOrEmpty(_engine.FinalWinner))
                    {
                        status = $"ДУЭЛЬ ЗАВЕРШЕНА! Победитель: {_engine.FinalWinner.ToUpper()}.";
                        hint = $"Приз: {duelBank:N0} ₽. Шоу окончено.";
                    }
                    else if (_engine.Player1FinalScores.Any(x => x != null))
                    {
                        int s1 = _engine.Player1FinalScores.Count(x => x == true);
                        int s2 = _engine.Player2FinalScores.Count(x => x == true);
                        status = $"Финальная дуэль. Счёт {s1}:{s2}. На кону {duelBank:N0} ₽.";
                        hint = "Принимайте ответы кнопками ВЕРНО / НЕВЕРНО.";
                    }
                    else
                    {
                        string duelStrongest = _engine.LastStrongestLinkName;
                        status = $"Подготовка к дуэли. На кону {duelBank:N0} ₽.";
                        hint = !string.IsNullOrEmpty(duelStrongest)
                            ? $"Ждите, пока {duelStrongest} выберет, кто начнёт первым."
                            : "Выберите первого отвечающего и нажмите ПРИМЕНИТЬ.";
                    }
                    break;

                default:
                    bg = StatusBgDefault;
                    status = state.ToString();
                    hint = "";
                    break;
            }

            _engine.StatusDescription = status;
            _engine.InstructionText = hint;

            StatusBarBorder.Background = bg;
            TxtStatusProcess.Text = status;
            TxtStatusHint.Text = hint;
            UpdateStateBadge(state);
        }

        private void UpdateStateBadge(GameState state)
        {
            if (GameStateBadge == null || TxtGameStateBadge == null) return;
            string text;
            string bgHex;
            string fgHex;
            switch (state)
            {
                case GameState.Idle:
                    text = "⏸ ОЖИДАНИЕ"; bgHex = "#1E293B"; fgHex = "#94A3B8"; break;
                case GameState.IntroOpening:
                    text = "🎬 ВСТУПЛЕНИЕ"; bgHex = "#2D1B4E"; fgHex = "#A78BFA"; break;
                case GameState.IntroNarrative:
                    text = "🎤 РАССКАЗ"; bgHex = "#2D1B4E"; fgHex = "#A78BFA"; break;
                case GameState.PlayerIntro:
                    text = "👥 ПРЕДСТАВЛЕНИЕ"; bgHex = "#1E3A5F"; fgHex = "#60A5FA"; break;
                case GameState.RulesExplanation:
                    text = "📋 ПРАВИЛА"; bgHex = "#1E3A5F"; fgHex = "#60A5FA"; break;
                case GameState.RoundReady:
                    text = "⏳ ГОТОВНОСТЬ"; bgHex = "#1A3320"; fgHex = "#4ADE80"; break;
                case GameState.Playing:
                    text = "🔥 ИДЁТ РАУНД"; bgHex = "#14532D"; fgHex = "#22C55E"; break;
                case GameState.RoundSummary:
                    text = "📊 ИТОГИ"; bgHex = "#1C1917"; fgHex = "#D4AA40"; break;
                case GameState.Voting:
                    text = "🗳 ГОЛОСОВАНИЕ"; bgHex = "#422006"; fgHex = "#F59E0B"; break;
                case GameState.Discussion:
                    text = "💬 ОБСУЖДЕНИЕ"; bgHex = "#422006"; fgHex = "#FB923C"; break;
                case GameState.Reveal:
                    text = "🔓 ВСКРЫТИЕ"; bgHex = "#422006"; fgHex = "#FB923C"; break;
                case GameState.Elimination:
                    text = "❌ ВЫБЫВАНИЕ"; bgHex = "#450A0A"; fgHex = "#EF4444"; break;
                case GameState.FinalDuel:
                    text = "⚔ ФИНАЛЬНАЯ ДУЭЛЬ"; bgHex = "#450A0A"; fgHex = "#F87171"; break;
                default:
                    text = state.ToString(); bgHex = "#1E293B"; fgHex = "#94A3B8"; break;
            }
            var converter = new System.Windows.Media.BrushConverter();
            GameStateBadge.Background = (System.Windows.Media.Brush)converter.ConvertFromString(bgHex);
            TxtGameStateBadge.Foreground = (System.Windows.Media.Brush)converter.ConvertFromString(fgHex);
            TxtGameStateBadge.Text = text;
        }

        private void OnEngineBankChanged(object sender, BankChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TxtRoundBank.Text = e.RoundBank.ToString("N0");
                TxtTotalBank.Text = e.TotalBank.ToString("N0");
                TxtRound.Text = e.CurrentRound.ToString();

                // Обновляем видимую статусную панель
                TxtCurrentBank.Text = e.RoundBank.ToString("N0");
                TxtRoundNumber.Text = e.CurrentRound == 0 ? "—" : e.CurrentRound.ToString();
                if (TxtRoundNumberCenter != null)
                {
                    TxtRoundNumberCenter.Text = e.CurrentRound == 0 ? "—" : e.CurrentRound.ToString();
                    TimerArchBorder.Visibility = e.CurrentRound > 0 ? Visibility.Visible : Visibility.Collapsed;
                }

                // Обновляем левый мини-сайдбар
                TxtCurrentBank_Side.Text = e.RoundBank.ToString("N0");
                TxtTotalBank_Side.Text = e.TotalBank.ToString("N0");
                TxtRoundNumber_Side.Text = e.CurrentRound == 0 ? "—" : e.CurrentRound.ToString();
                

                
                if (!string.IsNullOrEmpty(e.CurrentPlayerTurn))
                {
                    StartingPlayerTextBlock.Text = $"Ход игрока: {e.CurrentPlayerTurn}";
                    StartingPlayerTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow);
                }

                UpdateBankChainUI();
                
                // Синхронизация с экраном ведущего и сетью
                _hostScreen?.UpdateBank(e.CurrentChainIndex, e.RoundBank);
                _hostModernScreen?.UpdateBank(e.CurrentChainIndex, e.RoundBank);
                _hostPremiumScreen?.UpdateBank(e.CurrentChainIndex, e.RoundBank);
                _hostModernPremiumScreen?.UpdateBank(e.CurrentChainIndex, e.RoundBank);
                if (_broadcastWindow != null && _broadcastWindow.IsLoaded) _broadcastWindow.UpdateBank(e.CurrentChainIndex, e.RoundBank);
                if (_audienceScreen  != null && _audienceScreen.IsLoaded)  _audienceScreen.UpdateBank(e.CurrentChainIndex, e.RoundBank);
                _server.Broadcast($"UPDATE_BANK|{e.CurrentChainIndex}|{e.RoundBank}");

                // Логирование важных финансовых операций
                if (e.CurrentChainIndex == 0)
                {
                    // Мог быть "Банк" или "Неверно"
                    // В данном случае просто обновляем визуализацию
                }
            }));
        }

        private void OnMaxBankReached()
        {
            Dispatcher.Invoke(() =>
            {
                Log("МАКСИМАЛЬНЫЙ БАНК ДОСТИГНУТ! Остановка таймера.");
                _roundTimer.Stop();

                _audioManager.Stop();

                try
                {
                    if (_engine.CurrentState == GameState.Playing)
                    {
                        if (_engine.ActivePlayers.Count == 2)
                        {
                            int rawBank = _engine.RoundBank;
                            _lastRoundBankForSummary = rawBank;
                            _engine.ApplyRoundBankToTotal();
                            VotingBorder.Visibility = Visibility.Collapsed;
                            BtnToFinal.Visibility = Visibility.Visible;
                            BtnToFinal.IsEnabled = true;
                            BtnToFinal.Background = BrushActiveGreen;
                            BtnToFinal.Foreground = Brushes.White;

                            _server.Broadcast($"HOST_MESSAGE|В этом раунде вы заработали {rawBank:N0} руб. Мы удваиваем эту сумму!\nВаш общий призовой фонд сегодня составляет {_engine.TotalBank:N0} рублей.");
                            SetOperatorAction($"Банк 7-го раунда удвоен! Итого: {_engine.TotalBank:N0} ₽");
                            Log($"Префинал: полный банк ({rawBank} ₽ × 2 = {rawBank * 2} ₽). Итого: {_engine.TotalBank} ₽.");

                            _audioManager.Play("Assets/Audio/Full_Bank_End.mp3", loop: false);
                            _hostScreen?.ShowFullBank(_engine.TotalBank);
                            _hostModernScreen?.ShowFullBank(_engine.TotalBank);
                            _hostPremiumScreen?.ShowFullBank(_engine.TotalBank);
                            _hostModernPremiumScreen?.ShowFullBank(_engine.TotalBank);

                            _engine.TransitionTo(GameState.Idle);
                            OpenRoundAnalytics();
                        }
                        else
                        {
                            _lastRoundBankForSummary = _engine.RoundBank;
                            _engine.ApplyRoundBankToTotal();
                            Log($"Полный банк ({_lastRoundBankForSummary} ₽). Full_Bank_End.mp3 → итоги.");

                            _fullBankTriggered = true;
                            _audioManager.Play("Assets/Audio/Full_Bank_End.mp3", loop: false);
                            _hostScreen?.ShowFullBank(_lastRoundBankForSummary);
                            _hostModernScreen?.ShowFullBank(_lastRoundBankForSummary);
                            _hostPremiumScreen?.ShowFullBank(_lastRoundBankForSummary);
                            _hostModernPremiumScreen?.ShowFullBank(_lastRoundBankForSummary);

                            _engine.TransitionTo(GameState.RoundSummary);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("Ошибка перехода после полного банка: " + ex.Message);
                }
            });
        }

        #endregion

        #region Таймер

        private void RoundTimer_Tick(object sender, EventArgs e)
        {
            const string timeoutMessage = "Время вышло, я не успеваю закончить вопрос";
            if (_timeLeftSeconds == 2 && _engine.CurrentState == GameState.Playing && !string.IsNullOrEmpty(TxtCurrentQuestion.Text))
            {
                TxtCurrentQuestion.Text = timeoutMessage;
                TxtCurrentAnswer.Text = "ОТВЕТ: —";
                _hostScreen?.UpdateQuestion(timeoutMessage, "—", _questionCount);
                _hostModernScreen?.UpdateQuestion(timeoutMessage, "—", _questionCount);
                _hostPremiumScreen?.UpdateQuestion(timeoutMessage, "—", _questionCount);
                _hostModernPremiumScreen?.UpdateQuestion(timeoutMessage, "—", _questionCount);
                _server.Broadcast($"QUESTION|{timeoutMessage}|—|{_questionCount}");
            }

            if (_timeLeftSeconds > 0)
            {
                _timeLeftSeconds--;
                UpdateTimerDisplay();
            }
            else
            {
                _roundTimer.Stop();
                _isAutoTestRunning = false;
                Log("ВРЕМЯ ВЫШЛО!");
                
                try 
                {
                    if (_engine.CurrentState == GameState.Playing)
                    {
                        if (_engine.ActivePlayers.Count == 2)
                        {
                            int rawBank = _engine.RoundBank;
                            _lastRoundBankForSummary = rawBank;
                            _engine.ApplyRoundBankToTotal();

                            _server.Broadcast($"HOST_MESSAGE|В этом раунде вы заработали {rawBank:N0} руб. Мы удваиваем эту сумму!\nВаш общий призовой фонд сегодня составляет {_engine.TotalBank:N0} рублей.");
                            SetOperatorAction($"Банк 7-го раунда удвоен! Итого: {_engine.TotalBank:N0} ₽");
                            Log($"Префинал завершён. Банк {rawBank} ₽ × 2 = {rawBank * 2} ₽. Итого: {_engine.TotalBank} ₽.");

                            _currentQuestion = null;
                            UpdateQuestionDisplay();

                            _engine.TransitionTo(GameState.Idle);
                            _eliminationPerformedThisRound = true;
                            _audioManager.PlayBed("general_bed.mp3", loop: true);

                            VotingBorder.Visibility = Visibility.Collapsed;
                            BtnToFinal.Visibility = Visibility.Visible;
                            BtnToFinal.IsEnabled = true;
                            BtnToFinal.Background = BrushActiveGreen;
                            BtnToFinal.Foreground = Brushes.White;

                            OpenRoundAnalytics();
                            Log("Нажмите ПЕРЕЙТИ К ФИНАЛУ в карточке аналитики.");
                        }
                        else
                        {
                            _lastRoundBankForSummary = _engine.RoundBank;
                            _engine.ApplyRoundBankToTotal();
                            Log($"Время вышло. Банк раунда ({_lastRoundBankForSummary} ₽) добавлен в общий.");

                            _currentQuestion = null;
                            UpdateQuestionDisplay();

                            _audioManager.StartBedWithFadeIn("general_bed.mp3", 2.0);
                            _engine.TransitionTo(GameState.RoundSummary);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("Ошибка автоперехода: " + ex.Message);
                }
            }
        }

        private void ResetTimer(int seconds)
        {
            _hubRoundDuration = seconds > 0 ? seconds : 1; // avoid division by zero
            _timeLeftSeconds = seconds;
            GenerateTimerHubDots(seconds);
            UpdateTimerDisplay();
        }

        /// <summary>
        /// Generates exactly 20 time-segment dots per wing, representing blocks of seconds.
        /// Dots are styled as outlines with near-black centers to leave a "depression" when faded.
        /// </summary>
        private void GenerateTimerHubDots(int totalSeconds)
        {
            _hubDots.Clear();
            _hubTotalDots = 0;
        }

        private System.Windows.Controls.Border CreateHubDot()
        {
            var dot = new System.Windows.Controls.Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Colors.Transparent), // Transparent inside
                BorderBrush = new SolidColorBrush(HubDotBorder), // Thin gunmetal
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2, 0, 2, 0),
            };
            dot.Effect = new DropShadowEffect
            {
                Color = HubColorGlow, // Bright cyan glow
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.0
            };
            return dot;
        }

        private void UpdateTimerDisplay()
        {
            int minutes = _timeLeftSeconds / 60;
            int seconds = _timeLeftSeconds % 60;
            string timeStr = $"{minutes}:{seconds:D2}";
            TxtTimer.Text = timeStr;
            TxtTimer_Side.Text = timeStr;

            // Determine active color tier
            Color activeColor;
            Color activeGlowColor;
            bool isCritical = false;
            if (_engine.CurrentState == GameState.Playing && _timeLeftSeconds > 0 && _timeLeftSeconds <= 10)
            {
                activeColor = HubColorCritical;
                activeGlowColor = HubColorCritical;
                isCritical = true;
            }
            else
            {
                activeColor = HubColorNormal;
                activeGlowColor = HubColorGlow;
            }

            // Apply color to timer digits (white normally, red when critical)
            var timerBrush = isCritical ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.White);
            TxtTimer.Foreground = timerBrush;
            TxtTimer_Side.Foreground = timerBrush;

            // Apply glow to timer text and arch
            ApplyTimerGlow(activeGlowColor);

            // Pulse animation for critical state
            if (isCritical)
                StartTimerPulse();
            else
                StopTimerPulse();

            // Smooth dot dimming logic (Energy Filling effect)
            if (_hubTotalDots > 0)
            {
                UpdateHubDots(activeColor, activeGlowColor);
            }

            if (_hostScreen != null && _hostScreen.IsLoaded)
                _hostScreen.UpdateTimer(timeStr);
            if (_hostModernScreen != null && _hostModernScreen.IsLoaded)
                _hostModernScreen.UpdateTimer(timeStr);
            if (_hostPremiumScreen != null && _hostPremiumScreen.IsLoaded)
                _hostPremiumScreen.UpdateTimer(timeStr);
            if (_hostModernPremiumScreen != null && _hostModernPremiumScreen.IsLoaded)
                _hostModernPremiumScreen.UpdateTimer(timeStr);
            if (_broadcastWindow != null && _broadcastWindow.IsLoaded)
                _broadcastWindow.UpdateTimer(timeStr, _timeLeftSeconds);
            if (_audienceScreen != null && _audienceScreen.IsLoaded)
                _audienceScreen.UpdateTimer(timeStr, _timeLeftSeconds);
        }

        /// <summary>
        /// Smoothly dims dots from edges toward center based on exact elapsed fractional time.
        /// As time passes, segments un-fill (fade out completely to 0 opacity), leaving only outline.
        /// </summary>
        private void UpdateHubDots(Color fillColor, Color glowColor)
        {
            if (_hubDots.Count == 0 || _hubRoundDuration <= 0) return;

            int totalDots = _hubDots.Count;
            int dotsPerWing = totalDots / 2;
            double timePerDot = (double)_hubRoundDuration / dotsPerWing;
            double elapsed = _hubRoundDuration - _timeLeftSeconds;

            for (int i = 0; i < totalDots; i++)
            {
                // index 0..19 is Left Wing (inner to outer). index 20..39 is Right Wing (inner to outer).
                int distanceFromCenter = i % dotsPerWing;
                
                // Fading happens from edges to center. So the outer edge (highest distance) fades first (burnIndex = 0).
                int burnIndex = (dotsPerWing - 1) - distanceFromCenter;

                var dot = _hubDots[i];

                double dotStartTime = burnIndex * timePerDot;
                double dotEndTime = (burnIndex + 1) * timePerDot;

                double progress = 0; // 0 = fully active (filled), 1 = fully extinguished (empty contour)
                if (elapsed >= dotEndTime) progress = 1.0;
                else if (elapsed <= dotStartTime) progress = 0.0;
                else progress = (elapsed - dotStartTime) / timePerDot;

                // Filling transition: Outline remains solid, Background Fill & Glow fade smoothly
                dot.Background = new SolidColorBrush(fillColor) { Opacity = 1 - progress };
                
                if (dot.Effect is DropShadowEffect eff)
                {
                    eff.Color = glowColor;
                    eff.Opacity = 0.8 * (1 - progress);
                }
            }
        }

        private void ApplyTimerGlow(Color glowColor)
        {
            if (TxtTimer.Effect is DropShadowEffect glow)
                glow.Color = glowColor;
            if (TimerArchBorder?.Effect is DropShadowEffect archGlow)
            {
                archGlow.Color = glowColor;
                // Leave the BlurRadius to be managed by the Storyboard (breathing) when critical
            }
        }

        private void StartTimerPulse()
        {
            if (_timerPulseRunning) return;
            _timerPulseRunning = true;
                // Timer pulse removed (simple timer)
                try
                {
                    if (TimerArchBorder?.Parent is FrameworkElement parent
                        && parent.Resources.Contains("TimerHubPulseStoryboard"))
                    {
                        var sb = (Storyboard)parent.Resources["TimerHubPulseStoryboard"];
                        sb.Begin();
                    }
                }
            catch { /* storyboard not found — silent */ }
        }

        private void StopTimerPulse()
        {
            if (!_timerPulseRunning) return;
            _timerPulseRunning = false;
                // Timer pulse removed (simple timer)
                try
                {
                    if (TimerArchBorder?.Parent is FrameworkElement parent
                        && parent.Resources.Contains("TimerHubPulseStoryboard"))
                    {
                        var sb = (Storyboard)parent.Resources["TimerHubPulseStoryboard"];
                        sb.Stop();
                    }
                    if (TimerArchBorder != null)
                        TimerArchBorder.Opacity = 1.0;
                }
            catch { /* storyboard not found — silent */ }
        }

        // VotingDictorStatusTimer_Tick removed — voting system pending redesign

        // VotingTimer_Tick / StartVotingCountdown removed — voting system pending redesign

        #endregion

        #region Обработчики кнопок

        private void BtnCorrect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string player = _engine.CurrentPlayerTurn;
                _engine.CorrectAnswer();
                SetOperatorAction($"Принято: ВЕРНО ({player})");
                Log($"ВЕРНО! Игрок {player} (+1 к статистике). Теперь ход: {_engine.CurrentPlayerTurn}");
                UpdateStatsTable();
                UpdateBankChainUI();
                TxtRoundBank.Text = _engine.RoundBank.ToString("N0");
                TxtTotalBank.Text = _engine.TotalBank.ToString("N0");
                
                // ПУЛЕМЕТНЫЙ ТЕМП: грузим следующий вопрос сразу
                LoadNextQuestion();
            }
            catch (Exception ex)
            {
                Log("ОШИБКА: " + ex.Message);
            }
        }

        private void BtnWrong_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string player = _engine.CurrentPlayerTurn;
                _engine.WrongAnswer();
                SetOperatorAction($"Принято: НЕВЕРНО ({player})");
                Log($"НЕВЕРНО! Цепочка сброшена ({player}). Теперь ход: {_engine.CurrentPlayerTurn}");
                UpdateStatsTable();
                UpdateBankChainUI();
                TxtRoundBank.Text = _engine.RoundBank.ToString("N0");
                TxtTotalBank.Text = _engine.TotalBank.ToString("N0");

                // ПУЛЕМЕТНЫЙ ТЕМП: грузим следующий вопрос сразу
                LoadNextQuestion();
            }
            catch (Exception ex)
            {
                Log("ОШИБКА: " + ex.Message);
            }
        }

        private void BtnPass_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string player = _engine.CurrentPlayerTurn;
                _engine.Pass();
                SetOperatorAction($"Принято: ПАС ({player})");
                Log($"ПАС! {player} передал ход. Цепочка сброшена.");
                UpdateStatsTable();
                UpdateBankChainUI();
                TxtRoundBank.Text = _engine.RoundBank.ToString("N0");
                TxtTotalBank.Text = _engine.TotalBank.ToString("N0");

                // ПУЛЕМЕТНЫЙ ТЕМП: грузим следующий вопрос сразу
                LoadNextQuestion();
            }
            catch (Exception ex)
            {
                Log("ОШИБКА: " + ex.Message);
            }
        }

        private void BtnBank_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int indexBefore = _engine.CurrentChainIndex;
                if (indexBefore > 0)
                {
                    int value = _engine.BankChain[indexBefore - 1];
                    _engine.Bank();
                    SetOperatorAction($"Принято: БАНК +{value:N0} ₽");
                    Log($"БАНК! +{value}. Всего в раунде: {_engine.RoundBank}");
                    UpdateStatsTable();
                    UpdateBankChainUI();
                    TxtRoundBank.Text = _engine.RoundBank.ToString("N0");
                    TxtTotalBank.Text = _engine.TotalBank.ToString("N0");
                }
                else
                {
                    SetOperatorAction("БАНК (цепочка пуста)");
                    Log("БАНК невозможен (цепочка пуста).");
                }
            }
            catch (Exception ex)
            {
                Log("ОШИБКА: " + ex.Message);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Q — скриншот в любом состоянии
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Q)
            {
                TakeScreenshot();
                e.Handled = true;
                return;
            }

            if (_engine.CurrentState != GameState.Playing) return;

            switch (e.Key)
            {
                case Key.Right: // ВЕРНО
                    BtnCorrect_Click(null, null);
                    break;
                case Key.Left:  // НЕВЕРНО
                    BtnWrong_Click(null, null);
                    break;
                case Key.Down:  // ПАС
                case Key.Back:  // ПАС (BACKSPACE)
                    BtnPass_Click(null, null);
                    break;
                case Key.Space: // БАНК! Самая большая клавиша для самого важного действия
                    BtnBank_Click(null, null);
                    break;
            }
        }

        private void BtnReady_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((_engine.ActivePlayers.Count <= 2 && _engine.CurrentRound > 0) || _engine.CurrentRound >= 7)
                {
                    if (TglAutoBot != null && TglAutoBot.IsChecked == true)
                    {
                        TglAutoBot.IsChecked = false;
                        _autoBotTimer?.Stop();
                    }
                    
                    DarkMessageBox.Show("Осталось 2 игрока! Обычные раунды больше недоступны. Используйте кнопку ТО FINAL для запуска финала.", "Достигнут финал", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _nextRoundUsed = false;
                _eliminationPerformedThisRound = false;
                SetOperatorAction("Подготовка раунда");
                if (_engine.CurrentState == GameState.RoundReady && BtnBotTurn.Visibility != Visibility.Visible)
                    return;

                bool isTestMode = (BtnBotTurn.Visibility == Visibility.Visible);
                bool comingFromRules = (_engine.CurrentState == GameState.RulesExplanation);

                if (comingFromRules)
                {
                    _audioManager.Stop();
                }
                else if (isTestMode && _engine.CurrentState == GameState.RoundReady)
                {
                    _engine.FinalizeRoundSetup();
                }
                else
                {
                    _engine.PrepareNewRound();
                }
                TxtRound.Text = _engine.CurrentRound.ToString();
                if (TxtRoundNumberCenter != null)
                {
                    TxtRoundNumberCenter.Text = _engine.CurrentRound.ToString();
                    TimerArchBorder.Visibility = _engine.CurrentRound > 0 ? Visibility.Visible : Visibility.Collapsed;
                }

                _audioManager.PlayBed("playgame_with_general_bed.mp3", loop: false);
                _server.Broadcast("CLEAR_ELIMINATION");

                int newRoundDuration = (ChkTestLast30Sec != null && ChkTestLast30Sec.IsChecked == true) ? 30 : _engine.GetNewRoundDuration();
                _timeLeftSeconds = newRoundDuration;
                UpdateTimerDisplay();
                _server.Broadcast($"UPDATE_TIMER|{_timeLeftSeconds}");
                _server.Broadcast($"CURRENT_PLAYER|{_engine.CurrentPlayerTurn}");

                _questionCount = 0;
                LoadNextQuestion();

                _engine.TransitionTo(GameState.RoundReady);

                string firstPlayer = _engine.CurrentPlayerTurn ?? "";
                string prompterText = _engine.CurrentRound == 1
                    ? $"Играем в «Слабое звено»! Начинаем с {firstPlayer}. Время пошло!"
                    : $"Раунд {_engine.CurrentRound}. Начинаем с {firstPlayer}. Время пошло!";
                _server.Broadcast($"HOST_MESSAGE|{prompterText}");

                SetCentralContext("PLAY");
                GamePlayPanel.Visibility = Visibility.Visible;
                HeadToHeadPanel.Visibility = Visibility.Collapsed;
                WinnerPanel.Visibility = Visibility.Collapsed;
                UpdateBankChainUI();

                TxtRoundBank.Text = _engine.RoundBank.ToString("N0");
                TxtTotalBank.Text = _engine.TotalBank.ToString("N0");

                StartingPlayerTextBlock.Text = $"НАЧИНАЕТ: {_engine.CurrentPlayerTurn.ToUpper()}";
                StartingPlayerTextBlock.Foreground = new SolidColorBrush(Colors.Yellow);

                // ФИНАЛЬНЫЙ ПИНОК: гарантируем обновление UI после всех переключений контекста
                UpdateQuestionDisplay();

                // Кнопки ещё неактивны — ждём START O'CLOCK
                BtnCorrect.IsEnabled = false;
                BtnIncorrect.IsEnabled = false;
                BtnWrong.IsEnabled = false;
                BtnBank.IsEnabled = false;
                if (BtnPass != null) BtnPass.IsEnabled = false;

                BtnPlay.IsEnabled = true;
                BtnGamePlayPlay.IsEnabled = true;
                BtnPlay.Focus();

                Log($"Раунд {_engine.CurrentRound} готов к запуску. Жмите START O'CLOCK.");
            }
            catch (Exception ex)
            {
                Log($"КРИТИЧЕСКАЯ ОШИБКА READY: {ex.Message}");
            }
        }

        private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnPlay.IsEnabled = false;
                SetButtonDisabled(BtnPlay);
                BtnGamePlayPlay.IsEnabled = false;
                BtnGamePlayPlay.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
                BtnGamePlayPlay.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));

                SetOperatorAction("Таймер запущен (START O'CLOCK)");
                _server.Broadcast("HOST_MESSAGE|CLEAR");
                string fileName = _engine.CurrentRound switch
                {
                    1 => "Round_bed_(230).mp3",
                    2 => "Round_bed_(220).mp3",
                    3 => "Round_bed_(210).mp3",
                    4 => "Round_bed_(200).mp3",
                    5 => "Round_bed_(150).mp3",
                    6 => "Round_bed_(140).mp3",
                    7 => "Round_bed_(130).mp3",
                    _ => "Round_bed_(130).mp3"
                };
                bool test30 = (ChkTestLast30Sec != null && ChkTestLast30Sec.IsChecked == true);
                if (test30)
                {
                    int fullRoundDuration = _engine.GetRoundDuration();
                    double introSec = TRACK_INTRO_BEAT_MS / 1000.0;
                    double skipSeconds = (fullRoundDuration - 30) + introSec;
                    if (skipSeconds < 0) skipSeconds = 0;
                    _audioManager.Play($"Assets/Audio/{fileName}", startFromSeconds: skipSeconds);
                }
                else
                {
                    _audioManager.Play($"Assets/Audio/{fileName}");
                }
                await Task.Delay(TRACK_INTRO_BEAT_MS);

                _engine.TransitionTo(GameState.Playing);
                _roundTimer.Start();
                // LoadNextQuestion() убрано, так как вопрос уже загружен при переходе в READY.

                // Активация игровых кнопок (панели уже переключены в READY)
                BtnCorrect.IsEnabled = true;
                BtnWrong.IsEnabled = true;
                BtnBank.IsEnabled = true;
                if (BtnPass != null) BtnPass.IsEnabled = true;
                BtnIncorrect.IsEnabled = true;

                // 4. Перевод фокуса на кнопку БАНК (самая частая и быстрая кнопка)
                BtnBank.Focus();

                Log("РАУНД ЗАПУЩЕН. Время пошло.");

                if (_isAutoTestRunning)
                    _ = ProcessBotTurnAsync();
            }
            catch (Exception ex)
            {
                Log($"ОШИБКА ЗАПУСКА РАУНДА: {ex.Message}");
                DarkMessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnState_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string stateStr)
            {
                if (Enum.TryParse(stateStr, out GameState newState) && newState != GameState.Playing)
                {
                    try
                    {
                        if (newState == GameState.RoundReady)
                        {
                            if ((_engine.ActivePlayers.Count <= 2 && _engine.CurrentRound > 0) || _engine.CurrentRound >= 7)
                            {
                                DarkMessageBox.Show("Осталось 2 игрока! Обычные раунды больше недоступны. Используйте кнопку ТО FINAL для запуска финала.", "Достигнут финал", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }

                            _audioManager.PlayBed("playgame_with_general_bed.mp3", loop: false);
                            _server.Broadcast("CLEAR_ELIMINATION");
                            if (_engine.CurrentState == GameState.Elimination)
                            {
                                _engine.NextRound();
                            }
                        }

                        _engine.TransitionTo(newState);
                    }
                    catch (Exception ex)
                    {
                        Log("ОШИБКА ПЕРЕХОДА: " + ex.Message);
                        DarkMessageBox.Show(ex.Message, "Ошибка состояния", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private void BtnToFinal_Click(object sender, RoutedEventArgs e)
        {
            SetOperatorAction("Переход к финалу");
            if (_engine.ActivePlayers.Count != 2)
            {
                DarkMessageBox.Show("Для финала должно остаться ровно 2 игрока!", "Ошибка");
                return;
            }

            _audioManager.PlayBed("final_round_prestart.mp3", loop: false);
            Log("Запуск финальной дуэли (Head-to-Head). Трек final_round_prestart.");

            TxtFinalist1.Text = _engine.ActivePlayers[0].ToUpper();
            TxtFinalist2.Text = _engine.ActivePlayers[1].ToUpper();

            CmbFirstResponder.Items.Clear();
            CmbFirstResponder.Items.Add(new ComboBoxItem { Content = TxtFinalist1.Text, Tag = true });
            CmbFirstResponder.Items.Add(new ComboBoxItem { Content = TxtFinalist2.Text, Tag = false });
            CmbFirstResponder.SelectedIndex = 0;

            string strongest = _engine.LastStrongestLinkName;
            int totalBank = _engine.TotalBank;
            string duelPrompt = $"Сейчас вам придётся играть друг против друга. " +
                $"Вы по очереди ответите на 5 пар вопросов. На кону {totalBank:N0} рублей.\n\n" +
                $"{strongest}, как сильное звено прошлого раунда, вы выбираете, кто будет первым отвечать на вопросы.";
            _server.Broadcast($"HOST_MESSAGE|{duelPrompt}");

            FinalPrestartBorder.Visibility = Visibility.Visible;
            BtnStartDuel.Visibility = Visibility.Collapsed;

            BtnToFinal.Visibility = Visibility.Collapsed;
            SetCentralContext("PLAY");
            GamePlayPanel.Visibility = Visibility.Collapsed;
            HeadToHeadPanel.Visibility = Visibility.Visible;
            SetFinalDuelUIState(true);
            UpdateFinalCirclesUI();

            _isSuddenDeathMusicPlayed = false;
            _suddenDeathMessageSent = false;
            Log($"Финал: {TxtFinalist1.Text} VS {TxtFinalist2.Text}. Сильное звено: {strongest}. Выберите первого отвечающего.");
        }

        private void BtnApplyDuel_Click(object sender, RoutedEventArgs e)
        {
            bool player1First = true;
            if (CmbFirstResponder.SelectedItem is ComboBoxItem item && item.Tag is bool tag)
                player1First = tag;

            _engine.StartFinalDuel(player1First);
            _audioManager.Stop();
            _audioManager.Play("Assets/Audio/final_round_start.mp3", loop: false);
            _server.Broadcast("HOST_MESSAGE|CLEAR");
            SetOperatorAction("Дуэль запущена");
            Log($"ПРИМЕНИТЬ: Трек final_round_start. Первым отвечает {(player1First ? TxtFinalist1.Text : TxtFinalist2.Text)}.");

            FinalPrestartBorder.Visibility = Visibility.Collapsed;
            BtnStartDuel.Visibility = Visibility.Collapsed;
            BtnStartDuel.IsEnabled = false;

            FinalQuestionTextBlock.Text = _engine.GetNextFinalQuestion();
            UpdateDuelUI();

            try
            {
                string p1s = string.Join(",", _engine.Player1FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                string p2s = string.Join(",", _engine.Player2FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                string payload = $"{TxtFinalist1.Text}|{TxtFinalist2.Text}|{p1s}|{p2s}";
                _server.Broadcast($"DUEL_UPDATE|{payload}");
                _hostScreen?.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                _hostModernScreen?.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                _hostPremiumScreen?.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                _hostModernPremiumScreen?.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
            }
            catch (Exception ex) { Log($"ОШИБКА DUEL_UPDATE: {ex.Message}"); }
        }

        /// <summary>
        /// Переключает визуальное состояние панелей, которые не используются в финале.
        /// </summary>
        private void SetFinalDuelUIState(bool isFinal)
        {
            double opacity = isFinal ? 0.4 : 1.0;
            bool enabled = !isFinal;

            TimerPanel.Opacity = opacity;
            TimerPanel.IsEnabled = enabled;

            RoundBankPanel.Opacity = opacity;
            RoundBankPanel.IsEnabled = enabled;

            BankChainColumn.Opacity = opacity;
            BankChainColumn.IsEnabled = enabled;

            Log(isFinal ? "Интерфейс: Лишние панели приглушены для финала." : "Интерфейс: Панели восстановлены.");
        }

        private void BtnStartDuel_Click(object sender, RoutedEventArgs e)
        {
            SetOperatorAction("Дуэль запущена!");
            _audioManager.Play("Assets/Audio/final_round_start.mp3", loop: false);
            Log("СТАРТ ДУЭЛИ! Запуск фоновой музыки поединка.");
            BtnStartDuel.IsEnabled = false;
            BtnStartDuel.Opacity = 1.0; // оставляем видимым (стиль disabled — через SetButtonDisabled)
            
            // Прямая загрузка и отображение первого вопроса (Direct UI Update)
            FinalQuestionTextBlock.Text = _engine.GetNextFinalQuestion();
            UpdateDuelUI();
            // UpdateFinalCirclesUI() уже вызывается внутри UpdateDuelUI()
            
            // Дополнительная явная отправка данных для надежности
            try
            {
                string p1s = string.Join(",", _engine.Player1FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                string p2s = string.Join(",", _engine.Player2FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                string payload = $"{TxtFinalist1.Text}|{TxtFinalist2.Text}|{p1s}|{p2s}";
                _server.Broadcast($"DUEL_UPDATE|{payload}");
                _hostScreen?.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                _hostModernScreen?.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                Log($"Отправлена команда DUEL_UPDATE при старте дуэли: {payload}");
            }
            catch (Exception ex)
            {
                Log($"ОШИБКА ОТПРАВКИ DUEL_UPDATE ПРИ СТАРТЕ: {ex.Message}");
            }
        }

        private void BtnDuelCorrect_Click(object sender, RoutedEventArgs e)
        {
            SetOperatorAction("Дуэль: ВЕРНО");
            _engine.ProcessFinalAnswer(true);

            // Явная отправка команды клиентам для надежной синхронизации (Senior Dev Fix)
            try
            {
                string p1s = string.Join(",", _engine.Player1FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                string p2s = string.Join(",", _engine.Player2FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                string payload = $"{TxtFinalist1.Text}|{TxtFinalist2.Text}|{p1s}|{p2s}";
                _server.Broadcast($"DUEL_UPDATE|{payload}");
                _hostScreen?.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                _hostModernScreen?.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                Log("Отправлена команда DUEL_UPDATE (ВЕРНО)");
            }
            catch (Exception ex)
            {
                Log($"ОШИБКА РУЧНОЙ ОТПРАВКИ DUEL_UPDATE: {ex.Message}");
            }

            RefreshFinalUI();
        }

        private void BtnDuelWrong_Click(object sender, RoutedEventArgs e)
        {
            SetOperatorAction("Дуэль: НЕВЕРНО");
            _engine.ProcessFinalAnswer(false);

            // Явная отправка команды клиентам для надежной синхронизации (Senior Dev Fix)
            try
            {
                string p1s = string.Join(",", _engine.Player1FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                string p2s = string.Join(",", _engine.Player2FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                string payload = $"{TxtFinalist1.Text}|{TxtFinalist2.Text}|{p1s}|{p2s}";
                _server.Broadcast($"DUEL_UPDATE|{payload}");
                _hostScreen?.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                _hostModernScreen?.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                Log("Отправлена команда DUEL_UPDATE (НЕВЕРНО)");
            }
            catch (Exception ex)
            {
                Log($"ОШИБКА РУЧНОЙ ОТПРАВКИ DUEL_UPDATE: {ex.Message}");
            }

            RefreshFinalUI();
        }

        private bool _suddenDeathMessageSent = false;
        private List<string>? _tiedPlayerNames = null;
        private string? _tieStrongestLink = null;

        private void RefreshFinalUI()
        {
            UpdateFinalCirclesUI();

            if (string.IsNullOrEmpty(_engine.FinalWinner))
            {
                _engine.GetNextFinalQuestion();
                SendDuelPrompterUpdate();
            }

            UpdateDuelUI();
            _broadcastWindow?.UpdateFinalDuel();
        }

        private void SendDuelPrompterUpdate()
        {
            int score1 = _engine.Player1FinalScores.Count(r => r == true);
            int score2 = _engine.Player2FinalScores.Count(r => r == true);
            int remaining1 = _engine.Player1FinalScores.Count(r => r == null);
            int remaining2 = _engine.Player2FinalScores.Count(r => r == null);
            string p1Name = TxtFinalist1.Text;
            string p2Name = TxtFinalist2.Text;
            string currentPlayer = _engine.IsPlayer1Turn ? p1Name : p2Name;
            string opponent = _engine.IsPlayer1Turn ? p2Name : p1Name;
            int myScore = _engine.IsPlayer1Turn ? score1 : score2;
            int oppScore = _engine.IsPlayer1Turn ? score2 : score1;
            int myRemaining = _engine.IsPlayer1Turn ? remaining1 : remaining2;
            int oppRemaining = _engine.IsPlayer1Turn ? remaining2 : remaining1;

            if (_engine.IsSuddenDeath)
            {
                if (!_suddenDeathMessageSent)
                {
                    _suddenDeathMessageSent = true;
                    string sdText = $"После пяти пар вопросов счёт равный {score1}:{score2}.\n" +
                        "Мы продолжаем до первого проигрыша. Вопросы по-прежнему парами.\n\n" +
                        $"{currentPlayer}, если вы отвечаете правильно, {opponent} тоже обязан ответить верно, иначе проиграет.";
                    _server.Broadcast($"HOST_MESSAGE|{sdText}");
                    Log("HOST_MESSAGE: Sudden Death объявлен.");
                }
                return;
            }

            bool wrongMeansLoss = (oppScore > myScore) && (myRemaining <= 1) && (oppScore > myScore + myRemaining - 1);
            bool correctMeansWin = (myScore >= oppScore) && (myScore + 1 > oppScore + oppRemaining);

            if (wrongMeansLoss)
            {
                _server.Broadcast($"HOST_MESSAGE|Решающий момент: {currentPlayer}, если вы сейчас ответите неверно — вы проиграете.");
            }
            else if (correctMeansWin)
            {
                _server.Broadcast($"HOST_MESSAGE|{currentPlayer}, если вы сейчас ответите правильно — вы победите!");
            }
        }

        private void OnFinalDuelEnded(string winner)
        {
            _audioManager.Play("Assets/Audio/duel_winner.mp3", loop: false);
            
            WinnerPanel.Visibility = Visibility.Visible;

            TxtFinalWinnerName.Text = winner.ToUpper();
            TxtFinalWinnings.Text = _engine.TotalBank.ToString("N0") + " ₽";

            string victoryText = $"{winner.ToUpper()}, сегодня вы — самое сильное звено!\n" +
                $"И вы уходите домой с суммой {_engine.TotalBank:N0} рублей.";
            _server.Broadcast($"HOST_MESSAGE|{victoryText}");
            SetOperatorAction($"ПОБЕДИТЕЛЬ: {winner.ToUpper()}");
            
            Log($"!!! ПОБЕДИТЕЛЬ: {winner}. БАНК: {_engine.TotalBank} !!!");
            _server.Broadcast($"WINNER|{winner}|{_engine.TotalBank}");
        }

        private void UpdateDuelUI()
        {
            // Подсветка текущего игрока (делаем неактивного полупрозрачным)
            TxtFinalist1.Opacity = _engine.IsPlayer1Turn ? 1.0 : 0.4;
            TxtFinalist2.Opacity = !_engine.IsPlayer1Turn ? 1.0 : 0.4;

            // Проверка на Внезапную Смерть для музыки
            if (_engine.IsSuddenDeath && !_isSuddenDeathMusicPlayed)
            {
                _isSuddenDeathMusicPlayed = true;
                _audioManager.Play("Assets/Audio/sudden_death_bed.mp3", loop: false);
                Log("ВНЕЗАПНАЯ СМЕРТЬ! Смена фоновой заставки.");
                TurnTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
            
            // Текст вопроса для дуэли
            if (_engine.CurrentFinalQuestion != null)
            {
                FinalQuestionTextBlock.Text = _engine.CurrentFinalQuestion.Text;
                
                string fullAnswer = _engine.CurrentFinalQuestion.Answer;
                if (!string.IsNullOrEmpty(_engine.CurrentFinalQuestion.AcceptableAnswers))
                {
                    fullAnswer += $" ({_engine.CurrentFinalQuestion.AcceptableAnswers})";
                }
                TxtDuelAnswer.Text = $"ОТВЕТ: {fullAnswer}";
                
                // Отправляем вопрос на экран ведущего
                _hostScreen?.UpdateQuestion(_engine.CurrentFinalQuestion.Text, _engine.CurrentFinalQuestion.Answer, 0);
                _hostModernScreen?.UpdateQuestion(_engine.CurrentFinalQuestion.Text, _engine.CurrentFinalQuestion.Answer, 0);
                _server.Broadcast($"QUESTION|{_engine.CurrentFinalQuestion.Text}|{_engine.CurrentFinalQuestion.Answer}|0");
            }
            else
            {
                FinalQuestionTextBlock.Text = "ВОПРОСЫ ЗАКОНЧИЛИСЬ И РЕЗЕРВ НЕ СРАБОТАЛ";
                TxtDuelAnswer.Text = "";
            }

            int pairNumber = _engine.Player1FinalScores.Count(r => r != null);
            if (_engine.IsPlayer1Turn) pairNumber++; // Если сейчас снова ход первого игрока в новой паре

            TurnTextBlock.Text = $"ОЧЕРЕДЬ: {(_engine.IsPlayer1Turn ? TxtFinalist1.Text : TxtFinalist2.Text)} (Вопрос {pairNumber})";
            
            Log($"Ход: {(_engine.IsPlayer1Turn ? TxtFinalist1.Text : TxtFinalist2.Text)} (Вопрос {pairNumber})");
            
            UpdateFinalCirclesUI(); // Явное обновление кружочков
        }

        /// <summary>
        /// Метод Senior-уровня для надежного обновления цветов индикаторов.
        /// Использует жестко заданные в XAML элементы для исключения ошибок отрисовки.
        /// </summary>
        private void UpdateFinalCirclesUI()
        {
            if (_p1Circles == null || _p2Circles == null) return;

            // Обновление Игрока 1 (первые 5 вопросов)
            for (int i = 0; i < 5; i++)
            {
                if (i < _engine.Player1FinalScores.Count)
                {
                    var score = _engine.Player1FinalScores[i];
                    _p1Circles[i].Fill = score == true ? System.Windows.Media.Brushes.Green : 
                                       (score == false ? System.Windows.Media.Brushes.Red : 
                                        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30)));
                }
                else
                {
                    _p1Circles[i].Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30));
                }
            }

            // Обновление Игрока 2 (первые 5 вопросов)
            for (int i = 0; i < 5; i++)
            {
                if (i < _engine.Player2FinalScores.Count)
                {
                    var score = _engine.Player2FinalScores[i];
                    _p2Circles[i].Fill = score == true ? System.Windows.Media.Brushes.Green : 
                                       (score == false ? System.Windows.Media.Brushes.Red : 
                                        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30)));
                }
                else
                {
                    _p2Circles[i].Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30));
                }
            }

            // Ретрансляция статуса дуэли на другие экраны (TCP + прямой вызов для того же процесса)
            try {
                string p1s = string.Join(",", _engine.Player1FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                string p2s = string.Join(",", _engine.Player2FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                string payload = $"{TxtFinalist1.Text}|{TxtFinalist2.Text}|{p1s}|{p2s}";
                _server.Broadcast($"DUEL_STATUS|{payload}");
                _server.Broadcast($"DUEL_UPDATE|{payload}"); // Дублируем для надежности
                _hostScreen?.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                _hostModernScreen?.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                Log($"Отправка данных дуэли: {payload}");
            } catch (Exception ex) {
                Log($"ОШИБКА ОТПРАВКИ ДАННЫХ ДУЭЛИ: {ex.Message}");
            }
        }

        private bool _nextRoundUsed = false;
        private bool _eliminationPerformedThisRound = false;

        private void BtnNextRound_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetOperatorAction("Переход к новому раунду");
                Log("=== ПЕРЕХОД К НОВОМУ РАУНДУ ===");

                // 1. Остановка таймера (музыка продолжает играть — CLOSE ROUND не глушит аудио)
                _roundTimer?.Stop();

                // 2. Переносим заработанное в общий банк (если ещё не перенесено)
                if (_engine.RoundBank > 0)
                {
                    _engine.ApplyRoundBankToTotal();
                    TxtTotalBank.Text = _engine.TotalBank.ToString("N0");
                    Log($"Банк раунда перенесён в общий. Итого: {_engine.TotalBank:N0} ₽");
                }

                _server.Broadcast("CLEAR_ELIMINATION");
                RevealProgressPanel.Visibility = Visibility.Collapsed;
                VotingBorder.Visibility = Visibility.Collapsed;
                SetCentralContext("PLAY");
                GamePlayPanel.Visibility = Visibility.Visible;
                HeadToHeadPanel.Visibility = Visibility.Collapsed;
                WinnerPanel.Visibility = Visibility.Collapsed;

                // 4. Сброс стейт-машины в Idle (чистый лист)
                if (_engine.CurrentState != GameState.Idle)
                    _engine.TransitionTo(GameState.Idle);

                // 5. Разблокировка READY, блокировка NEXT ROUND
                _nextRoundUsed = true;
                BtnNextRound.IsEnabled = false;
                SetButtonDisabled(BtnNextRound);
                BtnStartRound.IsEnabled = true;
                BtnStartRound.Background = BrushActiveGreen;
                BtnStartRound.Foreground = Brushes.White;

                TxtCurrentQuestion.Text = "";
                TxtCurrentAnswer.Text = "ОТВЕТ: —";

                Log($"Сцена очищена. Активных игроков: {_engine.ActivePlayers.Count}. Жмите READY.");

                UpdateButtonStates();
                UpdateOperationalHints();

                // Обновить номер раунда в центральной плашке (показываем следующий раунд)
                if (TxtRoundNumberCenter != null)
                {
                    int nextRound = _engine.CurrentRound + 1;
                    TxtRoundNumberCenter.Text = nextRound.ToString();
                    TimerArchBorder.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Log($"ОШИБКА NEXT ROUND: {ex.Message}");
                DarkMessageBox.Show($"Ошибка при подготовке нового раунда: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // BtnBeforeVoting_Click removed — voting system pending redesign

        // VOTING START / VOTING END удалены — система голосования на пересмотре

        private DispatcherTimer? _votingTrackCrossfadeTimer;
        private bool _votingTrackCrossfadeDone = false;

        private void StartVotingTrackCrossfadeWatch()
        {
            _votingTrackCrossfadeDone = false;
            _votingTrackCrossfadeTimer?.Stop();
            _votingTrackCrossfadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _votingTrackCrossfadeTimer.Tick += VotingTrackCrossfade_Tick;
            _votingTrackCrossfadeTimer.Start();
        }

        private void StopVotingTrackCrossfadeWatch()
        {
            _votingTrackCrossfadeTimer?.Stop();
            _votingTrackCrossfadeTimer = null;
        }

        private void VotingTrackCrossfade_Tick(object? sender, EventArgs e)
        {
            if (_votingTrackCrossfadeDone) return;

            var (pos, dur) = _audioManager.GetAudioPosition();
            if (dur == TimeSpan.Zero)
            {
                _votingTrackCrossfadeDone = true;
                StopVotingTrackCrossfadeWatch();
                return;
            }

            double remaining = dur.TotalSeconds - pos.TotalSeconds;
            if (remaining <= 3.0 && remaining > 0)
            {
                _votingTrackCrossfadeDone = true;
                StopVotingTrackCrossfadeWatch();
                _audioManager.StartBedWithFadeIn("general_bed.mp3", 3.0);
                Log("Кроссфейд: general_bed.mp3 fade-in (3с до конца трека голосования).");
            }
        }

        private async void BtnStartVoting_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int votingDuration = 45;
                if (CmbVotingDuration.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
                {
                    if (int.TryParse(selectedItem.Tag.ToString(), out int parsed))
                        votingDuration = parsed;
                }

                SetOperatorAction($"VOTE START: голосование {votingDuration} сек");
                BtnStartVoting.IsEnabled = false;
                SetButtonDisabled(BtnStartVoting);

                // Активируем кнопку ручного завершения голосования
                BtnEndVoting.IsEnabled = true;
                BtnEndVoting.Background = new SolidColorBrush(Color.FromRgb(0xDA, 0x37, 0x3C));
                BtnEndVoting.Foreground = Brushes.White;

                _audioManager.Stop();
                _audioManager.Play("Assets/Audio/new_voting_system v3START.mp3", loop: false);

                Log("Аудио: new_voting_system v3START.mp3 запущен.");

                _engine.TransitionTo(GameState.Voting);

                _votingTimeLeft = votingDuration;
                UpdateVotingTimerDisplay();

                await Task.Delay(1000);

                StartVotingCountdown();
                Log($"Голосование запущено. Таймер: {votingDuration}с, обратный отсчёт начат.");
            }
            catch (Exception ex)
            {
                Log("Ошибка запуска голосования: " + ex.Message);
            }
        }

        private void StartVotingCountdown()
        {
            _votingTimer?.Stop();
            _votingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _votingTimer.Tick += VotingTimer_Tick;
            _votingTimer.Start();
        }

        private void VotingTimer_Tick(object? sender, EventArgs e)
        {
            if (_votingTimeLeft > 0)
            {
                _votingTimeLeft--;
                UpdateVotingTimerDisplay();

                if (_votingTimeLeft <= 10 && _votingTimeLeft > 0)
                {
                    TxtTimer.Foreground = TimerBrushCritical;
                    ApplyTimerGlow(HubColorCritical);
                    _hostScreen?.SetVotingTimerUrgent(true);
                    _hostModernScreen?.SetVotingTimerUrgent(true);
                    _hostPremiumScreen?.SetVotingTimerUrgent(true);
                    _hostModernPremiumScreen?.SetVotingTimerUrgent(true);
                }

                if (_votingTimeLeft <= 5 && _votingTimeLeft > 0)
                {
                    BtnReveal.IsEnabled = true;
                    BtnReveal.Background = new SolidColorBrush(Color.FromRgb(0x88, 0x00, 0xAA));
                    BtnReveal.Foreground = Brushes.White;
                }
            }
            else
            {
                _votingTimer?.Stop();
                _votingTimer = null;
                Log("Голосование завершено по времени (0:00).");

                TxtTimer.Foreground = Brushes.Yellow;
                TxtTimer_Side.Foreground = Brushes.Yellow;
                _hostScreen?.SetVotingTimerUrgent(false);
                _hostModernScreen?.SetVotingTimerUrgent(false);
                _hostPremiumScreen?.SetVotingTimerUrgent(false);
                _hostModernPremiumScreen?.SetVotingTimerUrgent(false);

                StartVotingTrackCrossfadeWatch();

                _engine.TransitionTo(GameState.Discussion);
            }
        }

        private void UpdateVotingTimerDisplay()
        {
            int minutes = _votingTimeLeft / 60;
            int seconds = _votingTimeLeft % 60;
            string timeStr = $"{minutes}:{seconds:D2}";

            TxtTimerLabel.Text = "ГОЛОСОВАНИЕ";
            TxtTimer.Text = timeStr;
            TxtTimer_Side.Text = timeStr;

            if (_votingTimeLeft <= 10 && _votingTimeLeft > 0)
            {
                TxtTimer.Foreground = TimerBrushCritical;
                TxtTimer_Side.Foreground = TimerBrushCritical;
                ApplyTimerGlow(HubColorCritical);
            }

            _hostScreen?.ShowVotingTimer(timeStr, _votingTimeLeft);
            _hostModernScreen?.ShowVotingTimer(timeStr, _votingTimeLeft);
            _hostPremiumScreen?.ShowVotingTimer(timeStr, _votingTimeLeft);
            _hostModernPremiumScreen?.ShowVotingTimer(timeStr, _votingTimeLeft);

            _server.Broadcast($"VOTING_TIMER|{timeStr}");
        }

        public void StopVotingTimerEarly()
        {
            if (_votingTimer != null && _engine.CurrentState == GameState.Voting)
            {
                _votingTimer.Stop();
                _votingTimer = null;
                Log("Голосование завершено досрочно (все голоса введены).");

                TxtTimer.Foreground = TimerBrushNormal;
                TxtTimer_Side.Foreground = TimerBrushNormal;
                ApplyTimerGlow(HubColorNormal);
                StopTimerPulse();
                _hostScreen?.SetVotingTimerUrgent(false);
                _hostModernScreen?.SetVotingTimerUrgent(false);
                _hostPremiumScreen?.SetVotingTimerUrgent(false);
                _hostModernPremiumScreen?.SetVotingTimerUrgent(false);

                // Деактивируем кнопку ручного завершения
                BtnEndVoting.IsEnabled = false;
                SetButtonDisabled(BtnEndVoting);

                StartVotingTrackCrossfadeWatch();

                _engine.TransitionTo(GameState.Discussion);
            }
        }

        private void BtnEndVoting_Click(object sender, RoutedEventArgs e)
        {
            if (_engine.CurrentState == GameState.Voting)
            {
                SetOperatorAction("END VOTING: ручное завершение");
                Log("Оператор завершил голосование вручную.");
                StopVotingTimerEarly();
            }
        }

        #region Reveal (Вскрытие голосов)

        private DispatcherTimer? _revealTrackTimer;

        private void BtnReveal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetOperatorAction("REVEAL: вскрытие голосов");
                BtnReveal.IsEnabled = false;
                SetButtonDisabled(BtnReveal);

                StopVotingTrackCrossfadeWatch();
                _audioManager.Stop();
                _audioManager.Play("Assets/Audio/new_voting_system_revealing.mp3", loop: false);

                _audioManager.OnMainPlaybackCompleted = () =>
                    Dispatcher.BeginInvoke(() =>
                    {
                        StopRevealTracking();
                        Log("Трек вскрытия завершён.");
                    });

                _engine.TransitionTo(GameState.Reveal);

                RevealProgressPanel.Visibility = Visibility.Visible;
                StartRevealTracking();

                Log("Вскрытие голосов: new_voting_system_revealing.mp3 запущен.");
            }
            catch (Exception ex)
            {
                Log("Ошибка вскрытия голосов: " + ex.Message);
            }
        }

        private void StartRevealTracking()
        {
            _revealTrackTimer?.Stop();
            _revealTrackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _revealTrackTimer.Tick += RevealTrackTimer_Tick;
            _revealTrackTimer.Start();
        }

        private void StopRevealTracking()
        {
            _revealTrackTimer?.Stop();
            _revealTrackTimer = null;
        }

        private void RevealTrackTimer_Tick(object? sender, EventArgs e)
        {
            var (pos, dur) = _audioManager.GetAudioPosition();
            if (dur == TimeSpan.Zero) return;

            double sec = pos.TotalSeconds;
            double pct = pos.TotalSeconds / dur.TotalSeconds * 100;
            RevealProgress.Value = pct;
            TxtRevealTime.Text = $"{pos:m\\:ss} / {dur:m\\:ss}";

            if (sec < 1)
            {
                TxtRevealPhase.Text = "ГОТОВНОСТЬ...";
                TxtRevealPhase.Foreground = Brushes.Gray;
                RevealCueMarker.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x2A, 0x00));
            }
            else if (sec < 6)
            {
                TxtRevealPhase.Text = "ИНТРО ВЕДУЩЕЙ";
                TxtRevealPhase.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
                RevealProgress.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
                RevealCueMarker.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x2A, 0x00));
            }
            else
            {
                TxtRevealPhase.Text = "▶ ВСКРЫТИЕ КАРТОЧЕК";
                TxtRevealPhase.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x00));
                RevealProgress.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x00));
                RevealCueMarker.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x66, 0x00));
            }
        }

        #endregion

        private void BtnHotDiscussion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetOperatorAction("VERDICT: вердикт ведущей");
                BtnHotDiscussion.IsEnabled = false;
                SetButtonDisabled(BtnHotDiscussion);

                StopRevealTracking();
                StopVotingTrackCrossfadeWatch();
                RevealProgressPanel.Visibility = Visibility.Collapsed;

                _audioManager.Stop();
                _audioManager.Play("Assets/Audio/new_voting_system_before_walkshame.mp3", loop: false);

                Log("Hot Discussion: new_voting_system_before_walkshame.mp3 запущен.");
            }
            catch (Exception ex)
            {
                Log("Ошибка Hot Discussion: " + ex.Message);
            }
        }

        private async void BtnEliminate_Click(object sender, RoutedEventArgs e)
        {
            // Support both old simple strings (for ties) and new EliminationComboItem (for normal voting)
            string targetName = "";
            if (EliminationComboBox.SelectedItem is EliminationComboItem item)
            {
                targetName = item.Name;
            }
            else
            {
                targetName = EliminationComboBox.SelectedItem as string;
            }

            if (string.IsNullOrEmpty(targetName))
            {
                SetOperatorAction("Не выбран игрок!");
                Log("Выберите игрока для исключения!");
                return;
            }

            try
            {
                SetOperatorAction($"ELIMINATE: {targetName}");
                BtnEliminate.IsEnabled = false;
                SetButtonDisabled(BtnEliminate);

                _engine.ActivePlayers.Remove(targetName);
                _engine.EliminatePlayer(targetName);
                _eliminationPerformedThisRound = true;

                _server.Broadcast($"ELIMINATE|{targetName}");
                Log($"ИГРОК ВЫБЫЛ: {targetName}. Walk of Shame запущен.");

                UpdateStatsTable();
                PlayersGrid.Items.Refresh();

                try
                {
                    await _audioManager.PlayOneShotThenGeneralBedWithCrossfadeAsync(
                        "updated_walk_of_shame_bed.mp3", "general_bed.mp3", 3.0);
                    Log("Walk of shame завершён, general_bed.mp3 с кроссфейдом.");
                }
                catch (Exception audioEx)
                {
                    Log($"Ошибка аудио Walk of Shame: {audioEx.Message}");
                }

                Dispatcher.Invoke(() =>
                {
                    if (_engine.CurrentState == GameState.Elimination)
                    {
                        _engine.TransitionTo(GameState.Idle);
                        Log("Переход в Idle. Готов к NEXT ROUND.");
                    }
                    UpdateButtonStates();
                    UpdateOperationalHints();
                });
            }
            catch (Exception ex)
            {
                Log("Ошибка исключения: " + ex.Message);
            }
        }

        private void BtnGeneralBed_Click(object sender, RoutedEventArgs e)
        {
            _audioManager.Play("Assets/Audio/general_bed.mp3", loop: true);
            Log("Ручной запуск фоновой музыки (General Bed).");
        }

        private void BtnAfterInterviewBed_Click(object sender, RoutedEventArgs e)
        {
            _audioManager.Play("Assets/Audio/after_walk_of_shame_bed.mp3", loop: false);
            Log("Запущен фон после интервью (After Interview Bed).");
        }

        private void BtnDebugTestRounds_Click(object sender, RoutedEventArgs e)
        {
            Log("DEBUG: Запуск теста раундов...");
            
            if (_engine.CurrentRound >= 7)
            {
                _engine.ResetRoundCounter();
                Log("DEBUG: Счетчик раундов сброшен на 0.");
            }

            _engine.NextRound();
            _roundTimer.Stop();

            int testTime = 150;
            switch (_engine.CurrentRound)
            {
                case 1: testTime = 150; break;
                case 2: testTime = 140; break;
                case 3: testTime = 130; break;
                case 4: testTime = 120; break;
                case 5: testTime = 110; break;
                case 6: testTime = 100; break;
                case 7: testTime = 90; break;
                default: testTime = 150; break;
            }

            TxtTimer.Text = TimeSpan.FromSeconds(testTime).ToString(@"m\:ss");

            string fileName = _engine.CurrentRound switch
            {
                1 => "Round_bed_(230).mp3",
                2 => "Round_bed_(220).mp3",
                3 => "Round_bed_(210).mp3",
                4 => "Round_bed_(200).mp3",
                5 => "Round_bed_(150).mp3",
                6 => "Round_bed_(140).mp3",
                7 => "Round_bed_(130).mp3",
                _ => "Round_bed_(130).mp3"
            };

            _audioManager.Play($"Assets/Audio/{fileName}", loop: false);
            Log($"DEBUG: Раунд {_engine.CurrentRound}, время {TxtTimer.Text}, звук {fileName}");
        }

        private void BtnPanic_Click(object sender, RoutedEventArgs e)
        {
            SetOperatorAction("ЭКСТРЕННАЯ ОСТАНОВКА!");
            var result = DarkMessageBox.Show("ВЫ ПОДТВЕРЖДАЕТЕ ЭКСТРЕННУЮ ОСТАНОВКУ ИГРЫ (PANIC)?\n\nЭто остановит таймер, музыку и сбросит текущую цепочку!", 
                "ЭКСТРЕННАЯ СИТУАЦИЯ", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Log("!!! ПАНИКА: ЭКСТРЕННАЯ ОСТАНОВКА !!!");
                
                _roundTimer.Stop();
                _audioManager.Stop();
                _engine.TransitionTo(GameState.Idle);
                _engine.ResetChain();
                
                _server.Broadcast("PANIC|EMERGENCY_STOP");
                _server.Broadcast("CLEAR_ELIMINATION");
                
                DarkMessageBox.Show("Игра остановлена. Состояние сброшено в IDLE.", "PANIC STOPPED");
            }
        }

        private void BtnLogo_Click(object sender, RoutedEventArgs e)
        {
            SetOperatorAction("Логотип вкл/выкл");
            _hostScreen?.ToggleLogoScreen();
            _hostModernScreen?.ToggleLogoScreen();
            _hostPremiumScreen?.ToggleLogoScreen();
            _hostModernPremiumScreen?.ToggleLogoScreen();
            Log("Экран ведущего: переключён логотип.");
        }

        private void BtnStopAudio_Click(object sender, RoutedEventArgs e)
        {
            SetOperatorAction("Аудио остановлено");
            _audioManager?.StopAll();
            Log("Звуки принудительно остановлены.");
        }

        private void BtnRestartRound_Click(object sender, RoutedEventArgs e)
        {
            var result = DarkMessageBox.Show(
                "Сбросить текущий раунд и перейти в режим ожидания (Idle)?\nТекущая цепочка будет обнулена.",
                "Перезапуск раунда",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _roundTimer?.Stop();
                _engine?.ResetChain();
                _engine?.TransitionTo(GameState.Idle);
                Log("Раунд перезапущен. Состояние: Idle.");
            }
        }

        private void BtnBreakGame_Click(object sender, RoutedEventArgs e)
        {
            var result = DarkMessageBox.Show(
                "Экстренно остановить игру?\n\nБудет остановлена музыка, сброшена цепочка, состояние переведено в Idle, экран ведущего очищен.",
                "Экстренная остановка",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _isAutoTestRunning = false;
                _roundTimer?.Stop();
                _audioManager?.StopAll();
                _engine?.ResetChain();
                _engine?.TransitionTo(GameState.Idle);
                _hostScreen?.ClearScreen();
                _hostModernScreen?.ClearScreen();
                _hostPremiumScreen?.ClearScreen();
                _hostModernPremiumScreen?.ClearScreen();
                Log("!!! ЭКСТРЕННАЯ ОСТАНОВКА ИГРЫ (BREAK GAME) !!!");
            }
        }

        private void BtnCloseSession_Click(object sender, RoutedEventArgs e)
        {
            var result = DarkMessageBox.Show(
                "Вы уверены, что хотите закрыть текущую сессию игроков и сбросить игру?",
                "Закрытие сессии",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _isAutoTestRunning = false;
                _roundTimer?.Stop();
                _audioManager?.StopAll();
                _engine?.ResetGame();
                TxtTimer.Foreground = TimerBrushNormal;
                ApplyTimerGlow(HubColorNormal);
                StopTimerPulse();
                _hostScreen?.ClearScreen();
                _hostModernScreen?.ClearScreen();
                _hostPremiumScreen?.ClearScreen();
                _hostModernPremiumScreen?.ClearScreen();
                CloseAnalytics();

                _isSessionStarted = false;
                FreezeRoster(false);
                BtnStartSession.IsEnabled = true;
                BtnStartSession.Content = "START SESSION";
                SetCentralContext("SETUP");
                PreGamePanel.Visibility = Visibility.Collapsed;
                PreGameButtons.Visibility = Visibility.Collapsed;

                _nextRoundUsed = false;

                TxtCurrentQuestion.Text = "";
                TxtCurrentAnswer.Text = "ОТВЕТ: —";
                TxtTimer.Text = "2:30";
                UpdateStatsTable();
                LstLog.Items.Clear();
                Log("Сессия закрыта. Пульт возвращён в предсессионное состояние.");
                UpdateButtonStates();
                UpdateOperationalHints();
            }
        }

        private void BtnRosterDone_Click(object sender, RoutedEventArgs e)
        {
            var filled = _rosterItems.Count(r => !string.IsNullOrWhiteSpace(r.GameName));
            if (filled < 8)
            {
                DarkMessageBox.Show("Введите имена всех 8 игроков перед утверждением состава.", "Недостаточно игроков",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            FreezeRoster(true);
            Log($"Состав утверждён. Заполнено {filled} из 8 пультов.");
        }

        private void FreezeRoster(bool freeze)
        {
            foreach (var item in _rosterItems)
                item.IsLocked = freeze;

            BtnRosterConfirm.Visibility = freeze ? Visibility.Collapsed : Visibility.Visible;
            TxtRosterConfirmed.Visibility = freeze ? Visibility.Visible : Visibility.Collapsed;
            LstPlayers.IsEnabled = !freeze;
            LstPlayers.Opacity = freeze ? 0.7 : 1.0;
            SetupPanel.Background = freeze
                ? new SolidColorBrush(Color.FromRgb(0x14, 0x1A, 0x14))
                : (Brush)FindResource("ObsidianBg");
        }

        // Smart Roster: Переместить вверх
        private void BtnRosterMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PlayerSetupItem item)
            {
                int idx = _rosterItems.IndexOf(item);
                if (idx > 0)
                {
                    _rosterItems.Move(idx, idx - 1);
                    RenumberConsoles();
                }
            }
        }

        // Smart Roster: Переместить вниз
        private void BtnRosterMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PlayerSetupItem item)
            {
                int idx = _rosterItems.IndexOf(item);
                if (idx >= 0 && idx < _rosterItems.Count - 1)
                {
                    _rosterItems.Move(idx, idx + 1);
                    RenumberConsoles();
                }
            }
        }

        // Пересчет номеров пультов после перемещения/удаления
        private void RenumberConsoles()
        {
            for (int i = 0; i < _rosterItems.Count; i++)
                _rosterItems[i].ConsoleNumber = i + 1;
        }

        // Синхронизация Smart Roster → GameEngine.ActivePlayers
        // Передаём ТОЛЬКО GameName (короткое имя с тумбы)
        private void SyncRosterToEngine()
        {
            _engine.ActivePlayers.Clear();
            foreach (var item in _rosterItems)
            {
                string gameName = item.GameName?.Trim() ?? "";
                if (!string.IsNullOrEmpty(gameName))
                    _engine.ActivePlayers.Add(gameName);
            }
        }

        // Текст представления для суфлёра (INTRO 2)
        // "Сегодня играют: Вадим Петров, слесарь из Омска. Лена Аксёнова, учитель из Казани."
        private string GetIntroductionText()
        {
            var lines = _rosterItems
                .Where(r => !string.IsNullOrWhiteSpace(r.GameName))
                .Select(r => r.PrompterLine);
            return "Сегодня играют: " + string.Join(". ", lines) + ".";
        }

        private static readonly string[] RandomFirstNames = {
            "Александр", "Анастасия", "Андрей", "Анна", "Артём", "Валерия",
            "Виктор", "Дарья", "Дмитрий", "Евгений", "Екатерина", "Елена",
            "Иван", "Ирина", "Кирилл", "Ксения", "Максим", "Марина",
            "Михаил", "Наталья", "Никита", "Ольга", "Павел", "Полина",
            "Роман", "Светлана", "Сергей", "Татьяна", "Тимофей", "Юлия"
        };

        private static readonly string[] RandomLastNames = {
            "Иванов", "Петрова", "Сидоров", "Козлова", "Новиков", "Морозова",
            "Волков", "Лебедева", "Соколов", "Кузнецова", "Попов", "Фёдорова",
            "Орлов", "Смирнова", "Васильев", "Захарова", "Никитин", "Белова"
        };

        private static readonly string[] RandomCities = {
            "Москва", "Санкт-Петербург", "Казань", "Сочи", "Екатеринбург",
            "Новосибирск", "Краснодар", "Нижний Новгород", "Ростов-на-Дону",
            "Самара", "Воронеж", "Тюмень", "Калининград", "Владивосток"
        };

        private static readonly string[] RandomProfessions = {
            "программист из", "учитель из", "врач из", "инженер из",
            "артист из", "юрист из", "менеджер из", "студент из"
        };

        private void BtnFillRandom_Click(object sender, RoutedEventArgs e)
        {
            var rnd = new Random();
            var names = RandomFirstNames.OrderBy(_ => rnd.Next()).Take(8).ToArray();
            var surnames = RandomLastNames.OrderBy(_ => rnd.Next()).Take(8).ToArray();
            var cities = RandomCities.OrderBy(_ => rnd.Next()).Take(8).ToArray();
            var profs = RandomProfessions.OrderBy(_ => rnd.Next()).Take(8).ToArray();
            for (int i = 0; i < _rosterItems.Count; i++)
            {
                _rosterItems[i].GameName = names[i];
                _rosterItems[i].FullName = $"{names[i]} {surnames[i]}";
                _rosterItems[i].CityDesc = $"{profs[i]} {cities[i]}";
            }
            Log($"Заполнено 8 случайных участников: {string.Join(", ", names)}");
        }

        private void BtnStartSession_Click(object sender, RoutedEventArgs e)
        {
            Log("🔧 BtnStartSession_Click: клик зафиксирован.");

            try
            {
                if (BtnRosterConfirm != null && BtnRosterConfirm.Visibility == Visibility.Visible)
                {
                    DarkMessageBox.Show("Сначала подтвердите участников! Нажмите 'ПРИНЯТЬ СОСТАВ' перед запуском сессии.", "Состав не утвержден", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SetOperatorAction("Сессия запущена");
                _isSessionStarted = true;
                // Синхронизация Smart Roster → движок (порядок тумб сохранён)
                SyncRosterToEngine();

                if (_engine.ActivePlayers.Count < 8)
                {
                    DarkMessageBox.Show("Заполните всех 8 игроков (колонка 'ИМЯ НА ТУМБЕ') для начала сессии!", "Недостаточно игроков", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка базы вопросов
                if (_questionProvider.LoadedCount == 0)
                {
                    DarkMessageBox.Show("БАЗА ВОПРОСОВ ПУСТА!\nНевозможно начать реальную игру.\nПроверьте файл вопросов в папке bin.", 
                        "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    Log("❌ START SESSION прервана: база вопросов не загружена.");
                    return;
                }

                Log($"✅ Игроков: {_engine.ActivePlayers.Count}, Вопросов: {_questionProvider.LoadedCount}");

                FreezeRoster(true);
                BtnStartSession.IsEnabled = false;

                // 3. Меняем состояние кнопки на «СЕССИЯ ЗАПУЩЕНА»
                BtnStartSession.Content = "СЕССИЯ ЗАПУЩЕНА";

                // 4. Раунд инициализируем как 0 — первый вызов PrepareNewRound() сделает его 1
                _engine.ResetRoundCounter();
                _questionCount = 0;
                TxtQuestionCount.Text = $"0/{_questionProvider.LoadedCount}";
                TxtQuestionCount_Side.Text = $"0/{_questionProvider.LoadedCount}";

                _server.Broadcast("CLEAR_ELIMINATION");

                // 5. Проверяем FastTrack: пропустить вступление?
                if (ChkFastTrack.IsChecked == true)
                {
                    // Сразу к Раунду 1 — пропускаем PRE-GAME целиком
                    _engine.TransitionTo(GameState.RulesExplanation);
                    PreGamePanel.Visibility = Visibility.Collapsed;
                    FinalizePregameAndPrepareRound();
                    Log("Сессия запущена (FastTrack). PRE-GAME пропущен. Жмите READY.");
                }
                else
                {
                    // Стандартный путь: показываем панель студийного вступления
                    PreGamePanel.Visibility = Visibility.Visible;
                    PreGameButtons.Visibility = Visibility.Visible;
                    BtnOpeningUI.IsEnabled = true;
                    BtnOpening.IsEnabled = true;
                    BtnStartRound.IsEnabled = false;
                    SetButtonDisabled(BtnStartRound);
                    Log("Сессия запущена. Начинайте студийное вступление (OPENING).");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ ОШИБКА в BtnStartSession_Click: {ex.Message}");
                DarkMessageBox.Show($"ОШИБКА ЗАПУСКА СЕССИИ:\n{ex.Message}\n\nСтек:\n{ex.StackTrace}", 
                    "Диагностика", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region PRE-GAME: Студийное вступление

        private void BtnOpening_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetOperatorAction("OPENING: главная тема");
                _audioManager.Stop();
                _audioManager.Play("Assets/Audio/main_theme_full.mp3", loop: false);
                _engine.TransitionTo(GameState.IntroOpening);
                _server.Broadcast("SET_STATE|IntroOpening");
                _server.Broadcast("HOST_MESSAGE|[ЛОГОТИП ИГРЫ]");

                BtnOpening.IsEnabled = false;
                SetButtonDisabled(BtnOpening);
                BtnOpeningUI.IsEnabled = false;
                BtnIntro1.IsEnabled = true;
                BtnIntro1UI.IsEnabled = true;
                BtnIntro1.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x55, 0x88));
                BtnIntro1.Foreground = Brushes.White;

                Log("PRE-GAME: Opening — main_theme_full.mp3");
            }
            catch (Exception ex) { Log($"Ошибка OPENING: {ex.Message}"); }
        }

        private void BtnIntro1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetOperatorAction("INTRO 1: рассказ о шоу");
                _audioManager.Stop();
                _audioManager.Play("Assets/Audio/intro_track_1st.mp3", loop: false);
                _engine.TransitionTo(GameState.IntroNarrative);
                _server.Broadcast("SET_STATE|IntroNarrative");
                _server.Broadcast("HOST_MESSAGE|В эфире «Слабое звено»... Каждый из 8 участников может заработать до 400к... Вот эта команда.");

                BtnIntro1.IsEnabled = false;
                SetButtonDisabled(BtnIntro1);
                BtnIntro1UI.IsEnabled = false;
                BtnIntro2.IsEnabled = true;
                BtnIntro2UI.IsEnabled = true;
                BtnIntro2.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x55, 0x66));
                BtnIntro2.Foreground = Brushes.White;

                Log("PRE-GAME: Intro — intro_track_1st.mp3");
            }
            catch (Exception ex) { Log($"Ошибка INTRO: {ex.Message}"); }
        }

        private void BtnIntro2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetOperatorAction("Запущен фон: Представление игроков");
                _audioManager.Stop();
                _audioManager.Play("Assets/Audio/intro_track_2nd.mp3", loop: false);
                _engine.TransitionTo(GameState.PlayerIntro);
                _server.Broadcast("SET_STATE|PlayerIntro");

                _server.Broadcast($"HOST_MESSAGE|{GetIntroductionText()}");

                BtnIntro2.IsEnabled = false;
                SetButtonDisabled(BtnIntro2);
                BtnIntro2UI.IsEnabled = false;
                BtnRulesIntro.IsEnabled = true;
                BtnRulesUI.IsEnabled = true;
                BtnRulesIntro.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x55, 0x44));
                BtnRulesIntro.Foreground = Brushes.White;

                Log("PRE-GAME: Players — intro_track_2nd.mp3");
            }
            catch (Exception ex) { Log($"Ошибка PLAYERS: {ex.Message}"); }
        }

        private void BtnRulesIntro_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetOperatorAction("RULES: правила игры");
                _audioManager.Stop();
                _audioManager.Play("Assets/Audio/intro_track_3rd.mp3", loop: false);
                _engine.TransitionTo(GameState.RulesExplanation);
                _server.Broadcast("SET_STATE|RulesExplanation");
                _server.Broadcast("HOST_MESSAGE|ПРАВИЛА: Цепь из 8 ответов. Слово БАНК. На 1-й раунд — 2:30.");

                BtnIntro2.IsEnabled = false;
                SetButtonDisabled(BtnIntro2);
                BtnRulesIntro.IsEnabled = false;
                SetButtonDisabled(BtnRulesIntro);
                BtnRulesUI.IsEnabled = false;

                // Скрываем PRE-GAME панель — вступление завершено
                PreGameButtons.Visibility = Visibility.Collapsed;

                FinalizePregameAndPrepareRound();

                Log($"PRE-GAME: Rules — intro_track_3rd.mp3. Раунд {_engine.CurrentRound} автоподготовлен. Жмите READY.");
            }
            catch (Exception ex) { Log($"Ошибка RULES: {ex.Message}"); }
        }

        /// <summary>
        /// Финализация PRE-GAME: подготовка раунда, скрытие панели вступления, активация READY.
        /// Вызывается как из стандартного пути (после RULES), так и из FastTrack.
        /// </summary>
        private void FinalizePregameAndPrepareRound()
        {
            _engine.PrepareNewRound();
            TxtRound.Text = _engine.CurrentRound.ToString();
            if (TxtRoundNumberCenter != null)
            {
                TxtRoundNumberCenter.Text = _engine.CurrentRound.ToString();
                TimerArchBorder.Visibility = _engine.CurrentRound > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            int roundDuration = _engine.GetNewRoundDuration();
            _timeLeftSeconds = roundDuration;
            UpdateTimerDisplay();
            _server.Broadcast($"UPDATE_TIMER|{_timeLeftSeconds}");

            PreGamePanel.Visibility = Visibility.Collapsed;

            SetCentralContext("PLAY");
            GamePlayPanel.Visibility = Visibility.Visible;
            HeadToHeadPanel.Visibility = Visibility.Collapsed;
            WinnerPanel.Visibility = Visibility.Collapsed;

            BtnStartRound.IsEnabled = true;
            BtnStartRound.Background = BrushActiveGreen;
            BtnStartRound.Foreground = Brushes.White;
        }

        #endregion

        private void LoadTestTeam(string[] names, string label)
        {
            int selectedIndex = TestRoundSelector?.SelectedIndex ?? 0;
            if (selectedIndex < 0) selectedIndex = 0;

            // Количество игроков: R1=8, R2=7... R7=2, Финал=2
            int playerCount = (selectedIndex >= 6) ? 2 : (8 - selectedIndex);
            var playerNames = names.Take(playerCount).ToArray();

            _engine.ResetGame();
            TxtTimer.Foreground = TimerBrushNormal;
            ApplyTimerGlow(HubColorNormal);
            StopTimerPulse();
            _engine.ActivePlayers.Clear();
            foreach (var name in playerNames)
                _engine.ActivePlayers.Add(name);

            // Установка раунда (1–7 или Финал)
            int targetRound = selectedIndex + 1;
            _engine.ResetRoundCounter();
            for (int i = 0; i < targetRound; i++)
                _engine.NextRound();

            _server.Broadcast("CLEAR_ELIMINATION");
            SetCentralContext("PLAY");
            GamePlayPanel.Visibility = Visibility.Visible;
            HeadToHeadPanel.Visibility = Visibility.Collapsed;
            WinnerPanel.Visibility = Visibility.Collapsed;

            // Кнопки неактивны до START O'CLOCK
            BtnCorrect.IsEnabled = false;
            BtnWrong.IsEnabled = false;
            BtnBank.IsEnabled = false;
            if (BtnPass != null) BtnPass.IsEnabled = false;
            BtnIncorrect.IsEnabled = false;

            if (selectedIndex == 7) // ФИНАЛ
            {
                GamePlayPanel.Visibility = Visibility.Collapsed;
                HeadToHeadPanel.Visibility = Visibility.Visible;
                FinalPrestartBorder.Visibility = Visibility.Collapsed;
                BtnStartDuel.Visibility = Visibility.Visible;
                BtnStartDuel.IsEnabled = false;
                if (_engine.ActivePlayers.Count >= 2)
                {
                    TxtFinalist1.Text = _engine.ActivePlayers[0].ToUpper();
                    TxtFinalist2.Text = _engine.ActivePlayers[1].ToUpper();
                }
                SetFinalDuelUIState(true);
                _isSuddenDeathMusicPlayed = false;
                _engine.StartFinalDuel(player1StartsFirst: true);
                FinalQuestionTextBlock.Text = _engine.GetNextFinalQuestion();
                UpdateFinalCirclesUI();
                UpdateDuelUI();
                _currentTestModeLabel = $"{label} · Финал (Дуэль)";
                UpdateTestModeBadge();
                Log($"ТЕСТ: {label} — ФИНАЛЬНАЯ ДУЭЛЬ (2 игр.).");
            }
            else
            {
                GamePlayPanel.Visibility = Visibility.Visible;
                HeadToHeadPanel.Visibility = Visibility.Collapsed;
                SetFinalDuelUIState(false);
                _engine.FinalizeRoundSetup();
                bool use30Sec = (ChkTestLast30Sec != null && ChkTestLast30Sec.IsChecked == true);
                int timeSeconds = use30Sec ? 30 : _engine.GetRoundDuration();
                _timeLeftSeconds = timeSeconds;
                if (use30Sec)
                    Log($"ТЕСТ 30 СЕК: раунд {targetRound} стартует с {timeSeconds} сек.");
                UpdateTimerDisplay();
                _server.Broadcast($"UPDATE_TIMER|{_timeLeftSeconds}");
                _server.Broadcast($"CURRENT_PLAYER|{_engine.CurrentPlayerTurn}");
                _currentQuestion = _questionProvider.GetRandomQuestion();
                if (_currentQuestion != null)
                {
                    TxtCurrentQuestion.Text = _currentQuestion.Text;
                    TxtCurrentAnswer.Text = $"ОТВЕТ: {_currentQuestion.Answer}";

                    _questionCount = 1;
                }
                else
                {
                    TxtCurrentQuestion.Text = "ВОПРОСЫ ЗАКОНЧИЛИСЬ";
                    TxtCurrentAnswer.Text = "ОТВЕТ: —";
                }
                _engine.TransitionTo(GameState.RoundReady);
                _currentTestModeLabel = use30Sec ? $"{label} · Раунд {targetRound} (30 сек)" : $"{label} · Раунд {targetRound}";
                UpdateTestModeBadge();
                Log($"ТЕСТ: {label} — РАУНД {targetRound} ({playerCount} игр.). Готов к старту.");
            }

            BtnBotTurn.Visibility = Visibility.Visible;
            TglAutoBot.Visibility = Visibility.Visible;
            _audioManager.Play("Assets/Audio/general_bed.mp3", loop: true);
            UpdateStatsTable();
        }

        private void BtnTestTeam1_Click(object sender, RoutedEventArgs e)
        {
            LoadTestTeam(new[] { "Алексей", "Борис", "Виктория", "Галина", "Дмитрий", "Елена", "Жанна", "Игорь" }, "Команда 1 (Алфавит)");
        }

        private void BtnTestTeam2_Click(object sender, RoutedEventArgs e)
        {
            LoadTestTeam(new[] { "Антон", "Вера", "Глеб", "Диана", "Егор", "Инна", "Максим", "Ольга" }, "Команда 2 (Альтернативная)");
        }

        private void BtnTestTeam3_Click(object sender, RoutedEventArgs e)
        {
            LoadTestTeam(new[] { "Айгуль", "Валентина", "Джон", "Зинаида", "Иван", "Махмуд", "Сергей", "Эмма" }, "Команда 3 (Срез общества)");
        }

        private void BtnTestTeam4_Click(object sender, RoutedEventArgs e)
        {
            LoadTestTeam(new[] { "Вовочка", "Димон", "Толян", "Жека", "Колян", "Серый", "Саня", "Макс" }, "Команда 4 (Двоечники)");
        }

        private void BtnTestTeam5_Click(object sender, RoutedEventArgs e)
        {
            LoadTestTeam(new[] { "Онотоле", "Альберт", "Аристарх", "Эдуард", "Леопольд", "Герман", "Эммануил", "Яков" }, "Команда 5 (Ботаники)");
        }

        private void BtnTestTeam6_Click(object sender, RoutedEventArgs e)
        {
            LoadTestTeam(new[] { "Онотоле", "Вовочка", "Аристарх", "Димон", "Эдуард", "Толян", "Яков", "Жека" }, "Команда 6 (Битва умов)");
        }

        private void BtnExitBotTest_Click(object sender, RoutedEventArgs e)
        {
            _isAutoTestRunning = false;
            _autoBotTimer.Stop();
            TglAutoBot.IsChecked = false;
            BtnBotTurn.Visibility = Visibility.Collapsed;
            TglAutoBot.Visibility = Visibility.Collapsed;
            _botVotes.Clear();
            _currentTestModeLabel = "";
            UpdateTestModeBadge();
            UpdateStatsTable();
            Log("Режим тестирования ботов отключён.");
        }

        private void BtnStartGeminiTest_Click(object sender, RoutedEventArgs e)
        {
            _aiPlayer = new GeminiTestPlayer();
            _aiPlayer.LogCallback = msg => Dispatcher.BeginInvoke(() => Log(msg));
            _isAutoTestRunning = true;

            _engine.ActivePlayers.Clear();
            var botNames = new[] { "Бот Альфа", "Бот Бета", "Бот Гамма", "Бот Дельта",
                                   "Бот Эпсилон", "Бот Зета", "Бот Эта", "Бот Тета" };
            foreach (var name in botNames)
                _engine.ActivePlayers.Add(name);

            _currentTestModeLabel = "AI Gemini (Автотест)";
            UpdateTestModeBadge();
            UpdateStatsTable();
            Log("AI-ТЕСТ: Загружены 8 ботов Gemini. Запуск сессии...");

            BtnStartSession_Click(sender, e);
        }

        // ──────────────────────────────────────────────────────────────────────
        //  ТЕСТ-ИГРА: 7 РАУНДОВ + ФИНАЛ (Бот 1 … Бот 8)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Вариант А — Полу-авто: загружает 8 ботов, ставит раунд 1, таймер 30 сек,
        /// включает AutoBot. Переходы между раундами делает оператор вручную.
        /// </summary>
        private void BtnBotGameSemiAuto_Click(object sender, RoutedEventArgs e)
        {
            var botNames = new[] { "Бот 1", "Бот 2", "Бот 3", "Бот 4",
                                   "Бот 5", "Бот 6", "Бот 7", "Бот 8" };
            // Гарантируем 30-секундный таймер
            if (ChkTestLast30Sec != null) ChkTestLast30Sec.IsChecked = true;
            LoadTestTeam(botNames, "Тест-игра Бот 1–8 (полу-авто)");

            // Включаем AutoBot сразу
            TglAutoBot.IsChecked = true;
            _autoBotTimer.Start();

            _currentTestModeLabel = "Бот 1 vs Бот 2 · Полу-авто";
            UpdateTestModeBadge();
            Log("🤖 ТЕСТ-ИГРА (ПОЛУ-АВТО): 8 ботов, раунд 1, 30 сек. AutoBot включён. Управляйте переходами вручную.");
        }

        /// <summary>
        /// Вариант Б — Полный автопилот: проигрывает все 7 раундов + финал без участия оператора.
        /// После каждого раунда автоматически выбывает бот с наихудшей статистикой.
        /// Финал разыгрывается между «Бот 1» и «Бот 2».
        /// </summary>
        private void BtnBotGameAuto_Click(object sender, RoutedEventArgs e)
        {
            if (_fullAutoBotTask != null && !_fullAutoBotTask.IsCompleted)
            {
                // Повторный клик = стоп
                _fullAutoBotCts?.Cancel();
                Log("⚡ Автопилот: ОСТАНОВЛЕН пользователем.");
                BtnBotGameAuto.Content = "⚡ АВТОПИЛОТ";
                BtnBotGameAuto.Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x5E, 0x20));
                return;
            }

            BtnBotGameAuto.Content = "⏹ СТОП";
            BtnBotGameAuto.Background = new SolidColorBrush(Color.FromRgb(0x8B, 0x00, 0x00));

            _fullAutoBotCts = new System.Threading.CancellationTokenSource();
            _fullAutoBotTask = RunFullAutoBotGameAsync(_fullAutoBotCts.Token);
        }

        private Task? _fullAutoBotTask;
        private System.Threading.CancellationTokenSource? _fullAutoBotCts;

        private async Task RunFullAutoBotGameAsync(System.Threading.CancellationToken ct)
        {
            const int RoundTimeSec  = 30;   // длительность каждого раунда
            const int BotTurnDelayMs = 600; // пауза между ходами бота (мс)

            var botNames = new[] { "Бот 1", "Бот 2", "Бот 3", "Бот 4",
                                   "Бот 5", "Бот 6", "Бот 7", "Бот 8" };

            // 1. Инициализация — раунд 1, 8 ботов
            await Dispatcher.InvokeAsync(() =>
            {
                if (ChkTestLast30Sec != null) ChkTestLast30Sec.IsChecked = true;
                LoadTestTeam(botNames, "Тест-игра Бот 1–8 (автопилот)");
                _currentTestModeLabel = "Бот 1 vs Бот 2 · Автопилот";
                UpdateTestModeBadge();
                Log("⚡ АВТОПИЛОТ: запуск полной игры 7 раундов + финал.");
            });

            // Раунды 1–7
            for (int round = 1; round <= 7 && !ct.IsCancellationRequested; round++)
            {
                int playerCount = 9 - round; // R1=8 игроков, R7=2 игрока

                await Dispatcher.InvokeAsync(() =>
                    Log($"── Раунд {round} | Игроков: {_engine.ActivePlayers.Count} | Таймер {RoundTimeSec} сек ──"));

                // 2. Старт раунда
                await Dispatcher.InvokeAsync(() =>
                {
                    if (_engine.CurrentState == GameState.RoundReady)
                        BtnPlay_Click(null!, null!);
                });

                // 3. Ждём, пока раунд Playing, делаем ходы
                while (!ct.IsCancellationRequested)
                {
                    bool stillPlaying = false;
                    await Dispatcher.InvokeAsync(() => stillPlaying = _engine.CurrentState == GameState.Playing);
                    if (!stillPlaying) break;
                    await Dispatcher.InvokeAsync(() => BtnBotTurn_Click(null!, null!));
                    await Task.Delay(BotTurnDelayMs, ct).ContinueWith(_ => { }); // не бросаем при отмене
                }

                if (ct.IsCancellationRequested) break;

                // 4. Раунд закончился (таймер вышел) — переходим к аналитике/RoundSummary
                await Task.Delay(800);
                await Dispatcher.InvokeAsync(() =>
                {
                    // Если таймер ещё идёт — принудительно завершаем
                    if (_engine.CurrentState == GameState.Playing)
                        BtnNextRound_Click(null!, null!);
                });
                await Task.Delay(800);

                // 5. Голосование: авто-выбывание наихудшего бота
                // (не Бот 1 и не Бот 2, пока есть другие)
                await Dispatcher.InvokeAsync(() =>
                {
                    string? toEliminate = FindWorstBot();
                    if (toEliminate == null) return;

                    // Переход к состоянию голосования
                    try
                    {
                        if (_engine.CurrentState == GameState.RoundSummary)
                            BtnStartVoting_Click(null!, null!);
                    }
                    catch { }
                    try
                    {
                        if (_engine.CurrentState == GameState.Voting)
                            BtnEndVoting_Click(null!, null!);
                    }
                    catch { }
                    try
                    {
                        if (_engine.CurrentState == GameState.Voting || _engine.CurrentState == GameState.Discussion)
                            BtnReveal_Click(null!, null!);
                    }
                    catch { }

                    // Выбываем
                    if (EliminationComboBox != null)
                    {
                        var item = EliminationComboBox.Items
                            .OfType<object>()
                            .FirstOrDefault(i => i.ToString()?.Contains(toEliminate) == true
                                             || (i.GetType().GetProperty("Name")?.GetValue(i)?.ToString() == toEliminate));
                        if (item != null) EliminationComboBox.SelectedItem = item;
                    }
                    try { BtnEliminate_Click(null!, null!); } catch { }

                    Log($"⚡ АВТО-ВЫБЫВАНИЕ: {toEliminate} покидает игру.");
                });

                await Task.Delay(1000);

                // 6. Подготовка следующего раунда
                if (round < 7)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        try { BtnNextRound_Click(null!, null!); } catch { }
                    });
                    await Task.Delay(500);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        try { BtnReady_Click(null!, null!); } catch { }
                    });
                    await Task.Delay(300);
                }
            }

            if (ct.IsCancellationRequested) goto Cleanup;

            // 7. Финал
            await Dispatcher.InvokeAsync(() =>
            {
                Log("⚡ АВТОПИЛОТ: переход к финальной дуэли Бот 1 vs Бот 2.");
                try { BtnToFinal_Click(null!, null!); } catch { }
            });

            await Task.Delay(800);

            // Запускаем дуэль
            await Dispatcher.InvokeAsync(() =>
            {
                try { BtnStartDuel_Click(null!, null!); } catch { }
            });

            // Финальные ходы (авто)
            while (!ct.IsCancellationRequested)
            {
                bool inFinal = false;
                await Dispatcher.InvokeAsync(() => inFinal = _engine.CurrentState == GameState.FinalDuel
                                                             && string.IsNullOrEmpty(_engine.FinalWinner));
                if (!inFinal) break;

                await Dispatcher.InvokeAsync(() =>
                {
                    // случайный ответ для финального дуэли (70% верно)
                    bool correct = new Random().Next(100) < 70;
                    if (correct) BtnDuelCorrect_Click(null!, null!);
                    else         BtnDuelWrong_Click(null!, null!);
                });
                await Task.Delay(BotTurnDelayMs);
            }

            await Dispatcher.InvokeAsync(() =>
            {
                string winner = _engine.FinalWinner;
                if (!string.IsNullOrEmpty(winner))
                    Log($"🏆 АВТОПИЛОТ ЗАВЕРШЁН! Победитель: {winner}");
                else
                    Log("⚡ АВТОПИЛОТ ЗАВЕРШЁН.");
            });

            Cleanup:
            await Dispatcher.InvokeAsync(() =>
            {
                BtnBotGameAuto.Content = "⚡ АВТОПИЛОТ";
                BtnBotGameAuto.Background = new SolidColorBrush(Color.FromRgb(0x1B, 0x5E, 0x20));
                _fullAutoBotCts = null;
            });
        }

        /// <summary>
        /// Возвращает имя бота, который должен выбыть (худшая статистика).
        /// Защищает «Бот 1» и «Бот 2» от выбывания до финала.
        /// </summary>
        private string? FindWorstBot()
        {
            if (_engine.ActivePlayers.Count <= 2) return null;

            int round = _engine.CurrentRound;

            // Выбираем «самого слабого», исключая Бот 1 и Бот 2
            string? worst = _engine.ActivePlayers
                .Where(p => p != "Бот 1" && p != "Бот 2")
                .OrderBy(p =>
                {
                    if (_engine.PlayerStatistics.TryGetValue(p, out var byRound)
                        && byRound.TryGetValue(round, out var stats))
                        return stats.CorrectAnswers;
                    return 0;
                })
                .ThenByDescending(p =>
                {
                    if (_engine.PlayerStatistics.TryGetValue(p, out var byRound)
                        && byRound.TryGetValue(round, out var stats))
                        return stats.IncorrectAnswers;
                    return 0;
                })
                .FirstOrDefault();

            // Если осталось только Бот 1 + Бот 2 + один чужой → выбываем чужого
            if (worst == null)
            {
                worst = _engine.ActivePlayers.FirstOrDefault(p => p != "Бот 1" && p != "Бот 2");
            }

            return worst;
        }

        private async Task ProcessBotTurnAsync()
        {
            if (!_isAutoTestRunning || _engine.CurrentState != GameState.Playing)
                return;

            await Task.Delay(6000);

            if (!_isAutoTestRunning || _engine.CurrentState != GameState.Playing)
                return;

            var question = _currentQuestion;
            if (question == null || _aiPlayer == null)
                return;

            int chainIndex = _engine.CurrentChainIndex;

            BotDecision decision;
            try
            {
                decision = await _aiPlayer.MakeMoveAsync(question.Text, chainIndex);
            }
            catch
            {
                decision = new BotDecision { Action = "pass", Text = "" };
            }

            if (!_isAutoTestRunning || _engine.CurrentState != GameState.Playing)
                return;

            string player = _engine.CurrentPlayerTurn;

            string aiLog = decision.Action?.ToLowerInvariant() switch
            {
                "bank" => $"[AI] {player}: БЕРЁТ БАНК! (ступень {chainIndex})",
                "answer" => $"[AI] {player}: ОТВЕЧАЕТ -> \"{decision.Text}\"",
                "pass" => $"[AI] {player}: ПАСУЕТ. Детали: {decision.Text}",
                _ => $"[AI] {player}: ПАСУЕТ (неизвестное действие)"
            };
            Log(aiLog);

            switch (decision.Action?.ToLowerInvariant())
            {
                case "bank":
                    SimulateButtonPress(BtnBank);
                    BtnBank_Click(null, null);
                    _ = ProcessBotTurnAsync();
                    return;

                case "answer":
                    string aiAnswer = (decision.Text ?? "").Trim();
                    string correctAnswer = question.Answer.Trim();
                    bool isCorrect = string.Equals(aiAnswer, correctAnswer, StringComparison.OrdinalIgnoreCase);
                    if (!isCorrect && !string.IsNullOrEmpty(question.AcceptableAnswers))
                    {
                        var acceptable = question.AcceptableAnswers
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        isCorrect = acceptable.Any(a =>
                            string.Equals(aiAnswer, a.Trim(), StringComparison.OrdinalIgnoreCase));
                    }

                    if (isCorrect)
                    {
                        Log($"[AI] ✓ Ответ \"{aiAnswer}\" — ВЕРНО");
                        SimulateButtonPress(BtnCorrect);
                        BtnCorrect_Click(null, null);
                    }
                    else
                    {
                        Log($"[AI] ✗ Ответ \"{aiAnswer}\" — НЕВЕРНО (правильно: \"{correctAnswer}\")");
                        SimulateButtonPress(BtnWrong);
                        BtnWrong_Click(null, null);
                    }
                    break;

                case "pass":
                default:
                    SimulateButtonPress(BtnPass);
                    BtnPass_Click(null, null);
                    break;
            }

            if (_isAutoTestRunning && _engine.CurrentState == GameState.Playing)
                _ = ProcessBotTurnAsync();
        }

        private void UpdateTestModeBadge()
        {
            if (TestModeBadge == null || TxtTestModeLabel == null) return;
            if (string.IsNullOrEmpty(_currentTestModeLabel))
            {
                TestModeBadge.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtTestModeLabel.Text = $"ТЕСТ: {_currentTestModeLabel}";
                TestModeBadge.Visibility = Visibility.Visible;
            }
        }

        private void MenuItemTestLast30Sec_Click(object sender, RoutedEventArgs e)
        {
            if (ChkTestLast30Sec != null)
            {
                ChkTestLast30Sec.IsChecked = !(ChkTestLast30Sec.IsChecked == true);
                Log(ChkTestLast30Sec.IsChecked == true ? "Тест: раунды будут стартовать с 30 сек." : "Тест: раунды со стандартной длительностью.");
            }
        }

        private void BtnTestFinal_Click(object sender, RoutedEventArgs e)
        {
            _engine.ResetGame();
            TxtTimer.Foreground = TimerBrushNormal;
            ApplyTimerGlow(HubColorNormal);
            StopTimerPulse();
            _engine.ActivePlayers.Clear();
            _engine.ActivePlayers.Add("Алексей");
            _engine.ActivePlayers.Add("Борис");

            // Эмулируем состояние «после 7 раунда»: раунд 7, Idle, 2 игрока
            _engine.ResetRoundCounter();
            for (int i = 0; i < 7; i++)
                _engine.NextRound();

            _currentTestModeLabel = "Финал (после 7 раунда)";
            UpdateStatsTable();
            UpdateTestModeBadge();

            SetCentralContext("PLAY");
            GamePlayPanel.Visibility = Visibility.Visible;
            HeadToHeadPanel.Visibility = Visibility.Collapsed;
            WinnerPanel.Visibility = Visibility.Collapsed;
            VotingBorder.Visibility = Visibility.Collapsed;

            BtnToFinal.Visibility = Visibility.Visible;
            BtnToFinal.IsEnabled = true;
            BtnToFinal.Background = BrushActiveGreen;
            BtnToFinal.Foreground = Brushes.White;

            _audioManager.PlayBed("general_bed.mp3");

            BtnBotTurn.Visibility = Visibility.Visible;
            TglAutoBot.Visibility = Visibility.Visible;

            Log("ТЕСТ: Загружено состояние после 7 раунда. Нажмите ПЕРЕЙТИ К ФИНАЛУ.");
        }

        private void BtnBotTurn_Click(object sender, RoutedEventArgs e)
        {
            if (_engine.CurrentState != GameState.Playing)
            {
                Log("БОТ: Раунд не идёт. Ход невозможен.");
                return;
            }

            int modeIndex = 3;
            var rnd = new Random();
            bool hasMoneyInChain = _engine.CurrentChainIndex > 0;

            // Решение о банке
            bool doBank = false;
            bool isBattleBotannik = false; // для режима 6: true = ботаник, false = двоечник
            if (modeIndex == 6) isBattleBotannik = rnd.Next(2) == 0;

            switch (modeIndex)
            {
                case 0: // Случайный
                    doBank = hasMoneyInChain && rnd.Next(100) < 30;
                    break;
                case 1: // Трусливый
                    doBank = hasMoneyInChain;
                    break;
                case 2: // Идеальный
                    doBank = false;
                    break;
                case 3: // Срез общества
                    doBank = hasMoneyInChain && rnd.Next(100) < 40;
                    break;
                case 4: // Двоечники
                    doBank = hasMoneyInChain && rnd.Next(100) < 10;
                    break;
                case 5: // Ботаники
                    doBank = _engine.CurrentChainIndex >= 4;
                    break;
                case 6: // Битва (50/50)
                    if (isBattleBotannik)
                        doBank = _engine.CurrentChainIndex >= 4;
                    else
                        doBank = hasMoneyInChain && rnd.Next(100) < 10;
                    break;
                default:
                    doBank = hasMoneyInChain && rnd.Next(100) < 40;
                    break;
            }

            if (doBank)
            {
                Log($"БОТ ({GetBotModeName(modeIndex)}): БАНК!");
                SimulateButtonPress(BtnBank);
                BtnBank_Click(null, null);
                return;
            }

            // Решение об ответе
            int action;
            switch (modeIndex)
            {
                case 0: // Случайный: 60% Верно, 30% Неверно, 10% Пас
                    int roll = rnd.Next(100);
                    action = roll < 60 ? 0 : (roll < 90 ? 1 : 2);
                    break;
                case 1: // Трусливый: 80% Верно, 20% Неверно
                    action = rnd.Next(100) < 80 ? 0 : 1;
                    break;
                case 2: // Идеальный: 100% Верно
                    action = 0;
                    break;
                case 3: // Срез общества: IQ 1-100, <=75 Верно, 76-90 Неверно, >90 Пас
                    int iq = rnd.Next(1, 101);
                    action = iq <= 75 ? 0 : (iq <= 90 ? 1 : 2);
                    Log($"БОТ (Срез общества): IQ={iq} -> {(action == 0 ? "ВЕРНО" : action == 1 ? "НЕВЕРНО" : "ПАС")}");
                    break;
                case 4: // Двоечники: 20% Верно, 60% Неверно, 20% Пас
                    int r4 = rnd.Next(100);
                    action = r4 < 20 ? 0 : (r4 < 80 ? 1 : 2);
                    break;
                case 5: // Ботаники: 90% Верно, 10% Неверно
                    action = rnd.Next(100) < 90 ? 0 : 1;
                    break;
                case 6: // Битва: 0=ботаник (90/10), 1=двоечник (20/60/20)
                    if (isBattleBotannik)
                        action = rnd.Next(100) < 90 ? 0 : 1;
                    else
                    {
                        int r6 = rnd.Next(100);
                        action = r6 < 20 ? 0 : (r6 < 80 ? 1 : 2);
                    }
                    break;
                default:
                    action = rnd.Next(100) < 60 ? 0 : (rnd.Next(100) < 75 ? 1 : 2);
                    break;
            }

            if (modeIndex != 3)
                Log($"БОТ ({GetBotModeName(modeIndex)}): {(action == 0 ? "ВЕРНО" : action == 1 ? "НЕВЕРНО" : "ПАС")}");

            if (action == 0)
            {
                SimulateButtonPress(BtnCorrect);
                BtnCorrect_Click(null, null);
            }
            else if (action == 1)
            {
                SimulateButtonPress(BtnWrong);
                BtnWrong_Click(null, null);
            }
            else
            {
                SimulateButtonPress(BtnPass);
                BtnPass_Click(null, null);
            }
        }

        private static string GetBotModeName(int index)
        {
            return index switch
            {
                0 => "Случайный", 1 => "Трусливый", 2 => "Идеальный", 3 => "Срез общества",
                4 => "Двоечники", 5 => "Ботаники", 6 => "Битва (50/50)",
                _ => "Срез общества"
            };
        }

        private void TglAutoBot_Click(object sender, RoutedEventArgs e)
        {
            if (TglAutoBot.IsChecked == true)
                _autoBotTimer.Start();
            else
                _autoBotTimer.Stop();
        }

        private void AutoBotTimer_Tick(object sender, EventArgs e)
        {
            if (_engine.CurrentState == GameState.Playing)
            {
                BtnBotTurn_Click(null, null);
            }
            else if (_engine.CurrentState == GameState.Voting || _engine.CurrentState == GameState.Idle)
            {
                TglAutoBot.IsChecked = false;
                _autoBotTimer.Stop();
            }
        }

        private void BtnQuickStart_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = TestRoundSelector.SelectedIndex;
            if (selectedIndex < 0) return;

            // 1. Очистка и генерация игроков
            _engine.ActivePlayers.Clear();
            var testNames = new[] { "АЛЕКСЕЙ", "БОРИС", "ВИКТОРИЯ", "ГАЛИНА", "ДМИТРИЙ", "ЕЛЕНА", "ЖАННА", "ИГОРЬ" };
            
            // Количество игроков: R1=8, R2=7... R7=2, Финал=2
            int playerCount = (selectedIndex >= 6) ? 2 : (8 - selectedIndex);
            
            for (int i = 0; i < playerCount; i++)
            {
                _engine.ActivePlayers.Add(testNames[i]);
            }

            // 2. Установка раунда (в движке 1-7, Финал для отладки ставим как 8)
            int targetRound = selectedIndex + 1;
            _engine.ResetRoundCounter();
            for (int i = 0; i < targetRound; i++)
            {
                _engine.NextRound();
            }

            _server.Broadcast("CLEAR_ELIMINATION");
            SetCentralContext("PLAY");
            WinnerPanel.Visibility = Visibility.Collapsed;

            if (selectedIndex == 7) // ФИНАЛ
            {
                GamePlayPanel.Visibility = Visibility.Collapsed;
                HeadToHeadPanel.Visibility = Visibility.Visible;
                FinalPrestartBorder.Visibility = Visibility.Collapsed;
                BtnStartDuel.Visibility = Visibility.Visible;
                BtnStartDuel.IsEnabled = true;

                if (_engine.ActivePlayers.Count >= 2)
                {
                    TxtFinalist1.Text = _engine.ActivePlayers[0].ToUpper();
                    TxtFinalist2.Text = _engine.ActivePlayers[1].ToUpper();
                }

                SetFinalDuelUIState(true);
                _isSuddenDeathMusicPlayed = false;
                _engine.StartFinalDuel(player1StartsFirst: true);
                UpdateFinalCirclesUI();
                Log("DEBUG: Быстрый старт -> ФИНАЛЬНАЯ ДУЭЛЬ");
            }
            else
            {
                GamePlayPanel.Visibility = Visibility.Visible;
                HeadToHeadPanel.Visibility = Visibility.Collapsed;
                SetFinalDuelUIState(false);
                Log($"DEBUG: Быстрый старт -> РАУНД {targetRound} ({playerCount} игр.)");
            }

            _audioManager.Play("Assets/Audio/general_bed.mp3", loop: true);
            UpdateStatsTable();
        }

        private void BtnOpenHost_Click(object sender, RoutedEventArgs e)
        {
            if (_hostScreen == null || !_hostScreen.IsLoaded)
            {
                _hostScreen = new HostScreen(_engine);
                _hostScreen.Show();
                Log("Экран ведущего открыт.");
                SetScreenToggle(BtnScreenHost, true);
                
                if (_currentQuestion != null)
                {
                    _hostScreen.UpdateQuestion(_currentQuestion.Text, _currentQuestion.Answer, _questionCount);
                }
                // Если уже идёт финальная дуэль — сразу передаём имена и кружки
                if (_engine.CurrentState == GameState.FinalDuel)
                {
                    string p1s = string.Join(",", _engine.Player1FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                    string p2s = string.Join(",", _engine.Player2FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                    _hostScreen.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                }
            }
            else
            {
                _hostScreen.Close();
                _hostScreen = null;
                Log("Экран ведущего закрыт.");
                SetScreenToggle(BtnScreenHost, false);
            }
        }

        private void BtnOpenHostModern_Click(object sender, RoutedEventArgs e)
        {
            if (_hostModernScreen == null || !_hostModernScreen.IsLoaded)
            {
                _hostModernScreen = new HostScreenModern(_engine);
                _hostModernScreen.Show();
                Log("Экран ведущего (Modern) открыт.");
                SetScreenToggle(BtnScreenModern, true);

                if (_currentQuestion != null)
                {
                    _hostModernScreen.UpdateQuestion(_currentQuestion.Text, _currentQuestion.Answer, _questionCount);
                }
                if (_engine.CurrentState == GameState.FinalDuel)
                {
                    string p1s = string.Join(",", _engine.Player1FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                    string p2s = string.Join(",", _engine.Player2FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                    _hostModernScreen.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                }
            }
            else
            {
                _hostModernScreen.Close();
                _hostModernScreen = null;
                Log("Экран ведущего (Modern) закрыт.");
                SetScreenToggle(BtnScreenModern, false);
            }
        }

        private void BtnOpenHostPremium_Click(object sender, RoutedEventArgs e)
        {
            if (_hostPremiumScreen == null || !_hostPremiumScreen.IsLoaded)
            {
                _hostPremiumScreen = new HostScreenPremium(_engine);
                _hostPremiumScreen.Show();
                Log("Экран PREMIUM открыт.");
                SetScreenToggle(BtnScreenPremium, true);

                if (_currentQuestion != null)
                {
                    _hostPremiumScreen.UpdateQuestion(_currentQuestion.Text, _currentQuestion.Answer, _questionCount);
                }
                if (_engine.CurrentState == GameState.FinalDuel)
                {
                    string p1s = string.Join(",", _engine.Player1FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                    string p2s = string.Join(",", _engine.Player2FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                    _hostPremiumScreen.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                }
            }
            else
            {
                _hostPremiumScreen.Close();
                _hostPremiumScreen = null;
                Log("Экран PREMIUM закрыт.");
                SetScreenToggle(BtnScreenPremium, false);
            }
        }

        private void BtnOpenHostModernPremium_Click(object sender, RoutedEventArgs e)
        {
            if (_hostModernPremiumScreen == null || !_hostModernPremiumScreen.IsLoaded)
            {
                _hostModernPremiumScreen = new HostScreenModernPremium(_engine);
                _hostModernPremiumScreen.Show();
                Log("Экран MODERN PREMIUM открыт.");
                SetScreenToggle(BtnScreenModPremium, true);

                if (_currentQuestion != null)
                {
                    _hostModernPremiumScreen.UpdateQuestion(_currentQuestion.Text, _currentQuestion.Answer, _questionCount);
                }
                if (_engine.CurrentState == GameState.FinalDuel)
                {
                    string p1s = string.Join(",", _engine.Player1FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                    string p2s = string.Join(",", _engine.Player2FinalScores.Select(s => s == true ? "1" : (s == false ? "0" : "-1")));
                    _hostModernPremiumScreen.SetDuelDisplay(TxtFinalist1.Text, TxtFinalist2.Text, p1s, p2s);
                }
            }
            else
            {
                _hostModernPremiumScreen.Close();
                _hostModernPremiumScreen = null;
                Log("Экран MODERN PREMIUM закрыт.");
                SetScreenToggle(BtnScreenModPremium, false);
            }
        }

        private void BtnOpenBroadcast_Click(object sender, RoutedEventArgs e)
        {
            if (_broadcastWindow == null || !_broadcastWindow.IsLoaded)
            {
                _broadcastWindow = new BroadcastWindow(_engine);
                _broadcastWindow.Show();
                Log("Экран BROADCAST открыт.");
                SetScreenToggle(BtnScreenBroadcast, true);
                // Синхронизация текущего таймера и банка
                _broadcastWindow.UpdateBank(_engine.CurrentChainIndex, _engine.RoundBank);
                int m = _timeLeftSeconds / 60, s = _timeLeftSeconds % 60;
                _broadcastWindow.UpdateTimer($"{m}:{s:D2}", _timeLeftSeconds);
            }
            else
            {
                _broadcastWindow.Close();
                _broadcastWindow = null;
                Log("Экран BROADCAST закрыт.");
                SetScreenToggle(BtnScreenBroadcast, false);
            }
        }

        private void BtnOpenAudience_Click(object sender, RoutedEventArgs e)
        {
            if (_audienceScreen == null || !_audienceScreen.IsLoaded)
            {
                _audienceScreen = new AudienceScreen(_engine);
                _audienceScreen.Show();
                Log("Экран AUDIENCE открыт.");
                SetScreenToggle(BtnScreenAudience, true);
                _audienceScreen.UpdateBank(_engine.CurrentChainIndex, _engine.RoundBank);
                _audienceScreen.UpdateRound(_engine.CurrentRound);
                int m = _timeLeftSeconds / 60, s = _timeLeftSeconds % 60;
                _audienceScreen.UpdateTimer($"{m}:{s:D2}", _timeLeftSeconds);
            }
            else
            {
                _audienceScreen.Close();
                _audienceScreen = null;
                Log("Экран AUDIENCE закрыт.");
                SetScreenToggle(BtnScreenAudience, false);
            }
        }

        private void BtnNextQuestion_Click(object sender, RoutedEventArgs e)
        {
            SetOperatorAction("Следующий вопрос");
            LoadNextQuestion();
        }

        private void LoadNextQuestion()
        {
            Log("Загрузка следующего вопроса...");
            try
            {
                _currentQuestion = _questionProvider.GetRandomQuestion();
                if (_currentQuestion != null)
                {
                    _questionCount++;
                    Log($"Вопрос #{_currentQuestion.Id} успешно получен (Всего задано: {_questionCount}).");
                    UpdateQuestionDisplay();
                }
                else
                {
                    Log("ВОПРОСЫ ЗАКОНЧИЛИСЬ!");
                    DarkMessageBox.Show("Все вопросы в базе использованы.", "Внимание");
                }
            }
            catch (Exception ex)
            {
                Log($"ОШИБКА ЗАГРУЗКИ ВОПРОСА: {ex.Message}");
            }
        }

        private void UpdateQuestionDisplay()
        {
            if (_currentQuestion == null) 
            {
                QuestionCard.Visibility = Visibility.Collapsed;
                LinkLogoPanel.Visibility = Visibility.Visible;
                return;
            }

            // Прячем лого, показываем карточку
            LinkLogoPanel.Visibility = Visibility.Collapsed;
            QuestionCard.Visibility = Visibility.Visible;

            // Обновляем текст на пульте оператора
            TxtCurrentQuestion.Text = _currentQuestion.Text;
            TxtCurrentAnswer.Text = $"ОТВЕТ: {_currentQuestion.Answer} ({_currentQuestion.AcceptableAnswers})";
            
            Log($"UI: Установка вопроса '{(_currentQuestion.Text.Length > 20 ? _currentQuestion.Text.Substring(0, 17) + "..." : _currentQuestion.Text)}' на пульте.");



            // Обновляем счётчик вопросов в статусной панели
            TxtQuestionCount.Text = $"{_questionCount}/{_questionProvider.LoadedCount}";
            TxtQuestionCount_Side.Text = $"{_questionCount}/{_questionProvider.LoadedCount}";

            // И обязательно на экране ведущего (прямое обновление)
            _hostScreen?.UpdateQuestion(_currentQuestion.Text, _currentQuestion.Answer, _questionCount);
            _hostModernScreen?.UpdateQuestion(_currentQuestion.Text, _currentQuestion.Answer, _questionCount);
            _hostPremiumScreen?.UpdateQuestion(_currentQuestion.Text, _currentQuestion.Answer, _questionCount);
            _hostModernPremiumScreen?.UpdateQuestion(_currentQuestion.Text, _currentQuestion.Answer, _questionCount);

            // Рассылка по сети всем клиентам
            _server.Broadcast($"QUESTION|{_currentQuestion.Text}|{_currentQuestion.Answer}|{_questionCount}");
            Log("Данные вопроса разосланы по сети.");
        }

        #endregion

        #region Вспомогательные методы

        private void UpdateBankChainUI()
        {
            var items = new List<BankChainItem>();
            int chainLen = _engine.BankChain.Length;
            int targetIndex = _engine.CurrentChainIndex;

            // Подсветка цели только в боевых фазах (READY / Playing)
            bool showTarget = (_engine.CurrentState == GameState.RoundReady
                            || _engine.CurrentState == GameState.Playing);

            for (int i = chainLen; i >= 1; i--)
            {
                int arrayIndex = i - 1;
                bool isTarget = showTarget && (
                    (targetIndex >= chainLen)
                        ? (arrayIndex == chainLen - 1)
                        : (arrayIndex == targetIndex)
                );

                items.Add(new BankChainItem
                {
                    Value = _engine.BankChain[arrayIndex],
                    Index = i,
                    IsActive = isTarget
                });
            }
            BankChainList.ItemsSource = items;

            // Обновляем имя текущего игрока на всех экранах
            string currentPlayer = _engine.CurrentPlayerTurn ?? "";
            _server.Broadcast($"CURRENT_PLAYER|{currentPlayer}");
            if (_broadcastWindow != null && _broadcastWindow.IsLoaded)
                _broadcastWindow.UpdateCurrentPlayer(currentPlayer);
            if (_audienceScreen != null && _audienceScreen.IsLoaded)
                _audienceScreen.UpdateCurrentPlayer(currentPlayer);
        }

        private void UpdateStateButtons(GameState state)
        {
            // Активные кнопки — полная непрозрачность; неактивные — тоже 1.0, чтобы не было "белого на белом"
            // (стиль disabled задаётся через SetButtonDisabled: тёмный фон #333 и серый текст #888)
            BtnStartRound.Opacity = 1.0;
            BtnPlay.Opacity = 1.0;
        }

        /// <summary>
        /// Помечает кнопку как неактивную. Визуальное отключение (полупрозрачность) 
        /// обрабатывается стилем в XAML через Opacity=0.4 при IsEnabled=False.
        /// </summary>
        private static void SetButtonDisabled(Button button)
        {
            button.IsEnabled = false;
        }

        /// <summary>
        /// Блокировка кнопок и цветовая подсказка в зависимости от текущего состояния игры (UI Hygiene).
        /// Вызывается при инициализации и при каждой смене GameState.
        /// </summary>
        private void UpdateButtonStates()
        {
            void SafeInvoke(Action action)
            {
                if (Dispatcher.CheckAccess())
                    action();
                else
                    Dispatcher.Invoke(action);
            }

            SafeInvoke(() =>
            {
                if (_engine == null) return;

                var state = _engine.CurrentState;
                var white = Brushes.White;

                switch (state)
                {
                    case GameState.Playing:
                        BtnCorrect.IsEnabled = true;
                        BtnCorrect.Background = BrushActiveGreen;
                        BtnCorrect.Foreground = white;
                        BtnWrong.IsEnabled = true;
                        BtnWrong.Background = BrushActiveRed;
                        BtnWrong.Foreground = white;
                        if (BtnPass != null) { BtnPass.IsEnabled = true; BtnPass.Background = BrushPassNeutral; BtnPass.Foreground = white; }
                        BtnBank.IsEnabled = true;
                        BtnBank.Background = BrushBankOrange;
                        BtnBank.Foreground = Brushes.Black;
                        BtnNextQuestion.IsEnabled = true;
                        BtnNextQuestion.Background = BrushPlayBlue;
                        BtnNextQuestion.Foreground = white;
                        BtnStartRound.IsEnabled = false;
                        SetButtonDisabled(BtnStartRound);
                        BtnPlay.IsEnabled = false;
                        SetButtonDisabled(BtnPlay);
                        EliminationComboBox.IsEnabled = false;
                        BtnEliminate.IsEnabled = false;
                        SetButtonDisabled(BtnEliminate);
                        BtnStartDuel.IsEnabled = false;
                        SetButtonDisabled(BtnStartDuel);
                        BtnDuelCorrect.IsEnabled = false;
                        SetButtonDisabled(BtnDuelCorrect);
                        BtnDuelWrong.IsEnabled = false;
                        SetButtonDisabled(BtnDuelWrong);
                        BtnToFinal.IsEnabled = false;
                        SetButtonDisabled(BtnToFinal);
                        BtnNextRound.IsEnabled = false;
                        SetButtonDisabled(BtnNextRound);
                        BtnCloseRoundFromAnalytics.IsEnabled = false;
                        BtnCloseRoundFromAnalytics.Visibility = Visibility.Collapsed;
                        BtnEndShow.IsEnabled = false;
                        SetButtonDisabled(BtnEndShow);
                        ApplyVotingStepper(0);
                        break;

                    case GameState.RoundSummary:
                        SetButtonDisabled(BtnCorrect); BtnCorrect.IsEnabled = false;
                        SetButtonDisabled(BtnWrong); BtnWrong.IsEnabled = false;
                        if (BtnPass != null) { SetButtonDisabled(BtnPass); BtnPass.IsEnabled = false; }
                        SetButtonDisabled(BtnBank); BtnBank.IsEnabled = false;
                        SetButtonDisabled(BtnNextQuestion); BtnNextQuestion.IsEnabled = false;
                        SetButtonDisabled(BtnStartRound); BtnStartRound.IsEnabled = false;
                        SetButtonDisabled(BtnPlay); BtnPlay.IsEnabled = false;
                        SetButtonDisabled(BtnGamePlayPlay); BtnGamePlayPlay.IsEnabled = false;
                        EliminationComboBox.IsEnabled = false;
                        SetButtonDisabled(BtnEliminate); BtnEliminate.IsEnabled = false;
                        SetButtonDisabled(BtnStartDuel); BtnStartDuel.IsEnabled = false;
                        SetButtonDisabled(BtnDuelCorrect); BtnDuelCorrect.IsEnabled = false;
                        SetButtonDisabled(BtnDuelWrong); BtnDuelWrong.IsEnabled = false;
                        SetButtonDisabled(BtnToFinal); BtnToFinal.IsEnabled = false;
                        SetButtonDisabled(BtnNextRound); BtnNextRound.IsEnabled = false;
                        BtnCloseRoundFromAnalytics.IsEnabled = false;
                        BtnCloseRoundFromAnalytics.Visibility = Visibility.Collapsed;
                        SetButtonDisabled(BtnEndShow); BtnEndShow.IsEnabled = false;
                        ApplyVotingStepper(1);
                        break;

                    case GameState.Voting:
                    case GameState.Discussion:
                    case GameState.Reveal:
                    case GameState.Elimination:
                        SetButtonDisabled(BtnCorrect); BtnCorrect.IsEnabled = false;
                        SetButtonDisabled(BtnWrong); BtnWrong.IsEnabled = false;
                        if (BtnPass != null) { SetButtonDisabled(BtnPass); BtnPass.IsEnabled = false; }
                        SetButtonDisabled(BtnBank); BtnBank.IsEnabled = false;
                        SetButtonDisabled(BtnNextQuestion); BtnNextQuestion.IsEnabled = false;
                        SetButtonDisabled(BtnStartRound); BtnStartRound.IsEnabled = false;
                        SetButtonDisabled(BtnPlay); BtnPlay.IsEnabled = false;
                        SetButtonDisabled(BtnGamePlayPlay); BtnGamePlayPlay.IsEnabled = false;
                        SetButtonDisabled(BtnStartDuel); BtnStartDuel.IsEnabled = false;
                        SetButtonDisabled(BtnDuelCorrect); BtnDuelCorrect.IsEnabled = false;
                        SetButtonDisabled(BtnDuelWrong); BtnDuelWrong.IsEnabled = false;
                        SetButtonDisabled(BtnToFinal); BtnToFinal.IsEnabled = false;
                        SetButtonDisabled(BtnEndShow); BtnEndShow.IsEnabled = false;

                        if (state == GameState.Voting)
                        {
                            ApplyVotingStepper(2);
                        }
                        else if (state == GameState.Discussion)
                        {
                            ApplyVotingStepper(3);
                        }
                        else if (state == GameState.Reveal)
                        {
                            ApplyVotingStepper(4);
                        }
                        else
                        {
                            ApplyVotingStepper(0);
                        }

                        bool canEliminate = (state == GameState.Discussion || state == GameState.Reveal);
                        EliminationComboBox.IsEnabled = canEliminate;
                        BtnEliminate.IsEnabled = canEliminate;
                        BtnEliminate.Foreground = canEliminate ? new SolidColorBrush(Color.FromRgb(0xDA, 0x37, 0x3C)) : BrushDisabledForeground;
                        BtnEliminate.BorderBrush = canEliminate ? new SolidColorBrush(Color.FromRgb(0xDA, 0x37, 0x3C)) : new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x2a));

                        bool canNextAfterElim = (state == GameState.Elimination) && _engine.ActivePlayers.Count >= 2 && !_nextRoundUsed && _eliminationPerformedThisRound;
                        BtnNextRound.IsEnabled = canNextAfterElim;
                        BtnNextRound.Background = canNextAfterElim ? BrushActiveGreen : BrushDisabledGray;
                        BtnNextRound.Foreground = canNextAfterElim ? white : BrushDisabledForeground;
                        if (!canNextAfterElim) SetButtonDisabled(BtnNextRound);
                        
                        BtnCloseRoundFromAnalytics.IsEnabled = canNextAfterElim;
                        BtnCloseRoundFromAnalytics.Visibility = canNextAfterElim ? Visibility.Visible : Visibility.Collapsed;
                        break;

                    case GameState.IntroOpening:
                    case GameState.IntroNarrative:
                    case GameState.PlayerIntro:
                    case GameState.RulesExplanation:
                    case GameState.Idle:
                    case GameState.RoundReady:
                        bool isPreGame = (state == GameState.IntroOpening || state == GameState.IntroNarrative
                            || state == GameState.PlayerIntro);
                        SetButtonDisabled(BtnCorrect);
                        BtnCorrect.IsEnabled = false;
                        SetButtonDisabled(BtnWrong);
                        BtnWrong.IsEnabled = false;
                        SetButtonDisabled(BtnPass);
                        if (BtnPass != null) BtnPass.IsEnabled = false;
                        SetButtonDisabled(BtnBank);
                        BtnBank.IsEnabled = false;
                        BtnNextQuestion.IsEnabled = (state == GameState.RoundReady);
                        BtnNextQuestion.Background = (state == GameState.RoundReady) ? BrushPlayBlue : BrushDisabledGray;
                        BtnNextQuestion.Foreground = (state == GameState.RoundReady) ? white : BrushDisabledForeground;
                        bool isFinalReachedForReady = (_engine.ActivePlayers.Count <= 2 && _engine.CurrentRound > 0) || _engine.CurrentRound >= 7;
                        bool readyEnabled = _isSessionStarted && !isPreGame && !isFinalReachedForReady && ((state == GameState.Idle) || (state == GameState.RulesExplanation) || (state == GameState.RoundReady && BtnBotTurn.Visibility == Visibility.Visible));
                        BtnStartRound.IsEnabled = readyEnabled;
                        BtnStartRound.Background = readyEnabled ? BrushActiveGreen : BrushDisabledGray;
                        BtnStartRound.Foreground = readyEnabled ? white : BrushDisabledForeground;
                        BtnPlay.IsEnabled = (state == GameState.RoundReady);
                        BtnPlay.Background = (state == GameState.RoundReady) ? BrushStartClock : BrushDisabledGray;
                        BtnPlay.Foreground = (state == GameState.RoundReady) ? BrushStartClockFg : BrushDisabledForeground;
                        BtnGamePlayPlay.IsEnabled = (state == GameState.RoundReady);
                        BtnGamePlayPlay.Background = (state == GameState.RoundReady) ? BrushStartClock : BrushDisabledGray;
                        BtnGamePlayPlay.Foreground = (state == GameState.RoundReady) ? BrushStartClockFg : BrushDisabledForeground;
                        EliminationComboBox.IsEnabled = false;
                        SetButtonDisabled(BtnEliminate);
                        BtnEliminate.IsEnabled = false;
                        SetButtonDisabled(BtnStartDuel);
                        BtnStartDuel.IsEnabled = false;
                        SetButtonDisabled(BtnDuelCorrect);
                        BtnDuelCorrect.IsEnabled = false;
                        SetButtonDisabled(BtnDuelWrong);
                        BtnDuelWrong.IsEnabled = false;
                        bool canGoToFinal = (state == GameState.RoundReady || state == GameState.Idle) && _engine.ActivePlayers.Count == 2;
                        BtnToFinal.IsEnabled = canGoToFinal;
                        BtnToFinal.Background = canGoToFinal ? BrushActiveGreen : BrushDisabledGray;
                        BtnToFinal.Foreground = canGoToFinal ? white : BrushDisabledForeground;
                        bool canNextRound = (state == GameState.Idle) && _engine.CurrentRound > 0 && _engine.ActivePlayers.Count >= 2 && !_nextRoundUsed && _eliminationPerformedThisRound;
                        BtnNextRound.IsEnabled = canNextRound;
                        BtnNextRound.Background = canNextRound ? BrushActiveGreen : BrushDisabledGray;
                        BtnNextRound.Foreground = canNextRound ? white : BrushDisabledForeground;

                        BtnCloseRoundFromAnalytics.IsEnabled = canNextRound;
                        BtnCloseRoundFromAnalytics.Visibility = canNextRound ? Visibility.Visible : Visibility.Collapsed;
                        BtnEndShow.IsEnabled = false;
                        SetButtonDisabled(BtnEndShow);
                        ApplyVotingStepper(0);
                        break;

                    case GameState.FinalDuel:
                        SetButtonDisabled(BtnCorrect);
                        BtnCorrect.IsEnabled = false;
                        SetButtonDisabled(BtnWrong);
                        BtnWrong.IsEnabled = false;
                        SetButtonDisabled(BtnPass);
                        if (BtnPass != null) BtnPass.IsEnabled = false;
                        SetButtonDisabled(BtnBank);
                        BtnBank.IsEnabled = false;
                        SetButtonDisabled(BtnNextQuestion);
                        BtnNextQuestion.IsEnabled = false;
                        SetButtonDisabled(BtnStartRound);
                        BtnStartRound.IsEnabled = false;
                        SetButtonDisabled(BtnPlay);
                        BtnPlay.IsEnabled = false;
                        SetButtonDisabled(BtnGamePlayPlay);
                        BtnGamePlayPlay.IsEnabled = false;
                        EliminationComboBox.IsEnabled = false;
                        SetButtonDisabled(BtnEliminate);
                        BtnEliminate.IsEnabled = false;
                        BtnStartDuel.IsEnabled = true;
                        BtnStartDuel.Background = BrushActiveGreen;
                        BtnStartDuel.Foreground = white;
                        BtnDuelCorrect.IsEnabled = true;
                        BtnDuelCorrect.Background = BrushActiveGreen;
                        BtnDuelCorrect.Foreground = white;
                        BtnDuelWrong.IsEnabled = true;
                        BtnDuelWrong.Background = BrushActiveRed;
                        BtnDuelWrong.Foreground = white;
                        SetButtonDisabled(BtnToFinal);
                        BtnToFinal.IsEnabled = false;
                        SetButtonDisabled(BtnNextRound);
                        BtnNextRound.IsEnabled = false;
                        BtnEndShow.IsEnabled = true;
                        BtnEndShow.Background = new SolidColorBrush(Colors.Transparent);
                        BtnEndShow.Foreground = Brushes.Gold;
                        ApplyVotingStepper(0);
                        break;

                    default:
                        SetButtonDisabled(BtnCorrect);
                        BtnCorrect.IsEnabled = false;
                        SetButtonDisabled(BtnWrong);
                        BtnWrong.IsEnabled = false;
                        SetButtonDisabled(BtnPass);
                        if (BtnPass != null) BtnPass.IsEnabled = false;
                        SetButtonDisabled(BtnBank);
                        BtnBank.IsEnabled = false;
                        SetButtonDisabled(BtnNextQuestion);
                        BtnNextQuestion.IsEnabled = false;
                        BtnStartRound.IsEnabled = true;
                        BtnStartRound.Background = BrushActiveGreen;
                        BtnStartRound.Foreground = white;
                        SetButtonDisabled(BtnPlay);
                        BtnPlay.IsEnabled = false;
                        SetButtonDisabled(BtnGamePlayPlay);
                        BtnGamePlayPlay.IsEnabled = false;
                        EliminationComboBox.IsEnabled = false;
                        SetButtonDisabled(BtnEliminate);
                        BtnEliminate.IsEnabled = false;
                        SetButtonDisabled(BtnStartDuel);
                        BtnStartDuel.IsEnabled = false;
                        SetButtonDisabled(BtnDuelCorrect);
                        BtnDuelCorrect.IsEnabled = false;
                        SetButtonDisabled(BtnDuelWrong);
                        BtnDuelWrong.IsEnabled = false;
                        SetButtonDisabled(BtnToFinal);
                        BtnToFinal.IsEnabled = false;
                        SetButtonDisabled(BtnNextRound);
                        BtnNextRound.IsEnabled = false;
                        SetButtonDisabled(BtnEndShow);
                        BtnEndShow.IsEnabled = false;
                        break;
                }
            });
        }

        private void SimulateBotVotes()
        {
            _botVotes.Clear();
            if (_engine.ActivePlayers.Count < 2) return;

            int roundDuration = Math.Max(0, _engine.GetRoundDuration() - _timeLeftSeconds);
            var analytics = _statsAnalyzer.AnalyzeRound(roundDuration);
            string target = analytics.EliminationPrediction;
            if (string.IsNullOrEmpty(target) || !_engine.ActivePlayers.Contains(target))
                target = _engine.ActivePlayers[0];

            var others = _engine.ActivePlayers.Where(p => p != target).ToList();
            var rnd = new Random();
            foreach (var voter in _engine.ActivePlayers)
            {
                if (others.Count == 0)
                    _botVotes[voter] = target;
                else
                    _botVotes[voter] = rnd.Next(100) < 75 ? target : others[rnd.Next(others.Count)];
            }
            UpdateStatsTable();
        }

        private void UpdateStatsTable()
        {
            var stats = new List<PlayerStatView>();
            var allStats = _engine.PlayerStatistics;
            bool isTestMode = BtnBotTurn.Visibility == Visibility.Visible;

            foreach (var playerName in _engine.ActivePlayers)
            {
                var view = new PlayerStatView { Name = playerName };
                
                // Суммируем статистику по всем раундам для этого игрока
                if (allStats.TryGetValue(playerName, out var roundsData))
                {
                    foreach (var round in roundsData.Values)
                    {
                        view.Correct += round.CorrectAnswers;
                        view.Incorrect += round.IncorrectAnswers;
                        view.Passes += round.Passes;
                        view.Banked += round.BankedMoney;
                    }
                }
                if (isTestMode && _botVotes.TryGetValue(playerName, out var votedFor))
                    view.VotedFor = votedFor;
                stats.Add(view);
            }

            VotedForColumn.Visibility = isTestMode ? Visibility.Visible : Visibility.Collapsed;
            PlayersGrid.ItemsSource = stats;
        }



        private void BtnValidateJson_Click(object sender, RoutedEventArgs e)
        {
            var errors = _questionProvider.ValidateDatabase();
            if (errors.Count == 0)
            {
                DarkMessageBox.Show(
                    "База вопросов полностью корректна. К эфиру готовы!",
                    "Валидация базы вопросов",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Log("Валидация JSON: ошибок не найдено.");
            }
            else
            {
                string report = string.Join("\n", errors);
                DarkMessageBox.Show(
                    report,
                    "Ошибки в базе вопросов",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Log($"Валидация JSON: найдено {errors.Count} проблем(а).");
            }
        }

        private void BtnSelfTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selfTest = new SelfTest();

                // Путь к исходнику OperatorPanel.xaml.cs для HostScreen Parity Check
                string sourcePath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, 
                    "..", "..", "..", "..", "Views", "OperatorPanel.xaml.cs");
                
                // Пробуем альтернативный путь если не нашли
                if (!System.IO.File.Exists(sourcePath))
                {
                    sourcePath = System.IO.Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory, 
                        "..", "..", "..", "Views", "OperatorPanel.xaml.cs");
                }
                if (!System.IO.File.Exists(sourcePath))
                {
                    // Прямой путь к проекту
                    sourcePath = @"O:\WEAKEST LINK SOPFTWARE AI TESTERING\Views\OperatorPanel.xaml.cs";
                }

                var results = selfTest.RunAll(sourcePath);
                string report = SelfTest.GenerateReport(results);

                int passed = results.Count(r => r.Passed);
                int failed = results.Count(r => !r.Passed);

                // Сохраняем полный отчёт в файл
                try
                {
                    string reportPath = System.IO.Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory, "SelfTestReport.txt");
                    System.IO.File.WriteAllText(reportPath, report);
                    Log($"Self-test отчёт сохранён: {reportPath}");
                }
                catch { /* не критично */ }

                // Показываем результат
                DarkMessageBox.Show(
                    report,
                    $"🧪 Self-Test: {passed} ✅  {failed} ❌",
                    MessageBoxButton.OK,
                    failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                Log($"Self-test завершён: {passed} прошло, {failed} провалено из {results.Count}.");
            }
            catch (Exception ex)
            {
                DarkMessageBox.Show(
                    $"Ошибка при запуске Self-Test:\n{ex.Message}",
                    "Self-Test Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Log($"ОШИБКА SELF-TEST: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════
        // 📺 ГРАФИКА БРОДКАСТА — toggle handlers
        // ═══════════════════════════════════════════════════

        private void TglBcTimer_Click(object sender, RoutedEventArgs e)
        {
            if (_broadcastWindow == null || !_broadcastWindow.IsLoaded) return;
            bool show = TglBcTimer.IsChecked == true;
            _broadcastWindow.SetTimerVisibility(show);
            Log($"Broadcast: Таймер {(show ? "показан" : "скрыт")}.");
        }

        private void TglBcChain_Click(object sender, RoutedEventArgs e)
        {
            if (_broadcastWindow == null || !_broadcastWindow.IsLoaded) return;
            bool show = TglBcChain.IsChecked == true;
            _broadcastWindow.SetChainVisibility(show);
            Log($"Broadcast: Цепочка {(show ? "показана" : "скрыта")}.");
        }

        private void TglBcChainTimer_Click(object sender, RoutedEventArgs e)
        {
            if (_broadcastWindow == null || !_broadcastWindow.IsLoaded) return;
            bool show = TglBcChainTimer.IsChecked == true;
            _broadcastWindow.SetChainVisibility(show);
            _broadcastWindow.SetTimerVisibility(show);
            // Синхронизируем индивидуальные кнопки
            TglBcTimer.IsChecked = show;
            TglBcChain.IsChecked = show;
            Log($"Broadcast: Цепочка+Таймер {(show ? "показаны" : "скрыты")}.");
        }

        private void TglBcBank_Click(object sender, RoutedEventArgs e)
        {
            if (_broadcastWindow == null || !_broadcastWindow.IsLoaded) return;
            bool show = TglBcBank.IsChecked == true;
            if (show)
                _broadcastWindow.ShowBankOverlay(_engine.TotalBank);
            else
                _broadcastWindow.HideBankOverlay();
            Log($"Broadcast: Банк {(show ? "показан" : "скрыт")}.");
        }

        private void TglBcDuel_Click(object sender, RoutedEventArgs e)
        {
            if (_broadcastWindow == null || !_broadcastWindow.IsLoaded) return;
            bool show = TglBcDuel.IsChecked == true;
            if (show)
            {
                string p1 = _engine.ActivePlayers.Count >= 1 ? _engine.ActivePlayers[0] : "";
                string p2 = _engine.ActivePlayers.Count >= 2 ? _engine.ActivePlayers[1] : "";
                _broadcastWindow.ShowFinalDuel(p1, p2);
            }
            else
                _broadcastWindow.HideFinalDuel();
            Log($"Broadcast: Дуэль {(show ? "показана" : "скрыта")}.");
        }

        private void BtnBcStyleClassic_Click(object sender, RoutedEventArgs e)
        {
            if (_broadcastWindow == null || !_broadcastWindow.IsLoaded) return;
            _broadcastWindow.SwitchStyle(false);
            BtnStyleClassic.Background = new SolidColorBrush(Color.FromRgb(0x58, 0x65, 0xF2));
            BtnStyleClassic.Foreground = Brushes.White;
            BtnStyleNew.Background = new SolidColorBrush(Color.FromRgb(0x4e, 0x50, 0x58));
            BtnStyleNew.Foreground = new SolidColorBrush(Color.FromRgb(0xdb, 0xde, 0xe1));
            Log("Broadcast: Стиль → CLASSIC.");
        }

        private void BtnBcStyleNew_Click(object sender, RoutedEventArgs e)
        {
            if (_broadcastWindow == null || !_broadcastWindow.IsLoaded) return;
            _broadcastWindow.SwitchStyle(true);
            BtnStyleNew.Background = new SolidColorBrush(Color.FromRgb(0x58, 0x65, 0xF2));
            BtnStyleNew.Foreground = Brushes.White;
            BtnStyleClassic.Background = new SolidColorBrush(Color.FromRgb(0x4e, 0x50, 0x58));
            BtnStyleClassic.Foreground = new SolidColorBrush(Color.FromRgb(0xdb, 0xde, 0xe1));
            Log("Broadcast: Стиль → NEW.");
        }

        private void Log(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            LstLog.Items.Insert(0, $"[{time}] {message}");
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // F5 — ход бота (работает всегда)
            if (e.Key == Key.F5)
            {
                BtnBotTurn_Click(null!, null!);
                e.Handled = true;
                base.OnKeyDown(e);
                return;
            }

            // F9 — DEBUG: тест финальной дуэли (показать оверлей с демо-сценарием)
            if (e.Key == Key.F9)
            {
                DebugTestFinalDuel();
                e.Handled = true;
                base.OnKeyDown(e);
                return;
            }

            // Защита: не срабатываем, если фокус в поле ввода (имя игрока и т.д.)
            if (e.OriginalSource is TextBox) return;

            bool handled = true;

            if (PlayContext.Visibility != Visibility.Visible && HeadToHeadPanel.Visibility != Visibility.Visible) 
            {
                base.OnKeyDown(e);
                return;
            }

            switch (e.Key)
            {
                case Key.Space:
                    BtnBank_Click(null, null);
                    break;

                case Key.Right:
                    if (HeadToHeadPanel.Visibility == Visibility.Visible)
                        BtnDuelCorrect_Click(null, null);
                    else
                        BtnCorrect_Click(null, null);
                    break;

                case Key.Left:
                    if (HeadToHeadPanel.Visibility == Visibility.Visible)
                        BtnDuelWrong_Click(null, null);
                    else
                        BtnWrong_Click(null, null);
                    break;

                case Key.Down:
                    BtnPass_Click(null, null);
                    break;

                case Key.Enter:
                    if (HeadToHeadPanel.Visibility == Visibility.Visible && BtnStartDuel.Visibility == Visibility.Visible)
                        BtnStartDuel_Click(null, null);
                    else
                        BtnNextQuestion_Click(null, null);
                    break;

                default:
                    handled = false;
                    break;
            }

            if (handled)
            {
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        private DispatcherTimer? _debugFinalTimer;
        private int _debugFinalStep;

        private void DebugTestFinalDuel()
        {
            // Убедимся, что BroadcastWindow открыт
            if (_broadcastWindow == null || !_broadcastWindow.IsLoaded)
            {
                _broadcastWindow = new BroadcastWindow(_engine);
                _broadcastWindow.Show();
            }

            // Сценарий: P1 = ✓ ✗ ✓ ✗ ✓ (3:2), P2 = ✗ ✓ ✓ ✓ ✗ (3:2)
            bool?[] p1Script = { true, false, true, false, true };
            bool?[] p2Script = { false, true, true, true, false };

            // Инициализировать движок для теста
            _engine.Player1FinalScores.Clear();
            _engine.Player2FinalScores.Clear();
            for (int i = 0; i < 5; i++)
            {
                _engine.Player1FinalScores.Add(null);
                _engine.Player2FinalScores.Add(null);
            }

            _broadcastWindow.ShowFinalDuel("ТЕСТИГРОК", "ДЕБАГОВ");
            Log("DEBUG F9: Тест финальной дуэли запущен. Демо-сценарий: 5 пар ответов за 10 секунд.");

            _debugFinalStep = 0;
            _debugFinalTimer?.Stop();
            _debugFinalTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            _debugFinalTimer.Tick += (s, ev) =>
            {
                if (_debugFinalStep >= 5)
                {
                    _debugFinalTimer.Stop();
                    Log("DEBUG F9: Демо завершено. Нажмите F9 ещё раз для повтора.");
                    return;
                }

                _engine.Player1FinalScores[_debugFinalStep] = p1Script[_debugFinalStep];
                _engine.Player2FinalScores[_debugFinalStep] = p2Script[_debugFinalStep];
                _broadcastWindow.UpdateFinalDuel();

                string r1 = p1Script[_debugFinalStep] == true ? "✓" : "✗";
                string r2 = p2Script[_debugFinalStep] == true ? "✓" : "✗";
                Log($"DEBUG F9: Пара {_debugFinalStep + 1}/5 — P1:{r1}  P2:{r2}");

                _debugFinalStep++;
            };
            _debugFinalTimer.Start();
        }

        private void BtnDebugTestFinal_Click(object sender, RoutedEventArgs e)
        {
            // 1. Подготовить движок: 2 игрока, раунд 7
            _engine.ResetGame();
            TxtTimer.Foreground = TimerBrushNormal;
            ApplyTimerGlow(HubColorNormal);
            StopTimerPulse();
            _engine.ActivePlayers.Clear();
            _engine.ActivePlayers.Add("Алексей");
            _engine.ActivePlayers.Add("Борис");
            _engine.ResetRoundCounter();
            for (int i = 0; i < 7; i++) _engine.NextRound();

            _currentTestModeLabel = "Финал (DEBUG-тест)";
            UpdateTestModeBadge();

            // 2. Перейти к финалу (минуя промежуточный экран)
            BtnToFinal_Click(null!, null!);

            // 3. Автоматически применить — первый отвечает Алексей
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                CmbFirstResponder.SelectedIndex = 0;
                BtnApplyDuel_Click(null!, null!);

                // 4. Открыть BroadcastWindow и показать оверлей
                if (_broadcastWindow == null || !_broadcastWindow.IsLoaded)
                {
                    _broadcastWindow = new BroadcastWindow(_engine);
                    _broadcastWindow.Show();
                }
                _broadcastWindow?.ShowFinalDuel("Алексей", "Борис");
                Log("DEBUG: Финальная дуэль запущена! Жми → Верно / ← Неверно для ответов.");
            });
        }

        private void BtnEndShow_Click(object sender, RoutedEventArgs e)
        {
            SetOperatorAction("Шоу завершено");
            _audioManager.Stop();
            _audioManager.Play("Assets/Audio/general_bed.mp3", loop: true);
            
            Log("ЗАВЕРШЕНИЕ ПРОГРАММЫ. Сброс системы в начальное состояние.");

            _isSessionStarted = false;
            FreezeRoster(false);
            BtnStartSession.IsEnabled = true;
            BtnStartSession.Content = "START SESSION";
            SetCentralContext("SETUP");

            // Восстановление прозрачности панелей (если были приглушены в финале)
            SetFinalDuelUIState(false);

            // Переход движка в IDLE
            if (_engine.CurrentState != GameState.Idle)
            {
                _engine.TransitionTo(GameState.Idle);
            }
            UpdateStateButtons(GameState.Idle);
            
            _server.Broadcast("RESET_GAME");
        }

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

        #region Integrated Modules (Question Editor & Analytics)

        private ObservableCollection<QuestionData> _editorQuestions = new();
        private const string EditorFilePath = "questions.json";

        private void BtnToggleQuestionEditor_Click(object sender, RoutedEventArgs e)
        {
            if (EditorContext.Visibility == Visibility.Visible)
                SetCentralContext(_isSessionStarted ? "PLAY" : "SETUP");
            else
                SetCentralContext("EDIT");
        }

        private void BtnToggleAnalytics_Click(object sender, RoutedEventArgs e)
        {
            if (AnalyticsContext.Visibility == Visibility.Visible)
                SetCentralContext(_isSessionStarted ? "PLAY" : "SETUP");
            else
                SetCentralContext("STATS");
        }

        private void BtnAnalytics_Click(object sender, RoutedEventArgs e)
        {
            if (AnalyticsContext.Visibility == Visibility.Visible)
                SetCentralContext(_isSessionStarted ? "PLAY" : "SETUP");
            else
                SetCentralContext("STATS");
        }

        private void LoadQuestionsForEditor()
        {
            try
            {
                if (!File.Exists(EditorFilePath))
                {
                    Log($"Файл {EditorFilePath} не найден.");
                    QuestionsDataGrid.ItemsSource = null;
                    return;
                }

                string json = File.ReadAllText(EditorFilePath);
                var questions = JsonSerializer.Deserialize<List<QuestionData>>(json, JsonOptions) ?? new List<QuestionData>();
                _editorQuestions = new ObservableCollection<QuestionData>(questions);

                QuestionsDataGrid.ItemsSource = _editorQuestions;
                Log($"📝 Загружено {_editorQuestions.Count} вопросов в редактор.");
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка загрузки базы: {ex.Message}");
            }
        }

        private async void SaveQuestionsForEditor()
        {
            try
            {
                string json = JsonSerializer.Serialize(_editorQuestions.ToList(), JsonOptions);
                File.WriteAllText(EditorFilePath, json);
                _questionProvider.LoadQuestions(EditorFilePath);
                Log("✅ База вопросов сохранена и синхронизирована.");
                ShowEditorSaveToast("СОХРАНЕНО ✓");
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка сохранения базы: {ex.Message}");
                ShowEditorSaveToast("ОШИБКА ✗");
            }
        }

        private async void ShowEditorSaveToast(string text)
        {
            TxtEditorSaveToast.Text = text;
            TxtEditorSaveToast.Opacity = 1;
            await Task.Delay(2000);
            for (double o = 1.0; o >= 0; o -= 0.1)
            {
                TxtEditorSaveToast.Opacity = o;
                await Task.Delay(30);
            }
            TxtEditorSaveToast.Opacity = 0;
        }

        private void BtnEditorLoad_Click(object sender, RoutedEventArgs e) => LoadQuestionsForEditor();
        private void BtnEditorSave_Click(object sender, RoutedEventArgs e) => SaveQuestionsForEditor();

        private async void OpenRoundAnalytics()
        {
            SetupContext.Visibility = Visibility.Collapsed;
            PlayContext.Visibility = Visibility.Collapsed;
            AnalyticsContext.Visibility = Visibility.Collapsed;
            EditorContext.Visibility = Visibility.Collapsed;

            TxtTransitionTitle.Text = $"ИТОГИ РАУНДА {_engine.CurrentRound}";
            TxtTransitionSub.Text = $"Банк: {_lastRoundBankForSummary:N0} ₽  •  Переход к аналитике...";
            TransitionOverlay.Visibility = Visibility.Visible;

            await Task.Delay(2000);

            TransitionOverlay.Visibility = Visibility.Collapsed;
            SetCentralContext("STATS");
        }
        private void RefreshAnalytics() => UpdateAnalyticsData();
        private void CloseAnalytics() => SetCentralContext(_isSessionStarted ? "PLAY" : "SETUP");

        #endregion

        #endregion

        #region Win32 Taskbar Fix (WM_GETMINMAXINFO)

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;

            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

                IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (GetMonitorInfo(monitor, ref monitorInfo))
                    {
                        var workArea = monitorInfo.rcWork;
                        mmi.ptMaxPosition.X = workArea.Left;
                        mmi.ptMaxPosition.Y = workArea.Top;
                        mmi.ptMaxSize.X = workArea.Right - workArea.Left;
                        mmi.ptMaxSize.Y = workArea.Bottom - workArea.Top;
                    }
                }

                Marshal.StructureToPtr(mmi, lParam, true);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private const int MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        #endregion

        #region Custom Title Bar

        private void BtnTitleMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnTitleMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }

        private void BtnTitleClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnTakeSelfie_Click(object sender, RoutedEventArgs e)
        {
            TakeScreenshot();
        }

        private void TakeScreenshot()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string screenshotDir = System.IO.Path.Combine(exeDir, "Screenshots");
                Directory.CreateDirectory(screenshotDir);

                string fileName = $"Selfie_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = System.IO.Path.Combine(screenshotDir, fileName);

                double dpiX = 96, dpiY = 96;
                var source = PresentationSource.FromVisual(this);
                if (source?.CompositionTarget != null)
                {
                    dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                    dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
                }

                int width = (int)(ActualWidth * dpiX / 96.0);
                int height = (int)(ActualHeight * dpiY / 96.0);

                var rtb = new RenderTargetBitmap(width, height, dpiX, dpiY, PixelFormats.Pbgra32);
                rtb.Render(this);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = new FileStream(fullPath, FileMode.Create))
                {
                    encoder.Save(fs);
                }

                System.Media.SystemSounds.Beep.Play();
                Log($"📸 Скриншот сохранён: {fullPath}");
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка скриншота: {ex.Message}");
            }
        }

        private void BtnToggleLogs_Click(object sender, RoutedEventArgs e)
        {
            if (LogContainer.Visibility == Visibility.Visible)
            {
                LogContainer.Visibility = Visibility.Collapsed;
                TxtLogToggleLabel.Text = "[>_] Логи";
                TxtLogToggleLabel.Foreground = (Brush)FindResource("ObsidianTextSecondary");
            }
            else
            {
                LogContainer.Visibility = Visibility.Visible;
                TxtLogToggleLabel.Text = "[>_] Скрыть";
                TxtLogToggleLabel.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private void TglServicePanel_Changed(object sender, RoutedEventArgs e)
        {
            if (ServiceSidePanel == null) return;
            ServiceSidePanel.Visibility = TglServicePanel.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void PlayerPhoto_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (sender is FrameworkElement fe && fe.DataContext is PlayerSetupItem player)
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = $"Фото для {player.GameName}",
                    Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Все файлы|*.*"
                };
                if (dlg.ShowDialog() == true)
                {
                    player.PhotoPath = dlg.FileName;
                    Log($"📷 Фото загружено для {player.GameName}: {System.IO.Path.GetFileName(dlg.FileName)}");
                }
            }
        }

        private void BtnShowParticipants_Click(object sender, RoutedEventArgs e)
        {
            if (ParticipantsOverlay == null) return;
            ParticipantsOverlay.Visibility = ParticipantsOverlay.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void BtnCloseParticipants_Click(object sender, RoutedEventArgs e)
        {
            if (ParticipantsOverlay != null)
                ParticipantsOverlay.Visibility = Visibility.Collapsed;
        }

        private void ParticipantsOverlay_BackgroundClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ParticipantsOverlay != null)
                ParticipantsOverlay.Visibility = Visibility.Collapsed;
        }

        private void ParticipantsOverlay_CardClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        // ═══ ДОСЬЕ УЧАСТНИКА ═══
        private void PlayerCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PlayerSetupItem player)
            {
                _currentDossierPlayer = player;
                DossierGameName.Text = player.GameName;
                DossierFullName.Text = player.FullName;
                DossierCityDesc.Text = player.CityDesc;
                DossierAge.Text = player.Age > 0 ? player.Age.ToString() : "";
                DossierBio.Text = player.Bio;
                DossierPhotoImage.Source = player.PhotoSource;
                DossierOverlay.Visibility = Visibility.Visible;
            }
        }

        private void BtnSaveDossier_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDossierPlayer == null) return;
            _currentDossierPlayer.GameName = DossierGameName.Text;
            _currentDossierPlayer.FullName = DossierFullName.Text;
            _currentDossierPlayer.CityDesc = DossierCityDesc.Text;
            if (int.TryParse(DossierAge.Text, out int age)) _currentDossierPlayer.Age = age;
            _currentDossierPlayer.Bio = DossierBio.Text;
            DossierOverlay.Visibility = Visibility.Collapsed;
            Log($"📋 Досье обновлено: {_currentDossierPlayer.GameName}");
        }

        private void BtnCloseDossier_Click(object sender, RoutedEventArgs e)
        {
            DossierOverlay.Visibility = Visibility.Collapsed;
        }

        private void DossierOverlay_BackgroundClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DossierOverlay.Visibility = Visibility.Collapsed;
        }

        private void DossierOverlay_CardClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void DossierPhoto_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (_currentDossierPlayer == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Фото для {_currentDossierPlayer.GameName}",
                Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Все файлы|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                _currentDossierPlayer.PhotoPath = dlg.FileName;
                DossierPhotoImage.Source = _currentDossierPlayer.PhotoSource;
                Log($"📷 Фото загружено: {System.IO.Path.GetFileName(dlg.FileName)}");
            }
        }

        private void BtnCloseToolbar_Click(object sender, RoutedEventArgs e)
        {
            if (ServiceSidePanel == null) return;
            ServiceSidePanel.Visibility = Visibility.Collapsed;
            if (TglServicePanel != null) TglServicePanel.IsChecked = false;
        }

        private void SetScreenToggle(System.Windows.Controls.Button btn, bool isOpen)
        {
            if (btn == null) return;
            btn.BorderBrush = isOpen 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0xC5, 0x5E))
                : System.Windows.Media.Brushes.Transparent;
        }

        private void ChkDebugEndRoundButton_Click(object sender, RoutedEventArgs e)
        {
            if (BtnDebugEndRoundTimer == null) return;
            BtnDebugEndRoundTimer.Visibility = ChkDebugEndRoundButton.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }


        private void BtnDebugEndRoundTimer_Click(object sender, RoutedEventArgs e)
        {
            if (_engine.CurrentState == GameState.Playing && _roundTimer != null && _roundTimer.IsEnabled)
            {
                const int jumpToSeconds = 5;
                Log($"✂ [DEBUG] Принудительное завершение времени раунда (осталось {jumpToSeconds} секунд).");
                if (_timeLeftSeconds > jumpToSeconds)
                {
                    _timeLeftSeconds = jumpToSeconds;
                    UpdateTimerDisplay();

                    var (position, duration) = _audioManager.GetAudioPosition();

                    if (duration > TimeSpan.Zero)
                    {
                        // Структура трека: [отбивка TRACK_INTRO_BEAT_MS] + [roundDuration сек игры] + [долгий фейд-аут]
                        // НЕ ориентируемся на duration файла — после гонга идёт длинный фейдинг.
                        // Позиция трека когда таймер = N: (roundDuration - N) + introBeat
                        int fullRoundDuration = _engine.GetRoundDuration();
                        double introSec = TRACK_INTRO_BEAT_MS / 1000.0;
                        double targetSec = (fullRoundDuration - jumpToSeconds) + introSec;

                        _audioManager.SetAudioPosition(TimeSpan.FromSeconds(targetSec));
                        Log($"[DEBUG] Аудио: {targetSec:F1}s (раунд {fullRoundDuration}с, отбивка {introSec:F1}с, таймер → {jumpToSeconds}с).");
                    }
                }
            }
            else
            {
                Log("⚠ [DEBUG] Кнопка работает только во время активного таймера раунда (игра идет).");
            }
        }

        #region Central Context Management

        /// <summary>
        /// Переключает центральный контекст между SETUP, PLAY, STATS, EDIT
        /// </summary>
        private void SetCentralContext(string mode)
        {
            try
            {
                SetupContext.Visibility = Visibility.Collapsed;
                PlayContext.Visibility = Visibility.Collapsed;
                AnalyticsContext.Visibility = Visibility.Collapsed;
                EditorContext.Visibility = Visibility.Collapsed;

                switch (mode.ToUpper())
                {
                    case "SETUP":
                        SetupContext.Visibility = Visibility.Visible;
                        break;

                    case "PLAY":
                        PlayContext.Visibility = Visibility.Visible;
                        if (_engine?.CurrentState == GameState.FinalDuel)
                        {
                            GamePlayPanel.Visibility = Visibility.Collapsed;
                            HeadToHeadPanel.Visibility = Visibility.Visible;
                        }
                        break;

                    case "STATS":
                        AnalyticsContext.Visibility = Visibility.Visible;
                        UpdateAnalyticsData();
                        break;

                    case "EDIT":
                        EditorContext.Visibility = Visibility.Visible;
                        LoadQuestionsForEditor();
                        break;

                    default:
                        SetupContext.Visibility = Visibility.Visible;
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Ошибка при переключении контекста: {ex.Message}");
            }
        }

        /// <summary>
        /// Обновляет данные в аналитическом контексте
        /// </summary>
        private void UpdateAnalyticsData()
        {
            try
            {
                int roundDuration = Math.Max(0, _engine.GetRoundDuration() - _timeLeftSeconds);
                var analytics = _statsAnalyzer.AnalyzeRound(roundDuration, _lastRoundBankForSummary);

                TxtAnalyticsTotalGames.Text = _engine.CurrentRound.ToString();
                TxtAnalyticsAvgBank.Text = $"{analytics.TotalBanked:N0} ₽";
                TxtAnalyticsBestPlayer.Text = string.IsNullOrEmpty(analytics.StrongestLink) ? "—" : analytics.StrongestLink;
                TxtAnalyticsPlayTime.Text = $"{roundDuration / 60}:{roundDuration % 60:D2}";

                var rows = analytics.PlayerStats.Select(p => new AnalyticsRow
                {
                    Name = p.Name,
                    CorrectAnswers = p.CorrectAnswers,
                    WrongAnswers = p.TotalMistakes,
                    MoneyLost = p.ChainBreaksLost,
                    PassCount = p.Passes,
                    BankedAmount = $"{p.BankedMoney:N0}",
                    Prediction = p.Name == analytics.WeakestLink ? "СЛАБОЕ ЗВЕНО"
                               : p.Name == analytics.StrongestLink ? "СИЛЬНОЕ ЗВЕНО"
                               : "",
                    IsWeakest = p.Name == analytics.WeakestLink,
                    IsStrongest = p.Name == analytics.StrongestLink
                }).ToList();

                AnalyticsPlayersGrid.ItemsSource = rows;

                AnalyticsPlayersGrid.LoadingRow -= AnalyticsGrid_LoadingRow;
                AnalyticsPlayersGrid.LoadingRow += AnalyticsGrid_LoadingRow;
            }
            catch (Exception ex)
            {
                Log($"Ошибка обновления аналитики: {ex.Message}");
            }
        }

        private void QuestionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QuestionsDataGrid.SelectedItem is QuestionData q)
            {
                TxtEditQuestion.Text = q.Text;
                TxtEditAnswer.Text = q.Answer;
                TxtEditAccept.Text = q.AcceptableAnswers;
                TxtEditRound.Text = q.Round?.ToString() ?? "";
            }
        }

        private void BtnEditorApply_Click(object sender, RoutedEventArgs e)
        {
            if (QuestionsDataGrid.SelectedItem is QuestionData q)
            {
                q.Text = TxtEditQuestion.Text;
                q.Answer = TxtEditAnswer.Text;
                q.AcceptableAnswers = TxtEditAccept.Text;
                if (int.TryParse(TxtEditRound.Text, out int round))
                    q.Round = round;
                else
                    q.Round = null;
                
                QuestionsDataGrid.Items.Refresh();
                SaveQuestionsForEditor();
            }
        }

        private void BtnEditorNew_Click(object sender, RoutedEventArgs e)
        {
            var newQ = new QuestionData
            {
                Text = "НОВЫЙ ВОПРОС",
                Answer = "ОТВЕТ",
                Round = 1
            };
            _editorQuestions.Add(newQ);
            QuestionsDataGrid.SelectedItem = newQ;
            QuestionsDataGrid.ScrollIntoView(newQ);
            Log("➕ Добавлен новый пустой вопрос.");
        }

        private void BtnEditorDelete_Click(object sender, RoutedEventArgs e)
        {
            if (QuestionsDataGrid.SelectedItem is QuestionData q)
            {
                _editorQuestions.Remove(q);
                Log($"🗑️ Вопрос '{q.Text}' удален.");
                SaveQuestionsForEditor();
            }
        }

        private void AnalyticsGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is AnalyticsRow row)
            {
                if (row.IsWeakest)
                    e.Row.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xDA, 0x37, 0x3C));
                else if (row.IsStrongest)
                    e.Row.Background = new SolidColorBrush(Color.FromArgb(0x20, 0x2E, 0xCC, 0x71));
                else
                    e.Row.Background = Brushes.Transparent;
            }
        }

        /// <summary>
        /// Stepper: highlights the active voting phase button, dims the rest.
        /// step: 1=VOTE, 2=END, 3=REVEAL, 4=VERDICT, 0=all dim
        /// </summary>
        private void ApplyVotingStepper(int step)
        {
            var buttons = new[] { BtnStartVoting, BtnEndVoting, BtnReveal, BtnHotDiscussion };
            var activeBorders = new[]
            {
                new SolidColorBrush(Color.FromRgb(0xF1, 0xC4, 0x0F)),
                new SolidColorBrush(Color.FromRgb(0xDA, 0x37, 0x3C)),
                new SolidColorBrush(Color.FromRgb(0x88, 0x00, 0xAA)),
                new SolidColorBrush(Color.FromRgb(0xE6, 0x5C, 0x00))
            };
            var activeFg = new[]
            {
                new SolidColorBrush(Color.FromRgb(0xF1, 0xC4, 0x0F)),
                Brushes.White,
                Brushes.White,
                Brushes.White
            };
            var activeBg = new Brush[]
            {
                new SolidColorBrush(Color.FromArgb(0x18, 0xF1, 0xC4, 0x0F)),
                new SolidColorBrush(Color.FromArgb(0x18, 0xDA, 0x37, 0x3C)),
                new SolidColorBrush(Color.FromArgb(0x18, 0x88, 0x00, 0xAA)),
                new SolidColorBrush(Color.FromArgb(0x18, 0xE6, 0x5C, 0x00))
            };

            for (int i = 0; i < buttons.Length; i++)
            {
                bool isActive = (i + 1) == step;
                buttons[i].IsEnabled = isActive;
                buttons[i].Opacity = isActive ? 1.0 : 0.4;
                buttons[i].BorderBrush = isActive ? activeBorders[i] : new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x2a));
                buttons[i].Foreground = isActive ? activeFg[i] : new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44));
                buttons[i].Background = isActive ? activeBg[i] : Brushes.Transparent;
            }
        }

        private void EliminationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AnalyticsPlayersGrid?.ItemsSource == null) return;

            string selectedName = "";
            if (EliminationComboBox.SelectedItem != null)
            {
                var prop = EliminationComboBox.SelectedItem.GetType().GetProperty("Name");
                selectedName = prop?.GetValue(EliminationComboBox.SelectedItem)?.ToString() ?? "";
            }

            foreach (var item in AnalyticsPlayersGrid.Items)
            {
                var row = AnalyticsPlayersGrid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                if (row == null) continue;

                bool isSelected = item is AnalyticsRow ar && ar.Name == selectedName;
                if (isSelected)
                {
                    row.Background = new SolidColorBrush(Color.FromArgb(0x40, 0xDA, 0x37, 0x3C));
                    row.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDA, 0x37, 0x3C));
                    row.BorderThickness = new Thickness(1);
                }
                else
                {
                    row.BorderThickness = new Thickness(0);
                    if (item is AnalyticsRow arx)
                    {
                        if (arx.IsWeakest)
                            row.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xDA, 0x37, 0x3C));
                        else if (arx.IsStrongest)
                            row.Background = new SolidColorBrush(Color.FromArgb(0x20, 0x2E, 0xCC, 0x71));
                        else
                            row.Background = Brushes.Transparent;
                    }
                    else
                    {
                        row.Background = Brushes.Transparent;
                    }
                }
            }
        }

        #endregion

        #endregion
    }

    public class AnalyticsRow
    {
        public string Name { get; set; } = "";
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int MoneyLost { get; set; }
        public int PassCount { get; set; }
        public string BankedAmount { get; set; } = "0";
        public string Prediction { get; set; } = "";
        public bool IsWeakest { get; set; }
        public bool IsStrongest { get; set; }
    }
}
