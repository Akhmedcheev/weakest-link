using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WeakestLink.Core.Models;

namespace WeakestLink.Views
{
    public partial class QuestionEditorWindow : Window
    {
        private List<QuestionModel> _questions = new();
        private const string DefaultFilePath = "questions.json";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public QuestionEditorWindow()
        {
            InitializeComponent();
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            QuestionsGrid.ItemsSource = null;
            QuestionsGrid.ItemsSource = _questions;
            TxtStatus.Text = $"Вопросов: {_questions.Count}";
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(DefaultFilePath))
                {
                    MessageBox.Show($"Файл {DefaultFilePath} не найден.", "Ошибка");
                    return;
                }
                string json = File.ReadAllText(DefaultFilePath);
                var loaded = JsonSerializer.Deserialize<List<QuestionModel>>(json, JsonOptions);
                _questions = loaded ?? new List<QuestionModel>();
                RefreshGrid();
                MessageBox.Show($"Загружено {_questions.Count} вопросов.", "Готово");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка");
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string json = JsonSerializer.Serialize(_questions, JsonOptions);
                File.WriteAllText(DefaultFilePath, json);
                TxtStatus.Text = $"Сохранено {_questions.Count} вопросов в {DefaultFilePath}";
                MessageBox.Show($"Сохранено в {DefaultFilePath}", "Готово");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка");
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            int nextId = _questions.Count > 0 ? _questions.Max(q => q.Id) + 1 : 1;
            _questions.Add(new QuestionModel
            {
                Id = nextId,
                Text = "Новый вопрос",
                CorrectAnswer = "Ответ",
                AcceptableAnswers = "",
                Round = null
            });
            RefreshGrid();
            QuestionsGrid.SelectedIndex = _questions.Count - 1;
            TxtEditText.Text = "Новый вопрос";
            TxtEditAnswer.Text = "Ответ";
            TxtEditRound.Text = "";
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (QuestionsGrid.SelectedItem is QuestionModel selected)
            {
                _questions.Remove(selected);
                RefreshGrid();
                ClearEditForm();
            }
            else
            {
                MessageBox.Show("Выберите вопрос для удаления.", "Подсказка");
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Очистить весь список вопросов?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _questions.Clear();
                RefreshGrid();
                ClearEditForm();
            }
        }

        private void QuestionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QuestionsGrid.SelectedItem is QuestionModel q)
            {
                TxtEditText.Text = q.Text;
                TxtEditAnswer.Text = q.CorrectAnswer;
                TxtEditRound.Text = q.Round?.ToString() ?? "";
            }
        }

        private void BtnApplyEdit_Click(object sender, RoutedEventArgs e)
        {
            if (QuestionsGrid.SelectedItem is not QuestionModel selected) return;

            selected.Text = TxtEditText.Text.Trim();
            selected.CorrectAnswer = TxtEditAnswer.Text.Trim();
            selected.AcceptableAnswers = selected.CorrectAnswer;

            if (int.TryParse(TxtEditRound.Text?.Trim(), out int round) && round >= 0)
                selected.Round = round;
            else
                selected.Round = null;

            RefreshGrid();
            TxtStatus.Text = "Изменения применены (сохраните в JSON для записи в файл).";
        }

        private void ClearEditForm()
        {
            TxtEditText.Text = "";
            TxtEditAnswer.Text = "";
            TxtEditRound.Text = "";
        }
    }
}
