# Feature: Drag-and-Drop Only Item / Crystal Movement

## 1. Feature Name
**Drag-and-drop only item/crystal movement** — single interaction model for moving every item and crystal in the game.

## 2. Objective
Drag-and-drop must become the **sole** interaction method for moving items and crystals from one slot to another. Remove every click-to-move, click-to-swap, and double-click-to-equip / double-click-to-use behavior across:

- Hero UI **Inventory** tab (`InventoryGrid` slots — bag, equipment, mercenary equipment, shortcut references)
- Hero UI **Crystals** tab (`CrystalsTab` — forge slots, inventory grid, queue)
- **Shortcut bar** (`ShortcutBar.ShortcutSlotVisual`)
- **Hero Info** tab skill grid (`HeroCrystalTab.SkillButton` — currently click-to-select-for-shortcut)
- All **Second Chance Shop** windows (`SecondChanceShopUI`, `VaultItemSlot`, `VaultCrystalSlot`, `SecondChanceHeroCrystalPanel`)

After this change there must be **no path** through which a left-click (or double-click) on a slot causes an item or crystal to move/swap/equip. Right-click context menu (Use / Discard) and the right-click crystal-card / shop click flows are **out of scope** and must be preserved.

---

## 3. Research Summary

### 3.1 Relevant Files & Why

| Path | Role |
|---|---|
| `PitHero/UI/InventorySlot.cs` | Per-slot input listener for the bag/equipment grid. Currently raises `OnSlotClicked`, `OnSlotDoubleClicked`, `OnSlotRightClicked`, plus drag events. Tracks `_lastClickTime` for double-click detection. |
| `PitHero/UI/InventoryGrid.cs` | Subscribes to `OnSlotClicked` (`HandleSlotClicked` → click-to-highlight + click-to-swap via `SwapSlotItems`) and `OnSlotDoubleClicked` (`HandleSlotDoubleClicked` → equip/unequip / use consumable). |
| `PitHero/UI/InventorySelectionManager.cs` | Static cross-component selection model used by click-to-swap (`SetSelectedFromInventory`, `SetSelectedFromShortcut`, `SetSelectedFromHeroCrystalTab`, `HasSelection`, `IsSelectionFromShortcutBar`, `IsSelectionFromHeroCrystalTab`, `GetSelectedSlot`, `GetSelectedSkill`, `OnSelectionCleared`). Drag-and-drop uses **none** of this selection model. Cross-component swap animation (`OnCrossComponentSwapAnimate`) is also tied to click-driven swaps. |
| `PitHero/UI/ShortcutBar.cs` | Contains `ShortcutSlotVisual` (per-shortcut input listener with click + double-click detection) and `ShortcutBar` (subscribes `OnSlotClicked` → `HandleSlotClicked` for cross-component reference assignment + shortcut-to-shortcut swap; `OnSlotDoubleClicked` → `HandleSlotDoubleClicked` for `UseConsumable`/`UseSkill`). |
| `PitHero/UI/HeroCrystalTab.cs` | Contains `SkillButton` with click handler that calls `InventorySelectionManager.SetSelectedFromHeroCrystalTab()` to select an active skill for ShortcutBar assignment via click. Also has working drag handlers (`OnDragStarted/Moved/Dropped`). |
| `PitHero/UI/CrystalSlotElement.cs` | Per-slot input listener used by both `CrystalsTab` and `SecondChanceHeroCrystalPanel`. Raises `OnSlotClicked` (no double-click) and full drag events. |
| `PitHero/UI/CrystalsTab.cs` | Subscribes to `OnSlotClicked` for forge / inventory / queue slots → `OnInventorySlotClicked`, `OnQueueSlotClicked`, `OnForgeSlotClicked`. These currently drive click-to-select + click-to-swap (`svc.SwapSlots` + `AnimateCrystalSwap`). Drag-drop (`HandleCrystalDragStarted/Moved/Dropped`) already implements the same swap path via drag. |
| `PitHero/UI/SecondChanceHeroCrystalPanel.cs` | Subscribes `OnSlotClicked` per crystal slot → `OnAnyCrystalSlotClicked` → fires `OnCrystalSlotClicked` (crystal-info card display). Important: this is **not** a move/swap action, just a display side-effect. The shop UI listens for it to show the vault crystal info card. |
| `PitHero/UI/VaultItemSlot.cs` | Vault item slot input listener. Has **no click handler** (no `OnSlotClicked` event); only hover + drag. Drag is handled in `VaultItemGrid` and routed through `InventoryDragManager.BeginVaultItemDrag`. ✅ Already drag-only. |
| `PitHero/UI/VaultCrystalSlot.cs` | Vault crystal slot. Raises `OnSlotClicked` (used by `VaultCrystalGrid` → `HandleSlotClicked` → `SecondChanceShopUI.HandleVaultCrystalSlotClicked` to show the crystal info card). This is a **display** click, not a move/swap. |
| `PitHero/UI/VaultItemGrid.cs` | Grid container of `VaultItemSlot`. Drag-only (no click subscriptions). ✅ Already correct. |
| `PitHero/UI/VaultCrystalGrid.cs` | Grid container of `VaultCrystalSlot`. Raises `OnVaultCrystalSlotClicked` for info card; no movement on click. |
| `PitHero/UI/SecondChanceShopUI.cs` | Wires the vault grids and the hero crystal panel. Click handlers (`HandleVaultCrystalSlotClicked`, `HandleHeroCrystalSlotClicked`) only show the crystal info card — no item/crystal movement on click. |
| `PitHero/UI/InventoryDragManager.cs` | Static drag state hub. Methods: `BeginDrag` / `BeginCrystalDrag` / `BeginSkillDrag` / `BeginVaultItemDrag` / `BeginVaultCrystalDrag` / `UpdateDrag` / `EndDrag` / `CancelDrag` / `NotifyDropRequested` / `NotifySkillDropRequested`. The drop-request callbacks (`OnDropRequested`, `OnSkillDropRequested`) are how non-local drop targets receive a drag (e.g. inventory→shortcut, skill list→shortcut). All five `Begin*` paths already exist and work. |
| `PitHero/UI/DragDropOverlay.cs` | Stage-overlay element drawing the dragged sprite at the cursor. No changes needed. |
| `PitHero/UI/SwapAnimationOverlay.cs` | Stage-overlay element animating two sprites between two stage points. Used by both click-driven and drag-driven swaps. Keep as-is. |
| `PitHero/UI/InventoryContextMenu.cs` | Right-click context menu (Use / Discard). Out of scope — preserved. |
| `PitHero/GameConfig.cs` | Defines `DoubleClickThresholdSeconds` and `DragThresholdPixels`. `DoubleClickThresholdSeconds` becomes dead after this change and should be removed. `DragThresholdPixels` is still used. |
| `PitHero.Tests/SkillShortcutBarTests.cs` | Tests `InventorySelectionManager.SetSelectedFromHeroCrystalTab` / `ClearSelection`. These tests exercise the **selection model** that is being removed — they must be updated or removed. |
| `PitHero.Tests/UI/InventoryGridTests.cs`, `PitHero.Tests/UI/InventorySlotDataTests.cs`, `PitHero.Tests/UI/HeroCrystalTabTests.cs`, `PitHero.Tests/ConsumableShortcutBarPriorityTests.cs` | Touch shortcut/inventory pieces. None of them currently invoke `OnSlotClicked` / `OnSlotDoubleClicked`, so they are unlikely to break, but each must be re-run. |

