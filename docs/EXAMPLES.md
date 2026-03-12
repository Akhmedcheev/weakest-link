# Примеры использования денежной цепочки

## ПРИМЕР 1: Базовое использование

```csharp
// Создание и инициализация
BroadcastScreen screen = new BroadcastScreen();

// Переместить на уровень 3
screen.SetLevel(3);

// Переместить на следующий уровень
screen.NextLevel();

// Получить текущий уровень
int currentLevel = screen.GetCurrentLevel();  // Вернет 4
```

---

## ПРИМЕР 2: Управление таймером

```csharp
// Установить время (в секундах)
screen.SecondsRemaining = 60;

// Программа будет отсчитывать: 59, 58, 57... до 0
// После 0 автоматически перезагружается на 30 секунд

// Проверить оставшееся время
int timeLeft = screen.SecondsRemaining;

// Установить 30 секунд
screen.SecondsRemaining = 30;
```

---

## ПРИМЕР 3: Логика игры - Полный сценарий

```csharp
public class GameController
{
    private BroadcastScreen broadcastScreen;
    private int currentQuestion = 0;

    public GameController(BroadcastScreen screen)
    {
        broadcastScreen = screen;
    }

    // Игрок ответил правильно
    public void PlayerAnsweredCorrectly()
    {
        currentQuestion++;
        broadcastScreen.SetLevel(currentQuestion);
        broadcastScreen.SecondsRemaining = 30;  // Новый раунд - новое время
    }

    // Игрок ответил неправильно
    public void PlayerAnsweredIncorrectly()
    {
        // Показываем, на каком уровне упал игрок
        // Система визуально покажет все пройденные уровни в стопке
        // А текущий уровень (где произошла ошибка) останется красным
    }

    // Перейти на следующий вопрос
    public void NextQuestion()
    {
        if (currentQuestion < 13)  // Максимум 13 уровней
        {
            broadcastScreen.NextLevel();
        }
    }
}
```

---

## ПРИМЕР 4: Детальное объяснение координат

### Визуализация процесса

```
НАЧАЛЬНОЕ СОСТОЯНИЕ (currentIndex = 0):
======================================

Уровень 0 (БАНК)
┌─────────────────────────┐
│         БАНК            │  Bottom = 0
└─────────────────────────┘
Уровень 1
                          ┌─────────────────────────┐
                          │        100$             │  Bottom = 50
                          └─────────────────────────┘
Уровень 2
                                                    ┌─────────────────────────┐
                                                    │        500$             │  Bottom = 100
                                                    └─────────────────────────┘
Уровень 3
                                                                              ┌─────────────────────────┐
                                                                              │       1000$             │  Bottom = 150
                                                                              └─────────────────────────┘


ПОСЛЕ ОТВЕТА НА ВОПРОС 1 (currentIndex = 1):
=============================================

Уровень 0 (БАНК)
┌─────────────────────────┐
│         БАНК            │  Bottom = 0 (всегда внизу)
└─────────────────────────┘
  ┌─────────────────────────┐
  │        100$ (BLUE)      │  Bottom = 25 (упал в стопку, нахлест)
  └─────────────────────────┘


Уровень 1 (ТЕКУЩИЙ - КРАСНЫЙ)
                          ┌─────────────────────────┐
                          │   1000$ (RED) ТЕКУЩИЙ   │  Bottom = 50
                          └─────────────────────────┘

Уровень 2
                                                    ┌─────────────────────────┐
                                                    │        500$             │  Bottom = 100
                                                    └─────────────────────────┘

Уровень 3
                                                                              ┌─────────────────────────┐
                                                                              │       2000$             │  Bottom = 150
                                                                              └─────────────────────────┘


ПОСЛЕ ОТВЕТА НА ВОПРОС 3 (currentIndex = 3):
=============================================

Уровень 0 (БАНК)
┌─────────────────────────┐
│         БАНК            │  Bottom = 0
└─────────────────────────┘
  ┌─────────────────────────┐
  │        100$ (BLUE)      │  Bottom = 25
  └─────────────────────────┘
    ┌─────────────────────────┐
    │        500$ (BLUE)      │  Bottom = 50
    └─────────────────────────┘
      ┌─────────────────────────┐
      │       1000$ (BLUE)      │  Bottom = 75
      └─────────────────────────┘


Уровень 3 (ТЕКУЩИЙ - КРАСНЫЙ)
                                                    ┌─────────────────────────┐
                                                    │   2000$ (RED) ТЕКУЩИЙ   │  Bottom = 150
                                                    └─────────────────────────┘

Уровень 4
                                                                              ┌─────────────────────────┐
                                                                              │       5000$             │  Bottom = 200
                                                                              └─────────────────────────┘
```

