# Денежная цепочка - Техническая документация

## Обзор решения

Реализована полная механика телевизионной игры "Денежная цепочка" с двумя основными компонентами:

### 1. ТАЙМЕР (Справа внизу)
### 2. ДЕНЕЖНАЯ ЦЕПОЧКА (Слева)

---

## ЗАДАЧА 1: ФИК ТАЙМЕРА

### Структура XAML

```xml
<Grid Width="350" Height="120" Name="TimerContainer">
    <!-- Фоновое изображение -->
    <Image Source="/Assets/TIMER.png" 
           Stretch="Uniform" 
           VerticalAlignment="Center"
           HorizontalAlignment="Center"/>

    <!-- Текст таймера в Viewbox для автоматического масштабирования -->
    <Viewbox Margin="20">
        <TextBlock Name="TimerText" 
                   Text="0:30" 
                   Foreground="White" 
                   FontSize="72" 
                   FontWeight="Bold"
                   FontFamily="Courier New"
                   TextAlignment="Center"
                   VerticalAlignment="Center"/>
    </Viewbox>
</Grid>
```

### Ключевые моменты:

1. **Жесткие размеры контейнера**: `Width="350" Height="120"`
   - Обеспечивает фиксированный размер таймера
   - Предотвращает вылезание за границы

2. **Stretch="Uniform"** для фонового изображения:
   - Масштабирует изображение пропорционально
   - Не растягивает, а масштабирует

3. **Viewbox для текста**:
   - Автоматически масштабирует текст внутри
   - Адаптируется к размерам контейнера
   - Никогда не вылезает за границы

### Код C# для таймера:

```csharp
private int secondsRemaining = 30;

public int SecondsRemaining
{
    get => secondsRemaining;
    set
    {
        secondsRemaining = value;
        UpdateTimerDisplay();
    }
}

private void UpdateTimerDisplay()
{
    int minutes = secondsRemaining / 60;
    int seconds = secondsRemaining % 60;
    TimerText.Text = $"{minutes}:{seconds:D2}";
}

private void UpdateTimer(object state)
{
    Dispatcher.Invoke(() =>
    {
        if (SecondsRemaining > 0)
        {
            SecondsRemaining--;
        }
        else
        {
            SecondsRemaining = 30; // Перезагружаем таймер
        }
    });
}
```

---

## ЗАДАЧА 2: ДЕНЕЖНАЯ ЦЕПОЧКА

### Архитектура

Используется **Canvas вместо StackPanel** для полного контроля над координатами каждой плашки.

```xml
<Grid Grid.Column="0" Margin="20">
    <Canvas Name="ChainCanvas" Width="350" Background="Transparent"/>
</Grid>
```

### Математика координат

#### Константы:

```csharp
private const double OverlapOffset = 25.0;      // Нахлест плашек в стопке
private const double StandardSpacing = 50.0;    // Шаг между несыгранными плашками
private const int AnimationDurationMs = 500;    // Длительность анимации
```

#### Логика позиционирования для `UpdateChainLayout(int newCurrentIndex)`:

```
Индекс 0 (БАНК):
    Canvas.SetBottom(item, 0)
    Всегда в самом низу
    Цвет: moneytree_blue.png

Пройденные уровни (0 < index < currentIndex):
    Canvas.SetBottom(item, 0 + (index * OverlapOffset))
    ПАДАЮТ в стопку на Банк (нахлест 25 пикселей)
    Цвет: moneytree_blue.png
    Пример: 
        - index=1: Bottom = 0 + (1 * 25) = 25
        - index=2: Bottom = 0 + (2 * 25) = 50
        - index=3: Bottom = 0 + (3 * 25) = 75

Текущий уровень (index == currentIndex):
    Canvas.SetBottom(item, 0 + (index * StandardSpacing))
    На исходной высоте
    Цвет: moneytree_red.png
    Пример:
        - Если currentIndex=3: Bottom = 0 + (3 * 50) = 150

Будущие уровни (index > currentIndex):
    Canvas.SetBottom(item, 0 + (index * StandardSpacing))
    На исходной высоте
    Цвет: moneytree_blue.png
    Пример:
        - index=4: Bottom = 0 + (4 * 50) = 200
        - index=5: Bottom = 0 + (5 * 50) = 250
```

### Визуальное представление

```
Уровень 0 (БАНК) - index=0
┌─────────────────┐
│     БАНК        │  Bottom=0, Blue
└─────────────────┘

Уровень 1 - index=1
┌─────────────────┐
│      100        │  Bottom=25 (в стопке), Blue
└─────────────────┘

Уровень 2 - index=2
┌─────────────────┐
│      500        │  Bottom=50 (в стопке), Blue
└─────────────────┘

Уровень 3 (ТЕКУЩИЙ) - index=3
                      ┌─────────────────┐
                      │     1000        │  Bottom=150, RED
                      └─────────────────┘

Уровень 4 (БУДУЩИЙ) - index=4
                                        ┌─────────────────┐
                                        │     2000        │  Bottom=200, Blue
                                        └─────────────────┘

Уровень 5 (БУДУЩИЙ) - index=5
                                                          ┌─────────────────┐
                                                          │     5000        │  Bottom=250, Blue
                                                          └─────────────────┘
```

