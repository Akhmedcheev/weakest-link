using System.Text.Json.Serialization;

namespace WeakestLink.Core.Models
{
    /// <summary>
    /// Результат решения AI-бота (Gemini) для текущего хода в игре.
    /// </summary>
    public class BotDecision
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = "pass";

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        public static BotDecision PassFallback => new() { Action = "pass", Text = "" };
    }
}