### 3.2 Existing Patterns to Reuse

- **`InventoryDragManager.Begin*` / `UpdateDrag` / `EndDrag` / `CancelDrag`** — already covers item, crystal, skill, vault item, and vault crystal drags.
- **`InventoryGrid.HandleSlotDragStarted / DragMoved / DragDropped`** — full implementation of drag-to-swap including stack-absorb, equip/unequip via `SwapSlotItems`, vault item drops, and synergy refresh. Drag is the canonical path.
- **`ShortcutBar.HandleShortcutDragStarted / DragMoved / DragDropped`** — full implementation of shortcut-to-shortcut swap (`SwapShortcuts`) and drop-outside-bar removal (`ClearShortcutReference`).
- **`ShortcutBar.HandleInventoryDropOnShortcut` / `HandleSkillDropOnShortcut`** — subscribed via `InventoryDragManager.OnDropRequested` / `OnSkillDropRequested`. These already cover inventory→shortcut and skill-list→shortcut placement.
- **`CrystalsTab.HandleCrystalDragStarted / Moved / Dropped`** — drag implementation that mirrors the click-to-swap behavior, including mastered-only enforcement for forge slots and `AnimateCrystalSwap` post-swap animation.
- **`SecondChanceHeroCrystalPanel.HandleHeroSlotDrop`** — already gated to vault-crystal drops only and validates empty target.
- **`SwapAnimationOverlay`** — keep using for post-drop animation (already used by drag paths in `InventoryGrid` via `InventorySelectionManager.TryAnimateSwap` and by `CrystalsTab.AnimateCrystalSwap`).
- **`HeroCrystalTab.HandleSkillButtonDragStarted/Moved/Dropped`** — already in place for active-skill drag onto shortcut bar; no equivalent for skills exists in the click model except the soon-removed selection.

### 3.3 Constraints & Rules (from `CLAUDE.md` / `.github/copilot-instructions.md`)

**AOT / hot-path:**
- Use `for` loops, not `foreach`.
- No LINQ in per-frame code.
- No reflection.
- Pre-allocate; avoid `new` in the game loop.
- No dynamic string concatenation in the game loop (debug logs exempt).

**Nez:**
- `Game1` inherits `Nez.Core` — never override `Update()`/`Draw()`.
- Use `Nez.Time.DeltaTime`, `Nez.Random`.
- Register/retrieve services via `Core.Services`.

**UI (Nez.UI / PitHeroSkin):**
- Use `"ph-default"` style for `PitHeroSkin` elements.
- Never call `SetFontScale()`.
- Never set `FontColor` on `ph-default`; create a child style.
- Use `IInputListener` for slot input — preserve the press / move / up pattern that backs drag detection (`_mouseDown`, `_mousePressPos`, `_isDragging`).

**Localization:**
- All display strings come from `TextService.GetText(...)`. Debug logs exempt.

**Code style:**
- Public method = `/// <summary>`.
- One component per file (structs exempt).
- Log `Vector2/Point/Rectangle` fields individually, not the whole object.

### 3.4 Risks / Edge Cases

