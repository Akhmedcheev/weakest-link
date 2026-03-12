using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WeakestLink.Core.Models;

namespace WeakestLink.Core
{
    /// <summary>
    /// Статистика игрока за раунд или всю игру.
    /// </summary>
    public class PlayerStats
    {
        public int CorrectAnswers { get; set; } = 0;
        public int IncorrectAnswers { get; set; } = 0;
        public int Passes { get; set; } = 0;
        public int BankedMoney { get; set; } = 0;
        public int BankPressCount { get; set; } = 0; // Количество раз, когда игрок сказал "Банк"
        /// <summary>Сумма, сгоревшая из-за неверных ответов этого игрока (обнуление цепочки).</summary>
        public int BurnedByWrongAnswers { get; set; } = 0;
        /// <summary>Точная сумма чужих денег, сброшенных игроком при неверном ответе или пасе (обнуление цепочки).</summary>
        public int ExactDroppedMoney { get; set; } = 0;
    }

    /// <summary>
    /// Аргументы события изменения игрового состояния.
    /// </summary>
    public class StateChangedEventArgs : EventArgs
    {
        public GameState OldState { get; }
        public GameState NewState { get; }

        public StateChangedEventArgs(GameState oldState, GameState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// Аргументы события изменения финансовой статистики (банка/цепочки).
    /// </summary>
    public class BankChangedEventArgs : EventArgs
    {
        public int CurrentChainIndex { get; }
        public int RoundBank { get; }
        public int TotalBank { get; }
        public int CurrentRound { get; }
        public ObservableCollection<string> ActivePlayers { get; }
        public string CurrentPlayerTurn { get; }

        public BankChangedEventArgs(int currentChainIndex, int roundBank, int totalBank, int currentRound, ObservableCollection<string> activePlayers, string currentPlayerTurn)
        {
            CurrentChainIndex = currentChainIndex;
            RoundBank = roundBank;
            TotalBank = totalBank;
            CurrentRound = currentRound;
            ActivePlayers = activePlayers;
            CurrentPlayerTurn = currentPlayerTurn;
        }
    }

    /// <summary>
    /// Игровой движок (Core). Отвечает за состояние игры и финансовую математику.
    /// Независим от UI или сети.
    /// </summary>
    public class GameEngine
    {
        /// <summary>
        /// Денежная цепочка для ответов.
        /// Массив включает 8 шагов: 1000, 2000, 5000, 10000, 20000, 30000, 40000, 50000.
        /// </summary>
        public readonly int[] BankChain = { 1000, 2000, 5000, 10000, 20000, 30000, 40000, 50000 };

        /// <summary>
        /// Текущее состояние игры.
        /// </summary>
        public GameState CurrentState { get; private set; } = GameState.Idle;

        /// <summary>
        /// Текущий шаг по цепочке выигрыша (от 0 до BankChain.Length).
        /// 0 значит нет накопленной суммы за непрерывные ответы.
        /// </summary>
        public int CurrentChainIndex { get; private set; } = 0;

        /// <summary>
        /// Сохраненные деньги в текущем раунде.
        /// </summary>
        public int RoundBank { get; private set; } = 0;

        /// <summary>
        /// Сумма, сгоревшая в текущем раунде из-за неверных ответов (обнуление цепочек).
        /// </summary>
        public int RoundBurned { get; private set; } = 0;

        /// <summary>
        /// Общий банк игры.
        /// </summary>
        public int TotalBank { get; private set; } = 0;

        /// <summary>
        /// Имя игрока, чей сейчас ход.
        /// </summary>
        public string CurrentPlayerTurn { get; private set; } = string.Empty;

        /// <summary>
        /// Имя игрока, покинувшего игру в текущем цикле голосования.
        /// </summary>
        public string EliminatedPlayerName { get; private set; } = string.Empty;

        /// <summary>Сильное звено последнего игрового раунда (для выбора в финальной дуэли).</summary>
        public string LastStrongestLinkName { get; set; } = string.Empty;

        public string LastActionText { get; set; } = "";
        public string StatusDescription { get; set; } = "Ожидание. Система готова.";
        public string InstructionText { get; set; } = "Добавьте игроков и нажмите READY.";

        /// <summary>
        /// Список имен активных игроков.
        /// </summary>
        public ObservableCollection<string> ActivePlayers { get; } = new ObservableCollection<string>();

        /// <summary>
        /// Текущий номер раунда. 0 = ещё не подготовлен ни один раунд; PrepareNewRound() вызывает NextRound(), который увеличивает до 1, 2, ...
        /// </summary>
        public int CurrentRound { get; private set; } = 0;

        /// <summary>
        /// Событие, вызываемое при успешном переходе в новое состояние (State Machine).
        /// </summary>
        public event EventHandler<StateChangedEventArgs> StateChanged;

        /// <summary>
        /// Событие, вызываемое при изменении текущей цепочки или банков (Round/Total).
        /// </summary>
        public event EventHandler<BankChangedEventArgs> BankChanged;

        public int MaxRoundBank { get; } = 50000;

        /// <summary>
        /// Событие, вызываемое при достижении максимального банка в раунде (50,000).
        /// </summary>
        public event Action? MaxBankReached;

        /// <summary>
        /// Статистика по игрокам и раундам: [Имя][Раунд] -> Данные.
        /// </summary>
        public Dictionary<string, Dictionary<int, PlayerStats>> PlayerStatistics { get; private set; } = new Dictionary<string, Dictionary<int, PlayerStats>>();

        /// <summary>
        /// Определяет игрока, который должен начинать текущий раунд.
        /// </summary>
        public string GetStartingPlayerForRound(int round)
        {
            if (ActivePlayers == null || ActivePlayers.Count == 0)
                return "-";

            // 1 раунд: по алфавиту
            if (round == 1)
            {
                return ActivePlayers.OrderBy(p => p).First();
            }

            // Раунды со 2 по 6 (и далее по той же логике)
            if (round >= 2)
            {
                int prevRound = round - 1;

                // Получаем список игроков, у которых есть статистика за прошлый раунд, отсортированный по убыванию
                var prevRoundStats = PlayerStatistics
                    .Select(pair => new { 
                        Name = pair.Key, 
                        Stats = pair.Value.ContainsKey(prevRound) ? pair.Value[prevRound] : new PlayerStats() 
                    })
                    .OrderByDescending(x => x.Stats.CorrectAnswers)
                    .ThenByDescending(x => x.Stats.BankedMoney)
                    .ToList();

                // а, б, в) Ищем Сильное звено или "второго по силе", если первый уже выбыл
                foreach (var entry in prevRoundStats)
                {
                    if (ActivePlayers.Contains(entry.Name))
                    {
                        return entry.Name;
                    }
                }

                // г) Если данных за прошлый раунд нет (нулевые stats) или никого не осталось
                // Ищем Сильное звено по совокупности ВСЕХ раундов среди оставшихся участников
                var allTimeStrongest = ActivePlayers
                    .Select(p => new {
                        Name = p,
                        TotalCorrect = PlayerStatistics.ContainsKey(p) ? PlayerStatistics[p].Values.Sum(s => s.CorrectAnswers) : 0,
                        TotalBanked = PlayerStatistics.ContainsKey(p) ? PlayerStatistics[p].Values.Sum(s => s.BankedMoney) : 0
                    })
                    .OrderByDescending(x => x.TotalCorrect)
                    .ThenByDescending(x => x.TotalBanked)
                    .FirstOrDefault();

                if (allTimeStrongest != null)
                {
                    CurrentPlayerTurn = allTimeStrongest.Name;
                    return allTimeStrongest.Name;
                }
            }

            string firstAlpha = ActivePlayers.OrderBy(p => p).First();
            CurrentPlayerTurn = firstAlpha;
            return firstAlpha;
        }

        public GameEngine()
        {
        }

        /// <summary>
        /// Переход в новое состояние игры (State Machine).
        /// Содержит базовую валидацию. При недопустимом переходе выбрасывает исключение.
        /// </summary>
        /// <param name="newState">Желаемое новое состояние.</param>
        public void TransitionTo(GameState newState)
        {
            if (CurrentState == newState)
                return;

            bool isValid = false;

            // Базовая логика валидации переходов
            switch (CurrentState)
            {
                case GameState.Idle:
                    if (newState == GameState.RoundReady || newState == GameState.FinalDuel
                        || newState == GameState.IntroOpening || newState == GameState.RulesExplanation) isValid = true;
                    break;
                case GameState.IntroOpening:
                    if (newState == GameState.IntroNarrative || newState == GameState.Idle) isValid = true;
                    break;
                case GameState.IntroNarrative:
                    if (newState == GameState.PlayerIntro || newState == GameState.Idle) isValid = true;
                    break;
                case GameState.PlayerIntro:
                    if (newState == GameState.RulesExplanation || newState == GameState.Idle) isValid = true;
                    break;
                case GameState.RulesExplanation:
                    if (newState == GameState.RoundReady || newState == GameState.Idle) isValid = true;
                    break;
                case GameState.RoundReady:
                    // После готовности начинается сам раунд (Playing), либо сброс в Idle
                    if (newState == GameState.Playing || newState == GameState.Idle) isValid = true;
                    break;
                case GameState.Playing:
                    if (newState == GameState.RoundSummary || newState == GameState.Voting || newState == GameState.Idle) isValid = true;
                    break;
                case GameState.RoundSummary:
                    if (newState == GameState.Voting || newState == GameState.Idle) isValid = true;
                    break;
                case GameState.Voting:
                    if (newState == GameState.Discussion || newState == GameState.Elimination || newState == GameState.Idle) isValid = true;
                    break;
                case GameState.Discussion:
                    if (newState == GameState.Reveal || newState == GameState.Elimination || newState == GameState.Idle) isValid = true;
                    break;
                case GameState.Reveal:
                    if (newState == GameState.Elimination || newState == GameState.Discussion || newState == GameState.Idle) isValid = true;
                    break;
                case GameState.Elimination:
                    // После исключения мы готовы к новому раунду (RoundReady), финалу или завершаем игру (Idle)
                    if (newState == GameState.RoundReady || newState == GameState.FinalDuel || newState == GameState.Idle) isValid = true;
                    break;
                case GameState.FinalDuel:
                    // После финала возможен только сброс в Idle
                    if (newState == GameState.Idle) isValid = true;
                    break;
            }

            if (!isValid)
            {
                throw new InvalidOperationException($"Недопустимый переход состояния из {CurrentState} в {newState}");
            }

            var oldState = CurrentState;
            CurrentState = newState;

            StateChanged?.Invoke(this, new StateChangedEventArgs(oldState, newState));
        }

        /// <summary>
        /// Увеличивает цепочку правильных ответов.
        /// </summary>
        public void CorrectAnswer()
        {
            if (CurrentState != GameState.Playing)
            {
                throw new InvalidOperationException("Отвечать на вопросы можно только во время игры (состояние Playing).");
            }

            // Запись в статистику правильных ответов
            UpdatePlayerStat(CurrentPlayerTurn, s => s.CorrectAnswers++);

            // Индекс шага цепочки (не выходим за верх цепочки).
            if (CurrentChainIndex < BankChain.Length)
            {
                CurrentChainIndex++;
            }

            // Автобанк при достижении вершины цепочки (50000).
            if (CurrentChainIndex >= BankChain.Length)
            {
                int valueToBank = BankChain[CurrentChainIndex - 1];
                UpdatePlayerStat(CurrentPlayerTurn, s =>
                {
                    s.BankedMoney += valueToBank;
                    s.BankPressCount++;
                });
                RoundBank += valueToBank;
                CurrentChainIndex = 0;

                if (RoundBank >= MaxRoundBank)
                {
                    RoundBank = MaxRoundBank;
                    NotifyBankChanged();
                    MoveToNextPlayer();
                    MaxBankReached?.Invoke();
                    return;
                }
            }

            MoveToNextPlayer();
            NotifyBankChanged();
        }

        /// <summary>
        /// Сбрасывает текущую цепочку в ноль при неправильном ответе.
        /// </summary>
        public void WrongAnswer()
        {
            if (CurrentState != GameState.Playing)
            {
                throw new InvalidOperationException("Отвечать на вопросы можно только во время игры (состояние Playing).");
            }

            // Сумма на цепочке в момент неверного ответа — сгорает
            int valueLost = CurrentChainIndex > 0 ? BankChain[CurrentChainIndex - 1] : 0;
            RoundBurned += valueLost;
            UpdatePlayerStat(CurrentPlayerTurn, s =>
            {
                s.IncorrectAnswers++;
                s.BurnedByWrongAnswers += valueLost;
                s.ExactDroppedMoney += valueLost;
            });

            CurrentChainIndex = 0;
            MoveToNextPlayer();
            NotifyBankChanged();
        }

        /// <summary>
        /// Сбрасывает текущую цепочку в ноль при пасе.
        /// </summary>
        public void Pass()
        {
            if (CurrentState != GameState.Playing)
            {
                throw new InvalidOperationException("Пасовать можно только во время игры (состояние Playing).");
            }

            // Запись в статистику пасов; сумма на цепочке сбрасывается — это сброшенные чужие деньги
            int valueDropped = CurrentChainIndex > 0 ? BankChain[CurrentChainIndex - 1] : 0;
            UpdatePlayerStat(CurrentPlayerTurn, s =>
            {
                s.Passes++;
                s.ExactDroppedMoney += valueDropped;
            });

            CurrentChainIndex = 0;
            MoveToNextPlayer();
            NotifyBankChanged();
        }

        /// <summary>
        /// Принудительно сбрасывает цепочку (например, при экстренной остановке).
        /// </summary>
        public void ResetChain()
        {
            CurrentChainIndex = 0;
            NotifyBankChanged();
        }

        /// <summary>
        /// Положить накопленную по цепочке сумму в банк раунда.
        /// Сбрасывает текущую цепочку.
        /// </summary>
        public void Bank()
        {
            if (CurrentState != GameState.Playing)
            {
                throw new InvalidOperationException("Класть деньги в банк можно только во время игры (состояние Playing).");
            }

            if (CurrentChainIndex > 0)
            {
                int valueToBank = BankChain[CurrentChainIndex - 1];
                
                // Запись в статистику игрока суммы, которую он положил в банк
                UpdatePlayerStat(CurrentPlayerTurn, s => 
                {
                    s.BankedMoney += valueToBank;
                    s.BankPressCount++; // Увеличиваем счетчик банковских операций
                });
                
                RoundBank += valueToBank;
                CurrentChainIndex = 0;

                if (RoundBank >= MaxRoundBank)
                {
                    RoundBank = MaxRoundBank;
                    NotifyBankChanged();
                    MaxBankReached?.Invoke();
                }
                else
                {
                    NotifyBankChanged();
                }
            }
        }

        /// <summary>
        /// Метод для переноса сохраненного банка текущего раунда в общий банк.
        /// (Дополнительный вспомогательный метод).
        /// </summary>
        public void ApplyRoundBankToTotal()
        {
            int amountToAdd = RoundBank;
            
            // ПРЕФИНАЛ: когда осталось 2 игрока, сумма удваивается
            if (ActivePlayers.Count == 2)
            {
                amountToAdd *= 2;
            }

            TotalBank += amountToAdd;
            RoundBank = 0;
            CurrentChainIndex = 0;

            NotifyBankChanged();
        }

        /// <summary>
        /// Переход к следующему раунду.
        /// </summary>
        public void NextRound()
        {
            CurrentRound++;
            RoundBank = 0;
            RoundBurned = 0;
            CurrentChainIndex = 0;
            
            // Сбрасываем текущего игрока перед новым раундом (будет установлен через GetStartingPlayerForRound)
            CurrentPlayerTurn = string.Empty;
            
            NotifyBankChanged();
        }

        /// <summary>
        /// Полная подготовка к новому раунду: сброс таймера, определение стартового игрока, подготовка вопроса
        /// </summary>
        public void PrepareNewRound()
        {
            // 0. Переходим к следующему раунду
            NextRound();
            
            // 1. Очистка и сброс статистики раунда
            RoundBank = 0;
            RoundBurned = 0;
            CurrentChainIndex = 0;
            
            // 2. Определение стартового игрока для нового раунда
            CurrentPlayerTurn = GetStartingPlayerForRound(CurrentRound);
            
            // 3. Инициализация статистики для нового раунда
            foreach (var playerName in ActivePlayers)
            {
                if (!PlayerStatistics.ContainsKey(playerName))
                    PlayerStatistics[playerName] = new Dictionary<int, PlayerStats>();
                
                if (!PlayerStatistics[playerName].ContainsKey(CurrentRound))
                    PlayerStatistics[playerName][CurrentRound] = new PlayerStats();
            }
            
            // 4. Уведомление об изменениях
            NotifyBankChanged();
        }

        /// <summary>
        /// Финализирует подготовку раунда (стартовый игрок, статистика) без вызова NextRound.
        /// Используется при тестировании с ботами при выборе стартового раунда.
        /// </summary>
        public void FinalizeRoundSetup()
        {
            RoundBank = 0;
            RoundBurned = 0;
            CurrentChainIndex = 0;
            CurrentPlayerTurn = GetStartingPlayerForRound(CurrentRound);
            foreach (var playerName in ActivePlayers)
            {
                if (!PlayerStatistics.ContainsKey(playerName))
                    PlayerStatistics[playerName] = new Dictionary<int, PlayerStats>();
                if (!PlayerStatistics[playerName].ContainsKey(CurrentRound))
                    PlayerStatistics[playerName][CurrentRound] = new PlayerStats();
            }
            NotifyBankChanged();
        }

        /// <summary>
        /// Получает время для нового раунда в секундах
        /// </summary>
        public int GetNewRoundDuration()
        {
            return GetRoundDuration();
        }

        /// <summary>
        /// Получает длительность текущего раунда в секундах.
        /// </summary>
        public int GetRoundDuration()
        {
            switch (CurrentRound)
            {
                case 1: return 150; // 2:30
                case 2: return 140; // 2:20
                case 3: return 130; // 2:10
                case 4: return 120; // 2:00
                case 5: return 110; // 1:50
                case 6: return 100; // 1:40
                default: 
                    // Если осталось 2 игрока — это Предфинал (90 сек)
                    if (ActivePlayers.Count == 2) return 90;
                    return 150;
            }
        }

        /// <summary>
        /// Удаляет игрока из игры.
        /// </summary>
        public void EliminatePlayer(string name)
        {
            EliminatedPlayerName = name;
            TransitionTo(GameState.Elimination);
        }

        /// <summary>
        /// Передает ход следующему активному игроку.
        /// </summary>
        public void MoveToNextPlayer()
        {
            if (ActivePlayers.Count == 0) return;

            int currentIndex = ActivePlayers.IndexOf(CurrentPlayerTurn);
            int nextIndex = (currentIndex + 1) % ActivePlayers.Count;
            CurrentPlayerTurn = ActivePlayers[nextIndex];
        }

        #region ФИНАЛЬНАЯ ДУЭЛЬ (Head-to-Head)

        public List<bool?> Player1FinalScores { get; } = new List<bool?>();
        public List<bool?> Player2FinalScores { get; } = new List<bool?>();
        public int CurrentFinalQuestionIndex { get; private set; } = 0;
        public bool IsSuddenDeath { get; private set; } = false;
        public bool IsPlayer1Turn { get; private set; } = true;
        public string FinalWinner { get; private set; } = string.Empty;

        // Используем статический список, чтобы данные не терялись при пересоздании движка (если такое случится)
        private static List<QuestionData> _finalQuestions = new List<QuestionData>();
        public QuestionData? CurrentFinalQuestion { get; private set; }

        public event Action<string>? FinalDuelEnded;

        public void LoadFinalQuestions(IEnumerable<QuestionData> questions)
        {
            try
            {
                _finalQuestions = questions.ToList();

                // ЖЕСТКИЙ РЕЗЕРВ: Если список пуст, добавляем минимум 3 вопроса программно
                if (_finalQuestions.Count == 0)
                {
                    _finalQuestions.Add(new QuestionData { Id = 1001, Text = "Резерв 1: Какого цвета небо в ясный день?", Answer = "Голубое", AcceptableAnswers = "Синее" });
                    _finalQuestions.Add(new QuestionData { Id = 1002, Text = "Резерв 2: Сколько будет два плюс два?", Answer = "Четыре", AcceptableAnswers = "4" });
                    _finalQuestions.Add(new QuestionData { Id = 1003, Text = "Резерв 3: Какая планета третья от Солнца?", Answer = "Земля", AcceptableAnswers = "Земля" });
                }

                // Перемешивание
                var rnd = new Random();
                _finalQuestions = _finalQuestions.OrderBy(x => rnd.Next()).ToList();
                
                CurrentFinalQuestionIndex = 0;
            }
            catch (Exception ex)
            {
                // Если совсем всё плохо - гарантируем наличие хотя бы одного вопроса
                _finalQuestions = new List<QuestionData> { 
                    new QuestionData { Text = "КРИТИЧЕСКАЯ ОШИБКА: " + ex.Message, Answer = "ОШИБКА" } 
                };
            }
        }

        /// <param name="player1StartsFirst">true = первый финалист отвечает первым, false = второй.</param>
        public void StartFinalDuel(bool player1StartsFirst = true)
        {
            if (ActivePlayers.Count != 2) return;

            Player1FinalScores.Clear();
            Player2FinalScores.Clear();
            for (int i = 0; i < 5; i++)
            {
                Player1FinalScores.Add(null);
                Player2FinalScores.Add(null);
            }

            IsSuddenDeath = false;
            IsPlayer1Turn = player1StartsFirst;
            FinalWinner = string.Empty;
            CurrentFinalQuestionIndex = 0;

            CurrentPlayerTurn = player1StartsFirst ? ActivePlayers[0] : ActivePlayers[1];
            TransitionTo(GameState.FinalDuel);
            
            // Мы НЕ вызываем здесь GetNextFinalQuestion, чтобы это сделала кнопка СТАРТ ДУЭЛИ напрямую
            NotifyBankChanged();
        }

        public string GetNextFinalQuestion()
        {
            if (_finalQuestions == null || _finalQuestions.Count == 0)
            {
                // ЖЕСТКИЙ РЕЗЕРВ: Если база пуста или очистилась, подгружаем встроенные базовые вопросы
                _finalQuestions = new List<QuestionData>
                {
                    new QuestionData { Text = "Вопрос 1: Какая планета самая большая в Солнечной системе?", Answer = "Юпитер" },
                    new QuestionData { Text = "Вопрос 2: Столица Японии?", Answer = "Токио" },
                    new QuestionData { Text = "Вопрос 3: Кто написал 'Муму'?", Answer = "Тургенев" },
                    new QuestionData { Text = "Вопрос 4: Самый твердый минерал?", Answer = "Алмаз" },
                    new QuestionData { Text = "Вопрос 5: Сколько океанов на Земле?", Answer = "Пять" }
                };
            }

            // Используем индекс с остатком от деления для бесконечного цикла (Modulo)
            var question = _finalQuestions[CurrentFinalQuestionIndex % _finalQuestions.Count];
            
            CurrentFinalQuestion = question; // Обновляем состояние текущего вопроса
            return question.Text;
        }

        public void ProcessFinalAnswer(bool isCorrect)
        {
            // Записываем ответ текущему игроку
            if (IsPlayer1Turn)
            {
                int index = Player1FinalScores.IndexOf(null);
                if (index != -1) Player1FinalScores[index] = isCorrect;
            }
            else
            {
                int index = Player2FinalScores.IndexOf(null);
                if (index != -1) Player2FinalScores[index] = isCorrect;
            }

            // Жестко передаем ход сопернику
            IsPlayer1Turn = !IsPlayer1Turn;

            // Если ход вернулся к первому игроку, значит пара вопросов завершена
            if (IsPlayer1Turn)
            {
                CurrentFinalQuestionIndex++;
            }

            // Проверяем статус дуэли (победа/внезапная смерть)
            CheckFinalStatus();
            
            NotifyBankChanged();
        }

        private void CheckFinalStatus()
        {
            int score1 = Player1FinalScores.Count(r => r == true);
            int score2 = Player2FinalScores.Count(r => r == true);

            bool p1Done = Player1FinalScores.Count >= 5 && Player1FinalScores.All(r => r != null);
            bool p2Done = Player2FinalScores.Count >= 5 && Player2FinalScores.All(r => r != null);
            bool pairFinished = (p1Done == p2Done); // Упрощенно: если оба ответили одинаковое кол-во раз

            if (!IsSuddenDeath)
            {
                int remaining1 = Player1FinalScores.Count(r => r == null);
                int remaining2 = Player2FinalScores.Count(r => r == null);

                // а) Математическая проверка (не может ли один догнать другого)
                if (score1 > score2 + remaining2)
                {
                    EndFinal(ActivePlayers[0]);
                    return;
                }
                if (score2 > score1 + remaining1)
                {
                    EndFinal(ActivePlayers[1]);
                    return;
                }

                // б) 5 пар сыграны и ничья -> Sudden Death
                if (p1Done && p2Done && score1 == score2)
                {
                    IsSuddenDeath = true;
                    // Добавляем по одной ячейке для внезапной смерти
                    Player1FinalScores.Add(null);
                    Player2FinalScores.Add(null);
                }
            }
            else
            {
                // в) Sudden Death: проверка после каждой ЗАВЕРШЕННОЙ пары
                if (p1Done && p2Done)
                {
                    if (score1 != score2)
                    {
                        EndFinal(score1 > score2 ? ActivePlayers[0] : ActivePlayers[1]);
                    }
                    else
                    {
                        // Ничья продолжается: добавляем еще по одной паре
                        Player1FinalScores.Add(null);
                        Player2FinalScores.Add(null);
                    }
                }
            }
        }

        private void EndFinal(string winner)
        {
            FinalWinner = winner;
            FinalDuelEnded?.Invoke(winner);
        }

        #endregion

        private void UpdatePlayerStat(string playerName, Action<PlayerStats> updateAction)
        {
            if (string.IsNullOrEmpty(playerName)) return;

            if (!PlayerStatistics.ContainsKey(playerName))
                PlayerStatistics[playerName] = new Dictionary<int, PlayerStats>();

            if (!PlayerStatistics[playerName].ContainsKey(CurrentRound))
                PlayerStatistics[playerName][CurrentRound] = new PlayerStats();

            updateAction(PlayerStatistics[playerName][CurrentRound]);
        }

        /// <summary>
        /// Сбрасывает счетчик раундов (для отладки).
        /// </summary>
        public void ResetRoundCounter()
        {
            CurrentRound = 0;
            NotifyBankChanged();
        }

        /// <summary>
        /// Полный сброс игры: очистка игроков, банков, статистики, раунда; переход в Idle.
        /// </summary>
        public void ResetGame()
        {
            ActivePlayers.Clear();
            RoundBank = 0;
            RoundBurned = 0;
            TotalBank = 0;
            CurrentChainIndex = 0;
            CurrentRound = 0;
            CurrentPlayerTurn = string.Empty;
            EliminatedPlayerName = string.Empty;
            LastStrongestLinkName = string.Empty;
            PlayerStatistics = new Dictionary<string, Dictionary<int, PlayerStats>>();

            if (CurrentState != GameState.Idle)
                TransitionTo(GameState.Idle);
            else
                NotifyBankChanged();
        }

        private void NotifyBankChanged()
        {
            BankChanged?.Invoke(this, new BankChangedEventArgs(CurrentChainIndex, RoundBank, TotalBank, CurrentRound, ActivePlayers, CurrentPlayerTurn));
        }
    }
}
