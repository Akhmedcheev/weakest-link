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

        private async void ShowToast(string message, bool isError = false)
        {
            TxtStatus.Text = message.ToUpper();
            TxtStatus.Foreground = isError 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xda, 0x37, 0x3c)) 
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0xff, 0x00));
            
            await System.Threading.Tasks.Task.Delay(3000);
            TxtStatus.Text = "IDLE";
            TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(DefaultFilePath))
                {
                    DarkMessageBox.Show($"Файл {DefaultFilePath} не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                string json = File.ReadAllText(DefaultFilePath);
                var loaded = JsonSerializer.Deserialize<List<QuestionModel>>(json, JsonOptions);
                _questions = loaded ?? new List<QuestionModel>();
                RefreshGrid();
                ShowToast($"ЗАГРУЖЕНО {_questions.Count} ВОПРОСОВ");
            }
            catch (Exception ex)
            {
                DarkMessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string json = JsonSerializer.Serialize(_questions, JsonOptions);
                File.WriteAllText(DefaultFilePath, json);
                ShowToast("ФАЙЛ СОХРАНЕН УСПЕШНО");
            }
            catch (Exception ex)
            {
                DarkMessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
            ShowToast("ВОПРОС ДОБАВЛЕН");
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (QuestionsGrid.SelectedItem is QuestionModel selected)
            {
                if (DarkMessageBox.Show($"Удалить вопрос ID {selected.Id}?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _questions.Remove(selected);
                    RefreshGrid();
                    ClearEditForm();
                    ShowToast("ВОПРОС УДАЛЕН");
                }
            }
            else
            {
                ShowToast("ВЫБЕРИТЕ ВОПРОС", true);
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (DarkMessageBox.Show("Очистить весь список вопросов?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _questions.Clear();
                RefreshGrid();
                ClearEditForm();
                ShowToast("СПИСОК ОЧИЩЕН");
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
            if (QuestionsGrid.SelectedItem is not QuestionModel selected) 
            {
                ShowToast("ВЫБЕРИТЕ ВОПРОС ИЗ ТАБЛИЦЫ", true);
                return;
            }

            selected.Text = TxtEditText.Text.Trim();
            selected.CorrectAnswer = TxtEditAnswer.Text.Trim();
            selected.AcceptableAnswers = selected.CorrectAnswer;

            if (int.TryParse(TxtEditRound.Text?.Trim(), out int round) && round >= 0)
                selected.Round = round;
            else
                selected.Round = null;

            RefreshGrid();
            ShowToast("ИЗМЕНЕНИЯ ПРИМЕНЕНЫ");
        }

        private void ClearEditForm()
        {
            TxtEditText.Text = "";
            TxtEditAnswer.Text = "";
            TxtEditRound.Text = "";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
