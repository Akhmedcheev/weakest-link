# Ситуация по BroadcastScreen — для NotebookLM

Краткий обзор последних отмен и откатов, чтобы NotebookLM был в курсе актуального состояния.

---

## Что такое BroadcastScreen (исторически)

**BroadcastScreen** — это экран/окно для «трансляции» зрителям: показ вопроса, ответа, денежной цепочки, таймера, дуэли финалистов и т.д.

- В коде предполагался отдельный тип `BroadcastScreen` (WPF `Window`), с методами вроде:
  - конструктор с одним аргументом (например, `GameEngine`);
  - `UpdateQuestion(question, answer, count)`;
  - `SetDuelDisplay(p1, p2, s1, s2)` для финальной дуэли;
  - а также API денежной цепочки: `SetLevel`, `NextLevel`, `SecondsRemaining`, `GetCurrentLevel`, `PreviousLevel`, `Dispose` (см. `files/EXAMPLES.md`).

---

## Что отменено и откачено

1. **Удалён отдельный экран BroadcastScreen в проекте Weakest Link**
   - Файлы `Views/BroadcastScreen.xaml` и `Views/BroadcastScreen.xaml.cs` в проекте **отсутствуют** (удалены).
   - В папке `obj/` остались только артефакты старой сборки (например, `Views/BroadcastScreen.g.cs`) — это след прошлой версии, не актуальный исходный код.

2. **В OperatorPanel отказ от _broadcastScreen**
   - Раньше в `OperatorPanel` было поле `_broadcastScreen` и вызовы:
     - создание: `new BroadcastScreen(_engine)` (конструктор с 1 аргументом);
     - `_broadcastScreen.UpdateQuestion(...)`;
     - `_broadcastScreen.SetDuelDisplay(...)`.
   - **Сейчас**: эти вызовы убраны. Данные вопроса и дуэли передаются на **HostScreen** — но HostScreen это **утилита для ведущей**, а не система трансляции для зрителей.

3. **Демо «денежная цепочка» с BroadcastScreen отключено в сборке**
   - `MainWindow.xaml` / `MainWindow.xaml.cs` содержат демо с контролом `BroadcastScreenControl` (уровни, таймер, кнопки).
   - В `WeakestLink.csproj` эти файлы **исключены** из компиляции:
     - `<Compile Remove="MainWindow.xaml.cs" />`
     - `<Page Remove="MainWindow.xaml" />`
   - То есть отдельное приложение/демо с BroadcastScreen в текущей сборке Weakest Link **не участвует**.

4. **Ошибки сборки, связанные с BroadcastScreen (из логов)**
   - Раньше сборка падала из‑за:
     - `BroadcastScreen` не содержит конструктора с 1 аргументом;
     - у `BroadcastScreen` нет методов `UpdateQuestion` и `SetDuelDisplay`;
     - ненулевое поле `_broadcastScreen` в `OperatorPanel` не инициализировалось.
   - После отката (удаление использования _broadcastScreen) эти ошибки в OperatorPanel не воспроизводятся. HostScreen при этом остаётся только утилитой для ведущей, не заменой broadcast-системы.

---

## Важно: HostScreen ≠ broadcast

- **HostScreen** — это **утилита для ведущей**: экран/панель, которой пользуется ведущая во время игры (подсказки, вопрос, ответ, дуэль и т.д.). Это не система трансляции для зрителей.
- **BroadcastScreen** (отменённый) — это как раз **система трансляции**: то, что должно было выводиться на большой экран / в эфир для зрителей. Отдельная сущность от экрана ведущей.

---

## Текущее состояние (на чём всё держится сейчас)

- **Отдельной broadcast-системы в проекте сейчас нет.** BroadcastScreen удалён и не заменён.
- **HostScreen** остаётся утилитой для ведущей: создаётся из OperatorPanel, получает вопросы и дуэль через прямые вызовы и/или TCP. Это не «экран для зала».
- Денежная цепочка и состояние игры отображаются на панели оператора (например, `BankChainList`) и на HostScreen (для ведущей). Отдельного окна/канала для трансляции зрителям в коде нет.

---

## Документация (EXAMPLES.md)

- В `files/EXAMPLES.md` описан API **BroadcastScreen** (SetLevel, NextLevel, SecondsRemaining, цепочка, таймер и т.д.) — то есть именно система трансляции/демо.
- В контексте текущего Weakest Link: отдельной broadcast-системы нет; HostScreen — только утилита для ведущей, не замена BroadcastScreen.

---

## Краткая сводка для NotebookLM

| Тема | Статус |
|------|--------|
| BroadcastScreen (система трансляции для зрителей) | **Отменён/удалён** |
| HostScreen | **Утилита для ведущей**, не broadcast |
| Поле _broadcastScreen и вызовы в OperatorPanel | **Откат**: не используются |
| Отдельная broadcast-система в проекте | **Нет** |
| MainWindow + BroadcastScreenControl (демо) | Исключены из сборки (.csproj) |
| EXAMPLES.md про BroadcastScreen | Описывает API трансляции/демо, не HostScreen |

Если в будущем снова введут отдельный BroadcastScreen, нужно будет заново добавить Views (XAML + code-behind), конструктор и методы UpdateQuestion/SetDuelDisplay и при необходимости снова завязать OperatorPanel на этот экран.