---

## ПРИМЕР 5: Расчет координат вручную

### Константы:
- `OverlapOffset = 25` (толщина нахлеста)
- `StandardSpacing = 50` (стандартный шаг)

### Сценарий: currentIndex = 3

| Индекс | Условие | Формула | Bottom | Цвет | Описание |
|--------|---------|---------|--------|------|-----------|
| 0 | index == 0 | 0 | 0 | Blue | Банк - всегда внизу |
| 1 | 0 < 1 < 3 | 0 + (1 × 25) | 25 | Blue | Пройден - в стопке |
| 2 | 0 < 2 < 3 | 0 + (2 × 25) | 50 | Blue | Пройден - в стопке |
| 3 | 3 == 3 | 0 + (3 × 50) | 150 | Red | Текущий уровень |
| 4 | 4 > 3 | 0 + (4 × 50) | 200 | Blue | Будущий уровень |
| 5 | 5 > 3 | 0 + (5 × 50) | 250 | Blue | Будущий уровень |
| 6 | 6 > 3 | 0 + (6 × 50) | 300 | Blue | Будущий уровень |

---

## ПРИМЕР 6: Изменение констант

### Вариант 1: Плотнее упакованная стопка

```csharp
private const double OverlapOffset = 15.0;  // Меньше - плотнее
private const double StandardSpacing = 40.0;  // Меньше - ближе друг к другу
```

**Результат**: Плашки в стопке более плотно прилегают друг к другу, занимают меньше места.

### Вариант 2: Разреженная стопка

```csharp
private const double OverlapOffset = 35.0;  // Больше - разреженнее
private const double StandardSpacing = 60.0;  // Больше - дальше друг от друга
```

**Результат**: Плашки видны лучше, но занимают больше места на экране.

### Вариант 3: Быстрая анимация

```csharp
private const int AnimationDurationMs = 200;  // Быстрее
```

**Результат**: Плашка очень быстро падает в стопку (200 мс вместо 500 мс).

---

## ПРИМЕР 7: Интеграция с UI контролами

### XAML для кнопок управления

```xml
<StackPanel Orientation="Vertical" Spacing="10">
    <Button Content="Следующий уровень" Click="NextLevel_Click"/>
    <Button Content="Предыдущий уровень" Click="PrevLevel_Click"/>
    
    <!-- Кнопки для прямого перехода -->
    <ItemsControl>
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <WrapPanel/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
    </ItemsControl>
</StackPanel>
```

### Code-Behind

```csharp
private void NextLevel_Click(object sender, RoutedEventArgs e)
{
    BroadcastScreenControl.NextLevel();
}

private void PrevLevel_Click(object sender, RoutedEventArgs e)
{
    BroadcastScreenControl.PreviousLevel();
}

private void JumpToLevel_Click(int level)
{
    BroadcastScreenControl.SetLevel(level);
}
```

---

## ПРИМЕР 8: Анимация в деталях

### Как работает DoubleAnimation

