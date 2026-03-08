# Подробный отчёт о прогрессе: «Слабое звено» (Weakest Link)

**Статус:** PRODUCTION READY
**Обновлено:** 07.03.2026
**Фреймворк:** .NET WPF (C# / XAML)

---

## Техническая реализация

### 1. Ядро игры (`Core/GameEngine.cs`) — 100%

* **Игровая логика:** Денежная цепочка (1 000 — 50 000 ₽), `RoundBank`, `TotalBank`.
* **Менеджмент раундов:** 7 раундов, динамическое время (150 → 90 сек, −10 сек/раунд).
* **Статистика игроков:** `PlayerStats` — верные, неверные, пасы, банк, BurnedByWrongAnswers, ExactDroppedMoney.
* **State Machine:** 13 состояний (`Idle`, `IntroOpening`, `IntroNarrative`, `PlayerIntro`, `RulesExplanation`, `RoundReady`, `Playing`, `RoundSummary`, `Voting`, `Discussion`, `Reveal`, `Elimination`, `FinalDuel`).
* **Финальная дуэль:** 5 пар вопросов, досрочная победа (Early Math Termination), Sudden Death при ничье 5:5.
* **Удвоение банка:** При `ActivePlayers.Count == 2` банк раунда ×2 с HOST_MESSAGE.
* **Свойства StatusBar:** `LastActionText`, `StatusDescription`, `InstructionText` — три строки для штурмана оператора.
* **LastStrongestLinkName:** Передача имени Сильного звена в финальную дуэль.
* **Безопасность:** `ResetChain()`, `ResetGame()`, `TransitionTo()` с валидацией переходов.

### 2. Сетевая часть (`Network/`) — 100%

* **TCP Server:** Пульт оператора (порт 8888).
* **TCP Client:** Экраны ведущего, студийные мониторы.
* **Протокол:** Строковые команды — `SET_STATE`, `UPDATE_BANK`, `UPDATE_TIMER`, `QUESTION`, `CURRENT_PLAYER`, `ELIMINATE`, `DUEL_UPDATE`, `WINNER`, `HOST_MESSAGE`, `HOST_MESSAGE|CLEAR`.
* **HOST_MESSAGE:** Суфлёр ведущего — динамические тексты для каждой фазы игры (PRE-GAME, раунд, голосование, дуэль, победа).
* **Устойчивость:** Обработка разрывов соединений.

### 3. Аналитика (`Core/Analytics/StatsAnalyzer.cs`) — 100%

* **AnalyzeRound:** Принимает `savedRoundBank` для корректного отображения банка.
* **GetStrongestLinkName / GetWeakestLinkName:** Определение Сильного и Слабого звена по формуле.
* **IsTieDetected:** Обнаружение ничьей при голосовании.
* **GetTiedPlayerNames:** Список игроков с максимальным равным числом голосов.
* **Panic Banker:** Игрок с наименьшим средним банком.
* **Прогноз на выбывание:** С объяснением причины.

---

## Интерфейсы

### 1. Пульт Оператора (`Views/OperatorPanel.xaml`) — 100%

**PRE-GAME блок:**
* Горизонтальная панель: OPENING → INTRO 1 → INTRO 2 → RULES → READY.
* Каждая кнопка запускает свой аудиотрек, меняет GameState, отправляет HOST_MESSAGE на экран ведущего.
* `PrepareNewRound()` вызывается автоматически при RULES (150 сек, первый по алфавиту).

**Игровой блок:**
* **START O'CLOCK:** Ярко-зелёная кнопка запуска таймера. Доступна только в `RoundReady`.
* **ВЕРНО / НЕВЕРНО / ПАС / БАНК:** С визуальным эффектом нажатия (анимация depress). Работает и для бот-нажатий.
* **VOTE START:** 45-секундный таймер, синхронизация с музыкальным акцентом (1 сек задержка).
* **REVEAL:** Вскрытие голосов с привязкой к аудио-таймингу (прогресс-бар с метками «ИНТРО ВЕДУЩЕЙ» / «ВСКРЫТИЕ КАРТОЧЕК»).
* **VERDICT:** Запуск трека обсуждения. Аналитика остаётся открытой.
* **ELIMINATE:** Выбор игрока из ComboBox → исключение → Walk of Shame → кроссфейд в general_bed → Idle.
* **NEXT ROUND:** Доступна после ELIMINATE, блокируется после нажатия до READY.
* **CLOSE SESSION:** Возврат в предсессионный экран ввода участников.

**StatusBar (50 px):**
* Три сегмента: «ВЫ СДЕЛАЛИ», «СЕЙЧАС», «ПОРА НАЖАТЬ».
* Динамический фон: синий (игра), пурпурный (голосование/вскрытие), красный (вылет), серый (ожидание).
* Подсказки для всех 13 состояний. Интеграция с tie-detection.

**Система голосования:**
* Auto-Stop при внесении всех голосов.
* Tie-breaker: Сценарий А (СЗ в ничье — иммунитет) и Сценарий Б (СЗ решает).
* ComboBox фильтруется по кандидатам ничьи.

**Debug Panel:**
* Бот-режим, быстрый переход к раундам, тестовые команды.

### 2. Экран Ведущего Classic (`Views/HostScreen.xaml`) — 100%

* Денежное дерево слева, центральный блок (банк + вопрос), таймер справа.
* Суфлёр (`HostRulesOverlay`): полупрозрачная подложка, крупный белый текст.
* Пульсация таймера: красный + анимация на последних 10 секундах.
* `HOST_MESSAGE|CLEAR`: мгновенное скрытие суфлёра.

### 3. Экран Ведущего Modern (`Views/HostScreenModern.xaml`) — 100%

* Горизонтальный «светофор», современный дизайн 2020.
* Суфлёр и пульсация таймера аналогичны Classic.

### 4. Аналитика раунда (`Views/RoundStatsWindow.xaml`) — 100%

* DataGrid с колонками: Имя, Верные, Неверные, Пасы, Ошибки, В банк, Средний банк, Успешность%.
* Автоматическое определение Сильного и Слабого звена.
* Золотая рамка вокруг Сильного звена при ничье.
* Tooltip: уточнение по «Всего вопросов».

### 5. Финальный экран (Head-to-Head Panel) — 100%

* 10 кружочков-маркеров (зелёный/красный/серый) для каждого финалиста.
* Динамическое добавление маркеров в Sudden Death.
* WinnerPanel: имя победителя, сумма банка.

---

## Аудиоплеер (`Audio/AudioManager.cs`) — 100%

* **Движок:** NAudio.
* **Методы:** `Play()`, `Stop()`, `StopAll()`, `StartBedWithFadeIn()`, `PlayOneShotThenGeneralBedWithCrossfadeAsync()`, `GetAudioPosition()`.
* **Аудио-карта:**

| Фаза | Файл |
|------|------|
| PRE-GAME: Opening | `main_theme_full.mp3` |
| PRE-GAME: Intro 1 | `intro_track_1st.mp3` |
| PRE-GAME: Intro 2 | `intro_track_2nd.mp3` |
| PRE-GAME: Rules | `intro_track_3rd.mp3` |
| PRE-GAME: Ready | `playgame_with_general_bed.mp3` |
| Фон раундов | `general_bed.mp3` |
| Раунды (по длит.) | `Round_bed_(230).mp3` — `Round_bed_(130).mp3` |
| Полный банк | `Full_Bank_End.mp3` |
| Голосование старт | `new_voting_system v3START.mp3` |
| Вскрытие | `new_voting_system_revealing.mp3` |
| Обсуждение | `new_voting_system_before_walkshame.mp3` |
| Walk of Shame | `updated_walk_of_shame_bed.mp3` |
| Финал: преамбула | `final_round_prestart.mp3` |
| Финал: основной | `final_round_start.mp3` |
| Sudden Death | `sudden_death_bed.mp3` |
| Победа | `duel_winner.mp3` |

---

## Карта проекта

```
Core/
├── GameEngine.cs           — Ядро: состояния, банк, таймер, статистика, дуэль
├── GameState.cs            — 13 состояний (enum)
├── GameServer.cs           — TCP-сервер (broadcast)
├── GameClient.cs           — TCP-клиент
├── QuestionProvider.cs     — JSON-загрузка вопросов
└── Analytics/
    └── StatsAnalyzer.cs    — Аналитика, tie-detection, strongest/weakest

Views/
├── OperatorPanel.xaml/.cs  — Пульт оператора (~3300 строк)
├── HostScreen.xaml/.cs     — Классический экран ведущего
├── HostScreenModern.xaml/.cs — Современный экран (2020)
└── RoundStatsWindow.xaml/.cs — Окно аналитики раунда

Audio/
├── AudioManager.cs         — NAudio: Play, Stop, Crossfade, Bed
└── Assets/Audio/           — 20+ mp3-файлов

Data/
├── questions.json          — Вопросы для раундов
└── final_questions.json    — Вопросы для финальной дуэли
```

---

## Исправленные критические ошибки

| # | Описание | Причина | Решение |
|---|----------|---------|---------|
| 1 | `Full_Bank_End.mp3` после голосования | Лишний вызов Play в VotingTimer_Tick | Удалён |
| 2 | Walk of Shame без кроссфейда | Ненадёжный DispatcherTimer | `PlayOneShotThenGeneralBedWithCrossfadeAsync` |
| 3 | NEXT ROUND авто-клик | Фокус переносился на кнопку | `_nextRoundUsed` флаг + блокировка |
| 4 | NEXT ROUND не активна после ELIMINATE | Условие `Count > 2` | Заменено на `Count >= 2` |
| 5 | READY не кликалась | Пропущен `BtnStartRound.IsEnabled` | Добавлена привязка |
| 6 | «Собрано в банк: 0 ₽» | Чтение банка после обнуления | `savedRoundBank` передаётся заранее |
| 7 | `.Name.ToUpper()` на строке | `ActivePlayers` — коллекция строк | `.ToUpper()` напрямую |
| 8 | Конфликт переменной `strongest` | Одинаковое имя в разных case | Переименование в `duelStrongest` |

---

## Предстоящие задачи

- [ ] Host Screen Layout Editor
- [ ] Экран титров (автопрокрутка)
- [ ] Валидация JSON (GUI)
- [ ] Интеграция с OBS/vMix (Alpha Channel)
- [ ] Экспорт статистики (CSV/PDF)
- [ ] Настройка пресетов игроков из TXT
- [ ] Студийное нагрузочное тестирование

---

*Проект полностью собирается без ошибок и готов к эксплуатации.*
