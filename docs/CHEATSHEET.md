# ⚡ ШПАРГАЛКА - Денежная цепочка

## 📍 ОДНА СТРАНИЦА - ВСЕ САМОЕ ВАЖНОЕ

---

## 🎯 ЗАДАЧА 1: ФИК ТАЙМЕРА

### XAML:
```xml
<Grid Width="350" Height="120">
    <Image Source="/Assets/TIMER.png" Stretch="Uniform"/>
    <Viewbox Margin="20">
        <TextBlock Name="TimerText" Text="0:30" FontSize="72"/>
    </Viewbox>
</Grid>
```

### C#:
```csharp
private int secondsRemaining = 30;

public int SecondsRemaining {
    get => secondsRemaining;
    set { secondsRemaining = value; UpdateTimerDisplay(); }
}

private void UpdateTimerDisplay() {
    int m = secondsRemaining / 60;
    int s = secondsRemaining % 60;
    TimerText.Text = $"{m}:{s:D2}";
}
```

---

## 🎯 ЗАДАЧА 2: ДЕНЕЖНАЯ ЦЕПОЧКА

### Canvas + UpdateChainLayout():

```csharp
private const double OverlapOffset = 25.0;      // Нахлест
private const double StandardSpacing = 50.0;    // Шаг

public void UpdateChainLayout(int newCurrentIndex) {
    currentLevelIndex = newCurrentIndex;
    
    foreach (ChainItem item in chainItems) {
        int index = item.Index;
        double newBottom = 0;
        
        // МАТЕМАТИКА ПОЗИЦИОНИРОВАНИЯ:
        if (index == 0) {
            newBottom = 0;  // БАНК: всегда внизу
        }
        else if (index < currentLevelIndex) {
            newBottom = index * OverlapOffset;  // Пройденные: в стопке
        }
        else {
            newBottom = index * StandardSpacing;  // Текущий и будущие
        }
        
        // АНИМАЦИЯ:
        DoubleAnimation anim = new DoubleAnimation {
            From = Canvas.GetBottom(element),
            To = newBottom,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard sb = new Storyboard();
        Storyboard.SetTarget(anim, element);
        Storyboard.SetTargetProperty(anim, new PropertyPath(Canvas.BottomProperty));
        sb.Children.Add(anim);
        sb.Begin();
        
        // ЦВЕТ:
        if (index == currentLevelIndex) {
            item.ImageControl.Source = new BitmapImage(new Uri("/Assets/moneytree_red.png", UriKind.Relative));
        } else {
            item.ImageControl.Source = new BitmapImage(new Uri("/Assets/moneytree_blue.png", UriKind.Relative));
        }
        
        // Z-INDEX:
        Panel.SetZIndex(element, index);
    }
}
```

---

## 📊 ТАБЛИЦА КООРДИНАТ

### Пример: currentIndex = 3

```
Index │ Условие      │ Bottom  │ Цвет │ Описание
──────┼──────────────┼─────────┼──────┼────────────────
  0   │ == 0         │ 0       │ Blue │ БАНК (всегда внизу)
  1   │ < 3 (пройден)│ 25      │ Blue │ В стопке (1 × 25)
  2   │ < 3 (пройден)│ 50      │ Blue │ В стопке (2 × 25)
  3   │ == 3 (текущ.)│ 150     │ Red  │ ТЕКУЩИЙ (3 × 50)
  4   │ > 3 (будущ.) │ 200     │ Blue │ Будущий (4 × 50)
  5   │ > 3 (будущ.) │ 250     │ Blue │ Будущий (5 × 50)
```

---

## 🎬 ГРАФИКА ПРОЦЕССА

```
Состояние 1: currentIndex = 0
═════════════════════════════════════════════════════
Level 0 (БАНК)
┌─────────────────┐
│     БАНК (0)    │ Bottom=0, Blue, Z=0
└─────────────────┘

Level 1
                    ┌─────────────────┐
                    │  100$ (1)       │ Bottom=50, Blue, Z=1
                    └─────────────────┘

Level 2
                                    ┌─────────────────┐
                                    │ 500$ (2)        │ Bottom=100, Blue, Z=2
                                    └─────────────────┘


Состояние 2: currentIndex = 1
═════════════════════════════════════════════════════
Level 0 (БАНК)
┌─────────────────┐
│     БАНК (0)    │ Bottom=0, Blue, Z=0
└─────────────────┘
  ┌─────────────────┐
  │ 100$ (1) RED    │ Bottom=50, Red, Z=1  ← ТЕКУЩИЙ
  └─────────────────┘

Level 2
                    ┌─────────────────┐
                    │ 500$ (2)        │ Bottom=100, Blue, Z=2
                    └─────────────────┘


Состояние 3: currentIndex = 2
═════════════════════════════════════════════════════
Level 0 (БАНК)
┌─────────────────┐
│     БАНК (0)    │ Bottom=0, Blue, Z=0
└─────────────────┘
  ┌─────────────────┐
  │ 100$ (1) BLUE   │ Bottom=25, Blue, Z=1  ← упал в стопку
  └─────────────────┘
    ┌─────────────────┐
    │ 500$ (2) RED    │ Bottom=100, Red, Z=2  ← ТЕКУЩИЙ
    └─────────────────┘

Level 3
                        ┌─────────────────┐
                        │ 1000$ (3)       │ Bottom=150, Blue, Z=3
                        └─────────────────┘
```

