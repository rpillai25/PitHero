# Dialogs, Hover Tooltips, ItemCardTooltip, Periodic Hover Safety

## ConfirmationDialog

`ConfirmationDialog` (extends `Window`) is the standard Yes/No prompt:

```csharp
var dialog = new ConfirmationDialog(
    "Confirm",
    "Are you sure?",
    skin,
    onYes: () => DoSomething(),
    onNo:  () => { /* optional */ });
dialog.Show(stage);   // centers and adds to stage
```

Dialogs self-remove via `Remove()` when a button is clicked.

## Manual Dialog Pattern

```csharp
var window = new Window("Info", skin.Get<WindowStyle>("ph-default"));
window.SetSize(350, 180);
window.SetMovable(false);

var dialogTable = new Table();
dialogTable.Pad(20);

var label = new Label(message, skin);
label.SetWrap(true);                       // word wrapping
dialogTable.Add(label).Width(300f).SetPadBottom(20);
dialogTable.Row();

var closeButton = new TextButton("OK", skin);
closeButton.OnClicked += (btn) => window.Remove();
dialogTable.Add(closeButton).Width(80);

window.Add(dialogTable).Expand().Fill();
window.SetPosition(
    (stage.GetWidth()  - window.GetWidth())  / 2f,
    (stage.GetHeight() - window.GetHeight()) / 2f);
stage.AddElement(window);
```

## HoverableLabel — Simple Tooltips on Labels

Drop-in replacement for `Label` when a hover tooltip is needed. Implements `IInputListener` internally.

```csharp
var label = new HoverableLabel(
    GetText(TextType.UI, UITextKey.SomeLabel),
    skin, "ph-default",
    GetText(TextType.UI, UITextKey.SomeLabelTooltip),
    _stage);
container.Add(label).Left();
```

