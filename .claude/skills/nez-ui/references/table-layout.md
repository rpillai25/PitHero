# Table Layout, Window, TabPane, ScrollPane

`Table` is the workhorse layout container — works like HTML tables with more flexibility. `Window`, `Tab`, and `ScrollPane` all build on it.

## Core Pattern: Nested Tables

```csharp
var mainTable = new Table();
mainTable.Pad(10f);

// Row 1: two-column layout
var topRow = new Table();
topRow.Add(leftContent).Left();
topRow.Add(rightContent).Right();
mainTable.Add(topRow).Expand().Fill();
mainTable.Row();

// Row 2: centered buttons
var bottomRow = new Table();
bottomRow.Add(button1).Width(80).SetPadRight(10);
bottomRow.Add(button2).Width(80);
mainTable.Add(bottomRow).Center();
```

## Cell Configuration Methods

**Size:**
- `.Width(float)` / `.Height(float)` — fixed dimensions
- `.Size(w, h)` — set both
- `.SetMinWidth()` / `.SetMinHeight()` — minimum constraints
- `.SetMaxWidth()` / `.SetMaxHeight()` — maximum constraints

**Padding (inside cell, around element):**
- `.Pad(float)` — all sides
- `.Pad(top, left, bottom, right)`
- `.SetPadTop()` / `.SetPadLeft()` / `.SetPadBottom()` / `.SetPadRight()`

**Fill & Expand:**
- `.Fill()` — fill both axes within cell
- `.Expand()` — claim extra space
- `.Grow()` — expand AND fill (shorthand)
- `.SetFillX()` / `.SetFillY()` — single axis
- `.SetExpandX()` / `.SetExpandY()` — single axis

**Alignment within cell:**
- `.Left()` / `.Center()` / `.Right()` — horizontal
- `.Top()` / `.Bottom()` — vertical
- Combinable: `.Top().Left()`

**Grid:**
- `.Row()` — start new row
- `.SetColspan(int)` — span multiple columns

## Table-Level Methods

```csharp
table.SetFillParent(true);           // fill entire stage/parent
table.SetBackground(drawable);       // background drawable
table.Defaults().SetPadBottom(8f);   // default cell settings for new rows
table.Top().Left();                  // table alignment within bounds
table.Pad(10f);                      // padding around table edges
table.Clip = true;                   // clip overflow
```

## Stage

The `Stage` is the root container:
- `stage.AddElement(element)` — add to root
- `stage.GetWidth()` / `stage.GetHeight()` — dimensions (for centering)
- `stage.GetMousePosition()` — current mouse position in stage coords
- `stage.SetGamepadFocusElement(element)` — enable gamepad input
- `stage.SetKeyboardFocus(element)` — set keyboard focus

## Window

`Window` extends `Table`, adding a title bar and drag/resize support.

```csharp
var windowStyle = skin.Get<WindowStyle>("ph-default");
var window = new Window("Title", windowStyle);
window.SetSize(450, 350);
window.SetMovable(false);

// Window IS a Table
window.Pad(10f);
window.Add(contentTable).Expand().Fill();

// Center on stage
window.SetPosition(
    (stage.GetWidth()  - window.GetWidth())  / 2f,
    (stage.GetHeight() - window.GetHeight()) / 2f);

stage.AddElement(window);
```

**Key properties:**
- `IsMovable` (default `true`) — can be dragged
- `IsResizable` — can be resized
- `KeepWithinStage` — clamp to stage bounds

## TabPane

```csharp
var tabWindowStyle = skin.Get<TabWindowStyle>("ph-default");
var tabPane = new TabPane(tabWindowStyle);

var tab1 = new Tab("Settings", tabStyle);
var tab2 = new Tab("Info", tabStyle);

tab1.Add(scrollPane).Expand().Fill().Pad(20);
tab2.Add(infoTable).Expand().Fill().Pad(20);

tabPane.AddTab(tab1);
tabPane.AddTab(tab2);

// Add to parent window flush (no extra padding)
window.Add(tabPane).Expand().Fill().Pad(0);
```

## ScrollPane

```csharp
var contentTable = new Table();
contentTable.Top().Left();
// ... add content rows ...

var scrollPane = new ScrollPane(contentTable, skin, "ph-default");
scrollPane.SetScrollingDisabled(true, false);  // vertical only (horizontal disabled)
scrollPane.SetFadeScrollBars(false);           // always show scrollbars

parent.Add(scrollPane).Expand().Fill();
```

**Key properties:**
- `SetScrollingDisabled(horizontal, vertical)` — disable axes
- `SetFadeScrollBars(bool)` — fade when idle
- `SmoothScrolling` — smooth scroll animation
- `ScrollSpeed` — scroll sensitivity (default 0.05f)
