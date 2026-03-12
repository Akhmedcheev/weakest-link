# Архитектура системы "Денежная цепочка"

## ОБЩАЯ СТРУКТУРА ПРИЛОЖЕНИЯ

```
┌─────────────────────────────────────────────────────────────┐
│                      MainWindow                              │
│  (Главное окно приложения с панелью управления)             │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       │ содержит
                       │
                       ▼
┌──────────────────────────────────────────────────────────────┐
│              BroadcastScreen (UserControl)                   │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Grid Layout (3 колонки)                               │ │
│  ├────────────────────────────────────────────────────────┤ │
│  │                                                        │ │
│  │ ┌─────────────────┐  ┌─────────┐  ┌──────────────────┐│ │
│  │ │  ДЕНЕЖНАЯ       │  │ ЦЕНТР   │  │   ТАЙМЕР         ││ │
│  │ │  ЦЕПОЧКА        │  │ (Canvas)│  │ (Grid 350x120)   ││ │
│  │ │ (Canvas 350)    │  │         │  │ ┌──────────────┐ ││ │
│  │ │ ┌─────────────┐ │  │         │  │ │ TIMER.png    │ ││ │
│  │ │ │ ChainItem_0 │ │  │         │  │ │ (Image)      │ ││ │
│  │ │ │ (БАНК)      │ │  │         │  │ └──────────────┘ ││ │
│  │ │ └─────────────┘ │  │         │  │ ┌──────────────┐ ││ │
│  │ │ ┌─────────────┐ │  │         │  │ │  Viewbox     │ ││ │
│  │ │ │ ChainItem_1 │ │  │         │  │ │ ┌──────────┐ │ ││ │
│  │ │ │ (100$)      │ │  │         │  │ │ │TimerText │ ││ ││
│  │ │ └─────────────┘ │  │         │  │ │ │(0:30)    │ ││ ││
│  │ │ ┌─────────────┐ │  │         │  │ │ └──────────┘ │ ││ │
│  │ │ │ ChainItem_2 │ │  │         │  │ └──────────────┘ ││ │
│  │ │ │ (500$)      │ │  │         │  └──────────────────┘│ │
│  │ │ └─────────────┘ │  │         │                      │ │
│  │ │ ... и т.д.      │  │         │                      │ │
│  │ └─────────────────┘  └─────────┘                      │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
│  Code-Behind: BroadcastScreen.xaml.cs                       │
│  ├─ chainItems: List<ChainItem>                             │
│  ├─ currentLevelIndex: int                                  │
│  ├─ broadcastTimer: System.Threading.Timer                 │
│  ├─ UpdateChainLayout(int): void                           │
│  ├─ NextLevel(): void                                       │
│  ├─ PreviousLevel(): void                                  │
│  ├─ SetLevel(int): void                                    │
│  └─ SecondsRemaining: property                             │
└──────────────────────────────────────────────────────────────┘
```

---

## ЖИЗНЕННЫЙ ЦИКЛ КОМПОНЕНТА

```
1. ИНИЦИАЛИЗАЦИЯ
   ↓
   InitializeChain()
   ├─ Создание ChainItems (0-12)
   ├─ Добавление на Canvas
   ├─ Установка начальных координат
   └─ Загрузка начальных изображений (all blue)
   ↓
   InitializeTimer()
   ├─ Создание System.Threading.Timer
   ├─ Установка интервала обновления (100 мс)
   └─ Инициализация SecondsRemaining = 30
   ↓

2. ОСНОВНОЙ ЦИКЛ (во время игры)
   ├─ Timer тикает каждые 100 мс
   │  └─ Обновляет TimerText в UI
   │
   ├─ Пользователь кликает "Next Level"
   │  ├─ NextLevel() → UpdateChainLayout(currentIndex + 1)
   │  │  │
   │  │  ├─ Цикл для каждого ChainItem:
   │  │  │  │
   │  │  │  ├─ Расчет newBottom в зависимости от индекса:
   │  │  │  │  ├─ index == 0: Bottom = 0 (БАНК)
   │  │  │  │  ├─ index < current: Bottom = index * OverlapOffset (падает)
   │  │  │  │  ├─ index == current: Bottom = index * StandardSpacing (текущий)
   │  │  │  │  └─ index > current: Bottom = index * StandardSpacing (будущий)
   │  │  │  │
   │  │  │  ├─ Создание DoubleAnimation:
   │  │  │  │  ├─ From = текущая Bottom
   │  │  │  │  ├─ To = newBottom
   │  │  │  │  ├─ Duration = 500 мс
   │  │  │  │  └─ EasingFunction = QuadraticEase
   │  │  │  │
   │  │  │  ├─ Выполнение анимации (Storyboard.Begin())
   │  │  │  │
   │  │  │  ├─ UpdateChainItemAppearance():
   │  │  │  │  ├─ index < current: Color = Blue
   │  │  │  │  ├─ index == current: Color = Red
   │  │  │  │  └─ index > current: Color = Blue
   │  │  │  │
   │  │  │  └─ Panel.SetZIndex(element, index)
   │  │  │
   │  │  └─ currentLevelIndex++
   │
   └─ Повторение при следующем действии игрока

3. ЗАВЕРШЕНИЕ
   ↓
   Dispose()
   └─ broadcastTimer.Dispose()
```

