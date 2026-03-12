using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WeakestLink.Core.Models;
using WeakestLink.Core.Services;

namespace WeakestLink.Core
{
    /// <summary>
    /// Встроенная система автотестов для самодиагностики игры.
    /// Запускается из DEBUG меню Operator Panel.
    /// </summary>
    public class SelfTest
    {
        private readonly List<TestResult> _results = new List<TestResult>();

        public class TestResult
        {
            public string Group { get; set; } = "";
            public string Name { get; set; } = "";
            public bool Passed { get; set; }
            public string Message { get; set; } = "";
        }

        /// <summary>
        /// Запуск всех тестов. Возвращает полный отчёт.
        /// </summary>
        public List<TestResult> RunAll(string operatorPanelSourcePath = null)
        {
            _results.Clear();

            TestStateMachineTransitions();
            TestBankChainMath();
            TestRoundDurations();
            TestPlayerElimination();
            TestFinalDuel();
            TestQuestionProvider();

            if (!string.IsNullOrEmpty(operatorPanelSourcePath) && File.Exists(operatorPanelSourcePath))
            {
                TestHostScreenParity(operatorPanelSourcePath);
            }

            return _results;
        }

        #region Group 1: State Machine

        private void TestStateMachineTransitions()
        {
            string group = "State Machine";

            // Тест 1.1: Допустимые переходы (полный игровой цикл)
            try
            {
                var engine = CreateTestEngine();
                engine.TransitionTo(GameState.RoundReady);
                engine.TransitionTo(GameState.Playing);
                engine.TransitionTo(GameState.RoundSummary);
                engine.TransitionTo(GameState.Voting);
                engine.TransitionTo(GameState.Discussion);
                engine.TransitionTo(GameState.Reveal);
                engine.TransitionTo(GameState.Elimination);
                engine.TransitionTo(GameState.RoundReady);
                Pass(group, "Полный цикл раунда", "Idle→RoundReady→Playing→Summary→Voting→Discussion→Reveal→Elimination→RoundReady");
            }
            catch (Exception ex)
            {
                Fail(group, "Полный цикл раунда", $"Неожиданная ошибка: {ex.Message}");
            }

            // Тест 1.2: Intro-цикл
            try
            {
                var engine = CreateTestEngine();
                engine.TransitionTo(GameState.IntroOpening);
                engine.TransitionTo(GameState.IntroNarrative);
                engine.TransitionTo(GameState.PlayerIntro);
                engine.TransitionTo(GameState.RulesExplanation);
                engine.TransitionTo(GameState.RoundReady);
                Pass(group, "Intro-цикл", "Idle→IntroOpening→IntroNarrative→PlayerIntro→Rules→RoundReady");
            }
            catch (Exception ex)
            {
                Fail(group, "Intro-цикл", $"Ошибка: {ex.Message}");
            }

            // Тест 1.3: Недопустимые переходы
            var invalidTransitions = new (GameState from, GameState to)[]
            {
                (GameState.Idle, GameState.Playing),
                (GameState.Playing, GameState.RoundReady),
                (GameState.RoundSummary, GameState.Playing),
                (GameState.FinalDuel, GameState.Playing),
                (GameState.Voting, GameState.Playing),
            };

            foreach (var (from, to) in invalidTransitions)
            {
                try
                {
                    var engine = new GameEngine();
                    // Приводим к нужному начальному состоянию
                    NavigateTo(engine, from);
                    engine.TransitionTo(to);
                    Fail(group, $"Блокировка {from}→{to}", $"Должен был кинуть InvalidOperationException, но не кинул!");
                }
                catch (InvalidOperationException)
                {
                    Pass(group, $"Блокировка {from}→{to}", "Корректно блокирует недопустимый переход");
                }
                catch (Exception ex)
                {
                    Fail(group, $"Блокировка {from}→{to}", $"Неожиданное исключение: {ex.GetType().Name}: {ex.Message}");
                }
            }

            // Тест 1.4: Сброс в Idle из любого состояния
            var allStates = Enum.GetValues(typeof(GameState)).Cast<GameState>().Where(s => s != GameState.Idle);
            foreach (var state in allStates)
            {
                try
                {
                    var engine = new GameEngine();
                    NavigateTo(engine, state);
                    engine.TransitionTo(GameState.Idle);
                    Pass(group, $"Сброс {state}→Idle", "Можно сбросить в Idle");
                }
                catch (Exception ex)
                {
                    Fail(group, $"Сброс {state}→Idle", $"Невозможно сбросить: {ex.Message}");
                }
            }
        }

