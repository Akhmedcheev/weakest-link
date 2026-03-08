# 🔍 Расследование: Исчезнувшая кнопка START SESSION

## Факты

### Что видим на скриншоте
- Центральная панель «НАСТРОЙКА КОМАНДЫ» отображается
- Видны: поле ввода + `[+]`, ListBox, кнопки УДАЛИТЬ / ЗАПОЛНИТЬ / ПРАВИЛА, чекбокс «Пропустить вступление»
- **START SESSION — НЕТ**
- Под чекбоксом — пустое тёмное пространство до самого лог-бара

### Что есть в XAML (строки 644–651)
```xml
<Button x:Name="BtnStartSession" Content="START SESSION" 
        MinHeight="50" Height="55" 
        Background="#5865F2" Foreground="White" 
        FontSize="18" FontWeight="Bold" 
        Margin="0,15,0,0" Padding="20,10" 
        Cursor="Hand" BorderThickness="0"
        Click="BtnStartSession_Click"/>
```
> Кнопка **физически существует** в XAML-дереве.

### Что есть в C# code-behind
- [BtnStartSession_Click](file:///o:/WEAKEST%20LINK%20SOPFTWARE%20AI%20TESTERING/Views/OperatorPanel.xaml.cs#2226-2267) метод на строке 2226 — **существует**
- `BtnStartSession.IsEnabled = true` на строке 2146 — **обращения есть** 
- Нет `BtnStartSession.Visibility = Collapsed` в конструкторе
- Нет `GoToState` вызовов — VisualStateManager не используется императивно

---

## Гипотезы

### ❌ Гипотеза 1: Кнопку скрывает код
**Вердикт: Отклонена**

В конструкторе нет `BtnStartSession.Visibility = Collapsed`. Единственное упоминание кнопки — `IsEnabled` и `Content`. Она никогда не скрывается кодом.

### ❌ Гипотеза 2: VisualState скрывает кнопку
**Вердикт: Отклонена**

VisualStates управляют видимостью `SetupPanel`, `GridGameControls`, `GamePlayPanel` и т.д. — но **не** [BtnStartSession](file:///o:/WEAKEST%20LINK%20SOPFTWARE%20AI%20TESTERING/Views/OperatorPanel.xaml.cs#2226-2267) напрямую. При запуске `SetupPanel.Visibility = Visible` (задано в XAML), никакой VisualState не применяется (нет `GoToState` в конструкторе).

### ❌ Гипотеза 3: Кнопка прозрачная / сливается с фоном
**Вердикт: Отклонена**

У кнопки `Background="#5865F2"` (яркий синий), `Foreground="White"`, `Height="55"`. Даже если бы фон не применился, кнопка должна занимать 55px + 15px margin = 70px пространства. На скриншоте этого пространства нет.

### ⚠️ Гипотеза 4: ScrollViewer + StackPanel работает, но контент обрезается Border'ом
**Вердикт: Возможна, но маловероятна**

`SetupPanel` — это `Border` с `ScrollViewer`. Border растягивается в родительском Grid, ScrollViewer должен показывать полосу прокрутки. На скриншоте полосы нет — значит либо ScrollViewer считает что контент помещается, либо...

### 🔴 Гипотеза 5: Z-порядок — другая панель перекрывает SetupPanel
**Вердикт: ГЛАВНЫЙ ПОДОЗРЕВАЕМЫЙ**

Структура центральной колонки (строки 629–749):

```
<Grid Grid.Column="1">                    <!-- line 629 -->
    <Border x:Name="SetupPanel" .../>      <!-- line 631 — Visible -->
    <Grid x:Name="GridGameControls" .../>  <!-- line 657 — Collapsed -->
    <Grid x:Name="GamePlayPanel" .../>     <!-- line 674 — Collapsed -->
    <Grid x:Name="HeadToHeadPanel" .../>   <!-- line 687 — Collapsed -->
    <Grid x:Name="WinnerPanel" .../>       <!-- line 741 — Collapsed -->
</Grid>
```

> [!IMPORTANT]
> Все 5 элементов лежат в **одной ячейке Grid** без Row/Column назначений = они **рендерятся поверх друг друга**.
> `Collapsed` элементы не занимают место, но **влияют на Layout Pass**.

В WPF, когда `GridGameControls` имеет `Visibility="Collapsed"`, он **не рендерится**, но WPF Grid всё равно может передавать неправильные размерные ограничения (constraints) другим дочерним элементам в той же ячейке.

**Но это не объясняет, почему кнопка не видна в SetupPanel**, если SetupPanel сам видимый.

### 🔴🔴 Гипотеза 6: ScrollViewer получает неправильный constraint от родителя
**Вердикт: НАИБОЛЕЕ ВЕРОЯТНАЯ ПРИЧИНА**

Цепочка Layout:

```
Grid Row="3" (Height="*")     ← Растягивается на всё доступное
  └─ Grid Column="1"          ← Занимает всю высоту  
      └─ Border (SetupPanel)  ← Занимает всю высоту Grid
          └─ ScrollViewer     ← Имеет конечную высоту ← КЛЮЧ!
              └─ StackPanel   ← Содержимое ~580px
```

ScrollViewer получает конечный constraint от Border. StackPanel внутри **может** занимать больше места чем доступно. Но **если ScrollViewer работает правильно**, он покажет полосу.

**Проблема**: на скриншоте видно, что содержимого видно **меньше**, чем суммарная высота. ListBox назначен `Height="200"`, но на скриншоте он выглядит **намного выше** — он занимает почти половину центра.

> [!CAUTION]
> **ListBox Height="200" задан жёстко**, но визуально он растянулся на ~400px. Это значит, что `GroupBox`/`Border` **растягивает** ListBox, вытесняя кнопку за пределы видимой области.

Подсчёт высоты содержимого StackPanel:
| Элемент | Высота (px) |
|---------|------------|
| TextBlock заголовок | ~25 |
| Grid (TextBox + Button) | ~46 |
| ListBox | 200 |
| BtnRemovePlayer | ~44 |
| BtnFillRandom | ~44 |
| BtnShowRules | ~46 |
| CheckBox | ~28 |
| **BtnStartSession** | **~70** |
| Итого | **~503** |

Если панель ~450px высотой, кнопка **обрезается** ScrollViewer'ом. Но ScrollViewer должен это обработать...

**Реальная проблема может быть в том**, что `Border (SetupPanel)` получает **бесконечный** constraint сверху (т.к. все элементы в Grid Column="1" без RowDefinitions перекрываются), и ScrollViewer думает, что у него **бесконечная** доступная высота.

> [!CAUTION]
> Когда ScrollViewer получает бесконечный constraint по высоте, он **НЕ СКРОЛЛИТ** — он просто рендерит весь контент, и нижние элементы **уходят за пределы экрана** без полосы прокрутки!

---

## 🎯 Корневая причина

**`SetupPanel` (Border) лежит в Grid без RowDefinitions. Grid без RowDefinitions передаёт дочерним элементам constraint, зависящий от родительского Grid Row="3" (Height="\*"). Но поскольку в ячейке 5 перекрывающихся элементов, и один из них (`HeadToHeadPanel`) имеет `RowDefinitions` с `Height="*"` что требует бесконечного constraint — Grid может передать `Double.PositiveInfinity` как доступную высоту для ScrollViewer.**

**Результат**: ScrollViewer думает что у него бесконечно места, StackPanel рендерится полностью, но нижняя часть (включая START SESSION) выходит за видимые границы окна. Полоса прокрутки не появляется, потому что ScrollViewer думает что места хватает.

---

## 💡 Предлагаемое решение

### Вариант A: Жёсткое ограничение высоты ScrollViewer
```xml
<Border x:Name="SetupPanel" Background="#313338" Padding="20" Visibility="Visible">
    <ScrollViewer VerticalScrollBarVisibility="Auto" MaxHeight="700">
        <StackPanel>
            ...
        </StackPanel>
    </ScrollViewer>
</Border>
```
**Минус**: Жёсткие пиксели не адаптивны.

### Вариант B: Вынести кнопку из ScrollViewer  
Разнести контейнер на 2 части: скроллируемый контент + фиксированная кнопка внизу:
```xml
<Border x:Name="SetupPanel" Background="#313338" Padding="20">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>    <!-- Скроллируемый контент -->
            <RowDefinition Height="Auto"/> <!-- Кнопка START -->
        </Grid.RowDefinitions>
        <ScrollViewer Grid.Row="0" VerticalScrollBarVisibility="Auto">
            <StackPanel>
                ... все элементы кроме кнопки ...
            </StackPanel>
        </ScrollViewer>
        <Button Grid.Row="1" x:Name="BtnStartSession" ... />
    </Grid>
</Border>
```
**Плюс**: Кнопка ВСЕГДА видна внизу панели, контент скроллится отдельно.

### Вариант C (рекомендуемый): Убрать ScrollViewer и дать StackPanel жить как есть
```xml
<Border x:Name="SetupPanel" Background="#313338" Padding="20">
    <DockPanel LastChildFill="False">
        ... все элементы ...
        <Button DockPanel.Dock="Bottom" x:Name="BtnStartSession" ... />
    </DockPanel>
</Border>
```
**DockPanel.Dock="Bottom"** гарантирует что кнопка прижата к низу, а остальной контент — сверху.

> [!TIP]  
> **Рекомендация**: Вариант B — самый надёжный. Кнопка START SESSION фиксирована внизу панели и никогда не уедет за экран.
