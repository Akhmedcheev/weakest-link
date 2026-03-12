# 🚀 БЫСТРЫЙ СТАРТ - Денежная цепочка

## ШАГ 1: Скопируйте файлы

```
BroadcastScreen.xaml          → /YourProject/Views/
BroadcastScreen.xaml.cs       → /YourProject/Views/
MainWindow.xaml               → /YourProject/
MainWindow.xaml.cs            → /YourProject/
```

## ШАГ 2: Создайте папку Assets

```
/YourProject/Assets/
├─ TIMER.png                  (размер примерно 350x120)
├─ moneytree_blue.png         (размер примерно 300x45)
└─ moneytree_red.png          (размер примерно 300x45)
```

## ШАГ 3: Используйте в коде

```csharp
// В MainWindow.xaml.cs
BroadcastScreen screen = new BroadcastScreen();

// Переместить на уровень
screen.SetLevel(3);

// Следующий уровень
screen.NextLevel();

// Таймер
screen.SecondsRemaining = 30;

// Очистить
screen.Dispose();
```

---

## 📋 ОСНОВНЫЕ ФУНКЦИИ

### Управление уровнями

| Функция | Описание |
|---------|---------|
| `SetLevel(int level)` | Перейти на конкретный уровень |
| `NextLevel()` | Следующий уровень |
| `PreviousLevel()` | Предыдущий уровень |
| `GetCurrentLevel()` | Получить текущий уровень |

### Управление таймером

```csharp
screen.SecondsRemaining = 45;  // Установить время
int time = screen.SecondsRemaining;  // Получить время
```

---

## ⚙️ КЛЮЧЕВЫЕ ПАРАМЕТРЫ

```csharp
// В BroadcastScreen.xaml.cs

// Толщина нахлеста плашек в стопке
private const double OverlapOffset = 25.0;

// Расстояние между плашками
private const double StandardSpacing = 50.0;

// Длительность анимации (мс)
private const int AnimationDurationMs = 500;
```

---

## 🎨 ТИПИЧНЫЙ СЦЕНАРИЙ ИГРЫ

```csharp
// 1. Инициализация
BroadcastScreen screen = new BroadcastScreen();

// 2. Начало игры
screen.SetLevel(0);
screen.SecondsRemaining = 30;

// 3. Игрок ответил правильно на вопрос 1
screen.NextLevel();  // Переходим на уровень 1
screen.SecondsRemaining = 30;

// 4. Игрок ответил правильно на вопрос 2
screen.NextLevel();  // Переходим на уровень 2
screen.SecondsRemaining = 30;

// 5. Игрок ответил неправильно
// Уровень 2 остается красным, уровни 0-1 в стопке (blue)

// 6. Конец игры
screen.Dispose();
```

---

## 🔍 ВИЗУАЛЬНОЕ ПРЕДСТАВЛЕНИЕ

### Начало игры (Level 0 - БАНК):
```
┌─────────────────┐
│     БАНК        │  Blue, Bottom=0
└─────────────────┘
```

### После 2 правильных ответов (Level 2):
```
┌─────────────────┐
│     БАНК        │  Blue, Bottom=0
└─────────────────┘
  ┌─────────────────┐
  │   100$ (Blue)   │  Bottom=25 (в стопке)
  └─────────────────┘
    ┌─────────────────┐
    │   500$ (Red)    │  ТЕКУЩИЙ, Bottom=100
    └─────────────────┘
                    ┌─────────────────┐
                    │ 1000$ (Blue)    │  Bottom=150
                    └─────────────────┘
```

---

## ✅ ЧЕКЛИСТ ИНТЕГРАЦИИ

- [ ] Скопированы XAML файлы
- [ ] Скопированы CS файлы
- [ ] Созданы изображения в /Assets/
- [ ] Добавлены ссылки на изображения в проекте
- [ ] Протестировано переключение уровней
- [ ] Проверена работа таймера
- [ ] Проверена анимация падения плашек
- [ ] Проверено отображение цветов (Blue/Red)

---

## 🐛 РЕШЕНИЕ ПРОБЛЕМ

### Проблема: Изображения не загружаются

**Решение**: Убедитесь, что путь правильный:
```csharp
// Правильно
new BitmapImage(new Uri("/Assets/moneytree_blue.png", UriKind.Relative));

// Проверьте Properties файла: Build Action = Resource
```

### Проблема: Таймер не обновляется

**Решение**: Убедитесь, что используется `Dispatcher.Invoke()`:
```csharp
Dispatcher.Invoke(() => {
    SecondsRemaining--;
});
```

### Проблема: Плашки не падают в стопку

**Решение**: Проверьте значения констант:
```csharp
OverlapOffset = 25.0   // не 0
StandardSpacing = 50.0 // не 0
```

### Проблема: Текст таймера вылезает за границы

**Решение**: Используется `Viewbox` - он автоматически масштабирует:
```xml
<Viewbox Margin="20">
    <TextBlock Name="TimerText" Text="0:30" FontSize="72"/>
</Viewbox>
```

---

## 📚 ДОПОЛНИТЕЛЬНО

Для более подробной информации смотрите:

- **DOCUMENTATION.md** - Полная документация
- **EXAMPLES.md** - Примеры кода и использования
- **ARCHITECTURE.md** - Архитектура и диаграммы

---

## 💡 СОВЕТЫ

1. **Тестируйте в реальном разрешении** вашего экрана трансляции
2. **Оптимизируйте изображения** (используйте PNG, сжимайте)
3. **Настраивайте константы** в зависимости от дизайна
4. **Используйте Storyboard** для группировки анимаций
5. **Кешируйте ссылки на элементы** для производительности

---

## 📞 ПОДДЕРЖКА

Все вопросы? Смотрите примеры в **EXAMPLES.md**.

---

**Версия**: 1.0
**Дата**: 2025-03-06
**Автор**: Senior C# WPF Developer