1. **`InventorySelectionManager` is a global static used by three components for both click-driven swap AND drag visual state.** It also exposes `OnInventoryChanged` (used widely after any inventory mutation) and `OnSelectionCleared` (subscribed by InventoryGrid + ShortcutBar to wipe local highlight state). After removing click-driven selection, `OnInventoryChanged` must remain (drag still needs it), but the entire selection API (`SetSelectedFromInventory`, `SetSelectedFromShortcut`, `SetSelectedFromHeroCrystalTab`, `GetSelectedSlot`, `GetSelectedSkill`, `GetSelectedShortcutIndex`, `IsSelectionFromShortcutBar`, `IsSelectionFromHeroCrystalTab`, `HasSelection`, `OnSelectionCleared`, `_isFromShortcutBar`, `_isFromHeroCrystalTab`, `_selectedShortcutIndex`, `_selectedSkill`, `_selectedSlot`) becomes dead. Decide whether to delete it or stub it; deleting is preferred. **Risk:** any leftover caller of these APIs becomes a compile error — that is *good*, it surfaces missed call sites.
2. **`HoverableImageButton`-style hover offsets and "select box" highlights** in `InventorySlot`, `ShortcutSlotVisual`, `CrystalSlotElement`, and `SkillButton` are partly driven by `InventorySelectionManager.HasSelection()`. Removing the selection model must also remove those "hover offset while a selection is active" branches so visuals do not appear stuck.
3. **`InventoryGrid.HandleSlotClicked` doubles as the entry point for stencil mode (`HandleStencilModeClick`).** Stencil placement / move / remove rely on left-click on grid cells. **Stencils are NOT items or crystals** — they are an independent grid editing mode and the user request explicitly limits scope to items and crystals. Stencil click handling **must be preserved**. The fix: keep `OnSlotClicked` plumbing for the stencil-mode branch only, and short-circuit when not in stencil mode (instead of falling through to selection/swap). Alternative: move stencil hit-testing into a different code path (e.g. directly in `InventoryGrid.Draw` overlay or via a stencil-only listener). Decision needed (see §10).
4. **Right-click on bag slots opens `InventoryContextMenu` (Use / Discard).** This is the *only* in-place way to use a consumable from the bag once double-click is gone. It must continue to work. Likewise, **shortcut-key 1-8 keyboard activation** of a shortcut item/skill (`ShortcutBar.HandleKeyboardShortcuts`) must continue to work — that is the replacement for double-click-to-use.
5. **Vault crystal slot click (`VaultCrystalSlot.OnSlotClicked` → show crystal info card) is a display action, not movement.** Same for `SecondChanceHeroCrystalPanel.OnCrystalSlotClicked`. These must remain. The `CrystalsTab` also shows the crystal card on click via `OnInventorySlotClicked`/`OnQueueSlotClicked`/`OnForgeSlotClicked`; the **info card** behavior should remain, but the **swap-on-second-click** behavior must be removed. Cleanest rewrite: on click, only `ShowCrystalCard` (or `HideCrystalCard` if empty), never `SwapSlots`.
6. **Cross-component swap animation (`InventorySelectionManager.OnCrossComponentSwapAnimate`)** is invoked nowhere we currently use except via the swap path; leaving the event in place but never fired is harmless. Still, prefer to remove it with the rest of the selection API.
7. **Drag threshold (`GameConfig.DragThresholdPixels = 4f`).** Removing click handlers means a "fast click" (mouse down, no move, mouse up) on an item slot will produce **no action**. Confirm this is the desired UX (i.e. clicking an item does nothing besides showing tooltip on hover). **Acceptance:** a quick click in an item-occupied slot must show no movement and no highlight.
8. **`InventorySlot._lastClickTime`, `ShortcutSlotVisual._lastClickTime`** become dead and must be removed to keep the code clean.
9. **Tests:** `SkillShortcutBarTests` exercises the selection API; those tests need to be deleted/rewritten alongside the API removal. **`UI.InventorySlotDataTests`, `UI.InventoryGridTests`, `UI.HeroCrystalTabTests`, `ConsumableShortcutBarPriorityTests`** do not call the click APIs directly — re-run them but do not expect to change.
10. **Save/load shortcut restore (`ShortcutBar.TryRestorePendingShortcuts`)** is independent of click vs drag — verify the restore path still works (it uses `SetShortcutReference` / `SetShortcutSkill` programmatically). No changes expected.

---

## 4. Scope