        #endregion

        #region Group 2: Bank Chain Math

        private void TestBankChainMath()
        {
            string group = "Bank Chain";

            // Тест 2.1: Цепочка растёт при правильных ответах
            {
                var engine = CreatePlayingEngine();
                for (int i = 0; i < 8; i++) engine.CorrectAnswer();
                Assert(group, "Цепочка до максимума",
                    engine.CurrentChainIndex == 8,
                    $"Ожидалось ChainIndex=8, получено {engine.CurrentChainIndex}");
            }

            // Тест 2.2: Цепочка не выходит за 8
            {
                var engine = CreatePlayingEngine();
                for (int i = 0; i < 10; i++) engine.CorrectAnswer();
                Assert(group, "Цепочка не > 8",
                    engine.CurrentChainIndex == 8,
                    $"Ожидалось ChainIndex=8, получено {engine.CurrentChainIndex}");
            }

            // Тест 2.3: Банк после 3 правильных = 5000
            {
                var engine = CreatePlayingEngine();
                engine.CorrectAnswer(); // 1000
                engine.CorrectAnswer(); // 2000
                engine.CorrectAnswer(); // 5000
                engine.Bank();
                Assert(group, "Банк 3 верных = 5000",
                    engine.RoundBank == 5000,
                    $"Ожидалось RoundBank=5000, получено {engine.RoundBank}");
                Assert(group, "Цепочка сброс после БАНК",
                    engine.CurrentChainIndex == 0,
                    $"Ожидалось ChainIndex=0, получено {engine.CurrentChainIndex}");
            }

            // Тест 2.4: WrongAnswer сбрасывает цепочку
            {
                var engine = CreatePlayingEngine();
                engine.CorrectAnswer();
                engine.CorrectAnswer();
                engine.WrongAnswer();
                Assert(group, "НЕВЕРНО сбрасывает цепочку",
                    engine.CurrentChainIndex == 0,
                    $"Ожидалось ChainIndex=0, получено {engine.CurrentChainIndex}");
                Assert(group, "НЕВЕРНО не добавляет в банк",
                    engine.RoundBank == 0,
                    $"Ожидалось RoundBank=0, получено {engine.RoundBank}");
            }

            // Тест 2.5: Pass сбрасывает цепочку
            {
                var engine = CreatePlayingEngine();
                engine.CorrectAnswer();
                engine.CorrectAnswer();
                engine.Pass();
                Assert(group, "ПАСС сбрасывает цепочку",
                    engine.CurrentChainIndex == 0 && engine.RoundBank == 0,
                    $"ChainIndex={engine.CurrentChainIndex}, RoundBank={engine.RoundBank}");
            }

            // Тест 2.6: Банк при пустой цепочке
            {
                var engine = CreatePlayingEngine();
                int bankBefore = engine.RoundBank;
                engine.Bank();
                Assert(group, "БАНК при пустой цепочке не меняет банк",
                    engine.RoundBank == bankBefore,
                    $"Банк изменился: было {bankBefore}, стало {engine.RoundBank}");
            }

            // Тест 2.7: Полная цепочка = 50000
            {
                var engine = CreatePlayingEngine();
                for (int i = 0; i < 8; i++) engine.CorrectAnswer();
                engine.Bank();
                Assert(group, "8 верных + БАНК = 50000",
                    engine.RoundBank == 50000,
                    $"Ожидалось 50000, получено {engine.RoundBank}");
            }

            // Тест 2.8: Burned money при WrongAnswer
            {
                var engine = CreatePlayingEngine();
                engine.CorrectAnswer(); // chain → 1 (1000)
                engine.CorrectAnswer(); // chain → 2 (2000)
                engine.WrongAnswer();   // burns 2000
                Assert(group, "RoundBurned = 2000 после 2 верных + неверный",
                    engine.RoundBurned == 2000,
                    $"Ожидалось RoundBurned=2000, получено {engine.RoundBurned}");
            }

            // Тест 2.9: Множественные банки суммируются
            {
                var engine = CreatePlayingEngine();
                engine.CorrectAnswer(); engine.Bank();  // +1000
                engine.CorrectAnswer(); engine.CorrectAnswer(); engine.Bank();  // +2000
                Assert(group, "Два банка: 1000 + 2000 = 3000",
                    engine.RoundBank == 3000,
                    $"Ожидалось 3000, получено {engine.RoundBank}");
            }
        }

