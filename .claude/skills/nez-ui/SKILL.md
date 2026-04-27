---
name: nez-ui
description: "**DOMAIN SKILL** — Nez UI framework best practices and PitHero UI conventions. USE FOR: building UI layouts with Table/Window/TabPane, creating custom UI elements, skin/style management, event handling, ScrollPane usage, dialog patterns, drag-and-drop with InventoryDragManager/DragDropOverlay, and all Nez.UI work in PitHero. DO NOT USE FOR: non-UI game logic, rendering pipelines, ECS components unrelated to UI."
applyTo: "**/*UI*.cs,**/UI/**,**/*Dialog*.cs,**/*Window*.cs,**/*Tab*.cs,**/*Skin*.cs,**/*Menu*.cs"
---

# Nez UI – Best Practices & PitHero Conventions

## Framework Overview

Nez UI is based on [TableLayout](https://github.com/EsotericSoftware/tablelayout) and the libGDX Scene2D UI system. Key terminology differences from libGDX:
- libGDX `Actor` = Nez `Element`
- libGDX `Widget`/`WidgetGroup` don't exist in Nez; use `Element` and `Group` instead

---

## CRITICAL RULES (Never Violate)

1. **NEVER use `SetFontScale()`** on any UI element. To scale a font, load a larger bitmap font file instead.
2. **Always use the `"ph-default"` style** when creating UI elements with `PitHeroSkin` (unless a unique style is explicitly requested).
3. **Never mutate `"ph-default"` style properties** — do NOT do `element.GetStyle().FontColor = someColor`. If a unique color is needed, create a **new style instance** with the desired color.
4. **Use `HoverableImageButton`** instead of Nez `ImageButton` (adds tooltip hover support).
5. **Use `EnhancedSlider`** instead of Nez `Slider` (adds deferred value commit on mouse release).
6. **Use `PausableSpriteAnimator`** instead of `SpriteAnimator`.
7. **AOT compliance**: No `foreach`, no LINQ in UI update loops. Use `for` loops. Pre-allocate collections. Avoid `new` during gameplay.

---

## Core Architecture

### UICanvas Setup in Scenes

`UICanvas` is a `RenderableComponent` that wraps a Nez `Stage`. Attach it to an entity in your scene:

```csharp
// Scene setup
SetDesignResolution(GameConfig.VirtualWidth, GameConfig.VirtualHeight,
    SceneResolutionPolicy.BestFit);

var screenSpaceRenderer = new ScreenSpaceRenderer(100, GameConfig.RenderLayerUI);
AddRenderer(screenSpaceRenderer);

var uiEntity = CreateEntity("ui-overlay");
var uiCanvas = uiEntity.AddComponent(new UICanvas());
uiCanvas.IsFullScreen = true;   // true = screen-space (raw mouse); false = world-space (scaled mouse)
uiCanvas.RenderLayer = GameConfig.RenderLayerUI;  // 998

// Build UI on the Stage
_myUI.InitializeUI(uiCanvas.Stage);
```

### Stage

The `Stage` is the root container for all UI elements:
- `stage.AddElement(element)` — add element to root
- `stage.GetWidth()` / `stage.GetHeight()` — stage dimensions (for centering)
- `stage.SetGamepadFocusElement(element)` — enable gamepad input
- `stage.SetKeyboardFocus(element)` — set keyboard focus

### Render Layers (from GameConfig)

| Constant | Value | Purpose |
|----------|-------|---------|
| `RenderLayerActionQueue` | 996 | Action queue (screen space) |
| `RenderLayerGraphicalHUD` | 997 | Graphical HUD |
| `RenderLayerUI` | 998 | UI layer (always on top) |
| `TransparentPauseOverlay` | 999 | Transparent overlay |

---

## PitHeroSkin & Style System

### Getting the Skin

```csharp
var skin = PitHeroSkin.CreateSkin();  // Cached singleton — safe to call repeatedly
```

`PitHeroSkin.CreateSkin()` returns a cached `Skin` instance. All styles are registered under the key `"ph-default"`. Nez's `Skin` stores styles by **type**, so multiple `Add("ph-default", ...)` calls with different style types don't conflict.

### Registered "ph-default" Styles

| Style Type | Notes |
|------------|-------|
| `LabelStyle` | Brown font color `(71, 36, 7)`, FontScale = 1f |
| `TextButtonStyle` | NinePatch Up/Down/Over, brown font, PressedOffset = 1,1 |
| `WindowStyle` | NinePatch background, brown title font |
| `CheckBoxStyle` | Checkbox sprites (Unchecked/Checked/Over) |
| `RadioButtonStyle` | Radio button sprites (Unselected/Selected) |
| `TabButtonStyle` | NinePatch Active/Inactive/Hover states |
| `TabWindowStyle` | Contains TabButtonStyle for TabPane |
| `ScrollPaneStyle` | VScroll/HScroll with NinePatch knobs |
| `SliderStyle` | PrimitiveDrawable background, knob sprites with states |

### Retrieving Styles

```csharp
var labelStyle = skin.Get<LabelStyle>("ph-default");
var buttonStyle = skin.Get<TextButtonStyle>("ph-default");
var windowStyle = skin.Get<WindowStyle>("ph-default");
var tabStyle = skin.Get<TabWindowStyle>("ph-default");
```

### Creating Elements with the Skin

Most Nez UI elements accept a `Skin` + optional style name in their constructor:

```csharp
var label = new Label("Text", skin);                      // uses default LabelStyle
var label = new Label("Text", skin, "ph-default");        // explicit (preferred)
var button = new TextButton("Click", skin);               // uses default TextButtonStyle
var checkbox = new CheckBox("Option", skin, "ph-default");
```

### Custom Styles (When Unique Colors Are Needed)

**Correct pattern** — create a new style instance:

```csharp
var defaultStyle = skin.Get<LabelStyle>("ph-default");
var blueLabelStyle = new LabelStyle
{
    Font = defaultStyle.Font,
    FontColor = new Color(0, 80, 156),
    FontScaleX = 1f,
    FontScaleY = 1f
};
var blueLabel = new Label("Special Text", blueLabelStyle);
```

**WRONG** — never mutate the shared style:
```csharp
// DO NOT DO THIS — mutates style for ALL labels using "ph-default"
label.GetStyle().FontColor = Color.Red;
```

### Common Font Colors

| Color | RGB | Usage |
|-------|-----|-------|
| Brown (default) | `(71, 36, 7)` | Standard UI text |
| Blue (detail) | `(37, 80, 112)` | Detail/info text |
| Green | `Color.Green` | Positive stat changes |
| Red | `Color.Red` | Negative stat changes |
| Gray | `Color.Gray` | Disabled/unavailable text |

### Font Paths (from GameConfig)

| Constant | Path | Usage |
|----------|------|-------|
| `FontMainUI` | `Content/Fonts/Express.fnt` | Main UI font |
| `FontPathHud` | `Content/Fonts/Skullboy.fnt` | HUD font (normal) |
| `FontPathHud2x` | `Content/Fonts/Skullboy2x.fnt` | HUD font (2x for half-size mode) |
| `FontPathHudSmall` | `Content/Fonts/CratesSmall.fnt` | Small HUD font |

---

## Table Layout System

`Table` is the primary layout container — works like HTML tables with more flexibility. It's the workhorse of all PitHero UI layouts.

### Core Pattern: Nested Tables

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

### Cell Configuration Methods

**Size:**
- `.Width(float)` / `.Height(float)` — fixed dimensions
- `.Size(w, h)` — set both
- `.SetMinWidth()` / `.SetMinHeight()` — minimum constraints
- `.SetMaxWidth()` / `.SetMaxHeight()` — maximum constraints

**Padding (inside cell, around element):**
- `.Pad(float)` — all sides
- `.Pad(top, left, bottom, right)` — individual
- `.SetPadTop()` / `.SetPadLeft()` / `.SetPadBottom()` / `.SetPadRight()`

**Fill & Expand:**
- `.Fill()` — fill both axes within cell
- `.Expand()` — claim extra space
- `.Grow()` — expand AND fill (shorthand)
- `.SetFillX()` / `.SetFillY()` — single axis
- `.SetExpandX()` / `.SetExpandY()` — single axis

**Alignment (within cell):**
- `.Left()` / `.Center()` / `.Right()` — horizontal
- `.Top()` / `.Bottom()` — vertical
- Combinable: `.Top().Left()`

**Grid:**
- `.Row()` — start new row
- `.SetColspan(int)` — span multiple columns

### Table-Level Methods

```csharp
table.SetFillParent(true);           // fill entire stage/parent
table.SetBackground(drawable);       // background drawable
table.Defaults().SetPadBottom(8f);   // default cell settings
table.Top().Left();                  // table alignment within bounds
table.Pad(10f);                      // padding around table edges
table.Clip = true;                   // clip overflow
```

---

## Window

`Window` extends `Table`, adding a title bar and drag/resize support.

```csharp
var windowStyle = skin.Get<WindowStyle>("ph-default");
var window = new Window("Title", windowStyle);
window.SetSize(450, 350);
window.SetMovable(false);   // disable dragging if needed

// Add content (Window IS a Table)
window.Pad(10f);
window.Add(contentTable).Expand().Fill();

// Center on stage
window.SetPosition(
    (stage.GetWidth() - window.GetWidth()) / 2f,
    (stage.GetHeight() - window.GetHeight()) / 2f
);

stage.AddElement(window);
```

**Key properties:**
- `IsMovable` (default `true`) — can be dragged
- `IsResizable` — can be resized
- `KeepWithinStage` — clamp to stage bounds

---

## TabPane

```csharp
var tabWindowStyle = skin.Get<TabWindowStyle>("ph-default");
var tabPane = new TabPane(tabWindowStyle);

// Create tabs (Tab extends Table)
var tab1 = new Tab("Settings", tabStyle);
var tab2 = new Tab("Info", tabStyle);

// Populate tabs
tab1.Add(scrollPane).Expand().Fill().Pad(20);
tab2.Add(infoTable).Expand().Fill().Pad(20);

tabPane.AddTab(tab1);
tabPane.AddTab(tab2);

// Add to parent window (flush, no extra padding)
window.Add(tabPane).Expand().Fill().Pad(0);
```

---

## ScrollPane

```csharp
var contentTable = new Table();
contentTable.Top().Left();
// ... add content rows ...

var scrollPane = new ScrollPane(contentTable, skin, "ph-default");
scrollPane.SetScrollingDisabled(true, false);  // vertical only
scrollPane.SetFadeScrollBars(false);           // always show scrollbars

parent.Add(scrollPane).Expand().Fill();
```

**Key properties:**
- `SetScrollingDisabled(horizontal, vertical)` — disable axes
- `SetFadeScrollBars(bool)` — fade when idle
- `SmoothScrolling` — smooth scroll animation
- `ScrollSpeed` — scroll sensitivity (default 0.05f)

---

## Dialogs & Popups

### ConfirmationDialog Pattern

PitHero uses `ConfirmationDialog` (extends `Window`) for Yes/No prompts:

```csharp
var dialog = new ConfirmationDialog(
    "Confirm",
    "Are you sure?",
    skin,
    onYes: () => DoSomething(),
    onNo: () => { /* optional */ }
);
dialog.Show(stage);  // centers and adds to stage
```

Dialogs self-remove via `Remove()` when a button is clicked.

### Manual Dialog Pattern

```csharp
var window = new Window("Info", skin.Get<WindowStyle>("ph-default"));
window.SetSize(350, 180);
window.SetMovable(false);

var dialogTable = new Table();
dialogTable.Pad(20);

var label = new Label(message, skin);
label.SetWrap(true);                          // enable word wrapping
dialogTable.Add(label).Width(300f).SetPadBottom(20);
dialogTable.Row();

var closeButton = new TextButton("OK", skin);
closeButton.OnClicked += (btn) => window.Remove();
dialogTable.Add(closeButton).Width(80);

window.Add(dialogTable).Expand().Fill();

// Center on stage
window.SetPosition(
    (stage.GetWidth() - window.GetWidth()) / 2f,
    (stage.GetHeight() - window.GetHeight()) / 2f
);
stage.AddElement(window);
```

---

## Event Handling

### Button Clicks
```csharp
button.OnClicked += (btn) => HandleClick();
```

### CheckBox / Toggle
```csharp
checkbox.OnChanged += (isChecked) => { /* handle toggle */ };
```

### Slider / EnhancedSlider
```csharp
// Continuous updates
slider.OnChanged += (value) => UpdateLabel(value);

// Deferred commit (EnhancedSlider only — fires on mouse release)
enhancedSlider.OnValueCommitted += (value) => ApplySetting(value);
```

### Custom Events (delegate pattern)
```csharp
public event System.Action<IItem, InventorySlot> OnItemHovered;
public event System.Action OnItemUnhovered;

// Fire events
OnItemHovered?.Invoke(item, slot);
```

---

## Custom UI Element Patterns

### Extending Nez Elements

PitHero extends Nez UI classes for custom behavior. Preferred approach: **inherit and override first**; only duplicate if inheritance is insufficient.

| PitHero Class | Base Class | Purpose |
|---------------|------------|---------|
| `HoverableImageButton` | `ImageButton` | ImageButton with tooltip hover |
| `ResettableTextButton` | `TextButton` | TextButton with state reset |
| `EnhancedSlider` | `ProgressBar` + `IInputListener` | Slider with deferred commit |
| `ConfirmationDialog` | `Window` | Yes/No dialog |
| `ItemCard` | `Window` | Item stats display |
| `StencilLibraryPanel` | `Window` | Scrollable stencil grid |
| `MercenaryHireDialog` | `Table` | Mercenary stats + hire button |
| `ReorderableTableList<T>` | `Table` | Generic reorderable list |
| `InventoryGrid` | `Group` | 20×9 slot grid with synergy |
| `ShortcutBar` | `Group` | 8-slot horizontal bar |
| `DragDropOverlay` | `Element` | Ghost sprite that follows cursor during drag |
| `ItemCardTooltip` | `Tooltip` | Cached item stat card that follows cursor |

### IInputListener Interface

Implement `IInputListener` for custom mouse/touch handling:

```csharp
public interface IInputListener
{
    void OnMouseEnter();
    void OnMouseExit();
    bool OnLeftMousePressed(Vector2 mousePos);   // return true to track
    bool OnRightMousePressed(Vector2 mousePos);
    void OnMouseMoved(Vector2 mousePos);
    void OnLeftMouseUp(Vector2 mousePos);
    void OnRightMouseUp(Vector2 mousePos);
    bool OnMouseScrolled(int mouseWheelDelta);   // return true to consume
}
```

---

## Drag-and-Drop Pattern

PitHero implements drag-and-drop on top of `IInputListener` using a three-class pattern: a **slot element** that fires events, a **container** that handles them, and a **static coordinator** (`InventoryDragManager`) for cross-component drops.

### Architecture Overview

```
InventorySlot / ShortcutSlotVisual / SkillButton
   → fires OnDragStarted / OnDragMoved / OnDragDropped

InventoryGrid / ShortcutBar (containers)
   → subscribes to slot events
   → calls InventoryDragManager.BeginDrag / UpdateDrag / EndDrag / CancelDrag
   → hit-tests children via GetSlotAtStagePosition()

InventoryDragManager (static coordinator)
   → owns DragDropOverlay (ghost sprite following cursor)
   → fires OnDropRequested / OnSkillDropRequested for cross-panel drops
   → ShortcutBar subscribes via ConnectToDragManager()
```

### Deferred Click + Drag Detection in IInputListener

The click-vs-drag decision is deferred to `OnLeftMouseUp`. `OnMouseMoved` accumulates distance and promotes to drag mode if threshold exceeded.

```csharp
// Fields
private bool _mouseDown;
private Vector2 _mousePressPos;
private bool _isDragging;

bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
{
    _mouseDown = true;
    _mousePressPos = mousePos;
    _isDragging = false;
    return true;   // must return true to receive OnMouseMoved / OnLeftMouseUp
}

void IInputListener.OnMouseMoved(Vector2 mousePos)
{
    if (!_mouseDown || _item == null) return;

    if (!_isDragging)
    {
        float threshold = GameConfig.DragThresholdPixels;   // 4px
        if (Vector2.DistanceSquared(mousePos, _mousePressPos) >= threshold * threshold)
        {
            _isDragging = true;
            OnDragStarted?.Invoke(this, mousePos);
        }
    }

    if (_isDragging)
        OnDragMoved?.Invoke(this, mousePos);
}

void IInputListener.OnLeftMouseUp(Vector2 mousePos)
{
    bool wasDragging = _isDragging;
    _mouseDown = false;
    _isDragging = false;

    if (wasDragging)
    {
        OnDragDropped?.Invoke(this, mousePos);
        return;
    }

    // No drag occurred — treat as a normal click
    OnSlotClicked?.Invoke(this);
}
```

### DragDropOverlay — Ghost Sprite

`DragDropOverlay` is a stage-level `Element` that renders the dragged item/skill at the cursor with 70% alpha. It is owned by `InventoryDragManager` (or the local container for shortcut-to-shortcut drags) and brought to front when a drag begins.

```csharp
// DragDropOverlay usage (managed internally by InventoryDragManager)
var overlay = new DragDropOverlay();
stage.AddElement(overlay);
overlay.SetVisible(false);
overlay.ToFront();

overlay.BeginDrag(new SpriteDrawable(sprite));   // show ghost
overlay.UpdatePosition(stagePos);               // call every OnDragMoved
overlay.EndDrag();                              // hide on drop or cancel
```

### InventoryDragManager — Cross-Component Coordinator

`InventoryDragManager` is a **static** class that holds the active drag state and routes drops to subscriber components.

```csharp
// --- Starting a drag ---
// From an inventory slot (items):
InventoryDragManager.BeginDrag(sourceSlot, GetStage());

// From a skill button (skills):
InventoryDragManager.BeginSkillDrag(skill, _stage);

// --- During drag ---
InventoryDragManager.UpdateDrag(stagePos);   // called each OnDragMoved

// --- On drop ---
// Successful local drop (same panel):
source.SetItemSpriteHidden(false);
SwapSlotItems(source, target);
InventoryDragManager.EndDrag();             // also restores source sprite

// No local target — broadcast to other panels:
InventoryDragManager.NotifyDropRequested(source, stagePos);
// If nothing handled it:
if (InventoryDragManager.IsDragging)
    InventoryDragManager.CancelDrag();      // restores source sprite

// For skill drags with no local target:
InventoryDragManager.NotifySkillDropRequested(skill, stagePos);
if (InventoryDragManager.IsDragging)
    InventoryDragManager.CancelDrag();
```

**Critical rule**: Always call `EndDrag()` (success) or `CancelDrag()` (failure/cancel) — both restore the source slot's sprite visibility.

### Hit-Testing Drop Targets

Containers determine the drop target by hit-testing all child slots in stage coordinates:

```csharp
private InventorySlot GetSlotAtStagePosition(Vector2 stagePos)
{
    for (int i = 0; i < _slots.Length; i++)
    {
        var slot = _slots.Buffer[i];
        if (slot == null) continue;
        var topLeft = slot.LocalToStageCoordinates(Vector2.Zero);
        if (stagePos.X >= topLeft.X && stagePos.X <= topLeft.X + slot.GetWidth() &&
            stagePos.Y >= topLeft.Y && stagePos.Y <= topLeft.Y + slot.GetHeight())
            return slot;
    }
    return null;
}
```

**Note**: Always convert local mouse coordinates to stage coordinates with `element.LocalToStageCoordinates(mousePos)` before calling `GetSlotAtStagePosition()`.

### Cross-Panel Subscription (ConnectToDragManager)

Components that can **receive** drops from another panel subscribe to `InventoryDragManager` events:

```csharp
// Wire in scene setup (e.g., MainGameScene)
shortcutBar.ConnectToDragManager();

// Inside ShortcutBar:
public void ConnectToDragManager()
{
    InventoryDragManager.OnDropRequested += HandleInventoryDropOnShortcut;
    InventoryDragManager.OnSkillDropRequested += HandleSkillDropOnShortcut;
}

private void HandleInventoryDropOnShortcut(InventorySlot inventorySource, Vector2 stagePos)
{
    int index = GetShortcutIndexAtStagePosition(stagePos);
    if (index < 0) { InventoryDragManager.CancelDrag(); return; }

    // Only consumables allowed on the shortcut bar
    if (inventorySource?.SlotData?.Item is not Consumable)
    {
        InventoryDragManager.CancelDrag();
        return;
    }

    SetShortcutReference(index, inventorySource);
    InventoryDragManager.EndDrag();
}

private void HandleSkillDropOnShortcut(ISkill skill, Vector2 stagePos)
{
    int index = GetShortcutIndexAtStagePosition(stagePos);
    if (index < 0) { InventoryDragManager.CancelDrag(); return; }
    SetShortcutSkill(index, skill);
    InventoryDragManager.EndDrag();
}
```

### Remove-on-Drop-Outside Pattern

When a slot is dragged and dropped outside all valid targets, the slot is cleared:

```csharp
private void HandleShortcutDragDropped(int index, ShortcutSlotVisual slot, Vector2 mousePos)
{
    var stagePos = slot.LocalToStageCoordinates(mousePos);
    int targetIndex = GetShortcutIndexAtStagePosition(stagePos);

    slot.SetItemSpriteHidden(false);   // always restore first

    if (targetIndex >= 0 && targetIndex != index)
        SwapShortcuts(index, targetIndex);   // dropped on different slot
    else if (targetIndex < 0)
        ClearShortcutReference(index);       // dropped outside — remove

    _shortcutDragOverlay?.EndDrag();
}
```

### Drag-and-Drop Gotchas

| Gotcha | Fix |
|--------|-----|
| Source sprite disappears after drop | Call `EndDrag()` (not just `CancelDrag()`). `EndDrag()` calls `SetItemSpriteHidden(false)` on the source slot. |
| Skill icon not following cursor | Load skill sprite from `SkillsStencils.atlas` using `skill.Id`; fall back to `UI.atlas/"SkillIcon1"`. Items use `Items.atlas` with `item.SpriteName` (not `item.Name`). |
| Sprite hidden during shortcut skill drag | The `else if (_referencedSkill != null)` branch in `Draw()` must also check `!_hideItemSprite`. |
| Drop not detected on target panel | Subscribe via `ConnectToDragManager()` in scene setup; `NotifyDropRequested` / `NotifySkillDropRequested` fires only when the source panel finds no local target. |
| Click fires even after drag | Track `wasDragging` flag in `OnLeftMouseUp` and return early when `true`. |

---

## Drawable System

### IDrawable Implementations

| Type | Usage | Example |
|------|-------|---------|
| `PrimitiveDrawable` | Colored rectangles | `new PrimitiveDrawable(Color.Black)` |
| `NinePatchDrawable` | Scalable bordered panels | `new NinePatchDrawable(sprite, left, right, top, bottom)` |
| `SpriteDrawable` | Static sprites | `new SpriteDrawable(sprite)` |

### NinePatch from Atlas (PitHero pattern)

```csharp
var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
var sprite = uiAtlas.GetSprite("NinePatchButton_Up");
var ninePatch = new NinePatchSprite(sprite, 4, 4, 4, 4);  // left, right, top, bottom margins
var drawable = new NinePatchDrawable(ninePatch);
drawable.SetPadding(0, 0, 25, 25);  // force text centering
```

### PrimitiveDrawable for Simple Backgrounds

```csharp
// Button from colors (must set min size — texture is 1×1)
var button = new Button(ButtonStyle.Create(Color.Black, Color.DarkGray, Color.Green));
table.Add(button).SetMinWidth(100).SetMinHeight(30);
```

---

## Gamepad Input

Enable gamepad navigation by setting the first focusable element:

```csharp
stage.SetGamepadFocusElement(firstButton);
```

For explicit focus control (required for Sliders):

```csharp
leftButton.ShouldUseExplicitFocusableControl = true;
leftButton.GamepadRightElement = middleSlider;
leftButton.GamepadLeftElement = rightButton;  // optional wrap-around

middleSlider.ShouldUseExplicitFocusableControl = true;
middleSlider.GamepadLeftElement = leftButton;
middleSlider.GamepadRightElement = rightButton;
```

Default action button: A (gamepad) / Enter (keyboard). Customizable via `stage.GamepadActionButton` and `stage.KeyboardActionKey`.

---

## Element Lifecycle & Cleanup

- **Add to stage:** `stage.AddElement(element)` — element starts receiving input/rendering
- **Remove from stage:** `element.Remove()` — removes from parent (stage or group)
- **Visibility:** `element.SetVisible(false)` — hides but keeps in hierarchy
- **Dialog self-removal:** Call `Remove()` in button callbacks

```csharp
// Dialog self-cleanup pattern
closeButton.OnClicked += (btn) =>
{
    onComplete?.Invoke();
    Remove();  // remove window from stage
};
```

---

## UI Class Organization Pattern

```csharp
public class MyFeatureUI
{
    private Stage _stage;
    private Skin _skin;

    // UI element references (for later updates)
    private Label _statusLabel;
    private Window _mainWindow;

    /// <summary>Initializes UI on the given stage.</summary>
    public void InitializeUI(Stage stage)
    {
        _stage = stage;
        _skin = PitHeroSkin.CreateSkin();

        CreateMainWindow();
        CreateDialogs();
    }

    private void CreateMainWindow()
    {
        var windowStyle = _skin.Get<WindowStyle>("ph-default");
        _mainWindow = new Window("My Feature", windowStyle);
        _mainWindow.SetSize(400, 300);

        var contentTable = new Table();
        contentTable.Pad(10f);

        _statusLabel = new Label("Ready", _skin, "ph-default");
        contentTable.Add(_statusLabel).Left().SetPadBottom(8f);
        contentTable.Row();

        var actionButton = new TextButton("Do Thing", _skin, "ph-default");
        actionButton.OnClicked += (btn) => HandleAction();
        contentTable.Add(actionButton).Width(100).Height(30);

        _mainWindow.Add(contentTable).Expand().Fill();

        // Center on stage
        _mainWindow.SetPosition(
            (_stage.GetWidth() - _mainWindow.GetWidth()) / 2f,
            (_stage.GetHeight() - _mainWindow.GetHeight()) / 2f
        );

        _stage.AddElement(_mainWindow);
    }
}
```

---

## Item Tooltip Caching & Hover Safety

### ItemCardTooltip

`ItemCardTooltip` (extends `Tooltip`) displays a rich item stat card at the cursor position. Building its content (`RebuildContent()`) creates many `Label`/`Image` widgets and is expensive. A per-instance content cache skips rebuilds when the same item is re-hovered.

**Showing the tooltip:**
```csharp
// Simplest — no synergies, no buy price
_itemTooltip.ShowItem(item);

// With synergies (from InventoryGrid.GetSynergiesForSlot)
_itemTooltip.ShowItem(item, synergies);

// With buy price (vault items)
_itemTooltip.ShowItem(item, showBuyPrice: true);
```

**Caching behaviour:** `ShowItem()` compares the incoming item *reference* (`ReferenceEquals`), synergy count, and `showBuyPrice` flag against the last build. If all match, it skips `RebuildContent()` entirely and just calls `Pack()`. The rebuild happens when:
- A different item reference is passed
- Synergy count changes (e.g. new stencil placed)
- Price mode changes

**When to invalidate the cache:**
```csharp
_itemTooltip.InvalidateCache();   // forces a full rebuild on the next ShowItem call
```
Call `InvalidateCache()` in these situations:
- When the hero window or shop opens (items may have changed since last open)
- When `RefreshShopData()` is called in `SecondChanceShopUI`
- When a new save is loaded (handled automatically because window opens fresh)

**Showing/hiding in the stage:**
```csharp
// Show
_itemTooltip.ShowItem(item, synergies);
if (_itemTooltip.GetContainer().GetParent() == null)
    _stage.AddElement(_itemTooltip.GetContainer());
_itemTooltip.GetContainer().ToFront();

// Hide
_itemTooltip.GetContainer().Remove();

// Check if visible
bool isShowing = _itemTooltip.GetContainer().HasParent();
```

**Positioning (call every frame while shown):**
```csharp
var mousePos = _stage.GetMousePosition();
var container = _itemTooltip.GetContainer();
container.Validate();   // ensure size is calculated before clamping
float tx = mousePos.X + 10f;
float ty = mousePos.Y + 10f;
float stageH = _stage.GetHeight();
if (ty + container.GetHeight() > stageH)
    ty = stageH - container.GetHeight();
if (ty < 0) ty = 0;
container.SetPosition(tx, ty);
```

---

### Periodic Hover Safety Check

Nez UI hover events (`OnMouseEnter`) fire once when the mouse enters an element. If the event is missed (e.g. window opens while cursor is already over a slot, or the event is consumed by an overlapping element), the tooltip never appears.

**Pattern:** In the owning UI class's `Update()` method, add a frame counter and every 5 frames hit-test all slots against the mouse position. If a slot with content is under the cursor and the tooltip is not showing, trigger the hover handler directly.

```csharp
// Field
private int _hoverCheckFrame;

// In Update():
_hoverCheckFrame++;
if (_hoverCheckFrame % 5 == 0)
    PerformPeriodicHoverCheck();

// Safety net method:
private void PerformPeriodicHoverCheck()
{
    if (_tooltip == null) return;
    if (_tooltip.GetContainer().HasParent()) return;   // already showing — do nothing

    var mousePos = _stage.GetMousePosition();

    // Hit-test all slots manually
    for (int i = 0; i < _slots.Length; i++)
    {
        var slot = _slots[i];
        if (slot == null || slot.Item == null) continue;
        var topLeft = slot.LocalToStageCoordinates(Vector2.Zero);
        if (mousePos.X >= topLeft.X && mousePos.X <= topLeft.X + slot.GetWidth() &&
            mousePos.Y >= topLeft.Y && mousePos.Y <= topLeft.Y + slot.GetHeight())
        {
            HandleSlotHovered(slot);   // reuse the normal hover handler
            return;
        }
    }
}
```

**Rules:**
- Use `% 5` (every 5 frames), not every frame — avoids calling expensive slot traversal at 60 Hz.
- Guard with `HasParent()` / `IsVisible()` so the check is a no-op when the tooltip is already on screen.
- Reuse the existing `HandleSlotHovered` / `OnSlotHovered` handler — do not duplicate tooltip positioning logic.
- The check belongs in the UI class that owns the tooltip, not in the slot element itself.

**Where this is applied in PitHero:**

| UI Context | Tooltip Type | Where Update() is called |
|---|---|---|
| `HeroUI` inventory tab | `ItemCardTooltip` | `HeroUI.Update()` → `PerformPeriodicHoverCheck()` |
| `HeroUI` crystals tab | `Window`+`Label` | `HeroUI.Update()` → `_crystalsTabComponent.Update()` |
| `SecondChanceShopUI` vault items | `ItemCardTooltip` | `SecondChanceShopUI.Update()` → `_vaultItemGrid.Update(mousePos)` |
| `SecondChanceShopUI` hero inventory | `ItemCardTooltip` | `SecondChanceShopUI.Update()` → `PerformHeroInventoryPeriodicHoverCheck()` |
| `SecondChanceShopUI` vault crystals | `Window`+`Label` | `SecondChanceShopUI.Update()` → `_vaultCrystalGrid.Update(mousePos)` |
| `SecondChanceShopUI` hero crystals | `Window`+`Label` | `SecondChanceShopUI.Update()` → `_heroCrystalPanel.Update(mousePos)` |

**For Window+Label crystal tooltips** (cheaper, no cache needed), check `GetParent() != null && IsVisible()` instead of `HasParent()` since `Remove()` is called on unhover:
```csharp
if (_hoverTooltipWindow != null && _hoverTooltipWindow.GetParent() != null && _hoverTooltipWindow.IsVisible()) return;
```

---

## Quick Reference: Common Gotchas

| Gotcha | Fix |
|--------|-----|
| Text too small / too large | Load a different font file — never use `SetFontScale()` |
| Style colors bleeding across elements | Create a **new** style instance, don't mutate `"ph-default"` |
| Button has no visible size | Set `.SetMinWidth()` / `.SetMinHeight()` on PrimitiveDrawable buttons |
| ScrollPane not scrolling | Ensure content is taller than ScrollPane; call `.SetScrollingDisabled(true, false)` for vertical-only |
| Window not centered | Use `(stage.GetWidth() - window.GetWidth()) / 2f` formula |
| Tab content not filling | Chain `.Expand().Fill()` on the TabPane cell |
| UI elements not responding to input | Check `SetTouchable(Touchable.Enabled)` and render layer order |
| Font not showing | Use `Graphics.Instance.BitmapFont` or load via `Content.LoadBitmapFont(path)` |
| Drag source sprite disappears | Always call `InventoryDragManager.EndDrag()` or `CancelDrag()` — both restore `SetItemSpriteHidden(false)` |
| Wrong sprite shown during drag | Items atlas: use `item.SpriteName`. Skills atlas: `SkillsStencils.atlas` keyed by `skill.Id`, fall back to `UI.atlas/"SkillIcon1"` |
| Item tooltip doesn't appear on hover | Add a periodic hover check (every 5 frames) in the owning UI's `Update()` — see "Periodic Hover Safety Check" section |
| Item tooltip rebuilds on every hover (slow) | Call `_itemTooltip.ShowItem()` — it skips `RebuildContent()` when the same item ref + synergy count + price mode is already built |
| Tooltip shows stale stats after save load or window reopen | Call `_itemTooltip.InvalidateCache()` when the window opens or `RefreshShopData()` is called |