---

## СТРУКТУРА CHAINITEM

```csharp
┌─────────────────────────────────────────┐
│         ChainItem (вложенный класс)      │
├─────────────────────────────────────────┤
│ ImageControl: Image                     │
│ ├─ Source: BitmapImage                 │
│ ├─ Stretch: Uniform                    │
│ └─ Актуальное изображение:             │
│    ├─ moneytree_blue.png (если ≤ уровня)
│    └─ moneytree_red.png (текущий)      │
│                                         │
│ TextControl: TextBlock                  │
│ ├─ Text: "БАНК" / "100" / "1000000"    │
│ ├─ Foreground: White                   │
│ └─ FontSize: 18                        │
│                                         │
│ Amount: string                          │
│ ├─ "БАНК"                              │
│ ├─ "100"                               │
│ └─ "250000"                            │
│                                         │
│ Index: int                              │
│ └─ 0 до 12                              │
└─────────────────────────────────────────┘
```

---

## КООРДИНАТНАЯ СИСТЕМА Canvas

```
              Canvas.Width = 350
    ┌──────────────────────────────┐
    │                              │
    │ Canvas.Left = положение X    │
    │ (горизонтальное смещение)    │
    │                              │
    │ Canvas.Top = положение Y     │
    │ (вертикальное смещение)      │
    │                              │
    │ Canvas.Bottom = положение    │
    │ снизу (что мы используем)    │
    │                              │
    │ ┌──────────────────────────┐ │
    │ │ ChainItem (300x45)       │ │
    │ │ ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔ │ │
    │ │ │  ИЗОБРАЖЕНИЕ  │ ТЕКСТ │ │ │
    │ │ └──────────────────────┘ │ │
    │ │ Canvas.Bottom = 50       │ │
    │ └──────────────────────────┘ │
    │                              │
    │ ┌──────────────────────────┐ │
    │ │ ChainItem (300x45)       │ │
    │ │ Canvas.Bottom = 100       │ │
    │ └──────────────────────────┘ │
    │                              │
    └──────────────────────────────┘
                  ▲
                  │
                  └─ Canvas.Height (вычисляется автоматически)
```

---

## МАТЕМАТИКА ПОЗИЦИОНИРОВАНИЯ

### Диаграмма движения при переходе currentIndex = 0 → 1