- The tooltip window is added to the stage in the constructor and stays hidden until hover.
- `OnMouseMoved` calls `_stage.GetMousePosition()` to track the cursor accurately — do NOT use the `mousePos` parameter (it's in local element space).

## HoverableTextButton — Simple Tooltips on Buttons

Drop-in replacement for `TextButton`. Uses `Draw()` polling (like `HoverableImageButton`) to track `_mouseOver`.

```csharp
var btn = new HoverableTextButton(
    GetText(TextType.UI, UITextKey.SomeButton),
    skin, "ph-default",
    GetText(TextType.UI, UITextKey.SomeButtonTooltip),
    _stage);
btn.OnClicked += OnSomeButtonClicked;
btn.OnClicked += (_) => btn.HideTooltip();   // suppress when opening another panel
```

**`HideTooltip()`** hides the tooltip and sets a `_suppressed` flag so it won't re-appear until the mouse physically leaves and re-enters the button. Always call it when the click opens a dialog or transitions to a new view — otherwise the tooltip lingers over the new UI. The tooltip also auto-hides whenever `IsVisible()` returns false (e.g. parent window closed).

## Tooltip Text Key Convention

Add a `*Tooltip` suffix to the base key name in both `UITextKey.cs` and `UI.txt`:

```
// UITextKey.cs
public const string CrystalForgeTitle        = "CrystalForgeTitle";
public const string CrystalForgeTitleTooltip = "CrystalForgeTitleTooltip";

// UI.txt
CrystalForgeTitle,Crystal Forge
CrystalForgeTitleTooltip,Combine mastered crystals to form more powerful crystal
```

## ItemCardTooltip — Rich Cached Tooltips

`ItemCardTooltip` (extends `Tooltip`) displays a rich item stat card at the cursor. Building its content (`RebuildContent()`) creates many `Label`/`Image` widgets and is expensive. A per-instance cache skips rebuilds when the same item is re-hovered.

### Showing

```csharp
_itemTooltip.ShowItem(item);                          // no synergies, no buy price
_itemTooltip.ShowItem(item, synergies);               // with synergies
_itemTooltip.ShowItem(item, showBuyPrice: true);      // with buy price (vault items)
```

### Caching Behavior

`ShowItem()` compares the incoming item *reference* (`ReferenceEquals`), synergy count, and `showBuyPrice` flag against the last build. If all match it skips `RebuildContent()` and just calls `Pack()`. Rebuild happens when:
- Different item reference
- Synergy count changes (e.g. new stencil placed)
- Price mode changes

### Cache Invalidation

```csharp
_itemTooltip.InvalidateCache();   // forces a full rebuild on the next ShowItem call
```

Call `InvalidateCache()`:
- When the hero window or shop opens (items may have changed since last open)
- When `RefreshShopData()` is called in `SecondChanceShopUI`
- When a new save is loaded (handled automatically because window opens fresh)

### Showing/Hiding in the Stage

```csharp
// Show
_itemTooltip.ShowItem(item, synergies);
if (_itemTooltip.GetContainer().GetParent() == null)
    _stage.AddElement(_itemTooltip.GetContainer());
_itemTooltip.GetContainer().ToFront();

// Hide
_itemTooltip.GetContainer().Remove();

// Check visible
bool isShowing = _itemTooltip.GetContainer().HasParent();
```

### Positioning (call every frame while shown)

```csharp
var mousePos  = _stage.GetMousePosition();
var container = _itemTooltip.GetContainer();
container.Validate();   // ensure size before clamping
float tx = mousePos.X + 10f;
float ty = mousePos.Y + 10f;
float stageH = _stage.GetHeight();
if (ty + container.GetHeight() > stageH)
    ty = stageH - container.GetHeight();
if (ty < 0) ty = 0;
container.SetPosition(tx, ty);
```

## Periodic Hover Safety Check

Nez UI hover events (`OnMouseEnter`) fire once when the mouse enters an element. If the event is missed (window opens while cursor is already over a slot, or the event is consumed by an overlapping element), the tooltip never appears.

**Pattern:** In the owning UI class's `Update()`, add a frame counter; every 5 frames hit-test all slots against the mouse. If a slot with content is under the cursor and the tooltip is not showing, trigger the hover handler directly.

```csharp
private int _hoverCheckFrame;

// In Update():
_hoverCheckFrame++;
if (_hoverCheckFrame % 5 == 0)
    PerformPeriodicHoverCheck();

private void PerformPeriodicHoverCheck()
{
    if (_tooltip == null) return;
    if (_tooltip.GetContainer().HasParent()) return;   // already showing

    var mousePos = _stage.GetMousePosition();

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
- Use `% 5` (every 5 frames), not every frame — avoids expensive slot traversal at 60 Hz.
- Guard with `HasParent()` / `IsVisible()` so the check is a no-op when the tooltip is already on screen.
- Reuse the existing `HandleSlotHovered` / `OnSlotHovered` handler — don't duplicate positioning logic.
- The check belongs in the UI class that owns the tooltip, not in the slot element itself.

### Where This is Applied in PitHero

| UI Context | Tooltip Type | Where Update() is called |
|---|---|---|
| `HeroUI` inventory tab | `ItemCardTooltip` | `HeroUI.Update()` → `PerformPeriodicHoverCheck()` |
| `HeroUI` crystals tab | `Window`+`Label` | `HeroUI.Update()` → `_crystalsTabComponent.Update()` |
| `SecondChanceShopUI` vault items | `ItemCardTooltip` | `SecondChanceShopUI.Update()` → `_vaultItemGrid.Update(mousePos)` |
| `SecondChanceShopUI` hero inventory | `ItemCardTooltip` | `SecondChanceShopUI.Update()` → `PerformHeroInventoryPeriodicHoverCheck()` |
| `SecondChanceShopUI` vault crystals | `Window`+`Label` | `SecondChanceShopUI.Update()` → `_vaultCrystalGrid.Update(mousePos)` |
| `SecondChanceShopUI` hero crystals | `Window`+`Label` | `SecondChanceShopUI.Update()` → `_heroCrystalPanel.Update(mousePos)` |

### Window+Label Crystal Tooltips

Cheaper tooltips (no cache needed) check `GetParent() != null && IsVisible()` instead of `HasParent()` since `Remove()` is called on unhover:

```csharp
if (_hoverTooltipWindow != null &&
    _hoverTooltipWindow.GetParent() != null &&
    _hoverTooltipWindow.IsVisible())
    return;
```