### In Scope
- Remove all left-click handlers that move, swap, equip, unequip, or use items/crystals/skills from `InventorySlot`, `InventoryGrid`, `ShortcutBar` (`ShortcutSlotVisual` + `ShortcutBar` itself), `CrystalsTab`, `CrystalSlotElement`, `SecondChanceHeroCrystalPanel`, `VaultCrystalSlot`/`VaultCrystalGrid` (only the *movement* parts; info-card click stays), and `HeroCrystalTab.SkillButton` (the click that selects an active skill for shortcut bar assignment).
- Remove all double-click handlers (`OnSlotDoubleClicked` events, `_lastClickTime`, `DoubleClickThresholdSeconds`).
- Remove or strip `InventorySelectionManager` to keep only what drag still needs (`OnInventoryChanged`, `TryAnimateSwap`, `CanAbsorbStacks`, `PerformAbsorbStacks`). Delete `OnSelectionCleared`, `OnCrossComponentSwapAnimate`, all `SetSelected*` / `GetSelected*` / `HasSelection` / `IsSelectionFrom*` APIs and their backing fields.
- Update / remove tests that exercise the deleted selection/click APIs.
- Preserve and verify drag paths cover every former click-driven movement use case (bag-to-equip, equip-to-bag, bag-to-bag swap, bag-to-shortcut reference, skill-to-shortcut reference, shortcut-to-shortcut swap, drop-outside-shortcut-to-clear, crystal forge/inventory/queue swaps, vault item / vault crystal purchase via drop on hero panel).
- Preserve **right-click context menu** on inventory slots (Use / Discard).
- Preserve **info-card click** on `CrystalSlotElement` (CrystalsTab and SecondChance hero panel) and `VaultCrystalSlot` (vault grid). Wherever this overlaps with "click triggers selection for swap," replace with "click only opens/closes the info card."
- Preserve **stencil mode click handling** in `InventoryGrid` (left-click on grid cells in stencil mode).
- Preserve **keyboard shortcut keys 1–8** for activating shortcut bar items/skills.

### Out of Scope
- Stencil placement / movement / removal interaction model.
- Right-click context menu behavior or layout.
- Crystal info card content, layout, or `HeroCrystalCard` itself.
- `InventoryContextMenu` look or behavior.
- Save/load behavior (other than verification).
- Nez framework or `PitHeroSkin` changes.
- New tooltips, new hover effects, or any UI restyle.
- Mercenary tab non-inventory interactions.
- Skill purchase flow (`HeroCrystalTab` `OnSkillClick` for *unlearned* skills calls `ShowConfirmationDialog` — that JP-purchase path is **kept**; only the "select learned active skill for shortcut bar" branch is removed).

---

## 5. Constraints & Standards

- **AOT compliance:** `for` loops, no LINQ in hot paths, no reflection.
- **Nez patterns:** `IInputListener` for input; `Stage.AddElement` for overlays; `Element.LocalToStageCoordinates` for hit-testing; `Touchable.Enabled` for interactive slots.
- **Style:** all new/modified public methods get `/// <summary>`. One class per file.
- **Localization:** any user-visible text added must come from `TextService` — no new display strings should be needed for this feature.
- **Logging:** keep `Debug.Log` lines that bracket meaningful state changes; remove logs that referenced the removed selection flow (e.g. `"Highlighted slot at..."`, `"Swapped items between..."` from click path — drag path has its own logs).
- **Build / test:**
  - `dotnet build PitHero.sln` must succeed.
  - `dotnet test PitHero.Tests/PitHero.Tests.csproj` must pass.
- **Pre-existing public APIs:** removing `InventorySelectionManager` public statics will affect callers. All call sites are inside `PitHero/UI` and `PitHero.Tests` (verified via grep).

---

## 6. Implementation Phases

### Phase 1 — Strip click/double-click from `InventorySlot` and `InventoryGrid`

1.1 — `PitHero/UI/InventorySlot.cs`
- **Subtasks:**
  - Delete fields: `_lastClickTime`.
  - Delete events: `OnSlotClicked`, `OnSlotDoubleClicked`. Keep `OnSlotHovered`, `OnSlotUnhovered`, `OnSlotRightClicked`, `OnDragStarted`, `OnDragMoved`, `OnDragDropped`.
  - Modify `IInputListener.OnLeftMouseUp(...)`:
    - Keep the "if `wasDragging` invoke `OnDragDropped`" branch.
    - Remove the click / double-click block (and its time logic).
- **Definition of done:** the file no longer references `_lastClickTime`, `OnSlotClicked`, `OnSlotDoubleClicked`, or `DoubleClickThresholdSeconds`. Drag still functions exactly as before.

1.2 — `PitHero/UI/InventoryGrid.cs`
- **Subtasks:**
  - In `BuildSlots()`, remove `slot.OnSlotClicked += HandleSlotClicked;` and `slot.OnSlotDoubleClicked += HandleSlotDoubleClicked;`. Keep right-click, hover, and drag subscriptions.
  - Delete methods: `HandleSlotClicked`, `HandleSlotDoubleClicked`, `ClearSelectionHighlight`, `ClearSelection` (public — verify HeroUI / SecondChanceShopUI no longer call it; if they do, gut the body to a no-op or remove the calls).
  - Inside `HandleSlotClicked` there is a stencil-mode branch (`HandleStencilModeClick`) — preserve stencil click handling by keeping `OnSlotClicked` wiring **only when stencil mode requires it**, OR move stencil click handling to a separate input path (recommended approach: keep `OnSlotClicked` event on `InventorySlot`, but only subscribe when entering stencil mode and unsubscribe when leaving). **Pick one approach in §10 first.**
  - In `HandleSlotHovered`, remove the `InventorySelectionManager.HasSelection()` branch from the hover-offset condition; keep the "another local slot is highlighted" branch only if highlights still exist (they will not after click is removed — so remove the branch entirely and only apply hover offset during drag, which is already handled in `HandleSlotDragMoved`). Result: `HandleSlotHovered` should just raise `OnItemHovered`.
  - Remove `_highlightedSlot`, `IsHighlighted` reads in `HandleSlotHovered`. **Note:** `IsHighlighted` is still set during drag via `_dragHoveredSlot.SetItemSpriteOffsetY(...)` — drag uses offset, not the `IsHighlighted` flag. Re-verify and remove `IsHighlighted` if no longer used (also check `InventorySlot.Draw`).
  - Update `ClearLocalSelectionState` to a no-op or remove and unsubscribe the manager event subscription.
  - Replace `InventorySelectionManager.SetSelectedFromInventory(...)` / `ClearSelection()` / `OnSelectionCleared` references in `ConnectToHero` with the minimum still required (`OnInventoryChanged` only).
