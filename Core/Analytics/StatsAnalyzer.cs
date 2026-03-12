using System;
using System.Collections.Generic;
using System.Linq;
using WeakestLink.Core;

namespace WeakestLink.Core.Analytics
{
    /// <summary>
    /// Класс для анализа статистики раунда и определения сильных/слабых звеньев
    /// </summary>
    public class StatsAnalyzer
    {
        private readonly GameEngine _engine;
        private RoundAnalytics _currentAnalytics;

        public StatsAnalyzer(GameEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public class PlayerRoundStats
        {
            public string Name { get; set; } = "";
            public int CorrectAnswers { get; set; }
            public int IncorrectAnswers { get; set; }
            public int Passes { get; set; }
            public int BankedMoney { get; set; }
            public int BankPressCount { get; set; } // Количество банковских операций
            public int TotalQuestions { get; set; }
            
            // Суммарные ошибки (неверные + пасы)
            public int TotalMistakes => IncorrectAnswers + Passes;
            
            // Средний размер банка (защита от деления на ноль)
            public double AverageBankAmount => BankPressCount > 0 ? Math.Round((double)BankedMoney / BankPressCount, 0) : 0;
            
            // Общий процент успешности с учетом пасов как ошибок
            public double SuccessPercentage 
            { 
                get 
                {
                    if (TotalQuestions == 0) return 0; // Если вопросов не было, 0%
                    return Math.Round((double)CorrectAnswers / TotalQuestions * 100, 1);
                } 
            }
            
            // Альтернативный процент успешности (только верные от всех вопросов)
            public double PureSuccessPercentage => TotalQuestions > 0 ? Math.Round((double)CorrectAnswers / TotalQuestions * 100, 1) : 0;
            
            public int ChainBreaksLost { get; set; } // Денег потеряно при обрывах цепочки этим игроком (deprecated, используйте ExactDroppedMoney)
            public int ExactDroppedMoney { get; set; } // Точная сумма сброшенных чужих денег (неверный ответ + пас)
            public double AccuracyStandardDeviation { get; set; } // Стандартное отклонение процента успешных ответов по раундам
        }

        public class RoundAnalytics
        {
            public List<PlayerRoundStats> PlayerStats { get; set; } = new();
            public int TotalBanked { get; set; }
            public int TotalBurned { get; set; } // Сгоревшие деньги из-за обрывов цепочек
            public int RoundDurationSeconds { get; set; }
            public int TotalQuestionsAsked { get; set; }
            public double AverageAnswerTime => TotalQuestionsAsked > 0 ? Math.Round((double)RoundDurationSeconds / TotalQuestionsAsked, 1) : 0;
            public string StrongestLink { get; set; } = "";
            public string WeakestLink { get; set; } = "";
            public string EliminationPrediction { get; set; } = "";
            public string EliminationReason { get; set; } = ""; // Объяснение выбора
            public string PanicBanker { get; set; } = "";
            public string Parasite { get; set; } = ""; // Игрок с высоким ExactDroppedMoney и отрицательным вкладом // Главный перестраховщик
            public int MaxPossibleBank { get; set; }
            /// <summary>Доля сохранённого в банк от общего оборота (собрано + сгорело), в процентах. Согласуется с цифрами "Собрано" и "Сгорело" на экране.</summary>
            public double EfficiencyPercentage
            {
                get
                {
                    long total = TotalBanked + TotalBurned;
                    return total > 0 ? Math.Round((double)TotalBanked / total * 100, 1) : 0;
                }
            }
        }

        public RoundAnalytics AnalyzeRound(int roundDurationSeconds, int savedRoundBank = -1)
        {
            System.Diagnostics.Debug.WriteLine($"[ANALYZER START] Current round: {_engine.CurrentRound}");
            
            _currentAnalytics = new RoundAnalytics
            {
                RoundDurationSeconds = roundDurationSeconds,
                TotalQuestionsAsked = GetTotalQuestionsAsked(),
                MaxPossibleBank = _engine.BankChain.Sum()
            };

            foreach (var playerName in _engine.ActivePlayers)
            {
                var playerStats = GetPlayerRoundStats(playerName, roundDurationSeconds);
                _currentAnalytics.PlayerStats.Add(playerStats);
            }

            int actualRoundBank = savedRoundBank >= 0 ? savedRoundBank : _engine.RoundBank;
            _currentAnalytics.TotalBanked = Math.Min(actualRoundBank, _engine.MaxRoundBank);
            _currentAnalytics.TotalBurned = _engine.RoundBurned;

            // Определяем сильное и слабое звено (передаём StrongestLink в WeakestLink для разведения по полюсам)
            _currentAnalytics.StrongestLink = DetermineStrongestLink(_currentAnalytics.PlayerStats);
            _currentAnalytics.WeakestLink = DetermineWeakestLink(_currentAnalytics.PlayerStats, _currentAnalytics.StrongestLink, _currentAnalytics.MaxPossibleBank);
            
            // Определяем главного перестраховщика и паразита
            _currentAnalytics.PanicBanker = DeterminePanicBanker(_currentAnalytics.PlayerStats);
            _currentAnalytics.Parasite = DetermineParasite(_currentAnalytics.PlayerStats);
            
            // Прогноз выбывания — всегда совпадает со Слабым звеном (с учётом иммунитета)
            var (prediction, reason) = PredictElimination(_currentAnalytics.PlayerStats, _currentAnalytics.WeakestLink);
            _currentAnalytics.EliminationPrediction = prediction;
            _currentAnalytics.EliminationReason = reason;

            return _currentAnalytics;
        }

        private PlayerRoundStats GetPlayerRoundStats(string playerName, int roundDurationSeconds)
        {
            var stats = new PlayerRoundStats { Name = playerName };

            if (_engine.PlayerStatistics.TryGetValue(playerName, out var playerRounds) &&
                playerRounds.TryGetValue(_engine.CurrentRound, out var roundStats))
            {
                stats.CorrectAnswers = roundStats.CorrectAnswers;
                stats.IncorrectAnswers = roundStats.IncorrectAnswers;
                stats.Passes = roundStats.Passes;
                stats.BankedMoney = roundStats.BankedMoney;
                stats.BankPressCount = roundStats.BankPressCount; // Копируем количество банковских операций
                stats.TotalQuestions = stats.CorrectAnswers + stats.IncorrectAnswers + stats.Passes;
                stats.ChainBreaksLost = roundStats.BurnedByWrongAnswers;
                stats.ExactDroppedMoney = roundStats.ExactDroppedMoney;
                stats.AccuracyStandardDeviation = CalculateAccuracyStandardDeviation(playerName);
                
                // Отладочная информация (только для банковых операций)
                if (stats.BankPressCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[StatsAnalyzer] {playerName}: " +
                        $"Banked={stats.BankedMoney}, PressCount={stats.BankPressCount}, " +
                        $"AvgBank={stats.AverageBankAmount}");
                }
            }
            else
            {
                // Если статистики нет, выводим отладочную информацию
                System.Diagnostics.Debug.WriteLine($"[StatsAnalyzer] {playerName}: Нет статистики для раунда {_engine.CurrentRound}");
            }

            return stats;
        }

