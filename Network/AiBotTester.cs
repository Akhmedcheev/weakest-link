using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WeakestLink.Core.Models;

namespace WeakestLink.Network
{
    /// <summary>
    /// Универсальный AI-бот для нагрузочного тестирования викторины.
    /// Работает с любым OpenAI-совместимым API (ChatGPT, DeepSeek, LM Studio, Ollama и т.д.).
    /// </summary>
    public class AiBotTester
    {
        private readonly string _apiUrl;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly HttpClient _httpClient;

        private const string SystemPrompt =
            "Ты играешь в жесткую телевизионную викторину. " +
            "У тебя нет ни звонка другу, ни помощи зала. Ты должен отвечать сам. " +
            "Твоя задача — вернуть строго валидный JSON без маркдауна. " +
            "Формат: {\"action\": \"answer\"|\"pass\"|\"bank\", \"text\": \"твой ответ\"}. " +
            "Если текущая ступень цепи высока и есть риск потери денег — возвращай \"bank\". " +
            "Если точно не знаешь ответ — возвращай \"pass\". " +
            "В остальных случаях возвращай \"answer\" и краткий текст ответа.";

        /// <param name="apiUrl">
        /// Базовый URL API без /chat/completions.
        /// Примеры: "https://api.openai.com/v1", "https://api.deepseek.com/v1", "http://localhost:1234/v1"
        /// </param>
        /// <param name="apiKey">Ключ авторизации (Bearer token).</param>
        /// <param name="model">Идентификатор модели: "gpt-4o-mini", "deepseek-chat", "local-model" и т.д.</param>
        public AiBotTester(string apiUrl, string apiKey, string model = "gpt-4o-mini", HttpClient? httpClient = null)
        {
            _apiUrl = apiUrl.TrimEnd('/');
            _apiKey = apiKey;
            _model = model;
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        }

        /// <summary>
        /// Запрашивает решение у LLM. При любом сбое возвращает pass, чтобы не крашить эфир.
        /// </summary>
        public async Task<BotDecision> GetDecisionAsync(string questionText, int currentChainStep, bool isFinalRound)
        {
            try
            {
                string roundContext = isFinalRound
                    ? "Это ФИНАЛЬНЫЙ РАУНД — дуэль один на один. Банк невозможен. Отвечай или пас."
                    : $"Текущая ступень цепи: {currentChainStep}";

                string userMessage = $"Вопрос: {questionText}\n{roundContext}";

                var request = new OpenAiRequest
                {
                    Model = _model,
                    Messages =
                    [
                        new ChatMessage { Role = "system", Content = SystemPrompt },
                        new ChatMessage { Role = "user", Content = userMessage }
                    ],
                    Temperature = 0.3,
                    MaxTokens = 150,
                    ResponseFormat = new ResponseFormat { Type = "json_object" }
                };

                string requestJson = JsonSerializer.Serialize(request, SerializerOptions);

                using var httpContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_apiUrl}/chat/completions");
                httpRequest.Content = httpContent;
                if (!string.IsNullOrEmpty(_apiKey))
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                using var httpResponse = await _httpClient.SendAsync(httpRequest);
                httpResponse.EnsureSuccessStatusCode();

                string responseJson = await httpResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);

                string? assistantText = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrWhiteSpace(assistantText))
                    return BotDecision.PassFallback;

                assistantText = assistantText.Trim();

                var decision = JsonSerializer.Deserialize<BotDecision>(assistantText);
                if (decision == null || string.IsNullOrWhiteSpace(decision.Action))
                    return BotDecision.PassFallback;

                if (isFinalRound && decision.Action.Equals("bank", StringComparison.OrdinalIgnoreCase))
                    return new BotDecision { Action = "pass", Text = "" };

                return decision;
            }
            catch (TaskCanceledException)
            {
                return BotDecision.PassFallback;
            }
            catch (HttpRequestException)
            {
                return BotDecision.PassFallback;
            }
            catch (JsonException)
            {
                return BotDecision.PassFallback;
            }
            catch (Exception)
            {
                return BotDecision.PassFallback;
            }
        }

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #region OpenAI API DTO

        private sealed class OpenAiRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; set; } = "";

            [JsonPropertyName("messages")]
            public ChatMessage[] Messages { get; set; } = [];

            [JsonPropertyName("temperature")]
            public double Temperature { get; set; }

            [JsonPropertyName("max_tokens")]
            public int MaxTokens { get; set; }

            [JsonPropertyName("response_format")]
            public ResponseFormat? ResponseFormat { get; set; }
        }

        private sealed class ChatMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = "";

            [JsonPropertyName("content")]
            public string Content { get; set; } = "";
        }

        private sealed class ResponseFormat
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = "json_object";
        }

        #endregion
    }
}
