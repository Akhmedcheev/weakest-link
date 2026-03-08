using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WeakestLink.QuestionEditor.Models;

namespace WeakestLink.QuestionEditor
{
    public partial class MainWindow : Window
    {
        private bool _isLoaded = false;
        private List<QuestionModel> _questions = new();
        private List<QuestionModel> _allQuestions = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private string CurrentFilePath
        {
            get
            {
                var dir = AppDomain.CurrentDomain.BaseDirectory;
                return CmbFile?.SelectedIndex == 0
                    ? Path.Combine(dir, "questions.json")
                    : Path.Combine(dir, "final_questions.json");
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            _isLoaded = true;
            Loaded += (s, _) => LoadOnStartup();
        }

        private void LoadOnStartup()
        {
            var path = CurrentFilePath;
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    _allQuestions = JsonSerializer.Deserialize<List<QuestionModel>>(json, JsonOptions) ?? new List<QuestionModel>();
                    _questions = new List<QuestionModel>(_allQuestions);
                    RefreshGrid();
                    TxtStatus.Text = $"Загружено {_questions.Count} вопросов";
                }
                catch (Exception ex)
                {
                    TxtStatus.Text = $"Ошибка: {ex.Message}";
                }
            }
            else
            {
                _allQuestions.Clear();
                _questions.Clear();
                RefreshGrid();
                TxtStatus.Text = $"Файл {path} не найден";
            }
        }

        private void RefreshGrid()
        {
            QuestionsGrid.ItemsSource = null;
            QuestionsGrid.ItemsSource = _questions;
            TxtStatus.Text = $"Вопросов: {_questions.Count}";
        }

        private void CmbFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Если флаг ещё false, значит это авто-запуск при инициализации. Игнорируем!
            if (!_isLoaded) return;
            if (CmbFile == null) return;
            if (CmbFile.SelectedIndex < 0) return;
            if (MessageBox.Show("Переключить файл? Несохранённые изменения будут потеряны.", "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;
            try
            {
                LoadOnStartup();
            }
            catch (Exception ex)
            {
                File.WriteAllText("load_error.txt", ex.ToString());
            }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = CurrentFilePath;
                if (!File.Exists(path))
                {
                    MessageBox.Show($"Файл {path} не найден.", "Ошибка");
                    return;
                }
                var json = File.ReadAllText(path);
                _allQuestions = JsonSerializer.Deserialize<List<QuestionModel>>(json, JsonOptions) ?? new List<QuestionModel>();
                ApplySearchFilter();
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
                var path = CurrentFilePath;
                var json = JsonSerializer.Serialize(_allQuestions, JsonOptions);
                File.WriteAllText(path, json);
                TxtStatus.Text = $"Сохранено {_allQuestions.Count} вопросов в {path}";
                MessageBox.Show($"Сохранено в {path}", "Готово");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка");
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            int nextId = _allQuestions.Count > 0 ? _allQuestions.Max(q => q.Id) + 1 : 1;
            var q = new QuestionModel
            {
                Id = nextId,
                Text = "Новый вопрос",
                CorrectAnswer = "Ответ",
                AcceptableAnswers = "",
                Round = null
            };
            _allQuestions.Add(q);
            ApplySearchFilter();
            RefreshGrid();
            QuestionsGrid.SelectedItem = q;
            if (QuestionsGrid.SelectedIndex >= 0)
                QuestionsGrid.ScrollIntoView(QuestionsGrid.SelectedItem);
            TxtEditText.Text = "Новый вопрос";
            TxtEditAnswer.Text = "Ответ";
            TxtEditRound.Text = "";
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (QuestionsGrid.SelectedItem is QuestionModel selected)
            {
                _allQuestions.Remove(selected);
                ApplySearchFilter();
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
                _allQuestions.Clear();
                ApplySearchFilter();
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
            TxtStatus.Text = "Изменения применены (сохраните в файл).";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            var query = TxtSearch.Text?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(query))
            {
                _questions = new List<QuestionModel>(_allQuestions);
            }
            else
            {
                _questions = _allQuestions
                    .Where(q => (q.Text?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                             || (q.CorrectAnswer?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }
            RefreshGrid();
        }

        private void ClearEditForm()
        {
            TxtEditText.Text = "";
            TxtEditAnswer.Text = "";
            TxtEditRound.Text = "";
        }
    }
}