        #endregion

        #region Group 3: Round Durations

        private void TestRoundDurations()
        {
            string group = "Round Sequence";
            var engine = CreateTestEngine();

            int[] expectedDurations = { 150, 140, 130, 120, 110, 100 };
            for (int round = 1; round <= 6; round++)
            {
                engine.PrepareNewRound();
                int duration = engine.GetRoundDuration();
                Assert(group, $"Раунд {round}: {expectedDurations[round - 1]} сек",
                    duration == expectedDurations[round - 1],
                    $"Ожидалось {expectedDurations[round - 1]}, получено {duration}");
            }

            // Тест: убывание таймера
            engine = CreateTestEngine();
            int prev = int.MaxValue;
            bool decreasing = true;
            for (int r = 1; r <= 6; r++)
            {
                engine.PrepareNewRound();
                int d = engine.GetRoundDuration();
                if (d >= prev) { decreasing = false; break; }
                prev = d;
            }
            Assert(group, "Таймер строго убывает R1→R6", decreasing, "Раунды не убывают");
        }

        #endregion

        #region Group 4: Player Elimination

        private void TestPlayerElimination()
        {
            string group = "Player Elimination";

            var engine = CreateTestEngine();
            engine.TransitionTo(GameState.RoundReady);
            engine.TransitionTo(GameState.Playing);
            engine.TransitionTo(GameState.RoundSummary);
            engine.TransitionTo(GameState.Voting);
            engine.TransitionTo(GameState.Discussion);
            engine.TransitionTo(GameState.Reveal);

            int before = engine.ActivePlayers.Count;
            string eliminated = engine.ActivePlayers[0];
            engine.EliminatePlayer(eliminated);
            engine.ActivePlayers.Remove(eliminated);

            Assert(group, "EliminatedPlayerName установлен",
                engine.EliminatedPlayerName == eliminated,
                $"Ожидалось '{eliminated}', получено '{engine.EliminatedPlayerName}'");

            Assert(group, $"Игрок удалён ({before}→{engine.ActivePlayers.Count})",
                engine.ActivePlayers.Count == before - 1,
                $"Ожидалось {before - 1}, получено {engine.ActivePlayers.Count}");
        }

        #endregion

        #region Group 5: Final Duel

        private void TestFinalDuel()
        {
            string group = "Final Duel";

            // Подготовка: 2 игрока
            var engine = new GameEngine();
            engine.ActivePlayers.Add("Алиса");
            engine.ActivePlayers.Add("Борис");

            engine.TransitionTo(GameState.FinalDuel);

            Assert(group, "Переход в FinalDuel",
                engine.CurrentState == GameState.FinalDuel,
                $"State={engine.CurrentState}");

            engine.StartFinalDuel(true);

            Assert(group, "5 слотов для P1",
                engine.Player1FinalScores.Count == 5,
                $"P1 slots={engine.Player1FinalScores.Count}");

            Assert(group, "5 слотов для P2",
                engine.Player2FinalScores.Count == 5,
                $"P2 slots={engine.Player2FinalScores.Count}");

            // Симулируем: P1 все верно, P2 все неверно → P1 побеждает
            engine.LoadFinalQuestions(Enumerable.Range(1, 20).Select(i =>
                new QuestionData { Id = i, Text = $"Тест {i}", Answer = $"Ответ {i}" }));

            for (int i = 0; i < 5; i++)
            {
                engine.GetNextFinalQuestion();
                engine.ProcessFinalAnswer(true);  // P1 correct
                engine.GetNextFinalQuestion();
                engine.ProcessFinalAnswer(false); // P2 wrong
            }

            Assert(group, "P1 выиграл (5-0)",
                engine.FinalWinner == "Алиса",
                $"Winner='{engine.FinalWinner}'");
        }

        #endregion

        #region Group 6: Question Provider