```
НАЧАЛО (currentIndex = 0):
═══════════════════════════════════════════════════════

Level 0 (БАНК) - index=0
┌─────────────────────────┐
│         БАНК            │  Canvas.Bottom = 0
└─────────────────────────┘  (всегда здесь)

Level 1 - index=1
                          ┌─────────────────────────┐
                          │        100$             │  Canvas.Bottom = 50
                          └─────────────────────────┘  (StandardSpacing = 50)

Level 2 - index=2
                                                    ┌─────────────────────────┐
                                                    │        500$             │  Canvas.Bottom = 100
                                                    └─────────────────────────┘


КОНЕЦ (currentIndex = 1):
═══════════════════════════════════════════════════════

Level 0 (БАНК) - index=0
┌─────────────────────────┐
│         БАНК            │  Canvas.Bottom = 0
└─────────────────────────┘  (не изменилось)

Level 1 - index=1  ← ТЕКУЩИЙ (RED)
┌─────────────────────────┐
│    100$ (RED)           │  Canvas.Bottom = 50
└─────────────────────────┘  (не изменилось, он был текущим)

Level 2 - index=2
                          ┌─────────────────────────┐
                          │       500$ (BLUE)       │  Canvas.Bottom = 100
                          └─────────────────────────┘  (не изменилось)


ЧТО ПРОИЗОЙДЕТ при следующем переходе currentIndex = 1 → 2:
═══════════════════════════════════════════════════════════════════

Level 0 (БАНК) - index=0
┌─────────────────────────┐
│         БАНК            │  Canvas.Bottom = 0 (остается)
└─────────────────────────┘  

Level 1 - index=1 (уже пройден: 0 < 1 < 2)
  ┌─────────────────────────┐
  │    100$ (BLUE)          │  Canvas.Bottom = 25 ← АНИМАЦИЯ
  └─────────────────────────┘  (формула: 1 * OverlapOffset = 1 * 25 = 25)
  
  Путь падения:
  50 ──┐ (начало)
       │ ANIMA
       │ TION
       └─→ 25 (конец, упал в стопку)

Level 2 - index=2 ← НОВЫЙ ТЕКУЩИЙ (RED)
                          ┌─────────────────────────┐
                          │   500$ (RED)            │  Canvas.Bottom = 100
                          └─────────────────────────┘  (формула: 2 * StandardSpacing = 2 * 50 = 100)

Level 3 - index=3
                                                    ┌─────────────────────────┐
                                                    │      1000$ (BLUE)       │  Canvas.Bottom = 150
                                                    └─────────────────────────┘
```

---

## ПОТОК ДАННЫХ (DATA FLOW)

```
┌───────────────────┐
│  Пользователь    │
│  (нажимает кнопку)│
└─────────┬─────────┘
          │
          ▼
    NextLevel() ──┐
    PrevLevel()   │
    SetLevel(n) ──┤──→ UpdateChainLayout(int newIndex)
                  │
└─────────────────┘
          │
          ▼
   ┌─────────────────────────────────────┐
   │  UpdateChainLayout()                 │
   │  ┌───────────────────────────────┐  │
   │  │ Цикл по всем ChainItem        │  │
   │  ├───────────────────────────────┤  │
   │  │ 1. Расчет newBottom           │  │
   │  │ 2. Создание DoubleAnimation   │  │
   │  │ 3. Storyboard.Begin()         │  │
   │  │ 4. UpdateChainItemAppearance()│  │
   │  │ 5. Panel.SetZIndex()          │  │
   │  └───────────────────────────────┘  │
   └──────────────┬──────────────────────┘
                  │
                  ▼
       ┌──────────────────────────┐
       │   Canvas переде​ляет      │
       │   элементы на новые      │
       │   позиции (Bottom)       │
       │                          │
       │   Анимация воспроизводится
       │   в течение 500 мс       │
       └──────────────────────────┘
```

---

## ТАЙМЕР - ЖИЗНЕННЫЙ ЦИКЛ

```
┌────────────────────────────────────┐
│ System.Threading.Timer             │
│ Интервал: 100 мс                  │
└────────┬─────────────────────────┬─┘
         │                         │
         ▼                         ▼
    UpdateTimer()          SecondsRemaining-─┐
         │                                  │
         ▼                                  ▼
    Dispatcher.Invoke()           UpdateTimerDisplay()
         │                              │
         ▼                              ▼
    Уменьшить                   Вычислить MM:SS
    SecondsRemaining            Обновить TextBlock


Пример отсчета (начало: 30 сек):
═════════════════════════════════════

0 мс:   30 → TimerText = "0:30"
100 мс: 29 → TimerText = "0:29"
200 мс: 28 → TimerText = "0:28"
...
2900 мс: 1  → TimerText = "0:01"
3000 мс: 0  → TimerText = "0:00"
3100 мс: 30 → TimerText = "0:30" (перезагрузка)
```

