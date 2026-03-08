# Слабое звено (Weakest Link) — Документ ввода в курс дела для Gemini

**Цель:** Полноценное погружение AI-ассистента (Gemini) в проект для эффективной помощи в разработке, отладке и расширении функциональности.

---

## 1. Что это за проект

**«Слабое звено»** — программно-аппаратный комплекс для проведения телевизионной игры «Слабое звено». Это WPF-приложение на C# (.NET 10), включающее:

- **Пульт оператора** — управление игровым процессом (ответы, банк, голосование, исключение)
- **Экран ведущего** — отображение вопросов, таймера, цепочки, банка, финальной дуэли
- **Сетевую синхронизацию** — пульт как TCP-сервер, экраны как клиенты
- **Аудиосистему** — фоновые треки, дикторские реплики, кроссфейды
- **Аналитику раунда** — сильное/слабое звено, прогноз выбывания, статистика

---

## 2. Стек и сборка

- **Платформа:** .NET 10, WPF, C#
- **Сборка:** `dotnet build` (из корня проекта)
- **Запуск:** `dotnet run` или `WeakestLink.exe` из `bin\Debug\net10.0-windows\`
- **Зависимости:** NAudio (звук), QRCoder

---

## 3. Структура проекта

```
WeakestLink/
├── Views/              # UI
│   ├── OperatorPanel.xaml(.cs)    # Пульт оператора (главное окно)
│   ├── HostScreen.xaml(.cs)       # Экран ведущего
│   ├── RoundStatsWindow.xaml(.cs) # Окно аналитики раунда
│   ├── WinWinner.xaml(.cs)        # Экран победителя
│   └── QuestionEditorWindow.xaml(.cs)
├── Core/               # Ядро
│   ├── GameEngine.cs   # Игровой движок, state machine, финансы
│   ├── GameState.cs    # Перечисление состояний
│   ├── Models/         # QuestionModel, QuestionData
│   └── Analytics/
│       └── StatsAnalyzer.cs  # Анализ раунда, прогноз выбывания
├── Network/            # Сеть
│   ├── GameServer.cs   # TCP-сервер (порт 8888)
│   └── WebRemoteController.cs
├── Audio/              # Звук
│   ├── AudioManager.cs # Воспроизведение, beds, one-shot, кроссфейд
│   └── LoopStream.cs   # Зацикливание аудио
├── QuestionEditor/     # Отдельный проект редактора вопросов
├── Assets/Audio/       # MP3-файлы
├── questions.json      # Вопросы игры
├── final_questions.json # Вопросы финальной дуэли
└── docs/               # Документация
```

---

## 4. State Machine (игровые состояния)

Состояния: **Idle** → **RoundReady** → **Playing** → **Voting** → **Elimination** → (RoundReady или FinalDuel) → **FinalDuel** → Idle

| Состояние    | Описание |
|--------------|----------|
| **Idle**     | Ожидание, игра не запущена |
| **RoundReady** | Раунд подготовлен, таймер установлен, ожидание нажатия PLAY |
| **Playing**  | Идёт раунд, таймер тикает, принимаются ответы (Верно/Пас/Неверно/Банк) |
| **Voting**   | Раунд завершён, голосование за исключение игрока |
| **Elimination** | Момент исключения (walk of shame, переход к следующему раунду) |
| **FinalDuel** | Финальная дуэль двух игроков |

**Переходы:**
- Playing → Voting: по таймеру или полному банку (50 000 ₽)
- Voting → Elimination: нажатие «ИСКЛЮЧИТЬ»
- Elimination → RoundReady или FinalDuel: нажатие «NEXT ROUND»

**Исключение:** при 2 игроках (предфинал, раунд 7) голосования нет — Playing → Idle, банк удваивается, показ «ПЕРЕЙТИ К ФИНАЛУ».

---

## 5. Финансовая логика

- **Цепочка (BankChain):** 1000 → 2000 → 5000 → 10000 → 20000 → 30000 → 40000 → 50000
- **Верный ответ:** шаг вперёд по цепочке, ход следующему игроку
- **Пас / Неверно:** цепочка сбрасывается в 0, сумма на цепочке «сгорает» (RoundBurned)
- **Банк:** деньги с цепочки идут в RoundBank раунда, цепочка обнуляется

---

## 6. Панель оператора (OperatorPanel)

### Кнопки управления эфиром
- **READY** — подготовка раунда (RoundReady)
- **PLAY** — старт раунда (Playing)
- **VOTING START** — старт голосования: трек `voting_dictor_part.mp3`, таймер 29 сек, строка «ГОЛОСОВАНИЕ НАЧАЛОСЬ»
- **VOTING END** — конец голосования: трек `voting_host_part.mp3`, таймер 15 сек, строка «ГОЛОСОВАНИЕ ПОДХОДИТ К КОНЦУ»
- **ELIMINATION** — переход к моменту исключения
- **NEXT ROUND** — подготовка нового раунда (PrepareNewRound)
- **BEFORE VOTING** — после полного банка, перед голосованием (показ при 2+ игроках)
- **ИСКЛЮЧИТЬ** — исключение выбранного игрока (ComboBox + кнопка)

### Кнопки ответов (в Playing)
- **ВЕРНО** — CorrectAnswer()
- **ПАС** — Pass()
- **НЕВЕРНО** — WrongAnswer()
- **БАНК** — Bank()

### Таймеры
- Раунд: длительность по раунду (150, 140, 130… сек)
- Голосование: VOTING START → 29 сек, VOTING END → 15 сек (отдельные запуски)

---

## 7. Аудиосистема (AudioManager)

### Основные методы
- **Play(path, loop)** — основной канал, заменяет текущее воспроизведение
- **PlayBed(fileName)** — bed из `Assets/Audio/`, зацикливание
- **PlayBedOneShotAsync(fileName)** — один трек без цикла, Task по окончании
- **PlayOneShotThenGeneralBedWithCrossfadeAsync(oneShot, bed, crossfadeSec)** — one-shot, затем general_bed с плавным нарастанием в последние 2.5 сек

### Кроссфейд
- За 2.5 сек до окончания one-shot запускается `general_bed.mp3` с громкостью 0
- Громкость плавно нарастает до 100% за 2.5 сек
- Используется: voting_dictor_part → general_bed, round_full_bank → general_bed, walk_of_shame → general_bed

### Ключевые треки (Assets/Audio/)
| Трек | Назначение |
|------|------------|
| general_bed.mp3 | Основная подложка |
| playgame_with_general_bed.mp3 | При переходе в RoundReady |
| Round_bed_(230–130).mp3 | По раундам (230 bpm и т.д.) |
| voting_dictor_part.mp3 | VOTING START |
| voting_host_part.mp3 | VOTING END |
| round_full_bank.mp3 | Полный банк раунда |
| updated_walk_of_shame_bed.mp3 | Уход исключённого |
| final_round_*.mp3 | Финал, дуэль |
| sudden_death_bed.mp3 | Внезапная смерть |

---

## 8. Сеть

- **GameServer** — порт 8888, рассылка строк (завершённых `\n`)
- **Формат:** `КОМАНДА|Поле1|Поле2|...`

| Команда | Описание |
|---------|----------|
| STATE / SET_STATE | Состояние игры |
| QUESTION\|текст\|ответ\|номер | Вопрос для HostScreen |
| UPDATE_BANK\|chainIndex\|banked | Банк |
| UPDATE_TIMER\|строка | Таймер |
| ELIMINATE\|имя | Исключение игрока |
| CLEAR_ELIMINATION | Сброс экрана исключения |
| DUEL_UPDATE\|name1\|name2\|s1\|s2 | Финал дуэли |
| WINNER\|имя | Победитель |
| HOST_MESSAGE\|текст | Сообщение на экран |

---

## 9. Аналитика раунда

- **StatsAnalyzer.AnalyzeRound(roundDuration)** — анализ текущего раунда
- **Сильное звено** — максимум верных, при равенстве — максимум денег в банк
- **Слабое звено** — максимум ошибок (неверные + пасы), при равенстве — минимум в банк
- **Прогноз выбывания** — взвешенная система (слабое звено, потери, успешность и т.д.)
- **Panic Banker** — минимальный средний банк при BankPressCount > 0

**RoundStatsWindow** — автооткрытие при Voting, автозакрытие при выходе из Voting.

---

## 10. Конвенции кода

- **UI:** изменения WPF — через `Dispatcher.Invoke` / `Dispatcher.BeginInvoke`
- **Состояние:** единственный источник — `GameEngine.CurrentState`, переходы — `TransitionTo()`
- **XAML:** элементы с `x:Name` должны совпадать с обращением в коде

---

## 11. Документация (связанные файлы)

- **RULES.md** — правила и конвенции
- **PROGRESS_WL.md** — статус разработки
- **files/VOTING_AND_STATS_SYSTEM.md** — голосование и аналитика
- **ANALYTICS_DOCUMENTATION.md** — система аналитики
- **ROUND_PREPARATION_DOCUMENTATION.md** — подготовка раунда
- **ELIMINATION_PREDICTION_FIX.md**, **PANIC_BANKER_METRIC.md** — детали алгоритмов

---

## 12. Последние существенные изменения (актуальное поведение)

1. **VOTING START / VOTING END** вместо одной кнопки VOTING:
   - VOTING START: трек `voting_dictor_part.mp3`, таймер 29 сек
   - VOTING END: трек `voting_host_part.mp3`, таймер 15 сек

2. **Кроссфейд:** general_bed.mp3 запускается с нарастанием в последние 2.5 сек предыдущего трека (voting_dictor_part, round_full_bank, walk_of_shame).

3. **Таймеры голосования:** раздельные — 29 сек (VOTING START) и 15 сек (VOTING END).

4. **PlayerStats:** добавлены `ExactDroppedMoney`, `BurnedByWrongAnswers` — учёт точных потерь при неверных ответах и пасах.

---

---
*Документ обновлён: 06.03.2026*
*Создан для ввода в курс дела AI-ассистента Gemini. Обновляй при изменении архитектуры и ключевых сценариев.*