- **Definition of done:** Click on a bag/equipment slot does nothing (no highlight, no swap, no equip). Drag still works, including stack absorb, equip/unequip, and vault item drops. Right-click context menu still works. Stencil clicks still work in stencil mode.

### Phase 2 — Strip click/double-click from `ShortcutBar` / `ShortcutSlotVisual`

2.1 — `PitHero/UI/ShortcutBar.cs` → `ShortcutSlotVisual` inner class
- **Subtasks:**
  - Delete `_lastClickTime`.
  - Delete events: `OnSlotClicked`, `OnSlotDoubleClicked`.
  - Modify `IInputListener.OnLeftMouseUp(...)`: keep only the drag-drop branch.

2.2 — `PitHero/UI/ShortcutBar.cs` → `ShortcutBar` outer class
- **Subtasks:**
  - In `BuildVisualSlots()`, remove `slot.OnSlotClicked += ...` and `slot.OnSlotDoubleClicked += ...`. Keep hover and drag wiring.
  - Delete methods: `HandleSlotClicked`, `HandleSlotDoubleClicked`. (Do NOT delete `SwapShortcuts` — it is used by `HandleShortcutDragDropped`.)
  - In `HandleSlotHovered`, remove the `InventorySelectionManager.HasSelection()` branch from the hover-offset condition; keep only what drag needs (drag overlays its own offset via `HandleShortcutDragMoved`). Result: `HandleSlotHovered` should just raise `OnItemHovered` / `OnSkillHovered`.
  - Remove `_highlightedIndex` field and any `SetHighlighted` calls that derive from clicks; only drag-driven `SetItemSpriteOffsetY(HOVER_OFFSET_Y)` remains.
  - In `ConnectToHero`, drop `InventorySelectionManager.OnSelectionCleared += ClearLocalSelectionState;` (and delete `ClearLocalSelectionState`). Keep `OnInventoryChanged += RefreshVisualSlots;`.
  - Public `ClearSelection()` becomes a no-op or is removed (check HeroUI). 
- **Definition of done:** Clicking a shortcut slot does nothing visible; only drag moves shortcuts or assigns inventory/skill references. Keyboard shortcuts 1–8 still consume items / cast skills. Inventory→shortcut and skill-list→shortcut drag-drop assignment still works (via `HandleInventoryDropOnShortcut` / `HandleSkillDropOnShortcut`). Shortcut-to-shortcut swap via drag still works. Drop-outside-bar still clears the shortcut.

### Phase 3 — Strip click-to-select-skill from `HeroCrystalTab.SkillButton`

3.1 — `PitHero/UI/HeroCrystalTab.cs`
- **Subtasks:**
  - In `OnSkillClick(...)`, remove the `if (isLearned && skill.Kind == SkillKind.Active)` branch that toggles `InventorySelectionManager.SetSelectedFromHeroCrystalTab` / `ClearSelection`. Keep the "if not learned, attempt purchase" branch (job-skill JP purchase via `ShowConfirmationDialog`). Keep the "synergy skill not purchasable" branch.
  - In `SkillButton.Draw(...)`, remove the `if (... InventorySelectionManager.IsSelectionFromHeroCrystalTab() && InventorySelectionManager.GetSelectedSkill() == _skill ...)` HighlightBox draw branch. Keep the SelectBox-on-hover draw branch.
- **Definition of done:** Clicking a learned active skill no longer "selects" it for shortcut bar assignment. Dragging a learned active skill (existing `OnDragStarted/Moved/Dropped`) still places it onto a shortcut slot. Clicking an unlearned job skill still opens the purchase confirmation dialog.

### Phase 4 — Strip click-to-swap from `CrystalsTab` / `CrystalSlotElement`

4.1 — `PitHero/UI/CrystalsTab.cs`
- **Subtasks:**
  - Delete the entire selection model: `_selType`, `_selIndex`, `_selElement`, `SelType`, `SetSelection`, `ClearSelection`, `GetSelectionSlot`, `GetSelectionElement`. (Drag uses `InventoryDragManager.SourceCrystalSlot`; selection state is unnecessary.)
  - Rewrite `OnInventorySlotClicked(int idx)`:
    ```
    var crystal = svc.GetInventoryCrystal(idx);
    if (crystal != null) ShowCrystalCard(crystal); else HideCrystalCard();
    ```
    — no swap logic.
  - Rewrite `OnQueueSlotClicked(int queueIdx)` similarly: show / hide card only.
  - Rewrite `OnForgeSlotClicked(SelType forgeSlot)`: show / hide card only. Replace the `SelType` parameter with a direct slot reference (e.g. pass the `CrystalSlotElement` and the `CrystalSlotType`) since `SelType` is being deleted.
  - Keep `_forgeOutput.OnSlotClicked += _ => { HideCrystalCard(); ClearSelection(); };` — but `ClearSelection` is gone, so simplify to `HideCrystalCard()`.
  - Confirm `HandleCrystalDragStarted` no longer calls `ClearSelection()` (or rewrite to not reference the deleted method).