### Реализация метода UpdateChainLayout()

```csharp
public void UpdateChainLayout(int newCurrentIndex)
{
    // Ограничиваем индекс допустимым диапазоном
    newCurrentIndex = Math.Max(0, Math.Min(newCurrentIndex, chainItems.Count - 1));
    currentLevelIndex = newCurrentIndex;

    foreach (ChainItem item in chainItems)
    {
        double newBottom = 0;
        int index = item.Index;

        // Расчет координаты Bottom
        if (index == 0)
        {
            newBottom = 0;  // БАНК: всегда в самом низу
        }
        else if (index < currentLevelIndex)
        {
            newBottom = 0 + (index * OverlapOffset);  // Пройденные: в стопке
        }
        else if (index == currentLevelIndex)
        {
            newBottom = 0 + (index * StandardSpacing);  // Текущий: на высоте
        }
        else
        {
            newBottom = 0 + (index * StandardSpacing);  // Будущие: на высоте
        }

        // Анимация движения
        UIElement element = ChainCanvas.FindName($"ChainItem_{index}") as UIElement;
        if (element != null)
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                From = Canvas.GetBottom(element),
                To = newBottom,
                Duration = new Duration(TimeSpan.FromMilliseconds(AnimationDurationMs)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            Storyboard storyboard = new Storyboard();
            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, new PropertyPath(Canvas.BottomProperty));
            storyboard.Children.Add(animation);

            storyboard.Begin();
        }

        // Обновление внешнего вида
        UpdateChainItemAppearance(item, index);

        // Z-Index для правильного наложения
        Panel.SetZIndex(element, index);
    }
}
```

### Управление Z-Index

```csharp
Panel.SetZIndex(item, index);
```

**Важно**: Верхние суммы (например, 5000) визуально лежат поверх нижних (2000) при нахлесте.

---

## ИСПОЛЬЗОВАНИЕ В КОДЕ

### Инициализация:

```csharp
BroadcastScreen screen = new BroadcastScreen();
// Цепочка инициализируется автоматически
```

### Управление уровнями:

```csharp
// Переместить на уровень 3
screen.SetLevel(3);

// Переместить на следующий уровень
screen.NextLevel();

// Переместить на предыдущий уровень
screen.PreviousLevel();

// Получить текущий уровень
int currentLevel = screen.GetCurrentLevel();
```

### Управление таймером:

```csharp
// Установить время (в секундах)
screen.SecondsRemaining = 45;

// Получить оставшееся время
int timeLeft = screen.SecondsRemaining;
```

---

## АНИМАЦИЯ

Используется `DoubleAnimation` с `QuadraticEase` для плавного движения плашек:

```csharp
DoubleAnimation animation = new DoubleAnimation
{
    From = Canvas.GetBottom(element),
    To = newBottom,
    Duration = new Duration(TimeSpan.FromMilliseconds(500)),
    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
};
```

**Результат**: Плашка плавно "падает" в стопку при переходе на следующий уровень.

---

## ТРЕБОВАНИЯ К АКТИВАМ

Убедитесь, что следующие файлы находятся в папке `Assets`:

```
/Assets/TIMER.png              - Фоновое изображение таймера
/Assets/moneytree_blue.png     - Плашка для несыгранных/пройденных уровней
/Assets/moneytree_red.png      - Плашка для текущего уровня
```

---

## ПРИМЕЧАНИЯ

1. **Четкость таймера**: Viewbox автоматически масштабирует текст без искажений
2. **Плавность анимации**: QuadraticEase обеспечивает естественное движение
3. **Правильное наложение**: Z-Index гарантирует корректный визуальный порядок
4. **Масштабируемость**: Легко добавить новые уровни в массив `amounts`

---

## КОНСТАНТЫ И ИХ НАЗНАЧЕНИЕ

| Константа | Значение | Назначение |
|-----------|----------|-----------|
| `OverlapOffset` | 25 | Толщина нахлеста плашек в стопке |
| `StandardSpacing` | 50 | Расстояние между плашками несыгранных уровней |
| `AnimationDurationMs` | 500 | Длительность анимации в миллисекундах |

Можно настроить эти значения для разных эффектов:
- Увеличить `OverlapOffset` → плашки будут более видны в стопке
- Уменьшить `StandardSpacing` → плашки будут ближе друг к другу
- Уменьшить `AnimationDurationMs` → анимация будет быстрее
