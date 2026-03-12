# Weakest Link: Project Progress Report

Актуальный статус разработки программно-аппаратного комплекса для проведения телеигры «Слабое звено».

**Обновлено:** 08.03.2026
**Статус:** Production-ready (все основные системы реализованы и протестированы)

---

## 1. Архитектура и Ядро (GameEngine) ✅

- [x] **State Machine:** Полная цепочка состояний — `Idle`, `IntroOpening`, `IntroNarrative`, `PlayerIntro`, `RulesExplanation`, `RoundReady`, `Playing`, `RoundSummary`, `Voting`, `Discussion`, `Reveal`, `Elimination`, `FinalDuel`.
- [x] **Денежная цепочка:** Гибкая настройка (1 000 – 50 000 ₽). Учёт текущей ступени, сброс при ошибке.
- [x] **Учёт статистики:** Сбор данных по каждому игроку в реальном времени (верные/неверные, пасы, банк, BurnedByWrongAnswers, ExactDroppedMoney).
- [x] **Таймер:** Точный отсчёт (150→90 сек) с автоматической остановкой.
- [x] **Удвоение банка (7-й раунд):** При `ActivePlayers.Count == 2` банк раунда умножается на 2 с HOST_MESSAGE суфлёром.
- [x] **LastStrongestLinkName:** Сохранение сильного звена для финальной дуэли.
- [x] **Сброс и инициализация:** Полная очистка состояния без перезапуска.

## 2. PRE-GAME: Студийное вступление ✅

Четырёхфазная система подготовки к эфиру:

| Кнопка | Аудио | GameState | HOST_MESSAGE |
|--------|-------|-----------|--------------|
| OPENING | `main_theme_full.mp3` | IntroOpening | [ЛОГОТИП ИГРЫ] |
| INTRO 1 | `intro_track_1st.mp3` | IntroNarrative | Реплики про шоу и 400к |
| INTRO 2 | `intro_track_2nd.mp3` | PlayerIntro | ПРЕДСТАВЛЕНИЕ УЧАСТНИКОВ |
| RULES | `intro_track_3rd.mp3` | RulesExplanation | Правила, цепь, БАНК |
| READY | `playgame_with_general_bed.mp3` | RoundReady | «Начинаем с {Имя}. Время пошло!» |

- [x] Последовательная активация кнопок (каждая разблокирует следующую)
- [x] `PrepareNewRound()` вызывается автоматически при нажатии RULES (150 сек, первый по алфавиту)
- [x] Динамическая подстановка имени первого игрока в суфлёр

## 3. Сетевая логика (Master-Slave Sync) ✅

- [x] **TCP Server:** Пульт оператора — сервер (порт 8888).
- [x] **Broadcast Protocol:** `STATE`, `SET_STATE`, `QUESTION`, `UPDATE_BANK`, `UPDATE_TIMER`, `DUEL_UPDATE`, `WINNER`, `ELIMINATE`, `HOST_MESSAGE`, `CLEAR_ELIMINATION`, `CLEAR_QUESTION`, `CURRENT_PLAYER`.
- [x] **HOST_MESSAGE:** Система суфлёра ведущего — полупрозрачный оверлей с крупным белым текстом по центру.
- [x] **HOST_MESSAGE|CLEAR:** Мгновенная очистка суфлёра при START O'CLOCK или начале дуэли.

## 4. Пульт Оператора (OperatorPanel) ✅

- [x] **Кнопка START O'CLOCK:** Ярко-зелёная кнопка запуска таймера, доступна только в `RoundReady`.
- [x] **VOTE START (45 сек):** Синхронизация звук-таймер с 1-секундной задержкой для музыкального акцента.
- [x] **REVEAL / VERDICT / ELIMINATE:** Полная цепочка голосования → вскрытие → обсуждение → исключение.
- [x] **Кнопка NEXT ROUND:** Доступна сразу после ELIMINATE, блокируется после нажатия до следующего READY.
- [x] **CLOSE SESSION:** Возврат в предсессионное окно ввода участников с полным сбросом.
- [x] **Визуальный «press» эффект:** Для кнопок ВЕРНО, НЕВЕРНО, БАНК, ПАС (включая бот-режим).
- [x] **PRE-GAME панель:** Горизонтальный блок с кнопками вступления.
- [x] **Debug Panel:** Быстрый старт, бот-режим, тестовые команды.

## 5. StatusBar (Штурман оператора) ✅

Трёхсегментная строка состояния (50 px):

