# Feature: Shortcut Bar Item Restore After Save/Load

## Objective
Fix the bug where inventory items assigned to shortcut bar slots are not restored after save/load, while skills restore correctly.

## Research Summary

### Root Cause
**`InventoryGrid._heroComponent` is null during shortcut bar item restoration.**

The initialization order during load is:

1. `MainGameScene.Initialize()` → creates `InventoryGrid` inside `HeroUI.PopulateInventoryTab()`. At this point, the hero entity doesn't exist yet, so `GetHeroComponent()` returns null and `InventoryGrid.ConnectToHero()` is **not called**.
2. `MainGameScene.Begin()`:
   - `SpawnHero()` → creates hero entity with `HeroComponent` (but `OnAddedToEntity` is deferred by Nez, so `Bag` is still null).
   - `ConnectShortcutBarToHero()` → gets `inventoryGrid` via `_settingsUI.HeroUI.GetInventoryGrid()` and passes it to `_shortcutBar.ConnectToHero(heroComponent, inventoryGrid)`. **The InventoryGrid itself is never connected to the hero here.**
   - `ApplyPendingLoadData()` → stores `PendingInventoryItems` on HeroComponent, stores pending shortcut slots on ShortcutBar.
3. Nez fires `HeroComponent.OnAddedToEntity()` → creates `Bag`, restores items from `PendingInventoryItems`, sets `PendingInventoryItems = null`. **Does not fire `OnInventoryChanged`.**
4. First `Update()` → `_shortcutBar.RefreshItems()` → `RefreshVisualSlots()` → `TryRestorePendingShortcuts()`:
   - All guard checks pass (`_pendingShortcutSlots != null`, `_heroComponent != null`, `_inventoryGrid != null`, `Bag != null`, `PendingInventoryItems == null`).
   - Calls `_inventoryGrid.UpdateItemsFromBag()` → **returns immediately** because `InventoryGrid._heroComponent` is null (never connected).
   - `_inventoryGrid.FindSlotContainingItem(item)` returns null because InventoryGrid slots were never populated.
   - Item shortcuts silently fail to restore.

**Why skills work:** Skill restoration uses `_heroComponent.LinkedHero.LearnedSkills` which goes through the ShortcutBar's own `_heroComponent` field (correctly set during `ConnectToHero`), bypassing InventoryGrid entirely.

### Relevant Files & Why
- [MainGameScene.cs](PitHero/ECS/Scenes/MainGameScene.cs) — `ConnectShortcutBarToHero()` does not connect InventoryGrid to hero; this is where the fix goes.
- [InventoryGrid.cs](PitHero/UI/InventoryGrid.cs) — `ConnectToHero()` adds event subscriptions with `+=` each call without unsubscribing first. Must be made idempotent since it will now be called during `Begin()` and again each time the inventory window opens (line 683 of HeroUI.cs).
- [ShortcutBar.cs](PitHero/UI/ShortcutBar.cs) — `TryRestorePendingShortcuts()` is the deferred restoration method. No changes needed here; the logic is correct once InventoryGrid is properly connected.
- [HeroUI.cs](PitHero/UI/HeroUI.cs) — Line 683 calls `ConnectToHero` every time inventory opens. No changes needed, but must be aware of interaction.
- [SaveLoadService.cs](PitHero/Services/SaveLoadService.cs) — Save/load serialization. No changes needed; data round-trip is correct (confirmed by existing unit tests).
- [SaveData.cs](PitHero/Services/SaveData.cs) — Persist/Recover for shortcut slots. No changes needed.

### Existing Patterns to Reuse
- `ReconnectUIToHero()` in MainGameScene already connects both ShortcutBar AND InventoryGrid to the hero — it's the pattern for the initial setup to follow.
- `InventoryGrid.ConnectToHero()` is designed to be called multiple times (HeroUI line 683 calls it every time the inventory opens).

### Constraints & Rules
- AOT compliance: no LINQ, no `foreach`, no dynamic strings in game loop.
- Use `for` loops for iteration.
- Event unsubscription uses `-=` before `+=` to prevent duplicate handlers.
- Must compile with `dotnet build` and pass `dotnet test PitHero.Tests/`.

### Risks / Edge Cases
- **Duplicate event subscriptions**: If `ConnectToHero` is called during `Begin()` and again when the inventory opens, event handlers would fire multiple times per event. Making `ConnectToHero` idempotent eliminates this.
- **Hero reconnection after promotion**: `ReconnectUIToHero()` already connects InventoryGrid, so the fix is consistent with that path.
- **Fresh game (no load data)**: `TryRestorePendingShortcuts` does nothing when `_pendingShortcutSlots` is null, so no impact on new games.

## Scope

### In Scope
- Fix InventoryGrid not being connected to HeroComponent during initial load sequence.
- Make `InventoryGrid.ConnectToHero()` idempotent (safe for multiple calls).
- Verify fix works with existing save/load unit tests.