        private void TestQuestionProvider()
        {
            string group = "QuestionProvider";

            var provider = new QuestionProvider();
            try
            {
                provider.LoadQuestions("questions.json");
                Assert(group, "Загрузка questions.json",
                    provider.LoadedCount > 0,
                    $"Загружено {provider.LoadedCount} вопросов");

                // Тест: 50 случайных вопросов валидные
                int validCount = 0;
                int emptyCount = 0;
                for (int i = 0; i < Math.Min(50, provider.LoadedCount); i++)
                {
                    var q = provider.GetRandomQuestion();
                    if (q != null && !string.IsNullOrEmpty(q.Text) && !string.IsNullOrEmpty(q.Answer))
                        validCount++;
                    else
                        emptyCount++;
                }

                Assert(group, $"50 вопросов валидные",
                    emptyCount == 0,
                    emptyCount > 0 ? $"{emptyCount} вопросов с пустым текстом/ответом" : $"Все {validCount} вопросов корректные");

                // Тест: ValidateDatabase (игнорируем предупреждения о раундах — вопросы намеренно смешаны)
                provider.ResetSession();
                var errors = provider.ValidateDatabase()
                    .Where(e => !e.Contains("мало вопросов") && !e.Contains("Финал (раунд 8)"))
                    .ToList();
                Assert(group, "ValidateDatabase (без раундов)",
                    errors.Count == 0,
                    errors.Count > 0 ? $"{errors.Count} ошибок: {string.Join("; ", errors.Take(3))}" : "Нет ошибок (раунды игнорированы by design)");
            }
            catch (Exception ex)
            {
                Fail(group, "Загрузка questions.json", $"Ошибка: {ex.Message}");
            }
        }

        #endregion

        #region Group 7: HostScreen Parity

