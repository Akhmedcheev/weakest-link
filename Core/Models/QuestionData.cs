using System.Text.Json.Serialization;

namespace WeakestLink.Core.Models
{
    /// <summary>
    /// Модель данных для игрового вопроса в стиле «Слабое звено».
    /// </summary>
    public class QuestionData
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("Text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("Answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonPropertyName("AcceptableAnswers")]
        public string AcceptableAnswers { get; set; } = string.Empty;

        [JsonPropertyName("Round")]
        public int? Round { get; set; }
    }
}