- **Definition of done:** Clicking a crystal in the inventory or queue shows the crystal info card; clicking again on the same slot or on an empty slot hides it. No click ever moves a crystal. Dragging a crystal between any two valid slots still triggers `svc.SwapSlots` + `AnimateCrystalSwap`. Mastered-only enforcement on forge slots still applies during drag.

4.2 — `PitHero/UI/CrystalSlotElement.cs`
- **Subtasks:**
  - Keep `OnSlotClicked` event — it is still needed for the info-card show in `CrystalsTab` and `SecondChanceHeroCrystalPanel`.
  - In `IInputListener.OnLeftMouseUp(...)`, keep the "if `wasDragging` raise `OnDragDropped`, else raise `OnSlotClicked`" pattern. The click event no longer triggers movement (movement code in `CrystalsTab` was removed in 4.1).
- **Definition of done:** Click still fires `OnSlotClicked` for info-card display only.

### Phase 5 — Verify / clean Second Chance Shop click paths

5.1 — `PitHero/UI/SecondChanceHeroCrystalPanel.cs`
- **Subtasks:**
  - Confirm `OnInventorySlotClicked` / `OnQueueSlotClicked` only fire `OnCrystalSlotClicked` for info-card display. ✅ already correct.
  - Verify `HandleHeroSlotDrop` only handles vault-crystal drops onto empty hero slots. ✅ already correct.
- **Definition of done:** No code changes needed; verified by code review.

5.2 — `PitHero/UI/VaultItemSlot.cs`, `VaultItemGrid.cs`, `VaultCrystalSlot.cs`, `VaultCrystalGrid.cs`, `SecondChanceShopUI.cs`
- **Subtasks:**
  - Confirm vault item slots have no click move behavior. ✅ already drag-only.
  - Confirm vault crystal slot click only opens info card via `HandleVaultCrystalSlotClicked` → `ShowVaultCrystalCard`. ✅ already correct.
  - Confirm `SecondChanceShopUI.HandleVaultItemDrop` and `HandleVaultCrystalDrop` are drag-driven (called from `OnVaultItemDropRequested` / `OnVaultCrystalDropRequested`). ✅ already correct.
- **Definition of done:** Verified by code review; no functional changes needed.

### Phase 6 — Strip / shrink `InventorySelectionManager`

6.1 — `PitHero/UI/InventorySelectionManager.cs`
- **Subtasks:**
  - Decide between (A) deleting the file entirely after migrating `OnInventoryChanged`, `TryAnimateSwap`, `CanAbsorbStacks`, `PerformAbsorbStacks` to a new utility class (e.g. `InventorySwapHelpers`); or (B) keeping the file but reducing it to just those four members (preferred — minimizes diff churn).
  - If (B): remove `_selectedSlot`, `_isFromShortcutBar`, `_isFromHeroCrystalTab`, `_heroComponent`, `_selectedShortcutIndex`, `_selectedSkill`, all `Set*`/`Get*`/`HasSelection`/`IsSelectionFrom*`/`ClearSelection`/`ClearSelectionInternal`, `OnSelectionCleared`, `OnCrossComponentSwapAnimate`, and `TrySwapCrossComponent`.
  - Keep: `_swapOverlay`, `OnInventoryChanged`, `TryAnimateSwap`, `CanAbsorbStacks`, `PerformAbsorbStacks`, `CrossComponentSwapDuration`.
- **Definition of done:** `InventorySelectionManager` exposes only the four members above. All compile errors elsewhere in the codebase are resolved (compile success is the verification).

### Phase 7 — Remove dead config and update tests

7.1 — `PitHero/GameConfig.cs`
- **Subtasks:**
  - Remove `DoubleClickThresholdSeconds` constant.
  - Keep `DragThresholdPixels`.

7.2 — `PitHero.Tests/SkillShortcutBarTests.cs`
- **Subtasks:**
  - Delete the four tests that exercise `InventorySelectionManager.SetSelectedFromHeroCrystalTab` / `ClearSelection` / `HasSelection` / `IsSelectionFromHeroCrystalTab` / `GetSelectedSkill`.
  - Keep `ShortcutSlotData_CreateItemReference_SetsCorrectType`, `ShortcutSlotData_CreateSkillReference_SetsCorrectType`, `ShortcutSlotData_Clear_ResetsToEmpty`, `ActiveSkill_HasCorrectKind`, `PassiveSkill_HasCorrectKind`.
  - If desired, add new tests that simulate drag-driven shortcut assignment (optional, may require more harness scaffolding — defer to Builder discretion).
- **Definition of done:** `dotnet test PitHero.Tests/` passes.

7.3 — Other test files
- **Subtasks:** Run `dotnet test`; confirm `PitHero.Tests/UI/InventoryGridTests.cs`, `InventorySlotDataTests.cs`, `HeroCrystalTabTests.cs`, `ConsumableShortcutBarPriorityTests.cs`, `CrystalCollectionServiceTests.cs`, `CrystalSaveRoundtripTests.cs`, save/load tests, etc., all still pass.

