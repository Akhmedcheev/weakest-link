using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WeakestLink.Core.Models;

namespace WeakestLink.Network
{
    /// <summary>
    /// AI-игрок на базе Google Gemini 1.5 Flash для автоматического тестирования викторины.
    /// </summary>
    public class GeminiTestPlayer
    {
        private const string ApiUrl =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key=";

        private readonly string _apiKey = "AIzaSyDMT5_BiPtNaypEFdAY_nfo-QtHqZJyJrc";
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Callback для вывода диагностики в лог оператора. Подключи: _aiPlayer.LogCallback = msg => Log(msg);
        /// </summary>
        public Action<string>? LogCallback { get; set; }

        private const string SystemPrompt =
            "Ты находишься в студии жесткой телевизионной викторины. " +
            "У тебя нет подсказок, нет звонка другу и помощи зала. Ты рассчитываешь только на себя. " +
            "Тебе дадут текст вопроса и текущую ступень банка. " +
            "Твоя задача — вернуть строго валидный JSON. " +
            "Формат: {\"action\": \"answer\", \"text\": \"твой короткий ответ\"} " +
            "ИЛИ {\"action\": \"bank\", \"text\": \"\"} " +
            "ИЛИ {\"action\": \"pass\", \"text\": \"\"}. " +
            "Если текущая ступень высока и ты не уверен — возвращай \"bank\". " +
            "Если совершенно не знаешь ответ — возвращай \"pass\". " +
            "В остальных случаях давай максимально точный и краткий \"answer\".";

        public GeminiTestPlayer(string? apiKey = null, HttpClient? httpClient = null)
        {
            if (!string.IsNullOrWhiteSpace(apiKey)) _apiKey = apiKey;
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        }

        /// <summary>
        /// Отправляет вопрос и состояние цепи в Gemini, возвращает решение бота.
        /// При любом сбое — возвращает безопасный pass.
        /// </summary>
        public async Task<BotDecision> MakeMoveAsync(string questionText, int currentChainStep)
        {
            try
            {
                string userMessage =
                    $"Вопрос: {questionText}\nТекущая ступень цепи: {currentChainStep}";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = SystemPrompt + "\n\n" + userMessage }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.4,
                        maxOutputTokens = 256
                    }
                };

                string requestJson = JsonSerializer.Serialize(requestBody);
                DiagLog($"[GEMINI] Отправка запроса: {questionText.Substring(0, Math.Min(60, questionText.Length))}...");

                using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(ApiUrl + _apiKey, content);

                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    DiagLog($"[GEMINI] HTTP {(int)response.StatusCode}: {responseBody.Substring(0, Math.Min(300, responseBody.Length))}");
                    return new BotDecision { Action = "pass", Text = $"HTTP {(int)response.StatusCode} - {responseBody}" };
                }

                using var doc = JsonDocument.Parse(responseBody);

                string? generatedText = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                DiagLog($"[GEMINI] Raw ответ: {generatedText}");

                if (string.IsNullOrWhiteSpace(generatedText))
                {
                    DiagLog("[GEMINI] Пустой ответ от модели");
                    return new BotDecision { Action = "pass", Text = "" };
                }

                generatedText = generatedText
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                var decision = JsonSerializer.Deserialize<BotDecision>(generatedText);
                if (decision == null || string.IsNullOrWhiteSpace(decision.Action))
                {
                    DiagLog($"[GEMINI] Не удалось десериализовать: {generatedText}");
                    return new BotDecision { Action = "pass", Text = "" };
                }

                return decision;
            }
            catch (TaskCanceledException ex)
            {
                DiagLog($"[GEMINI] ТАЙМАУТ: {ex.Message}");
                return new BotDecision { Action = "pass", Text = $"ТАЙМАУТ: {ex.Message}" };
            }
            catch (HttpRequestException ex)
            {
                DiagLog($"[GEMINI] СЕТЬ: {ex.Message}");
                return new BotDecision { Action = "pass", Text = $"СЕТЬ: {ex.Message}" };
            }
            catch (JsonException ex)
            {
                DiagLog($"[GEMINI] JSON PARSE: {ex.Message}");
                return new BotDecision { Action = "pass", Text = $"JSON: {ex.Message}" };
            }
            catch (Exception ex)
            {
                DiagLog($"[GEMINI] ОШИБКА: {ex.GetType().Name}: {ex.Message}");
                return new BotDecision { Action = "pass", Text = $"КРИТ. ОШИБКА: {ex.Message}" };
            }
        }

        private void DiagLog(string message)
        {
            Debug.WriteLine(message);
            LogCallback?.Invoke(message);
        }
    }
}