---

## 🎮 API (Публичные методы)

```csharp
screen.SetLevel(3);              // Перейти на уровень 3
screen.NextLevel();              // На следующий (текущий+1)
screen.PreviousLevel();          // На предыдущий (текущий-1)
screen.GetCurrentLevel();        // Получить текущий уровень
screen.SecondsRemaining = 45;    // Установить время
int time = screen.SecondsRemaining;  // Получить время
screen.Dispose();                // Остановить таймер
```

---

## 📁 ФАЙЛЫ (что куда копировать)

```
Ваш проект/
├─ Views/
│  ├─ BroadcastScreen.xaml       ← копируем
│  └─ BroadcastScreen.xaml.cs    ← копируем
├─ MainWindow.xaml               ← копируем
├─ MainWindow.xaml.cs            ← копируем
└─ Assets/
   ├─ TIMER.png                  ← добавить изображение
   ├─ moneytree_blue.png         ← добавить изображение
   └─ moneytree_red.png          ← добавить изображение
```

---

## ⚙️ КОНСТАНТЫ (настройка)

```csharp
private const double OverlapOffset = 25.0;    // ↓ меньше = плотнее в стопке
private const double StandardSpacing = 50.0;  // ↓ меньше = ближе друг к другу
private const int AnimationDurationMs = 500;  // ↓ меньше = быстрее падает
```

---

## 🐛 ЧАСТЫЕ ПРОБЛЕМЫ И РЕШЕНИЯ

| Проблема | Решение |
|----------|---------|
| Изображения не видны | Проверьте Build Action = Resource в свойствах файла |
| Таймер не обновляется | Используйте Dispatcher.Invoke() в Timer callback |
| Текст вылезает за границы | Viewbox автоматически масштабирует (уже реализовано) |
| Плашки не падают | Проверьте что OverlapOffset ≠ 0 |
| Неправильные цвета | Проверьте пути к изображениям в коде |
| Z-Index не работает | Убедитесь что SetZIndex вызывается для каждого элемента |

---

## 🔍 ОТЛАДКА В РЕАЛЬНОМ ВРЕМЕНИ

```csharp
// Вывести координаты всех плашек:
foreach (var item in chainItems) {
    var el = ChainCanvas.FindName($"ChainItem_{item.Index}") as UIElement;
    Console.WriteLine($"Item {item.Index}: Bottom={Canvas.GetBottom(el)}");
}

// Проверить текущий уровень:
Console.WriteLine($"Current level: {GetCurrentLevel()}");

// Проверить таймер:
Console.WriteLine($"Time: {SecondsRemaining} seconds");
```

---

## 📋 ЧЕКЛИСТ ИНТЕГРАЦИИ

- [ ] Скопированы XAML файлы
- [ ] Скопированы CS файлы  
- [ ] Добавлены изображения в Assets
- [ ] Build Action изображений = Resource
- [ ] Пути к изображениям правильные
- [ ] Протестирована смена уровней
- [ ] Таймер отсчитывает правильно
- [ ] Плашки падают в стопку плавно
- [ ] Цвета меняются правильно (Blue/Red)
- [ ] Z-Index корректен (верхние видны поверх нижних)

---

## 🚀 ПЕРВЫЙ ЗАПУСК

```csharp
// В MainWindow:
public partial class MainWindow : Window {
    public MainWindow() {
        InitializeComponent();
    }
}

// В XAML:
<local:BroadcastScreen Name="BroadcastScreenControl"/>

// При нажатии кнопки:
private void NextLevel_Click(object sender, RoutedEventArgs e) {
    BroadcastScreenControl.NextLevel();
}
```

---

## 📚 ДОКУМЕНТАЦИЯ

| Файл | Назначение |
|------|-----------|
| **QUICKSTART.md** | Быстрый старт за 5 минут ⚡ |
| **DOCUMENTATION.md** | Полная справка по каждому компоненту 📖 |
| **EXAMPLES.md** | 10 практических примеров кода 💻 |
| **ARCHITECTURE.md** | Архитектура и диаграммы 🏗️ |
| **README.md** | Обзор всего решения 📖 |
| **CHEATSHEET.md** | Эта шпаргалка ⚡ |

---

## 💡 ЗОЛОТЫЕ ПРАВИЛА

1. **Canvas > StackPanel** - Полный контроль над координатами
2. **DoubleAnimation + Storyboard** - Плавные анимации
3. **Panel.SetZIndex()** - Правильное наложение элементов
4. **Viewbox** - Автоматическое масштабирование текста
5. **Dispatcher.Invoke()** - Безопасное обновление UI из других потоков

---

## ✅ ГОТОВО!

Вся система полностью работающая и документированная.

**Начните с QUICKSTART.md** 🚀