---

## ВИЗУАЛЬНОЕ СОСТОЯНИЕ ЭЛЕМЕНТОВ

### По индексу относительно currentIndex

```
currentIndex = 3

Индекс │ Условие        │ Bottom         │ Цвет │ Z-Index │ Видимость
───────┼────────────────┼────────────────┼──────┼─────────┼──────────
   0   │ == 0           │ 0              │ Blue │    0    │ Видно (в стопке)
   1   │ < current      │ 25             │ Blue │    1    │ Видно (в стопке)
   2   │ < current      │ 50             │ Blue │    2    │ Видно (в стопке)
───────┼────────────────┼────────────────┼──────┼─────────┼──────────
   3   │ == current     │ 150            │ Red  │    3    │ Видно (ЯРКО)
───────┼────────────────┼────────────────┼──────┼─────────┼──────────
   4   │ > current      │ 200            │ Blue │    4    │ Видно
   5   │ > current      │ 250            │ Blue │    5    │ Видно
   6   │ > current      │ 300            │ Blue │    6    │ Видно
   ...  │ > current     │ ...            │ Blue │  ...    │ Видно

Z-Index гарантирует, что более высокие индексы
(более поздние уровни) визуально находятся поверх
нижних индексов, что особенно важно при нахлесте.

Пример визуального наложения:
┌─────────────┐
│   Level 4   │  Z=4 (поверх всех)
│ ┌─────────┐ │
│ │ Level 3 │ │  Z=3 (поверх стопки)
│ │ ┌─────┐ │ │
│ │ │Lvl 2│ │ │  Z=2 (в стопке)
│ │ │┌───┐│ │ │
│ │ ││Lvl│ │ │  Z=1
│ │ ││ 1 │ │ │
│ │ │└───┘│ │ │
│ │ └─────┘ │ │
│ └─────────┘ │
└─────────────┘

Уровень 0 (БАНК) - базовый, Z=0, всегда внизу
```

---

## ПРОИЗВОДИТЕЛЬНОСТЬ

### Оптимизация:

1. **Кеширование ссылок на элементы**:
   ```csharp
   // ❌ Плохо - поиск каждый раз
   for (int i = 0; i < 100; i++) {
       UIElement el = ChainCanvas.FindName($"ChainItem_{i}");
   }
   
   // ✅ Хорошо - поиск один раз
   var items = chainItems.Select(x => 
       ChainCanvas.FindName($"ChainItem_{x.Index}")).ToList();
   ```

2. **Батчинг анимаций**:
   ```csharp
   Storyboard masterStoryboard = new Storyboard();
   foreach (var item in itemsToAnimate) {
       // Добавляем все анимации в один Storyboard
       masterStoryboard.Children.Add(createAnimation(item));
   }
   masterStoryboard.Begin();  // Одно Begin() для всех
   ```

3. **Избегание лишних перерисовок**:
   ```csharp
   // Не вызываем UpdateChainLayout() слишком часто
   // Используйте debouncing если есть быстрые события
   ```

---

## ИНСТРУМЕНТЫ ОТЛАДКИ

```csharp
// Вывести состояние всей цепочки
private void DebugChainState()
{
    for (int i = 0; i < chainItems.Count; i++)
    {
        var element = ChainCanvas.FindName($"ChainItem_{i}") as UIElement;
        double bottom = Canvas.GetBottom(element);
        int zindex = Panel.GetZIndex(element);
        Console.WriteLine(
            $"Item {i}: Bottom={bottom:F1}, ZIndex={zindex}, " +
            $"IsCurrent={i == currentLevelIndex}"
        );
    }
}

// Проверить размеры
private void DebugSizes()
{
    Console.WriteLine($"Canvas: {ChainCanvas.ActualWidth}x{ChainCanvas.ActualHeight}");
    Console.WriteLine($"Timer: {TimerContainer.ActualWidth}x{TimerContainer.ActualHeight}");
}

// Проверить таймер
private void DebugTimer()
{
    Console.WriteLine($"Seconds remaining: {secondsRemaining}");
    Console.WriteLine($"Timer text: {TimerText.Text}");
}
```
