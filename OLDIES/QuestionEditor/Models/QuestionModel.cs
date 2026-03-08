using System.Text.Json.Serialization;

namespace WeakestLink.QuestionEditor.Models
{
    public class QuestionModel
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("Text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("Answer")]
        public string CorrectAnswer { get; set; } = string.Empty;

        [JsonPropertyName("AcceptableAnswers")]
        public string AcceptableAnswers { get; set; } = string.Empty;

        [JsonPropertyName("Round")]
        public int? Round { get; set; }
    }
}
