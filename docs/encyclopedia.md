# ЭНЦИКЛОПЕДИЯ ПРОЕКТА «СЛАБОЕ ЗВЕНО» (WEAKEST LINK)

> Полная техническая документация проекта — архитектура, модули, API, игровой цикл, дизайн-система, сетевое взаимодействие, AI-интеграция, файловая структура.

---

## ОГЛАВЛЕНИЕ

1. [Обзор проекта](#1-обзор-проекта)
2. [Файловая структура](#2-файловая-структура)
3. [Технологический стек](#3-технологический-стек)
4. [Архитектура приложения](#4-архитектура-приложения)
5. [Игровой движок (GameEngine)](#5-игровой-движок-gameengine)
6. [Конечный автомат состояний (GameState)](#6-конечный-автомат-состояний-gamestate)
7. [Полный игровой цикл](#7-полный-игровой-цикл)
8. [Операторская панель (OperatorPanel)](#8-операторская-панель-operatorpanel)
9. [Система голосования](#9-система-голосования)
10. [Финальная дуэль (Head-to-Head)](#10-финальная-дуэль-head-to-head)
11. [Аналитика и статистика](#11-аналитика-и-статистика)
12. [Экраны ведущего (Host Screens)](#12-экраны-ведущего-host-screens)
13. [Экран зрителей (AudienceScreen)](#13-экран-зрителей-audiencescreen)
14. [Трансляция (BroadcastScreen / BroadcastWindow)](#14-трансляция-broadcastscreen--broadcastwindow)
15. [Аудио-система (AudioManager)](#15-аудио-система-audiomanager)
16. [Сетевое взаимодействие](#16-сетевое-взаимодействие)
17. [Web-пульт (WebRemoteController)](#17-web-пульт-webremotecontroller)
18. [AI-интеграция](#18-ai-интеграция)
19. [Дизайн-система Obsidian](#19-дизайн-система-obsidian)
20. [Система вопросов](#20-система-вопросов)
21. [Настройки и персистентность](#21-настройки-и-персистентность)
22. [Тестирование и автотесты](#22-тестирование-и-автотесты)
23. [Макропад](#23-макропад)
24. [Утилиты и вспомогательные инструменты](#24-утилиты-и-вспомогательные-инструменты)
25. [Сборка и деплой](#25-сборка-и-деплой)
26. [Горячие клавиши](#26-горячие-клавиши)
27. [Модели данных](#27-модели-данных)
28. [Полный справочник API](#28-полный-справочник-api)
29. [Известные особенности и решения](#29-известные-особенности-и-решения)

---

## 1. ОБЗОР ПРОЕКТА

**Weakest Link (Слабое звено)** — десктопное WPF-приложение для проведения телевизионной игры «Слабое звено» в реальном времени. Программа управляет всеми аспектами шоу: от ведения раундов и банка до голосования, исключения игроков и финальной дуэли.

### Ключевые возможности

- **Операторская панель** — единый центр управления игрой с полным контролем над всеми этапами
- **Экраны ведущего** — 4 варианта телесуфлёра (Classic, Modern, Premium, Modern Premium)
- **Экран зрителей** — отображение денежной цепочки, таймера, банка
- **Трансляция** — вывод для OBS/стриминга с хромакей-фоном
- **TCP-сервер** — синхронизация всех экранов в реальном времени (порт 8888)
- **Web-пульт** — управление с iPad/планшета через HTTP (порт 8080)
- **AI-ведущая** — Gemini + ElevenLabs для озвучки и ведения
- **AI-тестирование** — боты на базе OpenAI/Gemini для нагрузочного тестирования
- **Аналитика** — статистика по раундам, прогноз выбывания, экспорт CSV/HTML
- **Аудио-менеджер** — полное управление музыкой, SFX, кроссфейдами
- **Двуязычность** — русский и английский интерфейс
- **Редактор вопросов** — встроенный CRUD для базы вопросов

### Участники игры

- **8 игроков** в начале
- **7 раундов** (от 150 до 90 секунд)
- **Банковская цепочка**: 1 000 → 2 000 → 5 000 → 10 000 → 20 000 → 30 000 → 40 000 → 50 000
- **Финальная дуэль** между 2 оставшимися

---

## 2. ФАЙЛОВАЯ СТРУКТУРА

```
WEAKEST LINK SOPFTWARE AI TESTERING FINALE/
│
├── App.xaml / App.xaml.cs              # Точка входа, глобальный обработчик ошибок
├── WeakestLink.csproj                  # Конфигурация проекта (.NET 10, WPF)
├── Animations.cs                       # Анимации UI
├── app.ico / app.manifest              # Иконка и манифест
│
├── Core/                               # ═══ ЯДРО ИГРЫ ═══
│   ├── GameEngine.cs                   # Игровой движок: банк, цепочка, раунды, дуэль
│   ├── GameState.cs                    # Enum: 13 состояний конечного автомата
│   ├── SelfTest.cs                     # Автотесты (state machine, bank, rounds, duel)
│   │
│   ├── Analytics/
│   │   ├── StatsAnalyzer.cs            # Аналитика: strongest/weakest, прогноз, метрики
│   │   └── StatsExporter.cs            # Экспорт в CSV и HTML
│   │
│   ├── Models/
│   │   ├── QuestionData.cs             # Модель вопроса (Id, Text, Answer)
│   │   ├── QuestionModel.cs            # Модель для редактора
│   │   └── BotDecision.cs              # Решение AI-бота (Action, Text)
│   │
│   └── Services/
│       ├── AiHostService.cs            # AI-ведущая (Gemini + ElevenLabs)
│       ├── QuestionProvider.cs         # Загрузка и выдача вопросов
│       ├── ColorResourceHelper.cs      # Доступ к цветам из XAML в code-behind
│       └── MacroPadService.cs          # Интеграция с USB-макропадом
│
├── Network/                            # ═══ СЕТЕВОЙ СЛОЙ ═══
│   ├── GameServer.cs                   # TCP-сервер (порт 8888–8892)
│   ├── GameClient.cs                   # TCP-клиент
│   ├── WebRemoteController.cs          # HTTP-сервер для iPad/планшета (8080)
│   ├── AiBotTester.cs                  # AI-бот для нагрузочного тестирования
│   ├── GeminiBotClient.cs             # Клиент Gemini API
│   └── GeminiTestPlayer.cs            # AI-игрок на Gemini
│
├── Audio/                              # ═══ АУДИО ═══
│   ├── AudioManager.cs                 # Менеджер аудио (NAudio): play, loop, crossfade
│   └── LoopStream.cs                   # Бесконечный loop для NAudio
│
├── Views/                              # ═══ ИНТЕРФЕЙС ═══
│   ├── OperatorPanel.xaml/.cs          # Главная операторская панель (~7300 строк)
│   ├── HostScreen.xaml/.cs             # Экран ведущего (Classic)
│   ├── HostScreenModern.xaml/.cs       # Экран ведущего (Modern)
│   ├── HostScreenPremium.xaml/.cs      # Экран ведущего (Premium, с анимациями)
│   ├── HostScreenModernPremium.xaml/.cs # Экран ведущего (Modern Premium)
│   ├── AudienceScreen.xaml/.cs         # Экран зрителей (1920×1080)
│   ├── BroadcastScreen.xaml/.cs        # Компонент трансляции (UserControl)
│   ├── BroadcastWindow.xaml/.cs        # Окно трансляции (хромакей)
│   ├── RoundStatsWindow.xaml/.cs       # Окно статистики раунда
│   ├── WinWinner.xaml/.cs              # Оверлей победителя
│   ├── QuestionEditorWindow.xaml/.cs   # Редактор вопросов
│   ├── ServiceScreen.xaml/.cs          # Сервисный экран (макропад)
│   ├── DarkMessageBox.xaml/.cs         # Кастомный MessageBox
│   │
│   └── Styles/
│       ├── ObsidianStyles.xaml         # Цветовая палитра + стили (Obsidian Design System)
│       └── UIConstants.xaml            # Размеры, типографика, отступы, скругления
│
├── Assets/
│   ├── Images/                         # PNG: цепочка, таймер, банк, финал
│   └── Audio/
│       ├── ROUND SFX/                  # BANK.mp3, CORRECT.mp3, WRONG.mp3
│       ├── VOTING TRACKS/              # 5 треков голосования
│       ├── Round_bed_*.mp3             # Фоновая музыка раундов (130–230 BPM)
│       ├── main_theme_full.mp3         # Главная тема
│       ├── intro_track_*.mp3           # Треки вступления
│       ├── general_bed*.mp3            # Общий фон
│       ├── walk_of_shame.mp3           # Музыка исключения
│       └── duel_winner.mp3             # Финал — победитель
│
├── docs/                               # Документация
│   ├── encyclopedia.md                 # ← ЭТА ЭНЦИКЛОПЕДИЯ
│   ├── ARCHITECTURE.md                 # Архитектура
│   ├── ANALYTICS_DOCUMENTATION.md      # Система аналитики
│   ├── VOTING_AND_STATS_SYSTEM.md      # Голосование и статистика
│   ├── ИНСТРУКЦИЯ_ПОЛЬЗОВАТЕЛЯ.md     # Руководство пользователя
│   └── ...                             # ~30 файлов документации
│
├── Tools/                              # Утилиты
│   ├── analyze_sync.py                 # Анализ синхронизации
│   ├── find_first_beat.py              # Поиск первого бита
│   ├── layout_editor.html              # HTML-редактор раскладки
│   └── MetroDetect/                    # Утилита определения метронома
│
├── macropad/                           # Конфигурации макропада
│   ├── example-mapping.yaml
│   └── weakest-link-macropad.yaml
│
├── questions.json                      # База вопросов (RU)
├── questions_en.json                   # База вопросов (EN)
├── final_questions.json                # Финальные вопросы (RU)
├── final_questions_en.json             # Финальные вопросы (EN)
│
└── SuperDist/net10.0-windows/          # ═══ СБОРКА (OUTPUT) ═══
```

---

## 3. ТЕХНОЛОГИЧЕСКИЙ СТЕК

| Компонент | Технология | Версия |
|-----------|-----------|--------|
| Платформа | .NET | 10.0 |
| UI-фреймворк | WPF (Windows Presentation Foundation) | — |
| Язык | C# | 13 |
| Аудио | NAudio | 2.2.1 |
| QR-коды | QRCoder | 1.7.0 |
| USB HID | hidlibrary | 3.3.40 |
| AI (ведущая) | Google Gemini API + ElevenLabs TTS | — |
| AI (боты) | OpenAI-совместимый API | — |
| Сетевой протокол | TCP (System.Net.Sockets) | — |
| Web-пульт | HttpListener (System.Net) | — |
| Сериализация | System.Text.Json | — |
| Таргет ОС | Windows 10/11 | — |

### NuGet-пакеты

```xml
<PackageReference Include="hidlibrary" Version="3.3.40" />
<PackageReference Include="NAudio" Version="2.2.1" />
<PackageReference Include="QRCoder" Version="1.7.0" />
```

---

## 4. АРХИТЕКТУРА ПРИЛОЖЕНИЯ

```
┌─────────────────────────────────────────────────────────────────┐
│                        App.xaml (Entry Point)                    │
│                    StartupUri → OperatorPanel.xaml               │
│              Global Exception Handler → error_log.txt            │
└───────────────────────────┬─────────────────────────────────────┘
                            │
                ┌───────────▼───────────┐
                │    OperatorPanel       │ ← Центр управления
                │   (~7300 строк C#)    │
                └───┬───┬───┬───┬───┬───┘
                    │   │   │   │   │
        ┌───────────┘   │   │   │   └───────────┐
        ▼               ▼   │   ▼               ▼
   GameEngine      AudioMgr │  GameServer   WebRemote
   (Core logic)   (NAudio)  │  (TCP 8888)  (HTTP 8080)
                            ▼
                      AiHostService
                    (Gemini+ElevenLabs)
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼
   HostScreen(s)   AudienceScreen  BroadcastWindow
   (4 варианта)    (1920×1080)     (OBS/хромакей)
```

### Слои

1. **Presentation** — XAML + code-behind (Views/)
2. **Core** — игровой движок, state machine, аналитика (Core/)
3. **Services** — AI, вопросы, цвета, макропад (Core/Services/)
4. **Network** — TCP/HTTP серверы и клиенты (Network/)
5. **Audio** — воспроизведение, loop, crossfade (Audio/)

### Паттерны

- **State Machine** — `GameState` enum + `TransitionTo()` с валидацией переходов
- **Event-Driven** — `StateChanged`, `BankChanged`, `MaxBankReached`, `FinalDuelEnded`
- **Observer** — TCP-сервер рассылает сообщения всем подключённым клиентам
- **Code-Behind** — основная логика UI в `OperatorPanel.xaml.cs`

---

## 5. ИГРОВОЙ ДВИЖОК (GameEngine)

### Основные свойства

| Свойство | Тип | Описание |
|----------|-----|----------|
| `CurrentState` | `GameState` | Текущее состояние автомата |
| `CurrentRound` | `int` | Номер раунда (1–7) |
| `CurrentChainIndex` | `int` | Позиция в цепочке (0–7) |
| `RoundBank` | `int` | Банк текущего раунда |
| `TotalBank` | `int` | Общий банк за игру |
| `RoundBurned` | `int` | Сгоревшие деньги за раунд |
| `ActivePlayers` | `List<string>` | Список активных игроков |
| `CurrentPlayerTurn` | `string` | Чей сейчас ход |
| `EliminatedPlayerName` | `string` | Последний исключённый |
| `LastStrongestLinkName` | `string` | Сильнейшее звено прошлого раунда |
| `PlayerStatistics` | `Dictionary<string, Dictionary<int, PlayerStats>>` | Статистика: [игрок][раунд] |

### Денежная цепочка

```
Шаг 0: ────── (старт)
Шаг 1: 1 000 ₽
Шаг 2: 2 000 ₽
Шаг 3: 5 000 ₽
Шаг 4: 10 000 ₽
Шаг 5: 20 000 ₽
Шаг 6: 30 000 ₽
Шаг 7: 40 000 ₽
Шаг 8: 50 000 ₽  ← MAX BANK (автобанк)
```

### Длительность раундов

| Раунд | Длительность | Игроков |
|-------|-------------|---------|
| 1 | 150 сек | 8 |
| 2 | 140 сек | 7 |
| 3 | 130 сек | 6 |
| 4 | 120 сек | 5 |
| 5 | 110 сек | 4 |
| 6 | 100 сек | 3 |
| 7 (префинал) | 90 сек | 2 |

### Ключевые методы

| Метод | Описание |
|-------|----------|
| `CorrectAnswer()` | +1 шаг цепочки. При 8 шагах — автобанк 50 000, цепочка сбрасывается |
| `WrongAnswer()` | Сброс цепочки, сумма сгорает (`RoundBurned += потеря`) |
| `Pass()` | Сброс цепочки, деньги НЕ сгорают |
| `Bank()` | Текущая сумма → `RoundBank`, цепочка сбрасывается |
| `MoveToNextPlayer()` | Ход следующему по кругу (среди `ActivePlayers`) |
| `PrepareNewRound()` | `NextRound()` + стартовый игрок + инициализация статистики |
| `EliminatePlayer(name)` | Удаление из `ActivePlayers`, переход в `Elimination` |
| `ApplyRoundBankToTotal()` | `TotalBank += RoundBank` (×2 при 2 игроках) |
| `StartFinalDuel(p1First)` | Инициализация финальной дуэли |
| `ProcessFinalAnswer(isCorrect)` | Обработка ответа в финале |
| `ResetGame()` | Полный сброс всех полей |

### Стартовый игрок раунда

- **Раунд 1**: по алфавиту (первый по имени)
- **Раунды 2+**: сильнейшее звено прошлого раунда (`LastStrongestLinkName`)

### Статистика игрока (PlayerStats)

```csharp
public class PlayerStats
{
    int CorrectAnswers;        // Верные ответы
    int IncorrectAnswers;      // Неверные ответы
    int Passes;                // Пасы
    int BankedMoney;           // Сколько положил в банк
    int BankPressCount;        // Сколько раз нажал БАНК
    int BurnedByWrongAnswers;  // Сгорело из-за неверных ответов
    int ExactDroppedMoney;     // Точная сумма потерь
}
```

### События

| Событие | Аргументы | Когда |
|---------|-----------|-------|
| `StateChanged` | `StateChangedEventArgs` | Любая смена состояния |
| `BankChanged` | `BankChangedEventArgs` | Изменение банка/цепочки |
| `MaxBankReached` | — | Достигнут MAX 50 000 |
| `FinalDuelEnded` | — | Финальная дуэль завершена |

---

## 6. КОНЕЧНЫЙ АВТОМАТ СОСТОЯНИЙ (GameState)

### Все состояния

| Состояние | Описание |
|-----------|----------|
| `Idle` | Начальное / ожидание |
| `IntroOpening` | Вступление: логотип, главная тема |
| `IntroNarrative` | Рассказ о шоу |
| `PlayerIntro` | Представление игроков |
| `RulesExplanation` | Объяснение правил |
| `RoundReady` | Раунд подготовлен, ожидание PLAY |
| `Playing` | Идёт раунд, таймер тикает |
| `RoundSummary` | Итоги раунда |
| `Voting` | Голосование (45 сек) |
| `Discussion` | Обсуждение результатов |
| `Reveal` | Вскрытие голосов |
| `Elimination` | Исключение игрока |
| `FinalDuel` | Финальная дуэль |

### Матрица допустимых переходов

```
Idle ──────────→ RoundReady, FinalDuel, IntroOpening, RulesExplanation
IntroOpening ──→ IntroNarrative, Idle
IntroNarrative → PlayerIntro, Idle
PlayerIntro ──→ RulesExplanation, Idle
RulesExplanation → RoundReady, Idle
RoundReady ───→ Playing, Idle
Playing ──────→ RoundSummary, Voting, Idle
RoundSummary ─→ Voting, Idle
Voting ───────→ Discussion, Elimination, Idle
Discussion ──→ Reveal, Elimination, Idle
Reveal ──────→ Elimination, Discussion, Idle
Elimination ─→ RoundReady, FinalDuel, Idle
FinalDuel ───→ Idle
```

### Визуальная диаграмма игрового цикла

```
    ┌─────────────────────────────────────────────────┐
    │                     IDLE                         │
    └──────┬──────────────────────────────────┬────────┘
           │                                  │
    ┌──────▼──────┐                    ┌──────▼──────┐
    │IntroOpening │                    │ RoundReady  │◄─────────┐
    └──────┬──────┘                    └──────┬──────┘          │
    ┌──────▼──────┐                    ┌──────▼──────┐          │
    │IntroNarrat. │                    │   Playing   │          │
    └──────┬──────┘                    └──────┬──────┘          │
    ┌──────▼──────┐                    ┌──────▼──────┐          │
    │ PlayerIntro │                    │RoundSummary │          │
    └──────┬──────┘                    └──────┬──────┘          │
    ┌──────▼──────┐                    ┌──────▼──────┐          │
    │Rules Explan.│───────────────────→│   Voting    │          │
    └─────────────┘                    └──────┬──────┘          │
                                       ┌──────▼──────┐          │
                                       │ Discussion  │          │
                                       └──────┬──────┘          │
                                       ┌──────▼──────┐          │
                                       │   Reveal    │          │
                                       └──────┬──────┘          │
                                       ┌──────▼──────┐          │
                                       │ Elimination │──────────┘
                                       └──────┬──────┘
                                              │ (2 игрока)
                                       ┌──────▼──────┐
                                       │ FinalDuel   │
                                       └─────────────┘
```

---

## 7. ПОЛНЫЙ ИГРОВОЙ ЦИКЛ

### Фаза 1: Подготовка

1. Запуск `WeakestLink.exe` → `OperatorPanel` открывается
2. TCP-сервер стартует на порту 8888
3. Web-пульт стартует на порту 8080
4. Оператор заполняет **Smart Roster** (8 игроков: имя на тумбе, полное имя, город, фото)
5. Нажатие **УТВЕРДИТЬ** → состав заморожен, кнопка → «СПИСОК УТВЕРЖДЕН» (тусклая)
6. Нажатие **START SESSION** → `_isSessionStarted = true`, `ResetRoundCounter()`

### Фаза 2: PRE-GAME (вступление)

Если FastTrack **выключен** (стандартный путь):

| Шаг | Кнопка | Аудио | Состояние |
|-----|--------|-------|-----------|
| 1 | OPENING | `main_theme_full.mp3` | `IntroOpening` |
| 2 | INTRO 1 | `intro_track_1st.mp3` | `IntroNarrative` |
| 3 | INTRO 2 | `intro_track_2nd.mp3` | `PlayerIntro` |
| 4 | RULES | `intro_track_3rd.mp3` | `RulesExplanation` |

После RULES → `FinalizePregameAndPrepareRound()`:
- `PrepareNewRound()` (CurrentRound → 1)
- PRE-GAME панель скрывается
- READY становится доступна

Если FastTrack **включён**: сразу `RulesExplanation` → `FinalizePregameAndPrepareRound()`.

### Фаза 3: Раунд (повторяется 6 раз)

```
READY → RoundReady
  ↓
PLAY → Playing (таймер запущен, Round Bed играет)
  ↓
Вопрос → CORRECT / WRONG / BANK
  ↓ (таймер = 0 или все вопросы)
RoundSummary → ApplyRoundBankToTotal()
  ↓
OpenRoundAnalytics() → Аналитика + Голосование
```

#### Действия во время раунда

| Действие | Эффект |
|----------|--------|
| **CORRECT** | ChainIndex++ (если 8 → автобанк 50000, цепочка сбрасывается) |
| **WRONG** | Потеря текущей суммы цепочки, ChainIndex = 0 |
| **BANK** | Текущая сумма → RoundBank, ChainIndex = 0 |
| **Таймер = 0** | Раунд завершается, переход в RoundSummary |

### Фаза 4: Голосование (Film-Style, 5 шагов)

| Шаг | Название | Аудио | Описание |
|-----|----------|-------|----------|
| 1 | ОТБИВКА → СТОП МОТОР | `1_sting4_motor_off.mp3` | Оператор собирает голоса в панели |
| 2 | МОТОР ИДЁТ | `2_sting4_motor_on.mp3` | Камеры включены |
| 3 | ПОДНЯТЬ ТАБЛИЧКИ | `3_voting_reveal.mp3` | Подсчёт голосов, определение жертвы |
| 4 | ОБСУЖДЕНИЕ | `4_voting_discussion.mp3` | Ведущая обсуждает результаты |
| 5 | ПРОЩАЙТЕ | `5_walkofshame+after.mp3` | Исключение игрока |

### Фаза 5: Раунд 7 (Префинал)

- Остаются 2 игрока
- Банк за раунд **удваивается** (`ApplyRoundBankToTotal()` × 2)
- Голосование **не проводится**
- После раунда → кнопка «ПЕРЕЙТИ К ФИНАЛУ»

### Фаза 6: Финальная дуэль

- 5 пар вопросов, по очереди
- Если ничья после 5 пар → **Sudden Death** (дополнительные пары)
- Победитель определяется, когда один игрок не может быть догнан
- Аудио: `duel_winner.mp3`
- Оверлей `WinWinner` с именем победителя и суммой выигрыша

---

## 8. ОПЕРАТОРСКАЯ ПАНЕЛЬ (OperatorPanel)

Главное окно приложения (~7300 строк C#, ~2500 строк XAML).

### Навигация (Central Context)

| Контекст | Описание |
|----------|----------|
| `SETUP` | Настройка состава (Smart Roster) |
| `PLAY` | Игровой экран (вопросы, таймер, банк) |
| `STATS` | Аналитика и голосование |
| `EDITOR` | Редактор вопросов |
| `SETTINGS` | Настройки приложения |

### Левая панель (Sidebar)

- **Навигация**: Лобби, Игра, Аналитика, Редактор
- **Статистика**: БАНК, РАУНД, ВЕРНО, НЕВЕРНО, ВРЕМЯ, ВОПРОС
- **Раунды**: иконки раундов (1–7)
- **Игроки**: список с цветовой индикацией статуса

### Правая панель (Управление эфиром)

- **READY** / **START O'CLOCK** / **CLOSE ROUND** — управление раундом
- **Логотипы**: Classic / Premium
- **Раунд**: Таймер, Цепочка, Обе
- **Ответы**: БАНК, ДУЭЛЬ
- **PRE-GAME**: OPENING, INTRO 1, INTRO 2, RULES
- **Сервис**: STOP AUDIO, RESTART ROUND, CLOSE SESSION, ПАНИКА

### Центральная область

Переключается между контекстами. В режиме STATS содержит:
- Таблицу аналитики с фильтром по раундам (R1–R7)
- Панель голосования «ГОЛОСУЕТ ЗА» с ComboBox для каждого игрока
- Кнопку ПРИНЯТЬ для фиксации голосов

### Ключевые приватные поля

```csharp
GameEngine _engine;                         // Игровой движок
QuestionProvider _questionProvider;          // Провайдер вопросов
GameServer _server;                         // TCP-сервер
AudioManager _audioManager;                 // Аудио-менеджер
WebRemoteController? _webRemote;            // Web-пульт
AiHostService? _aiHost;                     // AI-ведущая
StatsAnalyzer _statsAnalyzer;               // Анализатор статистики
DispatcherTimer _roundTimer;                // Таймер раунда
DispatcherTimer _autoBotTimer;              // Авто-бот
bool _isSessionStarted;                     // Сессия запущена?
bool _eliminationPerformedThisRound;        // Исключение выполнено?
bool _nextRoundUsed;                        // CLOSE ROUND нажат?
int _analyticsFilterRound;                  // Фильтр раунда в аналитике
string _currentLanguage;                    // "RU" / "EN"
bool _isExpressVoting;                      // Экспресс-голосование?
bool _isLoadingSettings;                    // Загрузка настроек?
bool _isUIReady;                            // UI инициализирован?
```

### Логика доступности кнопки READY

```csharp
bool closeRoundDone = _nextRoundUsed
    || _engine.CurrentRound == 0
    || (_engine.CurrentRound == 1 && !_eliminationPerformedThisRound);

bool readyEnabled = _isSessionStarted
    && !isPreGame
    && !isFinalReachedForReady
    && closeRoundDone
    && (state == GameState.Idle || state == GameState.RulesExplanation);
```

- Доступна перед Раундом 1 (`CurrentRound == 0` или `CurrentRound == 1` до исключения)
- Доступна после CLOSE ROUND (`_nextRoundUsed = true`)
- Недоступна во время PRE-GAME, раунда, голосования
- Приглушённый вид, если ожидает CLOSE ROUND (`readyPending`)

---

## 9. СИСТЕМА ГОЛОСОВАНИЯ

### Film-Style (съёмочное) голосование

Основной режим. Управляется через 5-шаговый процесс в `FilmVotingPanel`.

#### Шаг 1: Отбивка → Стоп Мотор (`BtnFilmSting_Click`)
- Аудио: `1_sting4_motor_off.mp3`
- Оператор выбирает голоса в панели «ГОЛОСУЕТ ЗА»
- Кнопка ПРИНЯТЬ фиксирует голоса

#### Шаг 2: Мотор идёт (`BtnFilmMotor_Click`)
- Аудио: `2_sting4_motor_on.mp3`

#### Шаг 3: Поднять таблички (`BtnFilmReveal_Click`)
- Аудио: `3_voting_reveal.mp3`
- Подсчёт голосов: `voteCounts[target]++`
- Определение максимума голосов
- Обработка ничьей (сильнейшее звено решает)

#### Шаг 4: Обсуждение (`BtnFilmDiscussion_Click`)
- Аудио: `4_voting_discussion.mp3`
- Показ панели исключения

#### Шаг 5: Прощайте (`BtnFilmEliminate_Click`)
- Аудио: `5_walkofshame+after.mp3`
- `EliminatePlayer(target)`
- Обновление UI, статистики, списка игроков

### Express-голосование

Альтернативный быстрый режим. Бот автоматически голосует за слабейшее звено.

### Панель «ГОЛОСУЕТ ЗА»

- `ItemsControl` (не DataGrid) — для надёжной кликабельности ComboBox
- Каждый активный игрок видит список кандидатов (все кроме себя)
- Кнопка **ПРИНЯТЬ** (`BtnAcceptVotes_Click`):
  - Блокирует все ComboBox через `SetVotePanelComboBoxesEnabled(false)`
  - Устанавливает `IsVoteLocked = true` для всех строк
  - Меняет текст на «✓ Принято», opacity 0.8
- При новом голосовании — всё сбрасывается в `OpenFilmVoting()`

### Обработка ничьей

Если два (или более) игрока набрали одинаковое количество голосов:
1. Определяется **сильнейшее звено** раунда
2. Сильнейшему звену предлагается выбрать из связанных кандидатов
3. ComboBox с кандидатами появляется в панели исключения

---

## 10. ФИНАЛЬНАЯ ДУЭЛЬ (Head-to-Head)

### Инициализация

```csharp
_engine.StartFinalDuel(player1StartsFirst: true);
```

### Структура

- **5 пар** обычных вопросов (10 вопросов всего)
- Игроки отвечают **по очереди**
- После 5 пар: если ничья → **Sudden Death**
- Sudden Death: пары продолжаются, пока один не ответит верно, а другой — неверно

### UI элементы

- `P1_C1..P1_C5` — кружочки игрока 1 (зелёный = верно, красный = неверно)
- `P2_C1..P2_C5` — кружочки игрока 2
- `BtnDuelCorrect` / `BtnDuelWrong` — кнопки ответов
- `TxtDuelQuestion` — текст вопроса
- `TxtPlayer1Name` / `TxtPlayer2Name` — имена финалистов

### Подсчёт

```csharp
Player1FinalScores = [true, false, true, true, null]  // 3 верно
Player2FinalScores = [true, true, false, true, null]   // 3 верно → Sudden Death
```

### Завершение

- `FinalDuelEnded` → `OnFinalDuelEnded()`
- Аудио: `duel_winner.mp3`
- Оверлей `WinWinner` с золотым свечением

---

## 11. АНАЛИТИКА И СТАТИСТИКА

### StatsAnalyzer

Анализирует данные из `GameEngine.PlayerStatistics`.

#### Метрики игрока за раунд (PlayerRoundStats)

| Метрика | Описание |
|---------|----------|
| `CorrectAnswers` | Верные ответы |
| `IncorrectAnswers` | Неверные ответы |
| `Passes` | Пасы |
| `BankedMoney` | Положено в банк |
| `BankPressCount` | Количество нажатий БАНК |
| `ExactDroppedMoney` | Точная сумма потерь |
| `TotalQuestions` | Всего вопросов |
| `SuccessPercentage` | % верных ответов |
| `AverageBankAmount` | Средняя сумма банка |

#### Определение ролей

**Сильное звено** (StrongestLink):
```
Score = (CorrectAnswers - TotalMistakes) × 10000 + BankedMoney
```
Tiebreaker: CorrectAnswers → BankedMoney

**Слабое звено** (WeakestLink):
- Исключая StrongestLink
- Приоритет: `ExactDroppedMoney ≥ 70% MaxPossibleBank` → `TotalMistakes` → меньше BankedMoney и CorrectAnswers

**Паникёр** (PanicBanker): минимальный `AverageBankAmount` при `BankPressCount > 0`

**Паразит** (Parasite): `ExactDroppedMoney > 0` и `BankedMoney - ExactDroppedMoney < 0`

#### Прогноз выбывания

`EliminationPrediction` = WeakestLink, если нет иммунитета (0 ошибок).

### AnalyticsRow (модель таблицы)

```csharp
public class AnalyticsRow : INotifyPropertyChanged
{
    string Name;
    int CorrectAnswers, WrongAnswers, MoneyLost, PassCount;
    string BankedAmount, Prediction;
    bool IsWeakest, IsStrongest, IsActivePlayer;
    List<string> AvailableTargets;
    string SelectedVote;
    bool IsVoteLocked;
}
```

### Фильтр по раундам

- Кнопки R1–R7 в панели фильтра
- При открытии голосования фильтр автоматически переключается на текущий раунд
- `_analyticsFilterRound` определяет, какой раунд показывать

### Экспорт

- **CSV**: `StatsExporter.ExportCSV(filePath)` — таблица с разделителями
- **HTML**: `StatsExporter.ExportHTML(filePath)` — для печати в PDF

---

## 12. ЭКРАНЫ ВЕДУЩЕГО (Host Screens)

4 варианта экрана-телесуфлёра для ведущей.

### HostScreen (Classic)

- Левая колонка: денежная цепочка (подсвечивается текущий шаг)
- Правая колонка: вопрос + ответ
- Нижняя панель: «в банке», «следующая сумма», таймер, «забанковано»

### HostScreenModern

- Верхняя панель: имя игрока, таймер, банк, «играют за»
- Центр: вопрос и ответ

### HostScreenPremium

- Анимации входа/выхода (QuestionEntrance, BarEntrance)
- Градиентный фон

### HostScreenModernPremium

- Комбинация Modern + Premium
- Анимации + верхняя панель с контекстным меню

### Синхронизация

Все экраны получают данные через TCP:
```
UPDATE_QUESTION|текст
UPDATE_ANSWER|ответ
UPDATE_CHAIN|индекс
UPDATE_TIMER|секунды
UPDATE_BANK|раунд|общий
SET_STATE|состояние
HOST_MESSAGE|текст
```

---

## 13. ЭКРАН ЗРИТЕЛЕЙ (AudienceScreen)

- Разрешение: 1920×1080 (fullscreen)
- Левая часть: денежная цепочка (текстовый список)
- Правая часть: таймер + текущий банк
- Финальная дуэль: оверлей с двумя табличками имён и значками

---

## 14. ТРАНСЛЯЦИЯ (BroadcastScreen / BroadcastWindow)

### BroadcastWindow

- Хромакей (зелёный фон `#00B140` / `TVSafeChroma`)
- Левая колонка: Canvas с цепочкой
- Финальная дуэль: оверлей
- Оверлей банка

### BroadcastScreen (UserControl)

- 3 колонки: цепочка, контент, таймер
- Информация о раунде
- Текст вопроса
- Оверлей сообщений ведущей

### TV-Safe цвета

Специальная палитра для корректного отображения на телевизорах:
```
TVSafeBackground: #050510
TVSafeDark:       #1A1A1A
TVSafePanel:      #161616
TVSafeQuestionBg: #0A1128
TVSafeAnswer:     #C10000
TVSafeCyan:       #00CCCC
TVSafeTimer:      #006400
TVSafeBankNow:    #0000B8
TVSafeTotal:      #4A0020
TVSafeChroma:     #00B140
```

---

## 15. АУДИО-СИСТЕМА (AudioManager)

### Класс AudioManager

Основан на **NAudio**. Управляет фоновой музыкой и звуковыми эффектами.

### Ключевые методы

| Метод | Описание |
|-------|----------|
| `Play(path, loop)` | Воспроизведение файла (с опциональным зацикливанием) |
| `Stop()` | Остановка воспроизведения |
| `PlayOneShotThenGeneralBedWithCrossfadeAsync(shot, bed, fadeSeconds)` | Одноразовый трек → кроссфейд → фоновый трек |

### Свойства

| Свойство | Тип | Описание |
|----------|-----|----------|
| `MusicVolume` | `float` | Громкость музыки (0.0–1.0) |
| `SfxVolume` | `float` | Громкость SFX (0.0–1.0) |
| `OnMainPlaybackCompleted` | `Action?` | Callback по завершении трека |

### LoopStream

Обёртка `WaveStream` для бесконечного зацикливания аудио.

### Аудио-файлы

| Категория | Файлы |
|-----------|-------|
| **Главная тема** | `main_theme_full.mp3` |
| **Вступление** | `intro_track_1st/2nd/3rd.mp3` |
| **Раунды** | `Round_bed_(130–230).mp3` |
| **Общий фон** | `general_bed.mp3` |
| **Голосование** | `VOTING TRACKS/1–5_*.mp3` |
| **SFX** | `ROUND SFX/BANK.mp3, CORRECT.mp3, WRONG.mp3` |
| **Исключение** | `walk_of_shame.mp3`, `5_walkofshame+after.mp3` |
| **Финал** | `final_round_*.mp3`, `duel_winner.mp3` |

---

## 16. СЕТЕВОЕ ВЗАИМОДЕЙСТВИЕ

### TCP-сервер (GameServer)

- Порты: 8888 (основной), fallback 8889–8892
- Протокол: текстовые строки через TCP (UTF-8, разделитель `\n`)
- Рассылка: `Broadcast(message)` → все подключённые клиенты

### Протокол сообщений

| Сообщение | Направление | Описание |
|-----------|------------|----------|
| `SET_STATE\|{state}` | Сервер → Клиент | Смена состояния игры |
| `UPDATE_QUESTION\|{text}` | Сервер → Клиент | Новый вопрос |
| `UPDATE_ANSWER\|{answer}` | Сервер → Клиент | Ответ на вопрос |
| `UPDATE_CHAIN\|{index}` | Сервер → Клиент | Позиция в цепочке |
| `UPDATE_TIMER\|{seconds}` | Сервер → Клиент | Оставшееся время |
| `UPDATE_BANK\|{round}\|{total}` | Сервер → Клиент | Банк раунда и общий |
| `UPDATE_ROUND\|{number}` | Сервер → Клиент | Номер раунда |
| `HOST_MESSAGE\|{text}` | Сервер → Клиент | Сообщение ведущей |
| `ELIMINATE\|{name}` | Сервер → Клиент | Исключение игрока |
| `CLEAR_ELIMINATION` | Сервер → Клиент | Сброс исключения |
| `CORRECT` | Клиент → Сервер | Правильный ответ (от пульта) |
| `WRONG` | Клиент → Сервер | Неправильный ответ |
| `BANK` | Клиент → Сервер | Банк |
| `PASS` | Клиент → Сервер | Пас |
| `NEXT` | Клиент → Сервер | Следующий вопрос |

### TCP-клиент (GameClient)

```csharp
var client = new GameClient();
client.MessageReceived += (msg) => { /* обработка */ };
client.Connect("192.168.1.100", 8888);
```

---

## 17. WEB-ПУЛЬТ (WebRemoteController)

### HTTP-сервер

- Порты: 8080 (основной), fallback 8081
- Протокол: HTTP GET
- Автогенерация QR-кода для подключения

### Эндпоинты

| URL | Метод | Описание |
|-----|-------|----------|
| `/` | GET | HTML-страница пульта (iPad UI) |
| `/host` | GET | HTML-страница для ведущей |
| `/api/state` | GET | JSON: текущее состояние игры |
| `/api/host-state` | GET | JSON: данные для экрана ведущей |
| `/api/command?action={cmd}` | GET | Выполнение команды (BANK, CORRECT, WRONG, PASS) |

### Безопасность

- Команды BANK/CORRECT/WRONG/PASS блокируются, если `isActive = false` (не в состоянии Playing)

---

## 18. AI-ИНТЕГРАЦИЯ

### AiHostService (AI-ведущая «Мария Киселёва»)

- **Генерация текста**: Google Gemini API
- **Озвучка**: ElevenLabs TTS API
- **Воспроизведение**: NAudio

#### Игровые события

| Метод | Когда вызывается |
|-------|-----------------|
| `OnGameIntroAsync()` | Начало шоу |
| `OnRulesAsync()` | Правила игры |
| `OnRoundStartAsync(round)` | Начало раунда |
| `OnCorrectAnswer(player, chain)` | Верный ответ (при chain ≥ 8) |
| `OnWrongAnswerAsync(player, answer)` | Неверный ответ |
| `OnBank(player, amount)` | Банк |
| `OnTimeUpAsync()` | Время раунда истекло |
| `OnFullBankAsync()` | Достигнут MAX BANK (50 000) |
| `OnRoundResultAsync(round, banked, burned)` | Итоги раунда |
| `OnVotingStartAsync(round)` | Начало голосования |
| `OnPlayerEliminatedAsync(name, round)` | Исключение игрока |
| `ReadQuestionAsync(text)` | Чтение вопроса вслух |

### AiBotTester (AI-бот)

- Совместим с OpenAI API (ChatGPT, DeepSeek, LM Studio, Ollama)
- Возвращает `BotDecision`: Action (`"answer"` / `"pass"` / `"bank"`), Text
- Используется для нагрузочного тестирования

### GeminiTestPlayer

- AI-игрок на Gemini API
- Автоматически отвечает на вопросы в режиме тестирования
- Интегрирован с `AutoBotTimer` (3-секундный интервал)

---

## 19. ДИЗАЙН-СИСТЕМА OBSIDIAN

### Философия

Тёмная тема в стиле Discord/Obsidian. TV-safe палитра для трансляции.

### Цветовая палитра

#### Основные поверхности

| Ресурс | Hex | Использование |
|--------|-----|---------------|
| `ObsidianBg` | `#1E1E1E` | Фон приложения |
| `ObsidianBase` | `#1F1F1F` | Базовый фон |
| `ObsidianSurface` | `#252525` | Поверхности |
| `ObsidianDeep` | `#292929` | Глубокие слои |
| `ObsidianCard` | `#2D2D2D` | Карточки |
| `ObsidianRaised` | `#333333` | Приподнятые элементы |
| `ObsidianBorder` | `#363636` | Границы |
| `ObsidianBorderStrong` | `#3D3D3D` | Усиленные границы |
| `ObsidianBorderLight` | `#4D4D4D` | Лёгкие границы |

#### Текст

| Ресурс | Hex | Использование |
|--------|-----|---------------|
| `ObsidianTextPrimary` | `#E0E0E0` | Основной текст |
| `ObsidianText` | `#E8E8E8` | Светлый текст |
| `ObsidianTextSecondary` | `#999999` | Вторичный текст |
| `ObsidianTextMuted` | `#555555` | Приглушённый |
| `ObsidianTextDisabled` | `#717171` | Отключённый |

#### Семантические цвета

| Ресурс | Hex | Значение |
|--------|-----|----------|
| `ColorSuccess` | `#22C55E` | Успех / зелёный |
| `ColorDanger` | `#EF4444` | Опасность / красный |
| `ColorWarning` | `#F59E0B` | Предупреждение / жёлтый |
| `ColorInfo` | `#4F6BED` | Информация / синий |
| `ColorGold` | `#FFD700` | Золотой |
| `ColorBlurple` | `#5865F2` | Discord-стиль |
| `ColorTeal` | `#2DD4BF` | Бирюзовый |

#### Fire Buttons (игровые кнопки)

| Ресурс | Hex | Действие |
|--------|-----|----------|
| `FireButtonCorrect` | `#23a559` | ВЕРНО |
| `FireButtonBank` | `#1565C0` | БАНК |
| `FireButtonWrong` | `#da373c` | НЕВЕРНО |
| `FireButtonPass` | `#4e5058` | ПАС |

### Типографика

| Ресурс | Размер | Использование |
|--------|--------|---------------|
| `FontSizeBroadcastHuge` | 180 | Огромный текст трансляции |
| `FontSizeBroadcastMax` | 130 | Максимальный |
| `FontSizeBroadcastQuestion` | 96 | Вопрос на экране |
| `FontSizeQuestion` | 64 | Вопрос в операторской |
| `FontSizeDisplay` | 32 | Дисплейный |
| `FontSizeTitle` | 24 | Заголовок |
| `FontSizeBody` | 13 | Основной текст |
| `FontSizeSmall` | 12 | Мелкий |
| `FontSizeCaption` | 11 | Подпись |
| `FontSizeXXXSmall` | 9 | Микротекст |

### Отступы (Spacing)

| Ресурс | Значение |
|--------|----------|
| `SpacingXXS` | 2 |
| `SpacingXS` | 4 |
| `SpacingS` | 8 |
| `SpacingM` | 12 |
| `SpacingL` | 16 |
| `SpacingXL` | 20 |
| `SpacingXXL` | 24 |
| `Spacing3XL` | 32 |
| `Spacing4XL` | 40 |

### Скругления (CornerRadius)

| Ресурс | Значение |
|--------|----------|
| `CornerRadiusNone` | 0 |
| `CornerRadiusSmall` | 2 |
| `CornerRadiusDefault` | 3 |
| `CornerRadiusStandard` | 4 |
| `CornerRadiusMediumSmall` | 5 |
| `CornerRadiusMedium` | 6 |
| `CornerRadiusLarge` | 8 |
| `CornerRadiusXLarge` | 10 |
| `CornerRadiusRound` | 12 |
| `CornerRadiusPill` | 16 |
| `CornerRadiusBroadcast` | 20 |
| `CornerRadiusCircle` | 45 |

### Именованные стили

| Стиль | Описание |
|-------|----------|
| `FlatButton` | Плоская кнопка без рамки |
| `GhostButton` | Прозрачная кнопка |
| `DangerGhostButton` | Красная прозрачная кнопка |
| `AccentButton` | Акцентная кнопка |
| `RedButton` | Красная кнопка |
| `ObsidianCheckBox` | Чекбокс в стиле Obsidian |
| `ObsidianTextBox` | Текстовое поле |
| `FlatToggleStyle` | Плоский переключатель |
| `SurfaceCard` | Карточка-поверхность |
| `MetricCard` | Карточка метрики |
| `FireCorrectButton` | Кнопка ВЕРНО (зелёная, 120×60) |
| `FireBankButton` | Кнопка БАНК (синяя) |
| `FireWrongButton` | Кнопка НЕВЕРНО (красная) |
| `FirePassButton` | Кнопка ПАС (серая) |
| `WindowControlButton` | Кнопка окна (свернуть/развернуть) |
| `WindowCloseButton` | Кнопка закрытия окна |

---

## 20. СИСТЕМА ВОПРОСОВ

### QuestionProvider

Загружает вопросы из JSON и выдаёт случайным образом без повторений.

#### Методы

| Метод | Описание |
|-------|----------|
| `LoadQuestions(filePath)` | Загрузка из JSON-файла |
| `GetRandomQuestion()` | Случайный неиспользованный вопрос |
| `ResetSession()` | Сброс использованных вопросов |
| `ValidateDatabase()` | Проверка базы: пустые поля, раунды 1–8, мин. количество |

### Формат questions.json

```json
[
  {
    "Id": 1,
    "Text": "В каком году Юрий Гагарин полетел в космос?",
    "Answer": "1961",
    "AcceptableAnswers": "1961 год; шестьдесят первый"
  },
  {
    "Id": 2,
    "Text": "Столица Франции?",
    "Answer": "Париж",
    "AcceptableAnswers": ""
  }
]
```

### Формат final_questions.json

```json
[
  {
    "Id": 1,
    "Text": "Какой химический элемент имеет символ Fe?",
    "Answer": "Железо"
  }
]
```

### Языки

- `questions.json` / `final_questions.json` — русский
- `questions_en.json` / `final_questions_en.json` — английский

### Редактор вопросов (QuestionEditorWindow)

- CRUD-интерфейс для вопросов
- Load/Save JSON
- DataGrid с колонками: ID, Text, CorrectAnswer, AcceptableAnswers, Round
- Кнопки: Добавить, Удалить, Очистить

---

## 21. НАСТРОЙКИ И ПЕРСИСТЕНТНОСТЬ

### Файл настроек

Путь: `{AppDomain.CurrentDomain.BaseDirectory}/app_settings.json`

### Формат

```json
{
  "language": "RU",
  "expressVoting": true,
  "skipIntro": false,
  "roundSfx": true,
  "musicVolume": 100,
  "sfxVolume": 87
}
```

### Сохраняемые параметры

| Параметр | Тип | Описание |
|----------|-----|----------|
| `language` | string | "RU" или "EN" |
| `expressVoting` | bool | Экспресс-голосование |
| `skipIntro` | bool | FastTrack (пропуск вступления) |
| `roundSfx` | bool | Короткие звуки (BANK, CORRECT, WRONG) |
| `musicVolume` | int | Громкость музыки (0–100) |
| `sfxVolume` | int | Громкость SFX (0–100) |

### Когда сохраняется

- При закрытии окна (`OnClosed`)
- При уходе с экрана НАСТРОЙКИ (`SetCentralContext`)
- При изменении слайдеров громкости
- При изменении чекбоксов (FastTrack, RoundSfx)

### Когда загружается

- Событие `Loaded` окна (`OperatorPanel`)
- Флаг `_isLoadingSettings` предотвращает рекурсивное сохранение
- Флаг `_isUIReady` предотвращает крах при инициализации XAML

### Защита от ошибок

```csharp
private void SaveSettings()
{
    if (_isLoadingSettings || !_isUIReady) return;
    // ...
}

private void SliderMusicVolume_ValueChanged(...)
{
    if (!_isUIReady) return;
    // ...
}
```

---

## 22. ТЕСТИРОВАНИЕ И АВТОТЕСТЫ

### SelfTest

Автоматические тесты игрового движка.

#### Группы тестов

| Группа | Тесты |
|--------|-------|
| **State Machine** | Допустимые переходы, intro-цикл, блокировка недопустимых, сброс в Idle |
| **Bank Chain** | Рост цепочки, банк, WrongAnswer, Pass, RoundBurned |
| **Round Sequence** | Длительности раундов 1–6 |
| **Player Elimination** | EliminatePlayer, удаление из ActivePlayers |
| **Final Duel** | Переход в FinalDuel, 5 слотов, победа P1 |
| **QuestionProvider** | Загрузка questions.json, валидность вопросов |
| **HostScreen Parity** | Проверка совпадения методов на 4 экранах ведущего |

### Бот-тестирование

#### Полу-авто (Semi-Auto)
- 8 ботов: «Бот 1» – «Бот 8»
- 30-секундный таймер
- AutoBot включён (3 сек интервал)
- Оператор управляет переходами вручную

#### Полный автопилот (Auto)
- Все 7 раундов + финал автоматически
- Автоматическое исключение худшего
- Финал между оставшимися

### AI-тестирование (GeminiTestPlayer)

- Подключается к Gemini API
- Автоматически отвечает на вопросы
- Имитирует реальную игру

---

## 23. МАКРОПАД

### MacroPadService

Интеграция с USB-макропадом для физических кнопок.

### Раскладка

| Кнопка | Действие |
|--------|----------|
| A | ВЕРНО (Correct) |
| B | НЕВЕРНО (Wrong) |
| C | БАНК (Bank) |
| D | READY |
| F | START |

### Конфигурация

Файлы: `macropad/weakest-link-macropad.yaml`, `macropad/example-mapping.yaml`

---

## 24. УТИЛИТЫ И ВСПОМОГАТЕЛЬНЫЕ ИНСТРУМЕНТЫ

### Python-скрипты

| Скрипт | Назначение |
|--------|-----------|
| `gen_questions.py` | Генерация вопросов |
| `gen_final_questions.py` | Генерация финальных вопросов |
| `merge_questions.py` | Слияние баз вопросов |
| `add_questions.py` | Добавление вопросов |

### Tools/

| Инструмент | Назначение |
|-----------|-----------|
| `analyze_sync.py` | Анализ синхронизации аудио |
| `find_first_beat.py` | Поиск первого бита в аудио |
| `layout_editor.html` | HTML-редактор раскладки экранов |
| `MetroDetect/` | C#-утилита определения метронома |

### ColorResourceHelper

Статический класс для доступа к XAML-ресурсам из code-behind:

```csharp
SolidColorBrush brush = ColorResourceHelper.ColorInfo;
Color color = ColorResourceHelper.ColorInfo.Color;
```

Доступные свойства: `ObsidianBg`, `ObsidianSurface`, `ObsidianCard`, `ObsidianBorder`, `ObsidianTextPrimary`, `ObsidianTextSecondary`, `ObsidianTextDisabled`, `ColorSuccess`, `ColorDanger`, `ColorWarning`, `ColorInfo`, `ColorGold`, `ColorRed`, `ColorBlurple`, `ColorOrange`, `ColorPurple`, `ColorTeal`.

---

## 25. СБОРКА И ДЕПЛОЙ

### Команда сборки

```bash
dotnet publish WeakestLink.csproj -c Release -o SuperDist/net10.0-windows
```

### Выходная директория

`SuperDist/net10.0-windows/` — содержит:
- `WeakestLink.exe` — исполняемый файл
- `WeakestLink.dll` — основная сборка
- `NAudio.dll` — аудио-библиотека
- `QRCoder.dll` — генератор QR-кодов
- `questions.json` — база вопросов
- `final_questions.json` — финальные вопросы
- `app_settings.json` — настройки (создаётся при первом запуске)
- `error_log.txt` — лог ошибок
- `Assets/` — аудио и изображения

### Обработка ошибок сборки

Если `WeakestLink.exe` запущен, сборка не может перезаписать файл:
```powershell
taskkill /F /IM WeakestLink.exe
dotnet publish WeakestLink.csproj -c Release -o SuperDist/net10.0-windows
```

---

## 26. ГОРЯЧИЕ КЛАВИШИ

Обрабатываются в `Window_PreviewKeyDown`:

| Клавиша | Действие |
|---------|----------|
| `Up` / `R` / `S` | READY (если доступна) |
| `Space` / `Enter` | PLAY / Следующий вопрос |
| `Right` / `A` | ВЕРНО (Correct) |
| `Left` / `D` | НЕВЕРНО (Wrong) |
| `Down` / `B` | БАНК (Bank) |

---

## 27. МОДЕЛИ ДАННЫХ

### QuestionData

```csharp
public class QuestionData
{
    public int Id { get; set; }
    public string Text { get; set; }
    public string Answer { get; set; }
    public string AcceptableAnswers { get; set; }
    public int? Round { get; set; }
}
```

### PlayerStats

```csharp
public class PlayerStats
{
    public int CorrectAnswers { get; set; }
    public int IncorrectAnswers { get; set; }
    public int Passes { get; set; }
    public int BankedMoney { get; set; }
    public int BankPressCount { get; set; }
    public int BurnedByWrongAnswers { get; set; }
    public int ExactDroppedMoney { get; set; }
}
```

### BotDecision

```csharp
public class BotDecision
{
    public string Action { get; set; } = "pass";    // "answer" | "pass" | "bank"
    public string Text { get; set; } = "";
    public static BotDecision PassFallback => new() { Action = "pass", Text = "" };
}
```

### PlayerSetupItem (Smart Roster)

```csharp
public class PlayerSetupItem
{
    public int ConsoleNumber { get; set; }     // Номер тумбы (1–8)
    public string GameName { get; set; }       // Имя на тумбе
    public string FullName { get; set; }       // Полное имя
    public string CityDesc { get; set; }       // Город, описание
    public bool IsLocked { get; set; }         // Заблокирован?
    public string PhotoPath { get; set; }      // Путь к фото
    public double CropX { get; set; }          // Обрезка фото X
    public double CropY { get; set; }          // Обрезка фото Y
    public int Age { get; set; }               // Возраст
    public string Bio { get; set; }            // Биография
    public string PrompterLine { get; set; }   // Строка для суфлёра
}
```

### PlayerListItem (отображение в sidebar)

```csharp
public class PlayerListItem
{
    public string Name { get; set; }
    public string Initials { get; set; }
    public string StatusText { get; set; }
    public Brush StatusColor { get; set; }
    public Brush NameColor { get; set; }
    public TextDecorationCollection NameDecoration { get; set; }
    public Brush PillBackground { get; set; }
    public Brush PillForeground { get; set; }
}
```

### SettingsData (персистентность)

```csharp
private sealed class SettingsData
{
    public string Language { get; set; } = "RU";
    public bool ExpressVoting { get; set; } = true;
    public bool SkipIntro { get; set; }
    public bool RoundSfx { get; set; }
    public int MusicVolume { get; set; } = 100;
    public int SfxVolume { get; set; } = 100;
}
```

---

## 28. ПОЛНЫЙ СПРАВОЧНИК API

### GameEngine — публичные методы

| Метод | Сигнатура | Возврат |
|-------|-----------|---------|
| `TransitionTo` | `void TransitionTo(GameState newState)` | — |
| `CorrectAnswer` | `void CorrectAnswer()` | — |
| `WrongAnswer` | `void WrongAnswer()` | — |
| `Pass` | `void Pass()` | — |
| `Bank` | `void Bank()` | — |
| `ResetChain` | `void ResetChain()` | — |
| `ApplyRoundBankToTotal` | `void ApplyRoundBankToTotal()` | — |
| `NextRound` | `void NextRound()` | — |
| `PrepareNewRound` | `void PrepareNewRound()` | — |
| `FinalizeRoundSetup` | `void FinalizeRoundSetup()` | — |
| `GetNewRoundDuration` | `int GetNewRoundDuration()` | секунды |
| `GetRoundDuration` | `int GetRoundDuration()` | секунды |
| `EliminatePlayer` | `void EliminatePlayer(string name)` | — |
| `MoveToNextPlayer` | `void MoveToNextPlayer()` | — |
| `GetStartingPlayerForRound` | `string GetStartingPlayerForRound(int round)` | имя |
| `LoadFinalQuestions` | `void LoadFinalQuestions(IEnumerable<QuestionData>)` | — |
| `StartFinalDuel` | `void StartFinalDuel(bool player1StartsFirst)` | — |
| `GetNextFinalQuestion` | `string GetNextFinalQuestion()` | текст вопроса |
| `ProcessFinalAnswer` | `void ProcessFinalAnswer(bool isCorrect)` | — |
| `ResetRoundCounter` | `void ResetRoundCounter()` | — |
| `ResetGame` | `void ResetGame()` | — |

### StatsAnalyzer — публичные методы

| Метод | Сигнатура | Возврат |
|-------|-----------|---------|
| `AnalyzeRound` | `RoundAnalytics AnalyzeRound(int round)` | аналитика раунда |
| `IsTieDetected` | `bool IsTieDetected(...)` | есть ли ничья |
| `GetTiedPlayerNames` | `List<string> GetTiedPlayerNames(...)` | имена в ничьей |
| `GetStrongestLinkName` | `string GetStrongestLinkName(int round)` | имя сильнейшего |
| `GetSortedPlayersByPerformanceDesc` | `List<string> GetSortedPlayersByPerformanceDesc(int round)` | ранжирование |

### QuestionProvider — публичные методы

| Метод | Сигнатура | Возврат |
|-------|-----------|---------|
| `LoadQuestions` | `void LoadQuestions(string filePath)` | — |
| `GetRandomQuestion` | `QuestionData? GetRandomQuestion()` | вопрос или null |
| `ResetSession` | `void ResetSession()` | — |
| `ValidateDatabase` | `List<string> ValidateDatabase()` | список ошибок |

### AudioManager — публичные методы

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `Play` | `void Play(string path, bool loop)` | Воспроизвести файл |
| `Stop` | `void Stop()` | Остановить |
| `PlayOneShotThenGeneralBedWithCrossfadeAsync` | `Task PlayOneShotThenGeneralBedWithCrossfadeAsync(string shot, string bed, double fadeSeconds)` | Кроссфейд |

### GameServer — публичные методы

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `Start` | `void Start()` | Запуск сервера |
| `Stop` | `void Stop()` | Остановка |
| `Broadcast` | `void Broadcast(string message)` | Рассылка всем клиентам |

### WebRemoteController — публичные методы

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `Start` | `void Start()` | Запуск HTTP-сервера |
| `Stop` | `void Stop()` | Остановка |

---

## 29. ИЗВЕСТНЫЕ ОСОБЕННОСТИ И РЕШЕНИЯ

### Проблема: ComboBox в DataGrid не кликается

**Решение**: Голосование вынесено из `DataGrid` в отдельный `ItemsControl` (`VoteEntriesPanel`) рядом с таблицей. Это гарантирует надёжную кликабельность ComboBox.

### Проблема: XAML TargetInvocationException при старте

**Причина**: Обработчик `SliderMusicVolume_ValueChanged` вызывается во время `InitializeComponent()`, когда UI-элементы ещё не инициализированы. `SaveSettings()` → `Log()` → NullReferenceException.

**Решение**: Флаг `_isUIReady`, устанавливаемый после `InitializeComponent()`. Все обработчики проверяют `if (!_isUIReady) return;`.

### Проблема: READY недоступна после старта сессии

**Причина**: `BtnStartRound.IsEnabled = false` устанавливается в `BtnStartSession_Click`, но `UpdateButtonStates()` не вызывается, и пересчёт `readyEnabled` не происходит.

**Решение**: Добавлен вызов `UpdateButtonStates()` в конце `BtnStartSession_Click`.

### Проблема: Процесс блокирует сборку

**Причина**: `WeakestLink.exe` запущен и блокирует перезапись DLL/EXE.

**Решение**: `taskkill /F /IM WeakestLink.exe` перед `dotnet publish`.

### Проблема: Слабейшее звено не может голосовать

**Причина**: ComboBox в DataGrid не получает фокус для строки с пометкой «СЛАБЕЙШИЙ».

**Решение**: Переход на отдельный `ItemsControl` для голосования. Все игроки (включая слабейшего) могут голосовать.

### Проблема: Настройки не сохраняются

**Причина**: `LoadSettings()` вызывался в конструкторе до инициализации UI, `SaveSettings()` не включал все параметры.

**Решение**: `LoadSettings()` перенесён в событие `Loaded`. Класс `SettingsData` включает все 6 параметров. Сохранение происходит при закрытии окна, уходе с экрана настроек и при изменении каждого элемента управления.

---

> **Версия энциклопедии**: 2.0  
> **Дата**: 18 марта 2026  
> **Платформа**: .NET 10.0 / WPF / Windows  
> **Размер кодовой базы**: ~12 000 строк C# + ~4 000 строк XAML
