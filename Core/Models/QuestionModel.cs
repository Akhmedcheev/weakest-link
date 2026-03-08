using System.Text.Json.Serialization;

namespace WeakestLink.Core.Models
{
    /// <summary>
    /// Модель вопроса для редактора QEdit.
    /// JSON использует "Answer" для совместимости с questions.json.
    /// </summary>
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

        public static QuestionModel FromQuestionData(QuestionData q)
        {
            return new QuestionModel
            {
                Id = q.Id,
                Text = q.Text,
                CorrectAnswer = q.Answer,
                AcceptableAnswers = q.AcceptableAnswers,
                Round = q.Round
            };
        }

        public QuestionData ToQuestionData()
        {
            return new QuestionData
            {
                Id = Id,
                Text = Text,
                Answer = CorrectAnswer,
                AcceptableAnswers = AcceptableAnswers,
                Round = Round
            };
        }
    }
}
