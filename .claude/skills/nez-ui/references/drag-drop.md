# Drag-and-Drop Pattern

PitHero drag-and-drop builds on `IInputListener` with three classes: a **slot element** that fires events, a **container** that handles them, and a **static coordinator** (`InventoryDragManager`) for cross-component drops.

## Architecture

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

## Deferred Click + Drag Detection in IInputListener

The click-vs-drag decision is deferred to `OnLeftMouseUp`. `OnMouseMoved` accumulates distance and promotes to drag mode if the threshold is exceeded.

```csharp
private bool _mouseDown;
private Vector2 _mousePressPos;
private bool _isDragging;

bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
{
    _mouseDown     = true;
    _mousePressPos = mousePos;
    _isDragging    = false;
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
    _mouseDown  = false;
    _isDragging = false;

    if (wasDragging)
    {
        OnDragDropped?.Invoke(this, mousePos);
        return;
    }

    // No drag → normal click
    OnSlotClicked?.Invoke(this);
}
```

## DragDropOverlay — Ghost Sprite

`DragDropOverlay` is a stage-level `Element` rendering the dragged item/skill at the cursor at 70% alpha. Owned by `InventoryDragManager` (or the local container for shortcut-to-shortcut drags), brought to front when a drag begins.

```csharp
var overlay = new DragDropOverlay();
stage.AddElement(overlay);
overlay.SetVisible(false);
overlay.ToFront();

overlay.BeginDrag(new SpriteDrawable(sprite));   // show ghost
overlay.UpdatePosition(stagePos);                // call every OnDragMoved
overlay.EndDrag();                               // hide on drop or cancel
```

## InventoryDragManager — Cross-Component Coordinator

Static class holding active drag state, routing drops to subscribers.

```csharp
// --- Starting a drag ---
InventoryDragManager.BeginDrag(sourceSlot, GetStage());           // items
InventoryDragManager.BeginSkillDrag(skill, _stage);               // skills

// --- During drag ---
InventoryDragManager.UpdateDrag(stagePos);   // each OnDragMoved

// --- On drop ---
// Successful local drop (same panel):
source.SetItemSpriteHidden(false);
SwapSlotItems(source, target);
InventoryDragManager.EndDrag();   // also restores source sprite

// No local target — broadcast to other panels:
InventoryDragManager.NotifyDropRequested(source, stagePos);
if (InventoryDragManager.IsDragging)
    InventoryDragManager.CancelDrag();   // restores source sprite

// For skill drags with no local target:
InventoryDragManager.NotifySkillDropRequested(skill, stagePos);
if (InventoryDragManager.IsDragging)
    InventoryDragManager.CancelDrag();
```

**Critical:** Always call `EndDrag()` (success) or `CancelDrag()` (failure/cancel) — both restore the source slot's sprite visibility.

## Hit-Testing Drop Targets

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

**Note:** Always convert local mouse coordinates to stage coordinates with `element.LocalToStageCoordinates(mousePos)` before calling `GetSlotAtStagePosition()`.

## Cross-Panel Subscription (ConnectToDragManager)

Components that **receive** drops from another panel subscribe to `InventoryDragManager` events:

```csharp
// Wire in scene setup (e.g., MainGameScene)
shortcutBar.ConnectToDragManager();

// Inside ShortcutBar:
public void ConnectToDragManager()
{
    InventoryDragManager.OnDropRequested      += HandleInventoryDropOnShortcut;
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

## Remove-on-Drop-Outside

When a slot is dragged and dropped outside all valid targets, clear the slot:

```csharp
private void HandleShortcutDragDropped(int index, ShortcutSlotVisual slot, Vector2 mousePos)
{
    var stagePos    = slot.LocalToStageCoordinates(mousePos);
    int targetIndex = GetShortcutIndexAtStagePosition(stagePos);

    slot.SetItemSpriteHidden(false);   // always restore first

    if (targetIndex >= 0 && targetIndex != index)
        SwapShortcuts(index, targetIndex);
    else if (targetIndex < 0)
        ClearShortcutReference(index);

    _shortcutDragOverlay?.EndDrag();
}
```

## Gotchas

| Gotcha | Fix |
|---|---|
| Source sprite disappears after drop | Call `EndDrag()` (not just `CancelDrag()`) — both restore `SetItemSpriteHidden(false)` |
| Skill icon not following cursor | Load from `SkillsStencils.atlas` using `skill.Id`; fall back to `UI.atlas/"SkillIcon1"`. Items use `Items.atlas` with `item.SpriteName` (not `item.Name`) |
| Sprite hidden during shortcut skill drag | The `else if (_referencedSkill != null)` branch in `Draw()` must also check `!_hideItemSprite` |
| Drop not detected on target panel | Subscribe via `ConnectToDragManager()` in scene setup |
| Click fires even after drag | Track `wasDragging` flag in `OnLeftMouseUp` and return early when `true` |