### Phase 8 — Manual verification matrix

Run the game (`dotnet run --project PitHero/PitHero.csproj`) and walk through the following matrix. Each row must pass.

| # | Action | Expected |
|---|---|---|
| 1 | Click an item in the bag | No movement, no highlight, no equip; tooltip shown on hover only |
| 2 | Double-click an item in the bag | No equip, no use; same as single click |
| 3 | Right-click an item in the bag | Use / Discard context menu opens |
| 4 | Drag an item from bag to empty equipment slot | Equips |
| 5 | Drag equipped item to empty bag slot | Unequips |
| 6 | Drag two same consumables together | Stacks absorb correctly |
| 7 | Drag an item onto a shortcut slot | Reference assigned (consumables only) |
| 8 | Drag a shortcut slot to another shortcut slot | Swap |
| 9 | Drag a shortcut slot off the bar | Reference cleared |
| 10 | Press shortcut keys 1–8 | Item / skill activates as before |
| 11 | Click a learned active skill in Hero Info tab | No selection, no highlight |
| 12 | Drag a learned active skill onto a shortcut slot | Reference assigned |
| 13 | Click an unlearned job skill (affordable) | Purchase confirmation dialog opens |
| 14 | Click a crystal in CrystalsTab inventory | Crystal info card shown; clicking again or empty slot hides it |
| 15 | Drag a crystal between inventory slots | Swap with animation |
| 16 | Drag a non-mastered crystal onto a forge slot | Drop rejected |
| 17 | Drag a mastered crystal onto a forge slot | Placed |
| 18 | Drag a crystal between forge slots / inventory / queue | Swap |
| 19 | Open Second Chance Shop, click a vault crystal | Vault crystal info card shown |
| 20 | Drag vault item onto hero inventory empty slot | Purchase confirmation appears (single or quantity) |
| 21 | Drag vault crystal onto hero crystal empty slot | Purchase confirmation appears |
| 22 | Stencil mode: click bag cell to place / move / remove | Works as before |

---

## 7. Test & Validation Plan

### Build
```
dotnet build PitHero.sln
```
Must succeed with zero errors. Warnings introduced by removed code (e.g. `CS0067` unused event) must be resolved by deleting the offending event/field.

### Unit tests
```
dotnet test PitHero.Tests/PitHero.Tests.csproj
```
- Existing passing tests must continue to pass.
- `SkillShortcutBarTests` will lose four tests (selection-API tests deleted).
- Optionally add tests for: `InventoryDragManager` lifecycle (begin/end/cancel state), `ShortcutSlotData` round-trip, `CrystalCollectionService.SwapSlots` (already covered).

### Manual verification
Run the matrix in §6 / Phase 8.

### Save/load regression
1. Place item references and skill references in shortcut bar.
2. Place crystals in forge / inventory / queue.
3. Quit and reload — verify all positions / references restore.
4. Existing `CrystalSaveRoundtripTests` and `ConsumableShortcutBarPriorityTests` cover most of this automatically.

---

## 8. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Stencil mode break (left-click on grid in stencil mode no longer fires) | Medium | High (gameplay-blocking) | Keep `OnSlotClicked` event on `InventorySlot`, but subscribe in `InventoryGrid` only when entering stencil mode (`SetMoveStencilsMode(true)` / `SetRemoveStencilsMode(true)`) and unsubscribe when leaving. Also acceptable: keep the click subscription always but make the handler a no-op outside stencil mode. **Decision §10.1.** |
| Crystal info card stops appearing in CrystalsTab after rewriting `OnInventorySlotClicked` etc. | Low | Medium (UX regression) | Keep `ShowCrystalCard` / `HideCrystalCard` calls; remove only the swap path; verify in matrix #14. |
| `InventorySelectionManager` removal misses a caller | Low | Build-breaking | Compile-fail surfaces it; grep ahead-of-time confirms only six files reference it. |
| Removing `_highlightedSlot` removes a still-needed visual cue (e.g. the per-slot HighlightBox draw) | Low | Cosmetic | After removal, run matrix; HighlightBox should never appear because nothing sets `IsHighlighted` post-change. If a test (`HeroCrystalTabTests`?) reads `IsHighlighted`, update / remove it. |
| Players relied on double-click-to-equip muscle memory | High | UX | Out of scope per request. Drag is now the only path. (May want to add a brief in-game hint, but that is a separate feature.) |
| Pre-existing `InventoryDragManager.OnDropRequested` / `OnSkillDropRequested` race conditions if drag flows interact | Low | Bug | No changes to these mechanisms; existing behavior retained. |
| `ShortcutBar.HandleInventoryDropOnShortcut` rejects non-consumables (today) — drag-only world means players cannot equip via shortcut | None (existing behavior) | n/a | Already documented; drag from shortcut to inventory or shortcut-to-shortcut still works. |

---

## 9. Open Questions / Decisions Needed

1. **§3.4 #3 — Stencil mode click handling:** Two options:
   - **(A)** Keep `OnSlotClicked` event on `InventorySlot` and have `InventoryGrid` subscribe a stencil-only handler always (early-out when not in stencil mode). Simpler diff; click event still fires per slot but does nothing visible.
   - **(B)** Remove `OnSlotClicked` from `InventorySlot` entirely and route stencil clicks via a different mechanism (e.g. `InventoryGrid` overrides `OnLeftMousePressed` itself, hit-tests, and dispatches).
   - **Recommendation: (A).** Lowest risk, smallest diff. Builder may revisit if (A) causes confusion.

