using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WeakestLink.Core.Models;

namespace WeakestLink.Core.Services
{
    /// <summary>
    /// Сервис для загрузки и выдачи вопросов.
    /// </summary>
    public class QuestionProvider
    {
        private List<QuestionData> _allQuestions = new List<QuestionData>();
        private readonly HashSet<int> _usedQuestions = new HashSet<int>();
        private readonly Random _random = new Random();

        public int LoadedCount => _allQuestions.Count;
        public int RemainingCount => _allQuestions.Count - _usedQuestions.Count;

        public QuestionProvider()
        {
        }

        /// <summary>
        /// Поиск и загрузка вопросов из JSON файла.
        /// </summary>
        public void LoadQuestions(string filePath = "questions.json")
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    string fullPath = Path.GetFullPath(filePath);
                    throw new FileNotFoundException($"Файл вопросов не найден! Программа искала его здесь: {fullPath}", filePath);
                }

                string jsonContent = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var loaded = JsonSerializer.Deserialize<List<QuestionData>>(jsonContent, options);
                
                if (loaded != null)
                {
                    _allQuestions = loaded;
                }
            }
            catch (Exception ex)
            {
                // В реальном приложении здесь было бы логирование
                throw new Exception($"Ошибка при загрузке вопросов: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Получение случайного неиспользованного вопроса.
        /// </summary>
        public QuestionData? GetRandomQuestion()
        {
            var availableQuestions = _allQuestions
                .Where(q => !_usedQuestions.Contains(q.Id))
                .ToList();

            if (availableQuestions.Count == 0)
            {
                return null;
            }

            int index = _random.Next(availableQuestions.Count);
            var question = availableQuestions[index];
            
            _usedQuestions.Add(question.Id);
            return question;
        }

        /// <summary>
        /// Сброс списка использованных вопросов (начало новой сессии).
        /// </summary>
        public void ResetSession()
        {
            _usedQuestions.Clear();
        }

        /// <summary>
        /// Валидация загруженной базы вопросов. Возвращает список ошибок/предупреждений.
        /// </summary>
        public List<string> ValidateDatabase()
        {
            var errors = new List<string>();

            if (_allQuestions.Count == 0)
            {
                errors.Add("База вопросов пуста! Загрузите файл questions.json.");
                return errors;
            }

            foreach (var q in _allQuestions)
            {
                if (string.IsNullOrWhiteSpace(q.Text) || string.IsNullOrWhiteSpace(q.Answer))
                    errors.Add($"Вопрос ID {q.Id}: отсутствует текст или ответ");

                if (q.Round.HasValue && (q.Round.Value < 1 || q.Round.Value > 8))
                    errors.Add($"Вопрос ID {q.Id}: некорректный раунд ({q.Round.Value}), допустимо 1–8");
            }

            for (int round = 1; round <= 7; round++)
            {
                int count = _allQuestions.Count(q => q.Round == round);
                if (count < 15)
                    errors.Add($"Раунд {round}: мало вопросов ({count} шт.), может не хватить для эфира");
            }

            int finalCount = _allQuestions.Count(q => q.Round == 8);
            if (finalCount < 10)
                errors.Add($"КРИТИЧЕСКАЯ: Финал (раунд 8): загружено {finalCount} вопросов, нужно минимум 10!");

            return errors;
        }
    }
}