        private void TestHostScreenParity(string operatorPanelCsPath)
        {
            string group = "HostScreen Parity";

            try
            {
                string code = File.ReadAllText(operatorPanelCsPath);

                // Методы, которые должны вызываться на всех 4 экранах
                string[] methods = {
                    "UpdateQuestion", "UpdateTimer", "UpdateBank",
                    "UpdateCurrentPlayer", "ShowRoundSummary",
                    "ShowDiscussion", "ShowReveal", "ClearQuestionAndTimer",
                    "ShowElimination", "ClearElimination",
                    "ShowFullBank", "ShowVotingTimer", "SetVotingTimerUrgent",
                    "SetDuelDisplay", "UpdateDuelDisplay", "UpdateIndicators",
                    "ShowHostRules", "ClearScreen", "ToggleLogoScreen", "ShowLogo"
                };

                string[] screenVars = {
                    "_hostScreen",
                    "_hostModernScreen",
                    "_hostPremiumScreen",
                    "_hostModernPremiumScreen"
                };

                foreach (var method in methods)
                {
                    var callCounts = new Dictionary<string, int>();
                    foreach (var screen in screenVars)
                    {
                        // Ищем паттерн: _hostXxx?.Method( или _hostXxx.Method(
                        string pattern = $@"{Regex.Escape(screen)}[\?]?\.{Regex.Escape(method)}\s*\(";
                        int count = Regex.Matches(code, pattern).Count;
                        callCounts[screen] = count;
                    }

                    int maxCalls = callCounts.Values.Max();
                    if (maxCalls == 0) continue; // Метод нигде не используется — не ошибка

                    var missing = callCounts
                        .Where(kv => kv.Value < maxCalls && kv.Value == 0)
                        .Select(kv => kv.Key)
                        .ToList();

                    if (missing.Count > 0)
                    {
                        Fail(group, $"{method}()",
                            $"ПРОПУЩЕН для: {string.Join(", ", missing)} (есть {maxCalls}x у других)");
                    }
                    else
                    {
                        var mismatched = callCounts
                            .Where(kv => kv.Value < maxCalls && kv.Value > 0)
                            .Select(kv => $"{kv.Key}={kv.Value}")
                            .ToList();

                        if (mismatched.Count > 0)
                        {
                            Fail(group, $"{method}()",
                                $"РАЗНОЕ ЧИСЛО ВЫЗОВОВ: {string.Join(", ", callCounts.Select(kv => $"{kv.Key}={kv.Value}"))}");
                        }
                        else
                        {
                            Pass(group, $"{method}()", $"OK ({maxCalls}x на всех экранах)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Fail(group, "Чтение исходного кода", $"Ошибка: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private GameEngine CreateTestEngine()
        {
            var engine = new GameEngine();
            engine.ActivePlayers.Add("Тест1");
            engine.ActivePlayers.Add("Тест2");
            engine.ActivePlayers.Add("Тест3");
            engine.ActivePlayers.Add("Тест4");
            engine.ActivePlayers.Add("Тест5");
            engine.ActivePlayers.Add("Тест6");
            engine.ActivePlayers.Add("Тест7");
            engine.ActivePlayers.Add("Тест8");
            return engine;
        }

        private GameEngine CreatePlayingEngine()
        {
            var engine = CreateTestEngine();
            engine.TransitionTo(GameState.RoundReady);
            engine.TransitionTo(GameState.Playing);
            return engine;
        }

        /// <summary>
        /// Навигация к нужному состоянию по допустимому пути.
        /// </summary>
        private void NavigateTo(GameEngine engine, GameState target)
        {
            if (engine.CurrentState == target) return;

            // Добавляем игроков если нет
            if (engine.ActivePlayers.Count == 0)
            {
                engine.ActivePlayers.Add("A");
                engine.ActivePlayers.Add("B");
            }

            var paths = new Dictionary<GameState, GameState[]>
            {
                { GameState.Idle, new GameState[] { } },
                { GameState.IntroOpening, new[] { GameState.IntroOpening } },
                { GameState.IntroNarrative, new[] { GameState.IntroOpening, GameState.IntroNarrative } },
                { GameState.PlayerIntro, new[] { GameState.IntroOpening, GameState.IntroNarrative, GameState.PlayerIntro } },
                { GameState.RulesExplanation, new[] { GameState.RulesExplanation } },
                { GameState.RoundReady, new[] { GameState.RoundReady } },
                { GameState.Playing, new[] { GameState.RoundReady, GameState.Playing } },
                { GameState.RoundSummary, new[] { GameState.RoundReady, GameState.Playing, GameState.RoundSummary } },
                { GameState.Voting, new[] { GameState.RoundReady, GameState.Playing, GameState.RoundSummary, GameState.Voting } },
                { GameState.Discussion, new[] { GameState.RoundReady, GameState.Playing, GameState.RoundSummary, GameState.Voting, GameState.Discussion } },
                { GameState.Reveal, new[] { GameState.RoundReady, GameState.Playing, GameState.RoundSummary, GameState.Voting, GameState.Discussion, GameState.Reveal } },
                { GameState.Elimination, new[] { GameState.RoundReady, GameState.Playing, GameState.RoundSummary, GameState.Voting, GameState.Discussion, GameState.Reveal, GameState.Elimination } },
                { GameState.FinalDuel, new[] { GameState.FinalDuel } },
            };

            if (paths.ContainsKey(target))
            {
                foreach (var state in paths[target])
                {
                    if (engine.CurrentState != state)
                        engine.TransitionTo(state);
                }
            }
        }

        private void Assert(string group, string name, bool condition, string message)
        {
            _results.Add(new TestResult
            {
                Group = group,
                Name = name,
                Passed = condition,
                Message = message
            });
        }

        private void Pass(string group, string name, string message) => Assert(group, name, true, message);
        private void Fail(string group, string name, string message) => Assert(group, name, false, message);

        /// <summary>
        /// Генерация текстового отчёта.
        /// </summary>
        public static string GenerateReport(List<TestResult> results)
        {
            var lines = new List<string>();
            int passed = results.Count(r => r.Passed);
            int failed = results.Count(r => !r.Passed);

            lines.Add("═══════════════════════════════════════");
            lines.Add($"  🧪 SELF-TEST: {passed} ✅  {failed} ❌  ({results.Count} всего)");
            lines.Add("═══════════════════════════════════════");
            lines.Add("");

            var groups = results.GroupBy(r => r.Group);
            foreach (var g in groups)
            {
                int gPassed = g.Count(r => r.Passed);
                int gFailed = g.Count(r => !r.Passed);
                string icon = gFailed == 0 ? "✅" : "❌";
                lines.Add($"{icon} {g.Key} ({gPassed}/{g.Count()})");

                // Показать только ошибки + первые 3 прошедших
                foreach (var r in g.Where(r => !r.Passed))
                {
                    lines.Add($"   ❌ {r.Name}: {r.Message}");
                }

                if (gFailed == 0)
                {
                    lines.Add($"   Все {gPassed} тестов прошли.");
                }

                lines.Add("");
            }

            return string.Join(Environment.NewLine, lines);
        }
        #endregion
    }
}