| Блок | Заголовок | Пример |
|------|-----------|--------|
| Левый | ВЫ СДЕЛАЛИ | «Таймер запущен (START O'CLOCK)» |
| Центр | СЕЙЧАС | «Идёт раунд 3. Игроки отвечают.» |
| Правый (золотой) | ПОРА НАЖАТЬ | «Ждите 0:00 или вносите голоса.» |

- [x] Динамический цвет фона: синий (игра), пурпурный (голосование), красный (вылет), серый (ожидание)
- [x] Мгновенное обновление при каждой смене GameState
- [x] Подсказки для всех 13 состояний на русском языке с английскими названиями кнопок
- [x] Интеграция с tie-detection: показ информации о ничье и роли Сильного звена

## 6. Система голосования и ничья (Tie-breaker) ✅

- [x] **IsTieDetected():** Определение ничьей (2+ игрока с максимальным равным числом голосов).
- [x] **GetTiedPlayerNames():** Список имён участников ничьей.
- [x] **Сценарий А (СЗ в ничье):** Автоматический иммунитет Сильного звена. Второй выбывает. HOST_MESSAGE + StatusBar.
- [x] **Сценарий Б (СЗ в безопасности):** Ожидание решения Сильного звена. ComboBox фильтруется до кандидатов ничьи.
- [x] **RoundStatsWindow:** Золотая рамка вокруг Сильного звена при ничье.

## 7. Финальная дуэль (Head-to-Head) ✅

- [x] **Логика пошаговых ходов:** Строгая очерёдность (Игрок 1 → Игрок 2).
- [x] **Досрочная победа (Early Math Termination):** `score1 > score2 + remaining2` → мгновенное завершение.
- [x] **Sudden Death:** Автоматическая активация при ничье 5:5. HOST_MESSAGE с объяснением правил.
- [x] **Match-ball суфлёр:** «Если вы ответите неверно — проиграете» / «Если правильно — победите».
- [x] **HOST_MESSAGE при старте дуэли:** Текст сценария с именем Сильного звена, суммой банка и правом выбора.
- [x] **Victory Announcement:** «{Имя}, сегодня вы — самое сильное звено! Сумма {Банк} рублей.»
- [x] **WinnerPanel:** Панель победителя с суммой выигрыша.

## 8. Аудиодвижок (AudioManager / NAudio) ✅

- [x] **Раундовые треки:** `Round_bed_(230).mp3` – `Round_bed_(130).mp3` с автоматическим выбором.
- [x] **Кроссфейд general_bed:** Плавное нарастание за 3 сек до конца предыдущего трека.
- [x] **PlayOneShotThenGeneralBedWithCrossfadeAsync:** Walk of Shame → general_bed.
- [x] **PRE-GAME аудио:** `main_theme_full`, `intro_track_1st/2nd/3rd`, `playgame_with_general_bed`.
- [x] **Голосование:** `new_voting_system v3START`, `new_voting_system_revealing`, `new_voting_system_before_walkshame`.
- [x] **Финал:** `final_round_prestart`, `final_round_start`, `sudden_death_bed`, `duel_winner`.
- [x] **Audio Priority:** `Stop()` перед каждым новым треком.

## 9. AI-тестирование (Gemini / OpenAI) ✅

Система автоматического нагрузочного тестирования игры с помощью LLM:

### Классы AI-клиентов (Network/)

| Класс | API | Модель | Назначение |
|-------|-----|--------|------------|
| `GeminiTestPlayer` | Google Gemini REST | `gemini-2.5-flash` | Основной бот для кнопки «ТЕСТОВАЯ ИГРА С БОТАМИ» |
| `GeminiBotClient` | Google Gemini REST | Конфигурируемая | Расширенный клиент с `currentBank` |
| `AiBotTester` | OpenAI-совместимый | Конфигурируемая | Универсальный (ChatGPT, DeepSeek, Ollama, LM Studio) |

- [x] **BotDecision** (`Core/Models/BotDecision.cs`): Структура ответа AI — `Action` ("answer"/"bank"/"pass") + `Text`
- [x] **Системный промпт:** Жёсткая викторина, без подсказок, строгий JSON-формат ответа
- [x] **`responseMimeType: "application/json"`** (Gemini) / **`response_format: json_object`** (OpenAI): Гарантия JSON без маркдауна
- [x] **Очистка маркдауна:** `Replace("```json", "").Replace("```", "").Trim()` перед десериализацией
- [x] **try-catch по типам:** `TaskCanceledException` (таймаут), `HttpRequestException` (сеть), `JsonException` (невалидный JSON) → fallback `pass`
- [x] **DiagLog:** Двойной вывод в `Debug.WriteLine` + `LogCallback` (лог оператора)
- [x] **HTTP-ошибки:** Чтение тела ответа при non-2xx (показ причины: «API key invalid», «quota exceeded» и т.д.)

### Интеграция в OperatorPanel

- [x] **Флаг `_isAutoTestRunning`:** Включает/выключает AI-автопилот
- [x] **Кнопка «ЗАПУСТИТЬ AI-ТЕСТ»:** Меню DEBUG → ТЕСТОВАЯ ИГРА С БОТАМИ → 4. AI-тестирование (Gemini)
- [x] **Поле API-ключа:** `TxtGeminiApiKey` в меню (+ хардкод по умолчанию для тестов)
- [x] **8 ботов:** Бот Альфа, Бот Бета, Бот Гамма, Бот Дельта, Бот Эпсилон, Бот Зета, Бот Эта, Бот Тета
- [x] **`ProcessBotTurnAsync()`:** Асинхронный цикл автопилота (задержка 2.5 сек → запрос к AI → обработка решения)
- [x] **Логика решений:**
  - `"bank"` → `BtnBank_Click` + рекурсия (бот сразу отвечает после банка)
  - `"answer"` → сравнение с `Answer` + `AcceptableAnswers` (регистронезависимо) → ВЕРНО/НЕВЕРНО
  - `"pass"` → `BtnPass_Click`
- [x] **Запуск:** Автоматически при START O'CLOCK (если `_isAutoTestRunning`)
- [x] **Остановка:** BREAK GAME, CLOSE SESSION, истечение таймера, EXIT BOT TEST → `_isAutoTestRunning = false`
- [x] **Логирование AI:** `[AI] {имя}: ОТВЕЧАЕТ -> "текст"`, `[AI] ✓ ВЕРНО`, `[AI] ✗ НЕВЕРНО (правильно: "...")`, `[AI] БЕРЁТ БАНК!`, `[AI] ПАСУЕТ`

## 10. Аналитика раунда (RoundStatsWindow) ✅

- [x] **Таблица игроков:** Верные, неверные, пасы, ошибки, В банк, Средний банк, Успешность%.
- [x] **Сильное / Слабое звено:** Автоматическое определение по формуле.
- [x] **Прогноз на выбывание:** С объяснением причины.
- [x] **Главный перестраховщик (Panic Banker):** Игрок с наименьшим средним банком.
- [x] **Эффективность:** TotalBanked / (TotalBanked + TotalBurned).
- [x] **Fix: «Собрано в банк»:** Сохранённый банк раунда передаётся через `savedRoundBank` до обнуления в движке.

## 10. UI-улучшения лога событий ✅

- [x] **Высота панели лога:** Увеличена с 100px до 200px (вдвое больше видимых строк)
- [x] **Шрифт:** Моноширинный `Consolas 12` для ровных колонок
- [x] **Горизонтальный скролл:** Длинные строки `[GEMINI]` не обрезаются
- [x] **Padding:** Уменьшен до 2,1 для максимальной плотности строк

## 11. Экраны ведущего (HostScreen / HostScreenModern) ✅

- [x] **Classic HostScreen:** Денежное дерево слева, таймер, вопрос, банк.
- [x] **Modern HostScreen (2020):** Горизонтальный «светофор», современный дизайн.
- [x] **Суфлёр (HostRulesOverlay):** Полупрозрачная подложка с крупным белым текстом.
- [x] **Пульсация таймера:** Красный цвет + анимация на последних 10 секундах.
- [x] **Финальная дуэль:** Кружочки результатов, имена финалистов.
- [x] **Автоскрытие суфлёра:** При переходе в `Playing` или `FinalDuel`.

---

## Структура файлов проекта

```
Core/
├── GameEngine.cs          — Ядро: состояния, банк, таймер, статистика, дуэль
├── GameState.cs           — Перечисление 13 состояний
├── Models/
│   ├── QuestionData.cs    — Модель вопроса
│   ├── QuestionModel.cs   — Модель для редактора
│   └── BotDecision.cs     — Решение AI-бота (Action + Text)
├── Services/
│   └── QuestionProvider.cs — Загрузка и выдача вопросов из JSON
└── Analytics/
    └── StatsAnalyzer.cs   — Аналитика раунда, tie-detection, strongest/weakest

Network/
├── GameServer.cs          — TCP-сервер (broadcast)
├── GameClient.cs          — TCP-клиент
├── WebRemoteController.cs — HTTP-пульт (iPad remote)
├── GeminiTestPlayer.cs    — AI-бот Google Gemini 2.5 Flash (основной)
├── GeminiBotClient.cs     — AI-бот Google Gemini (расширенный)
└── AiBotTester.cs         — AI-бот OpenAI-совместимый (ChatGPT, DeepSeek, Ollama)

Views/
├── OperatorPanel.xaml/.cs — Главный пульт оператора (~3500 строк)
├── HostScreen.xaml/.cs    — Классический экран ведущего
├── HostScreenModern.xaml/.cs — Современный экран (2020)
└── RoundStatsWindow.xaml/.cs — Окно аналитики раунда

Audio/
├── AudioManager.cs        — NAudio: Play, Stop, Crossfade, Bed
└── Assets/Audio/          — 20+ аудиофайлов (.mp3)
```

---

## Предстоящие задачи (TODO)

- [x] ~~**AI-тестирование:** Интеграция Gemini / OpenAI для нагрузочного тестирования~~ ✅
- [ ] **AI-тестирование финальной дуэли:** Подключить AI-автопилот к Head-to-Head раунду
- [ ] **Host Screen Layout Editor:** Визуальное перетаскивание элементов.
- [ ] **Экран титров:** Автоматическая прокрутка создателей.
- [ ] **Валидация JSON:** Графический интерфейс проверки вопросов.
- [ ] **Интеграция с OBS/vMix:** Alpha Channel для вывода в эфир.
- [ ] **Экспорт статистики:** CSV/PDF отчёт по завершению игры.