2. **§6 — `InventorySelectionManager`** — keep file (delete most members) or delete file (move four kept members elsewhere)?
   - **Recommendation: keep the file, shrink it.** Avoids needing to update `using` clauses. Rename later if needed.

3. **`HeroCrystalTab.OnSkillClick`** — when removing the skill-selection branch, should clicking a *learned* active skill do anything visible (e.g. show a "drag me to shortcut" hint tooltip)?
   - **Recommendation: no.** Hover already shows the skill tooltip; that is enough discovery.

4. **Should clicking the `_forgeOutput` (read-only forge result preview) still hide the crystal card?**
   - **Recommendation: yes** — preserve `_forgeOutput.OnSlotClicked += _ => HideCrystalCard();` as it is purely a dismiss action.

5. **`InventorySlot.IsHighlighted` field on `InventorySlotData`** — once nothing sets it, should it be removed from `InventorySlotData` entirely?
   - **Recommendation: defer.** Out of scope. Leave the field; just stop writing to it. Cleanup belongs in a follow-up.

---

## 10. Acceptance Criteria

A. **No left-click or double-click on any inventory slot, equipment slot, mercenary equipment slot, shortcut slot, crystal slot (forge/inventory/queue), vault item slot, or skill button moves an item, equips an item, swaps a crystal, assigns a shortcut, or uses a consumable / skill.**

B. **Drag-and-drop continues to be the only working method for:**
- Bag-to-equipment (and reverse) on hero & mercenary
- Bag-to-bag swap / stack absorb
- Bag-to-shortcut reference (consumables only — existing rule)
- Skill-list-to-shortcut reference (active learned skills only)
- Shortcut-to-shortcut swap & drop-outside-to-clear
- Crystal forge / inventory / queue swaps (with mastered-only forge enforcement)
- Vault item / vault crystal purchase via drop on hero panel

C. **Right-click bag context menu (Use / Discard) still works.**

D. **Keyboard shortcut keys 1–8 still activate items/skills.**

E. **Crystal info card still appears on click in CrystalsTab and SecondChance hero crystal panel; vault crystal info card still appears on click in vault grid.**

F. **Stencil placement / move / remove via click still works in stencil mode.**

G. **Skill purchase confirmation dialog still opens when clicking an unlearned, affordable job skill in Hero Info tab.**

H. `dotnet build PitHero.sln` succeeds with zero errors.

I. `dotnet test PitHero.Tests/PitHero.Tests.csproj` passes (modulo the four deleted selection-API tests in `SkillShortcutBarTests`).

J. **No reference to `OnSlotClicked` / `OnSlotDoubleClicked` in `InventorySlot.cs` or `ShortcutSlotVisual` (in `ShortcutBar.cs`); no reference to `_lastClickTime`, `DoubleClickThresholdSeconds`, `SetSelectedFromInventory`, `SetSelectedFromShortcut`, `SetSelectedFromHeroCrystalTab`, `IsSelectionFromShortcutBar`, `IsSelectionFromHeroCrystalTab`, `OnSelectionCleared`, `OnCrossComponentSwapAnimate`, or `HasSelection` anywhere in `PitHero/`.** (Verifiable via Grep.)

---

## 11. Downstream Handoff Notes (for Principal Game Engineer)

- **Order matters.** Phase 1 → 2 → 3 → 4 first (each phase touches its own file set, no cross-dependency). Phase 6 (`InventorySelectionManager` shrink) must happen *after* Phases 1–4 because every selection-API caller is removed in those phases. Phase 7 (config + tests) last.
- After each phase, run `dotnet build` to surface unused-field / unused-event warnings; clean them within the same phase. Do **not** suppress warnings.
- `InventorySelectionManager.OnInventoryChanged` is invoked from many places (`InventoryGrid`, `ShortcutBar`, `SecondChanceShopUI`, `HeroUI`). **Keep all those invocations.**
- `InventorySelectionManager.TryAnimateSwap` is called by `InventoryGrid.AnimateSwap`. Drag uses it. **Keep.**
- `CanAbsorbStacks` / `PerformAbsorbStacks` are called by `InventoryGrid.SwapSlotItems` (drag path). **Keep.**
- Watch for: any place that previously cleared a "highlight" visual on selection-cleared events. Once selection is gone, those highlights cannot appear, so the cleanup paths become dead. Delete them rather than leaving as no-ops.
- `InventorySlot.SetItemSpriteHidden` and `CrystalSlotElement.SetCrystalHidden` are used by the drag overlay to hide the source sprite while dragging. **Keep.**
- `ShortcutSlotVisual.SetItemSpriteHidden` is used by `HandleShortcutDragStarted` / `Dropped`. **Keep.**
- The `SwapAnimationOverlay` is shared across components via `InventorySelectionManager._swapOverlay`. Keep that field private to the manager — only expose `TryAnimateSwap`.
- Recommend a single squashed commit per phase with a short descriptive message (e.g. `feat(ui): drop click-to-swap on inventory slots`). User will run `/loop` review after.

---

*End of plan.*