```csharp
DoubleAnimation animation = new DoubleAnimation
{
    From = 150.0,              // Начальная позиция (текущая)
    To = 75.0,                 // Конечная позиция (упасть в стопку)
    Duration = TimeSpan.FromMilliseconds(500),
    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
};
```

**Анимация изменяет свойство `Canvas.BottomProperty` плавно:**
- 0 мс: Bottom = 150 (текущий уровень)
- 125 мс: Bottom ≈ 110 (падает)
- 250 мс: Bottom ≈ 75 (падает еще)
- 375 мс: Bottom ≈ 80 (начинает замедляться)
- 500 мс: Bottom = 75 (упал в стопку)

**Типы Easing для эффектов:**

```csharp
// Линейное движение (скучно)
EasingFunction = new LinearEase()

// Медленный старт, быстрое окончание (естественно)
EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }

// Быстрый старт, медленное окончание (отскок)
EasingFunction = new BounceEase { EasingMode = EasingMode.EaseOut }

// Упругое движение
EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut }
```

---

## ПРИМЕР 9: Обработка ошибок

```csharp
public void SafeSetLevel(int levelIndex)
{
    try
    {
        // Валидация входных данных
        if (levelIndex < 0 || levelIndex >= MAX_LEVELS)
        {
            throw new ArgumentOutOfRangeException(
                nameof(levelIndex), 
                $"Уровень должен быть от 0 до {MAX_LEVELS - 1}"
            );
        }

        BroadcastScreenControl.SetLevel(levelIndex);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Ошибка при переходе на уровень: {ex.Message}");
    }
}
```

---

## ПРИМЕР 10: Последовательность действий в игре

```csharp
// 1. Загрузка экрана
BroadcastScreen screen = new BroadcastScreen();

// 2. Начало игры - стартуем с уровня 0 (БАНК)
screen.SetLevel(0);
screen.SecondsRemaining = 30;

// 3. Игрок отвечает на вопрос 1 - ПРАВИЛЬНО
// Переходим на уровень 1, таймер перезагружается
screen.SetLevel(1);
screen.SecondsRemaining = 30;

// 4. Игрок отвечает на вопрос 2 - ПРАВИЛЬНО
// Переходим на уровень 2
screen.SetLevel(2);
screen.SecondsRemaining = 30;

// 5. Игрок отвечает на вопрос 3 - НЕПРАВИЛЬНО
// Остаемся на уровне 2 или возвращаемся на уровень 2
// Система покажет стопку пройденных уровней (0, 1, 2)
// А уровень 2 остается красным - признак ошибки

// 6. Конец игры
screen.Dispose();  // Очистка таймера
```

---

## ОТЛАДКА

### Логирование координат

```csharp
private void LogChainState(int levelIndex)
{
    Console.WriteLine($"=== Состояние цепочки на уровне {levelIndex} ===");
    
    foreach (ChainItem item in chainItems)
    {
        double bottom = Canvas.GetBottom(ChainCanvas.FindName($"ChainItem_{item.Index}") as UIElement);
        Console.WriteLine($"Индекс {item.Index}: Bottom = {bottom}, Amount = {item.Amount}");
    }
}
```

### Проверка размеров

```csharp
private void VerifyContainerSizes()
{
    Console.WriteLine($"Canvas ширина: {ChainCanvas.Width}");
    Console.WriteLine($"Canvas высота: {ChainCanvas.Height}");
    Console.WriteLine($"Timer контейнер: {TimerContainer.Width}x{TimerContainer.Height}");
}
```

---

## СОВЕТЫ ДЛЯ ОПТИМИЗАЦИИ

1. **Кешируйте ссылки на элементы управления** вместо поиска по имени каждый раз
2. **Используйте Storyboard для группировки анимаций** если нужно анимировать несколько элементов одновременно
3. **Рассмотрите виртуализацию** если количество уровней значительно
4. **Оптимизируйте изображения** (используйте формат PNG с хорошей компрессией)
5. **Профилируйте производительность** на целевом оборудовании