### Out of Scope
- No new monsters required.
- No new equipment required.
- No virtual game layer changes required.
- No save format changes (data serialization is correct).

## Constraints & Standards
- Follow AOT rules (for loops, no LINQ, const strings).
- Use `/// <summary>` comments on any new methods.
- Run `dotnet build` and `dotnet test PitHero.Tests/` after changes.

## Implementation Phases

### Phase 1: Make InventoryGrid.ConnectToHero() Idempotent
**Task 1.1**: Unsubscribe from events before subscribing in `InventoryGrid.ConnectToHero()`.

**Subtasks**:
- In `InventoryGrid.ConnectToHero()`, add `-=` calls for all three event subscriptions before the existing `+=` calls:
  ```csharp
  // Unsubscribe before re-subscribing to prevent duplicate handlers
  InventorySelectionManager.OnInventoryChanged -= UpdateItemsFromBag;
  InventorySelectionManager.OnSelectionCleared -= ClearLocalSelectionState;
  InventorySelectionManager.OnCrossComponentSwapAnimate -= HandleCrossComponentSwapAnimate;
  
  // Subscribe to cross-component inventory changes
  InventorySelectionManager.OnInventoryChanged += UpdateItemsFromBag;
  InventorySelectionManager.OnSelectionCleared += ClearLocalSelectionState;
  InventorySelectionManager.OnCrossComponentSwapAnimate += HandleCrossComponentSwapAnimate;
  ```

**Target files**: `PitHero/UI/InventoryGrid.cs` — `ConnectToHero()` method (around line 190-210).

**Definition of done**: `ConnectToHero` can be called multiple times without accumulating duplicate event handlers.

### Phase 2: Connect InventoryGrid to Hero During Begin()
**Task 2.1**: In `MainGameScene.ConnectShortcutBarToHero()`, call `inventoryGrid.ConnectToHero(heroComponent)` before passing the grid to the shortcut bar.

**Subtasks**:
- After retrieving `inventoryGrid` from `_settingsUI?.HeroUI?.GetInventoryGrid()`, add:
  ```csharp
  // Connect inventory grid to hero so shortcut bar restoration can populate grid slots
  if (inventoryGrid != null)
      inventoryGrid.ConnectToHero(heroComponent);
  ```
- This mirrors what `ReconnectUIToHero()` already does.

**Target files**: `PitHero/ECS/Scenes/MainGameScene.cs` — `ConnectShortcutBarToHero()` method (around line 1144-1170).

**Definition of done**: After `ConnectShortcutBarToHero()` returns, `InventoryGrid._heroComponent` is non-null, enabling `UpdateItemsFromBag()` to populate grid slots when called from `TryRestorePendingShortcuts()`.

## Test & Validation Plan

### Existing Test Coverage (must still pass)
- `ShortcutBarSlots_PreservedThroughSaveLoad` — verifies binary round-trip of shortcut slot data.
- `SaveData_Version1_LoadsWithoutShortcutSlots` — backward compatibility.
- `ShortcutSlotData_CreateItemReference_SetsCorrectType` — unit tests for slot data.
- All other `SaveLoadTests` and `SkillShortcutBarTests`.

### Manual Validation Steps
1. Start a game, pick up items, assign items to shortcut bar slots 1-3, assign a skill to slot 4.
2. Save the game.
3. Load the game.
4. Verify items appear in shortcut bar slots 1-3 with correct icons and stack counts.
5. Verify skill appears in shortcut bar slot 4.
6. Open inventory and verify items are in the correct bag positions.
7. Verify no duplicate log messages (confirming idempotent event subscriptions).

### Commands
- `dotnet build`
- `dotnet test PitHero.Tests/`

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Duplicate event handlers if ConnectToHero not made idempotent | High (if skipped) | Medium — double inventory refreshes | Phase 1 explicitly unsubscribes before subscribing |
| InventoryGrid.ConnectToHero at Begin() time has Bag=null | Expected | None — ConnectToHero handles null Bag by skipping UpdateItemsFromBag; TryRestorePendingShortcuts calls it later when Bag exists | Method handles null Bag gracefully |
| Side effects from early ConnectToHero call | Low | Low | ConnectToHero already handles null Bag; acquire maps only reset when hero instance changes |

## Open Questions / Decisions Needed
None — root cause is confirmed and fix is straightforward.

## Acceptance Criteria
1. After save/load, inventory items assigned to shortcut bar slots are visually restored with correct icon and stack count.
2. After save/load, skill shortcuts continue to restore correctly (no regression).
3. Opening the inventory window after load does not cause duplicate refreshes or visual glitches.
4. `dotnet build` succeeds.
5. `dotnet test PitHero.Tests/` passes all existing tests.
6. Fresh game (no save data) continues to work normally.

## Downstream Handoff Notes
- **Total changes**: 2 files, ~6 lines of code.
- **No architectural changes**: This is a bug fix addressing initialization order, not a design change.
- **No new files**: No new classes, tests, or config changes needed.
- The fix follows the same pattern already used in `ReconnectUIToHero()`.