        private double CalculateAccuracyStandardDeviation(string playerName)
        {
            if (!_engine.PlayerStatistics.TryGetValue(playerName, out var rounds) || rounds.Count == 0)
                return 0;

            var percentages = new List<double>();
            foreach (var kv in rounds)
            {
                var r = kv.Value;
                int total = r.CorrectAnswers + r.IncorrectAnswers + r.Passes;
                if (total > 0)
                    percentages.Add((double)r.CorrectAnswers / total * 100);
            }
            if (percentages.Count < 2) return 0;

            double mean = percentages.Average();
            double sumSq = percentages.Sum(p => (p - mean) * (p - mean));
            return Math.Round(Math.Sqrt(sumSq / percentages.Count), 2);
        }

        private int GetTotalQuestionsAsked()
        {
            return _engine.PlayerStatistics.Values
                .Where(rounds => rounds.ContainsKey(_engine.CurrentRound))
                .Sum(rounds => rounds[_engine.CurrentRound].CorrectAnswers + 
                               rounds[_engine.CurrentRound].IncorrectAnswers + 
                               rounds[_engine.CurrentRound].Passes);
        }

        private string DetermineStrongestLink(IEnumerable<PlayerRoundStats> stats)
        {
            var list = stats.ToList();
            if (list.Count == 0) return "";

            // Баланс: каждый «чистый» верный ответ ≈ 10 000, банк добавляется напрямую.
            // Банк 50 000 ≈ 5 чистых ответов — справедливое соотношение.
            return list
                .OrderByDescending(p => (p.CorrectAnswers - p.TotalMistakes) * 10000.0 + p.BankedMoney)
                .ThenByDescending(p => p.CorrectAnswers)
                .ThenByDescending(p => p.BankedMoney)
                .First().Name;
        }

        private string DetermineWeakestLink(IEnumerable<PlayerRoundStats> stats, string strongestLinkName, int maxPossibleBank)
        {
            var list = stats.ToList();
            if (list.Count == 0) return "";

            var candidates = list.Count > 1
                ? list.Where(p => p.Name != strongestLinkName).ToList()
                : list;

            if (candidates.Count == 0) return list.First().Name;

            double threshold70 = maxPossibleBank > 0 ? 0.7 * maxPossibleBank : 0;

            return candidates
                .OrderByDescending(p => p.ExactDroppedMoney >= threshold70) // Сбросил > 70% — приоритетный кандидат
                .ThenByDescending(p => p.ExactDroppedMoney)
                .ThenByDescending(p => p.TotalMistakes)
                .ThenBy(p => p.BankedMoney)
                .ThenBy(p => p.CorrectAnswers)
                .First().Name;
        }

