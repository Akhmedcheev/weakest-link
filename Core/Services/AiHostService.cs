using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace WeakestLink.Core.Services
{
    /// <summary>
    /// AI-ведущая «Мария Киселёва» — экспериментальный модуль.
    /// Генерирует реплики через Gemini и озвучивает голосом ElevenLabs.
    /// Полностью автономный модуль — ничего не ломает в основной программе.
    /// </summary>
    public class AiHostService : IDisposable
    {
        // ── ElevenLabs ──
        private readonly string _elevenLabsApiKey;
        private readonly string _voiceId;
        private const string ElevenLabsBaseUrl = "https://api.elevenlabs.io/v1/text-to-speech";

        // ── Gemini ──
        private readonly string _geminiApiKey;
        private const string GeminiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=";

        // ── HTTP ──
        private readonly HttpClient _httpClient;

        // ── Очередь реплик (не перебиваем себя) ──
        private readonly ConcurrentQueue<string> _speechQueue = new();
        private readonly SemaphoreSlim _speechSemaphore = new(1, 1);
        private volatile bool _disposed;

        // ── Воспроизведение (отдельный канал, не мешает игровому аудио) ──
        private WaveOutEvent? _voiceDevice;
        private readonly object _voiceLock = new();

        // ── Логирование ──
        public Action<string>? LogCallback { get; set; }

        // ── Включена ли ведущая ──
        public bool IsEnabled { get; set; }

        private const string SystemPrompt =
            "Ты — Мария Киселёва, ведущая телешоу «Слабое Звено» на российском телевидении. " +
            "Твой стиль: ироничный, жёсткий, холодный, иногда саркастичный, но справедливый. " +
            "Ты обращаешься к участникам на «вы». " +
            "Говори КОРОТКО — максимум 1-2 предложения. Никакого markdown, никаких ремарок в скобках. " +
            "Просто реплика ведущей, как будто говоришь вслух в студии.\n\n" +
            "ОБРАЗЦЫ ТВОИХ ФРАЗ ИЗ ШОУ (используй как эталон стиля):\n" +
            "— «Они не знают друг друга, но, если они хотят забрать главный приз, они должны стать командой.»\n" +
            "— «Семеро из вас уйдут отсюда ни с чем, ведь раунд за раундом мы будем терять игроков.»\n" +
            "— «Самый быстрый способ отправить деньги в банк — выстроить цепь из восьми правильных ответов.»\n" +
            "— «Если вы ответите неверно, цепь разорвётся, и деньги, накопленные в этой цепочке, сгорят.»\n" +
            "— «Время вышло. Я не успеваю закончить вопрос.»\n" +
            "— «В этом раунде вы заработали {сумма}, хотя могли заработать {максимум}.»\n" +
            "— «Один из вас должен уйти ни с чем. Пришло время определить самое слабое звено!»\n" +
            "— «{Имя}, вы — самое слабое звено. Прощайте.»\n" +
            "— «Кто тянет команду ко дну?»\n" +
            "— «Кто ломает цепочку?»\n" +
            "— «Правильный ответ — {ответ}.»\n" +
            "— «Это решение далось вам нелегко.»\n" +
            "— «Если вы сейчас ответите неверно, вы проиграете.»\n" +
            "— «Сегодня вы — самое сильное звено!»\n" +
            "— «Вы смотрели программу Слабое звено. Это была всего лишь игра. До встречи.»\n\n" +
            "Отвечай ТОЛЬКО репликой Марии, ничего больше. Не повторяй дословно образцы — " +
            "вдохновляйся ими и генерируй уникальные фразы в том же стиле.";

        /// <summary>
        /// Создаёт AI-ведущую.
        /// </summary>
        public AiHostService(
            string elevenLabsApiKey,
            string voiceId,
            string geminiApiKey)
        {
            _elevenLabsApiKey = elevenLabsApiKey;
            _voiceId = voiceId;
            _geminiApiKey = geminiApiKey;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        #region ── Игровые события (вызывать из OperatorPanel) ──

        /// <summary>Интро шоу.</summary>
        public async Task OnGameIntroAsync(string[] playerNames, int maxPrize)
        {
            if (!IsEnabled) return;
            string intro =
                $"В эфире игра Слабое звено. Каждый из {playerNames.Length} участников, находящихся сегодня в студии, " +
                $"сможет заработать до {maxPrize / 1000} тысяч рублей. " +
                "Они не знают друг друга, но, если они хотят забрать главный приз, они должны стать командой. " +
                $"{playerNames.Length - 1} из вас уйдут отсюда ни с чем, ведь раунд за раундом мы будем терять игроков. " +
                "Тех, кого команда назовёт, слабым звеном.";
            await SpeakDirectAsync(intro);
        }

        /// <summary>Правила игры.</summary>
        public async Task OnRulesAsync()
        {
            if (!IsEnabled) return;
            string rules =
                "Теперь правила нашей игры. В каждом раунде вы можете выиграть до определённой суммы рублей. " +
                "Время ограничено. Самый быстрый способ отправить деньги в банк — выстроить цепь из восьми правильных ответов. " +
                "Если вы ответите неверно, цепь разорвётся, и деньги, накопленные в этой цепочке, сгорят. " +
                "Но! Если вы успеете сказать слово «Банк» до того, как прозвучит вопрос, вы сохраните деньги, " +
                "однако цепь начнёте строить заново. Помните: вашим выигрышем становятся только те деньги, " +
                "которые вы успели отправить в банк.";
            await SpeakDirectAsync(rules);
        }

        /// <summary>Начало раунда.</summary>
        public async Task OnRoundStartAsync(int roundNumber, int timeSeconds, string firstPlayerName, int playersLeft)
        {
            if (!IsEnabled) return;
            string text;
            if (roundNumber == 1)
            {
                text = $"На первый раунд у вас {FormatTime(timeSeconds)}. " +
                       $"И мы начнём с игрока, чьё имя первое по алфавиту. Это вы, {firstPlayerName}. " +
                       "Итак, играем в Слабое звено! Первый вопрос стоит 1 тысяча рублей. Время пошло.";
            }
            else
            {
                text = $"Вас осталось {playersLeft}, и этот раунд будет короче предыдущего на 10 секунд. " +
                       $"Мы начнём с самого сильного звена прошлого раунда — это вы, {firstPlayerName}. " +
                       "Итак, играем в Слабое звено! Время пошло.";
            }
            await SpeakDirectAsync(text);
        }

        /// <summary>Верный ответ.</summary>
        public void OnCorrectAnswer(string playerName, string question, string answer, int chainStep)
        {
            // Молчим при верных ответах — не тормозим пулемётный темп игры.
            // Только при полной цепочке (8) — быстрый комментарий.
            if (!IsEnabled) return;
            if (chainStep >= 8)
                _ = SpeakDirectAsync("Полная цепочка! Отлично.");
        }

        /// <summary>Неверный ответ — прямой шаблон без Gemini для скорости. Возвращает Task, завершающийся когда реплика доиграна.</summary>
        public async Task OnWrongAnswerAsync(string playerName, string question, string correctAnswer)
        {
            if (!IsEnabled) return;
            // Прямо в ElevenLabs, без Gemini = быстро
            await SpeakDirectAsync($"Нет. Правильный ответ — {correctAnswer}.");
        }

        /// <summary>Банк.</summary>
        public void OnBank(string playerName, int bankAmount)
        {
            if (!IsEnabled) return;
            // Просто «Банк» без AI — быстро
            _ = SpeakDirectAsync("Банк.");
        }

        /// <summary>Время вышло.</summary>
        public async Task OnTimeUpAsync()
        {
            if (!IsEnabled) return;
            await SpeakDirectAsync("Время вышло, я не успеваю закончить вопрос.");
        }

        /// <summary>Полный банк досрочно.</summary>
        public async Task OnFullBankAsync(int totalBank)
        {
            if (!IsEnabled) return;
            string sum = totalBank >= 1000 ? $"{totalBank / 1000} тысяч рублей" : $"{totalBank} рублей";
            await SpeakDirectAsync($"Браво, вы смогли заработать {sum}. Но нам всё равно нужно определить Слабое Звено.");
        }

        /// <summary>Итоги раунда.</summary>
        public async Task OnRoundResultAsync(int roundBank, int maxPossible, int totalBank)
        {
            if (!IsEnabled) return;
            string text = $"В этом раунде вы заработали {roundBank:N0} рублей, " +
                          $"хотя могли заработать {maxPossible:N0}. " +
                          $"И у вас в банке {totalBank:N0} рублей.";
            await SpeakDirectAsync(text);
        }

        /// <summary>Голосование.</summary>
        public async Task OnVotingStartAsync(int roundNumber)
        {
            if (!IsEnabled) return;
            await SpeakDirectAsync("Один из вас должен уйти ни с чем. Пришло время определить самое слабое звено!");
        }

        /// <summary>Исключение игрока.</summary>
        public async Task OnPlayerEliminatedAsync(string playerName)
        {
            if (!IsEnabled) return;
            await SpeakDirectAsync($"{playerName}, вы самое слабое звено. Прощайте.");
        }

        /// <summary>Задаёт вопрос вслух (чтение вопроса).</summary>
        public async Task ReadQuestionAsync(string playerName, string questionText)
        {
            if (!IsEnabled) return;
            await SpeakDirectAsync($"{playerName}, {questionText}");
        }

        #endregion

        #region ── Утилиты форматирования ──

        private static string FormatTime(int totalSeconds)
        {
            int min = totalSeconds / 60;
            int sec = totalSeconds % 60;
            if (min > 0 && sec > 0)
                return $"{min} {(min == 1 ? "минута" : min < 5 ? "минуты" : "минут")} {sec} секунд";
            if (min > 0)
                return $"{min} {(min == 1 ? "минута" : min < 5 ? "минуты" : "минут")}";
            return $"{sec} секунд";
        }

        #endregion

        #region ── Движок: Gemini → ElevenLabs → NAudio ──

        private void EnqueueSpeech(string geminiContext)
        {
            _speechQueue.Enqueue(geminiContext);
            _ = ProcessQueueAsync();
        }

        private async Task ProcessQueueAsync()
        {
            if (!await _speechSemaphore.WaitAsync(0))
            {
                Log("🎙️ [DEBUG] Очередь уже обрабатывается, пропуск");
                return;
            }
            try
            {
                while (_speechQueue.TryDequeue(out string? context) && !_disposed)
                {
                    try
                    {
                        Log($"🎙️ [1/3] Gemini: генерация реплики...");
                        string line = await GenerateLineAsync(context);
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            Log("🎙️ [1/3] Gemini вернул пустую строку!");
                            continue;
                        }
                        Log($"🎙️ [1/3] Gemini OK: {line}");

                        Log($"🎙️ [2/3] ElevenLabs: синтез речи...");
                        byte[]? audio = await SynthesizeSpeechAsync(line);
                        if (audio == null || audio.Length == 0)
                        {
                            Log("🎙️ [2/3] ElevenLabs вернул пустой аудио!");
                            continue;
                        }
                        Log($"🎙️ [2/3] ElevenLabs OK: {audio.Length} байт");

                        Log($"🎙️ [3/3] NAudio: воспроизведение...");
                        await PlayAudioBytesAsync(audio);
                        Log($"🎙️ [3/3] NAudio: воспроизведение завершено");
                    }
                    catch (Exception ex)
                    {
                        Log($"⚠️ AI Host ошибка: {ex.Message}");
                    }
                }
            }
            finally
            {
                _speechSemaphore.Release();
            }
        }

        /// <summary>Прямое озвучивание текста (без Gemini). Обрывает предыдущую реплику.</summary>
        private async Task SpeakDirectAsync(string text)
        {
            if (_disposed || string.IsNullOrWhiteSpace(text)) return;

            // Обрываем всё что играет сейчас — новое действие важнее
            StopSpeaking();

            // Не блокируемся на семафоре — если занят, пропускаем
            if (!await _speechSemaphore.WaitAsync(100)) return;
            try
            {
                Log($"🎙️ Мария: {text}");
                byte[]? audio = await SynthesizeSpeechAsync(text);
                if (audio != null && audio.Length > 0)
                {
                    Log($"🎙️ [DIRECT] Аудио: {audio.Length} байт");
                    await PlayAudioBytesAsync(audio);
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️ AI Host direct speak error: {ex.Message}");
            }
            finally
            {
                _speechSemaphore.Release();
            }
        }

        /// <summary>Генерирует реплику через Gemini.</summary>
        private async Task<string> GenerateLineAsync(string context)
        {
            try
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = SystemPrompt + "\n\n" + context }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.8,
                        maxOutputTokens = 150
                    }
                };

                string json = JsonSerializer.Serialize(requestBody);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(GeminiUrl + _geminiApiKey, content);

                if (!response.IsSuccessStatusCode)
                {
                    string err = await response.Content.ReadAsStringAsync();
                    Log($"⚠️ Gemini HTTP {(int)response.StatusCode}: {err[..Math.Min(200, err.Length)]}");
                    return string.Empty;
                }

                string responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);

                string? text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text?.Trim().Trim('"') ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log($"⚠️ Gemini error: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>Синтезирует речь через ElevenLabs API.</summary>
        private async Task<byte[]?> SynthesizeSpeechAsync(string text)
        {
            try
            {
                string url = $"{ElevenLabsBaseUrl}/{_voiceId}";

                var requestBody = new
                {
                    text = text,
                    model_id = "eleven_turbo_v2_5",
                    voice_settings = new
                    {
                        stability = 0.80,
                        similarity_boost = 0.85,
                        style = 0.05,
                        use_speaker_boost = true
                    }
                };

                string json = JsonSerializer.Serialize(requestBody);
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("xi-api-key", _elevenLabsApiKey);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    string err = await response.Content.ReadAsStringAsync();
                    Log($"⚠️ ElevenLabs HTTP {(int)response.StatusCode}: {err[..Math.Min(200, err.Length)]}");
                    return null;
                }

                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                Log($"⚠️ ElevenLabs error: {ex.Message}");
                return null;
            }
        }

        /// <summary>Проигрывает аудио-байты через NAudio на отдельном канале.</summary>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private Task PlayAudioBytesAsync(byte[] audioData)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                var stream = new MemoryStream(audioData);
                var mp3Reader = new Mp3FileReader(stream);
                var device = new WaveOutEvent();

                lock (_voiceLock)
                {
                    // Останавливаем предыдущее
                    _voiceDevice?.Stop();
                    _voiceDevice?.Dispose();
                    _voiceDevice = device;
                }

                void OnStopped(object? sender, StoppedEventArgs e)
                {
                    device.PlaybackStopped -= OnStopped;
                    lock (_voiceLock)
                    {
                        if (_voiceDevice == device) _voiceDevice = null;
                    }
                    try { device.Dispose(); mp3Reader.Dispose(); stream.Dispose(); } catch { }
                    tcs.TrySetResult();
                }

                device.PlaybackStopped += OnStopped;
                device.Init(mp3Reader);
                device.Play();
            }
            catch (Exception ex)
            {
                Log($"⚠️ Playback error: {ex.Message}");
                tcs.TrySetResult();
            }
            return tcs.Task;
        }

        #endregion

        #region ── Утилиты ──

        /// <summary>Останавливает текущую реплику.</summary>
        public void StopSpeaking()
        {
            lock (_voiceLock)
            {
                _voiceDevice?.Stop();
                _voiceDevice?.Dispose();
                _voiceDevice = null;
            }
            // Очищаем очередь
            while (_speechQueue.TryDequeue(out _)) { }
        }

        private void Log(string message)
        {
            Debug.WriteLine(message);
            LogCallback?.Invoke(message);
        }

        public void Dispose()
        {
            _disposed = true;
            StopSpeaking();
            _httpClient.Dispose();
            _speechSemaphore.Dispose();
        }

        #endregion
    }
}
