# Premium Dark Redesign — Roadmap

**Цель:** Привести интерфейс панели оператора «Слабое Звено» к стильному, премиальному dark-формату на основе образцов из `I:\SAMPLES OF DARK UI UX` и `I:\SAMPLES OF DARK UI UX II`.

---

## 1. Принципы (из образцов DocPanel, Editor Tools, Captions)

| Принцип | Реализация |
|---------|------------|
| **Глубокий charcoal** | Фон #0F1115, #16191F — не чисто чёрный |
| **Один акцент** | Teal #14B8A6 для активных элементов, синий #0A84FF для подтверждений |
| **Bento-карточки** | Скругление 12–16 px, мягкая тень, рамка #252A36 |
| **Pill-кнопки** | Сильное скругление (14–20 px) для главных действий |
| **Ghost/outline** | Прозрачный фон + тонкая рамка для вторичных действий |
| **Без токсичных цветов** | Приглушённые оттенки, «дышащие» отступы |

---

## 2. Цветовая палитра (финальная)

| Роль | Hex | Использование |
|------|-----|---------------|
| App bg | #0D0F13 | Окно, корневой фон |
| Surface | #0F1115 | Панели, левая колонка |
| Card | #16191F | Карточки, заголовки |
| Raised | #1A1D24 | Hover, выпадающие списки |
| Border | #252A36 | Рамки, разделители |
| Accent teal | #14B8A6 | Segmented toggle активный |
| Accent blue | #0A84FF | Подтверждения, бейджи |
| Text primary | #E2E5F0 | Основной текст |
| Text muted | #747A8A, #8B92A4 | Подписи, вторичный текст |

---

## 3. Компоненты для обновления

### Уже применено
- [x] Единый тёмный фон (Activity Bar, Title Bar, Content)
- [x] SegmentedToggleStyle (CLASSIC/PREMIUM, ТАЙМЕР/ЦЕПОЧКА/ОБА)
- [x] PillButton, PillButtonCompact
- [x] PremiumComboBoxItem (VotePopup)
- [x] Studio-палитра (StudioCardBg, StudioAccent)
- [x] PremiumCheckBox, PremiumRadioButton

### Выполнено (март 2026)
- [x] **Premium TextBox** — тёмный фон #16191F, teal focus accent (DocPanel)
- [x] **ComboBox** — глобально ItemContainerStyle=PremiumComboBoxItem, фон #16191F
- [x] **Window controls** — hover #252A36 (тёмная палитра)
- [x] **GhostButton** — фон #16191F, рамка #252A36
- [x] **Bento constants** — CornerRadiusBento (12), CornerRadiusBentoLg (16)

### Из анализа Part II (выполнено)
- [x] **Circular progress ring** — таймер с teal-кольцом (4707a76a)
- [x] **Input focus glow** — blue drop shadow при фокусе (7b4ca548)
- [x] **Border-glow** — блок Сервис + ПАНИКА с red glow (098cce1e98)
- [ ] **Split-button** — CLOSE SESSION (отложено)

### Будущее
- [ ] ContextMenu — иконки по категориям (Editor Tools)
- [ ] Панель ИНСТРУМЕНТЫ — стиль DocPanel Filters
- [ ] DataGrid — премиальные строки, hover

---

## 4. Референсы из образцов

| Образец | Элемент |
|---------|---------|
| DocPanel Dropdown | Single-select с teal-галочкой |
| DocPanel Filters | Underline-поля, pill Apply/Cancel |
| Editor Tools | Контекстное меню с иконками |
| Captions | Чекбоксы teal, радиокнопки |
| DocPanel Case Details | Pill-табы, Actions dropdown |

---

*Документ создан для систематического премиального редизайна. Март 2026.*