        private string DetermineParasite(IEnumerable<PlayerRoundStats> stats)
        {
            var parasites = stats
                .Where(p => p.ExactDroppedMoney > 0 && p.BankedMoney - p.ExactDroppedMoney < 0)
                .OrderByDescending(p => p.ExactDroppedMoney)
                .ThenBy(p => p.BankedMoney)
                .ToList();

            return parasites.Count > 0 ? parasites.First().Name : "";
        }

        private string DeterminePanicBanker(IEnumerable<PlayerRoundStats> stats)
        {
            var bankers = stats.Where(p => p.BankPressCount > 0).ToList();
            if (bankers.Count == 0) return "";

            return bankers
                .OrderBy(p => p.AverageBankAmount)
                .ThenByDescending(p => p.BankPressCount)
                .First().Name;
        }

        /// <summary>
        /// Определяет, есть ли ничья по голосам (2+ игрока с максимальным и равным числом голосов).
        /// </summary>
        public bool IsTieDetected(Dictionary<string, string> votes)
        {
            if (votes == null || votes.Count == 0) return false;

            var voteCounts = new Dictionary<string, int>();
            foreach (var target in votes.Values)
            {
                if (!voteCounts.ContainsKey(target))
                    voteCounts[target] = 0;
                voteCounts[target]++;
            }

            if (voteCounts.Count == 0) return false;

            int maxVotes = voteCounts.Values.Max();
            return voteCounts.Values.Count(v => v == maxVotes) >= 2;
        }

        /// <summary>
        /// Возвращает список имён игроков, попавших в ничью (максимальное и равное количество голосов).
        /// </summary>
        public List<string> GetTiedPlayerNames(Dictionary<string, string> votes)
        {
            if (votes == null || votes.Count == 0) return new List<string>();

            var voteCounts = new Dictionary<string, int>();
            foreach (var target in votes.Values)
            {
                if (!voteCounts.ContainsKey(target))
                    voteCounts[target] = 0;
                voteCounts[target]++;
            }

            if (voteCounts.Count == 0) return new List<string>();

            int maxVotes = voteCounts.Values.Max();
            return voteCounts.Where(kv => kv.Value == maxVotes).Select(kv => kv.Key).ToList();
        }

        /// <summary>
        /// Возвращает имя сильного звена текущего раунда.
        /// </summary>
        public string GetStrongestLinkName(int roundDurationSeconds)
        {
            var analytics = AnalyzeRound(roundDurationSeconds);
            return analytics.StrongestLink;
        }

        /// <summary>
        /// Возвращает отсортированный список игроков текущего раунда: от самого сильного к самому слабому.
        /// В качестве основы сортировки используется та же логика, что и у DetermineStrongestLink.
        /// </summary>
        public List<string> GetSortedPlayersByPerformanceDesc(int roundDurationSeconds)
        {
            var analytics = AnalyzeRound(roundDurationSeconds);
            if (analytics.PlayerStats == null || analytics.PlayerStats.Count == 0) return new List<string>();

            return analytics.PlayerStats
                .OrderByDescending(p => (p.CorrectAnswers - p.TotalMistakes) * 10000.0 + p.BankedMoney)
                .ThenByDescending(p => p.CorrectAnswers)
                .ThenByDescending(p => p.BankedMoney)
                .Select(p => p.Name)
                .ToList();
        }

        private (string prediction, string reason) PredictElimination(IEnumerable<PlayerRoundStats> stats, string weakestLinkName)
        {
            var list = stats.ToList();
            if (list.Count == 0) return ("", "");

            var weakestPlayer = list.FirstOrDefault(p => p.Name == weakestLinkName);

            if (weakestPlayer != null && weakestPlayer.CorrectAnswers > 0 && weakestPlayer.TotalMistakes == 0)
            {
                // Weakest link has immunity (perfect record). Find alternative.
                // Priority 1: players WITH mistakes (excluding the immune weakest link)
                var withMistakes = list
                    .Where(p => p.Name != weakestLinkName && p.TotalMistakes > 0)
                    .OrderByDescending(p => p.TotalMistakes)
                    .ThenBy(p => p.CorrectAnswers)
                    .ToList();

                if (withMistakes.Count > 0)
                {
                    var candidate = withMistakes.First();
                    return (candidate.Name,
                        $"Иммунитет {weakestLinkName} (без ошибок). У {candidate.Name} ошибок: {candidate.TotalMistakes}");
                }

                // Priority 2: ALL players played perfectly — pick the one with fewest correct answers
                var others = list.Where(p => p.Name != weakestLinkName).ToList();
                var pool = others.Count > 0 ? others : list;

                var leastActive = pool
                    .OrderBy(p => p.CorrectAnswers)
                    .ThenBy(p => p.BankedMoney)
                    .First();

                return (leastActive.Name,
                    $"Идеальный раунд! Все без ошибок. У {leastActive.Name} наименьше ответов: {leastActive.CorrectAnswers}");
            }

            return (weakestLinkName, "слабое звено");
        }
    }
}
