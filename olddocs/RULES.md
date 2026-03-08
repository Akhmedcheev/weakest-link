# Rules — Слабое звено (Weakest Link)

Правила и конвенции проекта для разработки и работы с кодом.

**Главные документы (по ним сверять состояние проекта):**  
[RULES.md](RULES.md) · [PROGRESS_WL.md](PROGRESS_WL.md) · [ROUND_PREPARATION_DOCUMENTATION.md](ROUND_PREPARATION_DOCUMENTATION.md) · [ANALYTICS_DOCUMENTATION.md](ANALYTICS_DOCUMENTATION.md) · [ANALYTICS_MATH_FIX.md](ANALYTICS_MATH_FIX.md) · [ELIMINATION_PREDICTION_FIX.md](ELIMINATION_PREDICTION_FIX.md) · [FINAL_ANALYTICS_SYSTEM.md](FINAL_ANALYTICS_SYSTEM.md) · [NETWORK_FIX_SUMMARY.md](NETWORK_FIX_SUMMARY.md) · [PANIC_BANKER_METRIC.md](PANIC_BANKER_METRIC.md)

---

## Стек и структура

- **Платформа:** .NET (WPF), C#
- **Сборка:** `dotnet build` / `dotnet run`
- **Структура:**
  - `Views/` — окна (OperatorPanel, HostScreen, RoundStatsWindow, WinWinner)
  - `Core/` — GameEngine, GameState, модели, сервисы
  - `Network/` — GameServer, GameClient (TCP)
  - `Audio/` — воспроизведение звука
  - `docs/` — описание исправлений и прочая документация

---

## Конвенции кода

- **Поток UI:** все изменения элементов WPF на HostScreen и OperatorPanel выполняются в потоке UI: `Dispatcher.Invoke` или `Dispatcher.BeginInvoke`.
- **Состояние игры:** единственный источник состояния — `GameEngine` и перечисление `GameState` (Idle, RoundReady, Playing, Voting, Elimination, FinalDuel). Переходы — через `TransitionTo(...)`.
- **Сеть:** пульт — сервер (`GameServer`, порт 8888), экран ведущего и др. — клиенты (`GameClient`). Сообщения — строка, завершённая `\n`; поля внутри строки разделяются `|`.

---

## Ключевые форматы сообщений (TCP)

- `STATE|{GameState}` / `SET_STATE|{GameState}` — смена состояния.
- `DUEL_UPDATE|{name1}|{name2}|{s1}|{s2}` — финальная дуэль: имена двух финалистов и очки (s1, s2 — списки через запятую: `1` верно, `0` неверно, `-1` пусто).
- `QUESTION|{text}|{answer}|{number}` — вопрос и ответ для экрана ведущего.
- `ELIMINATE|{name}` / `CLEAR_ELIMINATION` — показ/скрытие экрана выбывания.
- `UPDATE_BANK|{chainIndex}|{banked}` — обновление банка.
- `HOST_MESSAGE|{text}` — текст правил на экран ведущего.

---

## Экран ведущего (HostScreen) и финал

- Данные финальной дуэли (имена и кружки) обновляются только из `DUEL_UPDATE` или прямого вызова `SetDuelDisplay(...)` с пульта. Сообщения `STATE`/`SET_STATE` не должны перезаписывать имена и индикаторы.
- Пульт при каждой отправке `DUEL_UPDATE` по TCP также вызывает `_hostScreen?.SetDuelDisplay(...)` для гарантированного обновления в том же процессе.

---

## Именование и стиль

- Логика окна — в code-behind (`*.xaml.cs`). Общие типы и движок — в `Core/`.
- Элементы с именами в XAML (`x:Name`) должны совпадать с обращением в коде (например, `TxtFinalist1Name`, `P1_I1`…`P1_I5`, `P2_I1`…`P2_I5` на HostScreen).

---

## Сборка и запуск

- Сборка: `dotnet build`
- Запуск: `dotnet run` (или запуск `WeakestLink.exe` из `bin\Debug\net10.0-windows\`).
