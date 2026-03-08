using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WeakestLink.Core.Models;

namespace WeakestLink.Network
{
    /// <summary>
    /// HTTP-клиент для Google Gemini REST API.
    /// Используется для автоматического тестирования игры AI-ботом.
    /// </summary>
    public class GeminiBotClient
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly string _model;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private const string GeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

        private const string SystemPrompt =
            "Ты играешь в телевизионную викторину. Тебе дадут текст вопроса и текущее состояние банка. " +
            "Твоя задача — вернуть строго валидный JSON без маркдауна. " +
            "Формат: {\"action\": \"answer\"|\"pass\"|\"bank\", \"text\": \"твой ответ\"}. " +
            "Выбирай \"bank\", если текущая ступень цепи высока и есть риск потери денег. " +
            "Выбирай \"pass\", если не знаешь ответ. " +
            "Иначе возвращай \"answer\" и текст ответа.";

        public GeminiBotClient(string apiKey, string model = "gemini-2.0-flash", HttpClient? httpClient = null)
        {
            _apiKey = apiKey;
            _model = model;
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        /// <summary>
        /// Запрашивает решение у Gemini на основе текста вопроса и игрового состояния.
        /// При любой ошибке (сеть, невалидный JSON) возвращает action=pass.
        /// </summary>
        public async Task<BotDecision> GetDecisionAsync(string questionText, int currentBank, int currentChainStep)
        {
            try
            {
                string userPrompt =
                    $"Вопрос: {questionText}\n" +
                    $"В банке сейчас: {currentBank} руб.\n" +
                    $"Текущая ступень цепи: {currentChainStep}";

                var requestBody = new GeminiRequest
                {
                    SystemInstruction = new SystemInstruction
                    {
                        Parts = [new Part { Text = SystemPrompt }]
                    },
                    Contents =
                    [
                        new Content
                        {
                            Role = "user",
                            Parts = [new Part { Text = userPrompt }]
                        }
                    ],
                    GenerationConfig = new GenerationConfig
                    {
                        Temperature = 0.3,
                        MaxOutputTokens = 256,
                        ResponseMimeType = "application/json"
                    }
                };

                string json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                string url = $"{GeminiBaseUrl}/{_model}:generateContent?key={_apiKey}";

                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                string responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);

                string? candidateText = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrWhiteSpace(candidateText))
                    return BotDecision.PassFallback;

                var decision = JsonSerializer.Deserialize<BotDecision>(candidateText, _jsonOptions);
                return decision ?? BotDecision.PassFallback;
            }
            catch (Exception)
            {
                return BotDecision.PassFallback;
            }
        }

        #region Gemini API DTO

        private sealed class GeminiRequest
        {
            [JsonPropertyName("system_instruction")]
            public SystemInstruction? SystemInstruction { get; set; }

            [JsonPropertyName("contents")]
            public Content[] Contents { get; set; } = [];

            [JsonPropertyName("generationConfig")]
            public GenerationConfig? GenerationConfig { get; set; }
        }

        private sealed class SystemInstruction
        {
            [JsonPropertyName("parts")]
            public Part[] Parts { get; set; } = [];
        }

        private sealed class Content
        {
            [JsonPropertyName("role")]
            public string Role { get; set; } = "user";

            [JsonPropertyName("parts")]
            public Part[] Parts { get; set; } = [];
        }

        private sealed class Part
        {
            [JsonPropertyName("text")]
            public string Text { get; set; } = string.Empty;
        }

        private sealed class GenerationConfig
        {
            [JsonPropertyName("temperature")]
            public double Temperature { get; set; } = 0.3;

            [JsonPropertyName("maxOutputTokens")]
            public int MaxOutputTokens { get; set; } = 256;

            [JsonPropertyName("responseMimeType")]
            public string ResponseMimeType { get; set; } = "application/json";
        }

        #endregion
    }
}
