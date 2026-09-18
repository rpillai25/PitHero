# Automation System

**Settings → Automation** is the single home for every "the game does this for me" toggle. This
document covers the tab as a whole: which service owns each control, when each automation actually
runs, how the settings persist, and the non-obvious constraints that are easy to break when adding
a new option.

Monster job assignment is the one automation with its own deep-dive — see
[AutoJobAssignmentSystem.md](AutoJobAssignmentSystem.md). This document does not repeat it.

## The tab at a glance

Built by `SettingsUI.PopulateAutomationTab` (`PitHero/UI/SettingsUI.cs`), which delegates the
newest blocks to `PopulateAutoPurchaseControls`, `PopulateAutoEquipControls`,
`PopulateAutoLearnControls` and `PopulateAutoHireControls`. The whole tab is wrapped in a vertical
`ScrollPane` — it has outgrown the 450×350 settings window, so **new controls just get appended;
do not try to keep the tab short**.

| Control group | Owning service / state | Runs | Save section |
|---|---|---|---|
| Automate monster jobs | `AutoJobAssignmentService` | Ticked (cadence) | 34 (v19) |
| **Gold Buffer** (label + slider) | `AutoSeedPurchaseService.GoldBuffer` | — (read by others) | 30 (v15) |
| Auto-Purchase Seeds | `AutoSeedPurchaseService` | Ticked (1s throttle) | 30 (v15) |
| Auto-Sell Crops + "Choose Crops to Sell" | `AutoCropSellService` | Ticked | 31–32 (v16/v17) |
| Auto-Sell Excess Items + priority + **Inventory Sell %** slider + "Gear Sell Options" + "Consumable Sell Options" | `AutoSellExcessItemsService` | Call-driven (pre-jump sweep + full-bag chest safety net) | 36–38, 43, 49 (v21/v22/v23/v26/v34) |
| Auto-Purchase Items + priority + merc opt-in + "Gear Purchase Options" + "Consumable Purchase Options" | `AutoItemPurchaseService` (Keep Stacks array shared with auto-sell) | Call-driven (pre-jump, after the sweep) | 39 (v23) |
| Auto-Equip Options | `HeroComponent.AutoEquipHero` / `.AutoEquipMercenaries` (**no service**) | Call-driven | 40 (v23) |
| Auto-Learn Hero Skills + "Learn Mode" cycler | `AutoLearnSkillsService` | Ticked (1s throttle) | 42 (v25) |
| Auto-Hire Mercenaries + two "MercenaryN Job" cyclers | `AutoHireMercenaryService` | Call-driven | 41 (v24) |
| Placed stencils (inventory grid snapshot) | `GameStateService.PlacedStencils` | — (save/load only) | 44 (v27) |
| Refrigerator contents + Pre-Stock Stack Size (window opened by clicking the kitchen fridge, not from this tab) | `FridgeInventoryService` | — (runners tick via `KitchenTaskCoordinator`) | 45 (v28) |
| Runner carry level (no UI yet — raised by future one-of-a-kind items; 1/5/10 units per crop per trip) | `GameStateService.RunnerCarryLevel` | — (read by runner trips) | 46 (v29) |
| Local artifacts (bought in the Second Chance shop, not from this tab; crop growth and worker speed multipliers) | `GameStateService` local-artifact store, read via `LocalArtifactEffects` | — (sim reads every fixed step) | 48 (v34) |

Dialogs opened from the tab:

| Dialog | File | Notes |
|---|---|---|
| Auto-Sell Crop Types | `PitHero/UI/AutoSellCropTypesDialog.cs` | Per-crop checkboxes + keep-stacks slider |
| Gear Sell Options / Gear Purchase Options | `PitHero/UI/GearFilterOptionsDialog.cs` | **One class, two instances** — rarity + gear-type filters, parameterized by title/label keys and `Func<bool[]>` accessors |
| Consumable Purchase Options | `PitHero/UI/ConsumablePurchaseOptionsDialog.cs` | Sprite + checkbox + per-item 0–3 "Keep Stacks" slider; nothing selected by default |
| Consumable Sell Options | `PitHero/UI/ConsumableSellOptionsDialog.cs` | Sprite + checkbox + per-item 0–3 "Keep Stacks" slider; everything selected by default, keep 1 — auto-sell never drains a potion to zero unless asked |

**Keep Stacks is one value per consumable** (issue #411): `AutoItemPurchaseService.ConsumableStackTargets`
*is* `AutoSellExcessItemsService.ConsumableKeepStacks` (the same array instance, injected at
registration). Auto-sell never sells a stack below it, auto-purchase buys exactly up to it, so the two
dialogs can never disagree; opening one hides the other so a single slider is on screen. The two
dialogs keep their own `PlayerCommandType`s (`SetConsumableMinStacks`, `SetConsumableStackTarget`) so
recordings made before the unification still replay — both handlers write the shared array.

All dialogs follow the same shape: a plain class owning a `Nez.UI.Window`, built once in the
constructor, `SetVisible(false)`, added to the stage; `Show()` syncs from the service, packs,
centers, `ToFront()`. They are constructed lazily on first button click and reused thereafter.

## Ownership rules that are not obvious

**The Gold Buffer lives on `AutoSeedPurchaseService`.** It is the single shared gold floor for
*every* automated purchase, not just seeds — no auto-purchase may take `Funds` below it. The
setting predates item purchasing and kept its home, so `AutoItemPurchaseService` takes
`AutoSeedPurchaseService` by constructor injection and exposes a read-only
`GoldBuffer => _goldBufferSource?.GoldBuffer ?? 0`. Consequences:

- `AutoItemPurchaseService` and `AutoHireMercenaryService` **must be registered after**
  `AutoSeedPurchaseService` in `MainGameScene.Begin()`; `AutoItemPurchaseService` also after
  `AutoSellExcessItemsService`, whose Keep Stacks array it is handed.
- There is exactly one slider in the UI and one persisted field (`SaveData.AutoShopGoldBuffer`).
  Do not add a second buffer; route new automated spending through the same property.

**Every service registered in `MainGameScene.Begin()` needs a matching `RemoveService` in
`Unload()`** (see `AGENTS.md`). Loading a save tears the scene down and re-runs `Begin()`; a missing
removal crashes `AddService` with a duplicate-key `ArgumentException`. The automation removals sit
together near the top of `Unload()`.

**Auto-Equip has no service.** Its two flags live on `HeroComponent` and are consumed by
`PartyAutoEquipHelper`. This is why it needs a separate load-sync path (below).

## When each automation runs

Two distinct patterns — pick deliberately:

- **Ticked** — `Update()` called from the unpaused block of `MainGameScene.Update()`.
  `AutoSeedPurchaseService` (1-second throttle), `AutoCropSellService`, `AutoJobAssignmentService`,
  `AutoLearnSkillsService` (1-second throttle).
  Each exposes a public, throttle-free pass method (`TryPurchasePass()`, `TrySellPass()`,
  `ReassessNow()`, `TryLearnPass()`) so tests can drive it directly.
- **Call-driven** — no update loop; invoked from the game action that creates the situation.
  - `PrePitAutomationPass.Run(heroComp)` (`AI/PrePitAutomationPass.cs`) ← `JumpIntoPitAction`, in the
    first-frame branch **after** the landing tile is validated and **before** `StartJumpMovement`
    (an aborted jump never sells or buys; the branch runs once per jump). It runs, in this order:
    1. an **auto-equip sweep** — every gear item in the bag is offered to the party through
       `PartyAutoEquipHelper` (honoring both auto-equip flags), so upgrades are worn before anything
       is judged excess;
    2. `AutoSellExcessItemsService.TrySellDownToThreshold(bag, gearIsUpgrade, soldNames)` — while the
       bag is at or above **Inventory Sell %** (`IsAtOrAboveSellThreshold`, integer math: 100 = only a
       full bag, 0 = every jump sells everything eligible), sell the weakest eligible item and stop
       the moment the bag drops below the line;
    3. `AutoItemPurchaseService.TryPurchasePass(heroComp, soldNames)`.
  - `AutoSellExcessItemsService.TryMakeRoom(bag, incoming, gearIsUpgrade)` ← `OpenChestAction`, before
    adding a chest item to a full bag — the in-pit safety net under the sweep, same rules.
  - `PartyAutoEquipHelper.TryAutoEquipForParty(heroComp, item)` ← `OpenChestAction` (chest loot),
    `AutoItemPurchaseService` (each purchased item) and the pre-jump equip sweep.

  **Upgrade gear is the last sell tier, never exempt.** `ExcessItemSellSelector.Select` runs the two
  category passes with gear that `PartyGearUpgradeCheck.IsUpgradeForAnyone` flags skipped, then — only
  if both come up empty — a final pass over the upgrades alone, weakest first (`SellSelection.IsUpgrade`).
  Inventory must always be clearable, so if selling an upgrade is the only way to make room, it goes.

  **Why sell-then-buy never churns:** ordinary sales are gear that upgrades nobody, and purchase only
  buys an upgrade over the equipped/bag baseline, so a sold piece is never a candidate; the
  last-resort upgrade tier is covered by the per-pass `soldNames` exclusion (`RunPurchasePass` skips
  those names); consumables sell only stacks above Keep Stacks and are bought only up to Keep Stacks.
  - `AutoHireMercenaryService.TryAutoHire(mercEntity)` ← `MercenaryManager.WalkToTavern`, after the
    merc is seated (`IsWaitingInTavern` + patron component) — hiring earlier corrupts seat state.
    `TryHirePass()` ← both settings-close paths (`ToggleSettingsVisibility`, `ForceCloseSettings`),
    so mercs already seated when the option is configured get hired on exit. The service pre-checks
    `CanHireMore()` (which covers the hero-dead hiring block) and the Gold Buffer, because
    `MercenaryManager.HireMercenary` enforces neither.

## Persistence

**Backwards compatibility is mandatory.** `SaveData.CurrentVersion` is the version the build
writes; every version from `SaveData.MinSupportedVersion` up must remain loadable. `Recover`
rejects only versions outside that range with `InvalidDataException` (and `SaveLoadService` treats
that slot as empty). Fields added after `MinSupportedVersion` are read conditionally on the file's
version with safe, gracefully degrading defaults (e.g. `fileVersion >= 30 ? reader.ReadFloat() :
0f` — "no active buff"). Loads rewrite at `CurrentVersion` on the next save.

Periodic **save unifications** (v17 via issue #311, v29 via PR #391) collapse the supported range
back to a single version — they happen **only when the owner explicitly asks for one**, never as a
side effect of a feature. See AGENTS.md "Save Format".

Sections are appended at the end and numbered, because that keeps diffs and the Persist/Recover
pairing readable.

Removing a setting does **not** remove its bytes; reshaping a section in the middle is what forces
a version bump. The v26 removal of the "Auto-Purchase Consumables" master flag left its bool as a
dead slot in section 39 (`Persist` always writes `true`, `Recover` reads and discards it). Follow
that pattern when a setting goes away.

Merging two settings into one follows the same idea (v34 Keep Stacks): both `AutoSellConsumableMinStacks`
(section 43) and `AutoPurchaseConsumableStacks` (section 39) are still written, from the one shared
array, so the layout never moved. `Recover` reconciles files older than v34 into a single value: the
sell floor is raised to the purchase target where purchasing was on and the item selected (that was
the old *effective* floor), then the purchase target is set equal to it. A player whose sell floor
exceeded the target therefore sees the target come up to the floor.

Adding a persisted setting means, in order:

1. Add the field to `SaveData` with a default and a `// Feature name` comment.
2. Append a **new numbered section at the very end** of `Persist`. Arrays are written as a count
   followed by the elements.
3. Append the mirrored read at the very end of `Recover`, gated on the file version when the
   section postdates `MinSupportedVersion` (`if (fileVersion >= N)` with defaults pre-assigned for
   older files). Clamp array reads with
   `if (i < arr.Length)` and **pre-fill the array's defaults before reading** — a saved count
   shorter than the current array leaves the tail at those defaults, which is how a rarity, gear
   category, or consumable added later behaves like a fresh enable. Scalars need no pre-assignment;
   the read always overwrites them.
4. Bump `CurrentVersion`.
5. Service → `SaveData` in `SaveLoadService` (the collect method); `SaveData` → service in
   `MainGameScene`'s load-apply path.
6. Extend `SaveLoadTests` with a round-trip test, a defaults test (a null array persists a
   zero count, which exercises the pre-fill path), *and* a previous-version layout test proving
   the old byte layout still reads (pattern: `SaveData_V29DiningRecord_ReadsWithDefaultExpiry`).

**Two indices are persisted identities and must stay append-only:**

- `ConsumableCatalog` (`PitHero/RolePlayingFramework/Equipment/ConsumableCatalog.cs`) — the order of
  its 9 entries is what `AutoPurchaseConsumableSelected` / `AutoPurchaseConsumableStacks` and
  `AutoSellConsumableSelected` / `AutoSellConsumableMinStacks` index into. Reordering silently
  remaps a player's selections onto the wrong potions.
- `GearCategory` — indexes every gear filter array (`AutoSellGearTypeAllowed`,
  `AutoPurchaseGearTypeAllowed`).

`ItemRarity` is used the same way by the rarity filters.

### The two-phase load sync

This is the subtlest thing in the system. On load, `MainGameScene` applies automation state to the
**services** and calls `_settingsUI?.SyncAutomationControlsFromService()` — but that happens
*before* the hero entity is rebuilt. The auto-equip flags live on `HeroComponent`, so they are
restored later, in the hero-restore block, followed by a second, narrow
`_settingsUI?.SyncAutoEquipControlsFromHero()`.

> If you add an Automation control backed by `HeroComponent` (or anything else built late in the
> load sequence), wiring it into `SyncAutomationControlsFromService` will appear to work and then
> silently reset on every load. Extend `SyncAutoEquipControlsFromHero` instead — and do not move the
> earlier call, which other automation controls depend on.

Also note: **programmatic `CheckBox.IsChecked = x` does not fire `OnChanged`**
(`ProgrammaticChangeEvents` is off). Every sync path and every "Select All" must write the service
array *directly* in addition to setting the checkbox.

## Deactivated ("grayed") controls

Deactivated sub-controls stay on screen, faded, rather than disappearing — the player can see what
enabling the parent would give them. The recipe is three parts, plus a fourth for tooltips:

```csharp
control.SetDisabled(!active);
control.SetStyle(skin.Get<TStyle>(active ? "ph-default" : "ph-grayed"));
control.SetTouchable(active ? Touchable.Enabled : Touchable.Disabled);
hoverableLabel.SetTooltipEnabled(active);   // hoverable labels only
```

`PitHeroSkin` provides `ph-grayed` variants of `LabelStyle`, `TextButtonStyle` and `CheckBoxStyle`.
Helpers in `SettingsUI`: `SetButtonActive` (shared), `SetDesignateCropsActive`,
`SetExcessItemControlsActive`, `SetItemPurchaseControlsActive`, `SetConsumablePurchaseControlsActive`,
`SetAutoHireControlsActive`. `ReorderableTableList` has its own `SetGrayed(bool)`, used by the sell/purchase
lists and by `MonsterFarmPriorityPanel.SetControlsActive`.

Gotchas:

- **Sliders have no `ph-grayed` style.** They use `DisabledBackground` / `DisabledKnob` on the shared
  `ph-default` `SliderStyle`, so deactivating one is `slider.Disabled = true` for the look — and
  Nez's `Slider` input listener **ignores `Disabled`**, so `SetTouchable(Touchable.Disabled)` is
  what actually stops dragging. You need both.
- **Nested gates compose.** The "Mercenary2 Job" cycler needs the auto-hire checkbox on *and*
  slot 1 holding a job (cycling slot 1 to None also resets slot 2 to None) —
  `SetAutoHireControlsActive` passes the combined condition down rather than gating on one
  checkbox. (The old "Auto-Purchase Consumables" master checkbox was the other example until v26
  removed it as redundant: with nothing selected in the dialog, nothing is bought anyway.)
- **Hide open dialogs when the parent turns off**, or a deactivated feature's dialog stays on screen.
- `PitHeroSkin.CreateSkin()` returns a **cached singleton** — never mutate a shared style; `Clone()`
  first if you need a variant (see `VaultBuyQuantityDialog`).

## Shared logic worth reusing

| Piece | File | Use it for |
|---|---|---|
| `GearCategoryUtils` | `RolePlayingFramework/Equipment/GearCategoryUtils.cs` | `ItemKind` → `GearCategory`, localization keys, `IsAllowed(bool[], ItemKind)` |
| `GearAutoEquipService` | `RolePlayingFramework/Equipment/GearAutoEquipService.cs` | `GetGearScore`, `IsNewGearBetter`, `TryGetSlotForGear`, `GetHeroItemInSlot` / `GetMercItemInSlot`, `CategorySlots`, `GetEquippedBaseline` (accessory rule), `IsUpgradeFor(hero|merc, gear)` |
| `PartyGearUpgradeCheck` | `RolePlayingFramework/Equipment/PartyGearUpgradeCheck.cs` | `IsUpgradeForAnyone(hero, mercs, gear)` — the sell tier test |
| `PartyAutoEquipHelper` | `AI/PartyAutoEquipHelper.cs` | Hero → mercs equip cascade with hand-me-downs, honoring the auto-equip flags |
| `PrePitAutomationPass` | `AI/PrePitAutomationPass.cs` | The pre-jump equip → sell → purchase sequence; `CreateGearUpgradeCheck` for the chest safety net |
| `ExcessItemSellSelector` | `RolePlayingFramework/Equipment/ExcessItemSellSelector.cs` | Pure "which item should we sell" logic, filter delegates optional, upgrade gear as the last tier |
| `ItemSellHelper` | `Services/ItemSellHelper.cs` | Sell to vault + credit gold + analytics, one call |
| `ConsumableCatalog` | `RolePlayingFramework/Equipment/ConsumableCatalog.cs` | Enumerate consumables; `CreateFresh(i)` for purchases |

**`GearCategoryUtils.TryGetCategory` and `GearAutoEquipService.TryGetSlotForGear` describe the same
grouping in two switches.** Keep them in lockstep when adding an `ItemKind` — `ItemDisplayHelper`
once missed `WeaponBow`, which is exactly what drift looks like. `ItemDisplayHelper.GetItemTypeString`
now delegates to `GearCategoryUtils` for that reason.

Gear/consumable purchase notes: buy price is `IItem.Price` (sell price is `GetSellPrice()`, a
rarity-scaled fraction). The Second Chance vault has **no generated stock** — everything in it was
sold or lost by the player. Consumables must be instantiated with `Consumable.CreateFreshInstance()`
so each bag stack owns its own `StackCount`; never hand out the vault's template instance.

## Adding a new Automation option

1. **Localization** — add a `const string` to `PitHero/UITextKey.cs` and a `Key,Value` line to
   `PitHero/Content/Localization/en-us/UI.txt`. No hardcoded display strings.
2. **State** — a new service in `PitHero/Services/`, or a property on an existing one. Prefer a
   plain class with constructor injection and a public, throttle-free pass method so it is testable
   without `Core`. Guard `Core.Instance != null` before touching `Core.Services` — headless hosts
   (unit tests, virtual balance runs) have no `Core`.
3. **Registration** — `AddService` in `MainGameScene.Begin()` *after* its dependencies, matching
   `RemoveService` in `Unload()`, and a `?.Update()` call in the unpaused block if it is ticked.
4. **UI** — add the control in `PopulateAutomationTab` (or a new `Populate*Controls` helper if it is
   a block). Use `HoverableCheckBox` / `HoverableLabel` when it needs a tooltip. Sub-controls get a
   `Set*Active` helper and are grayed when their parent is off.
5. **Sync** — extend `SyncAutomationControlsFromService()`, or `SyncAutoEquipControlsFromHero()` if
   the state lives on the hero. Remember `IsChecked` does not fire `OnChanged`.
6. **Persistence** — follow the six-step append-only recipe above.
7. **Analytics** — economy-affecting automation should log; see
   [AnalyticsSchema.md](AnalyticsSchema.md) (`item_sold`, `item_purchased`, `seed_purchased`,
   `crop_sold`) and add the event row there.
8. **Console feedback** — automation that acts without the player watching should emit a localized
   line via `GameEventService.EmitLocalized` (e.g. `ConsoleAutoSoldItem`, `ConsoleAutoPurchasedItem`).
9. **Virtual layer** — check whether `PitHero/VirtualGame/` needs a counterpart; see
   [VirtualGameLogicLayer.md](VirtualGameLogicLayer.md).

## Testing

Automation services are deliberately constructible without Nez, so tests instantiate them directly
and call the pass method rather than simulating frames:

| Area | Test file |
|---|---|
| Seed purchasing | `PitHero.Tests/AutoSeedPurchaseServiceTests.cs` |
| Crop selling | `PitHero.Tests/AutoCropSellServiceTests.cs` |
| Monster jobs | `PitHero.Tests/AutoJobAssignmentServiceTests.cs` |
| Excess-item selling + gear filters + consumable sell options | `PitHero.Tests/AutoSellExcessItemsTests.cs` |
| Item purchasing | `PitHero.Tests/AutoItemPurchaseServiceTests.cs` |
| Auto-equip | `PitHero.Tests/GearAutoEquipServiceTests.cs` |
| Hero skill auto-learn | `PitHero.Tests/AutoLearnSkillsServiceTests.cs` |
| Mercenary auto-hire matching | `PitHero.Tests/AutoHireMercenaryServiceTests.cs` |
| Save round-trips + defaults | `PitHero.Tests/SaveLoadTests.cs` |

`AutoItemPurchaseService` splits its entry point for this reason: `TryPurchasePass(HeroComponent)`
is the game-facing call, while `RunPurchasePass(hero, bag, mercenaries, purchasedOut)` takes plain
model objects and is what the tests drive.

The tab's UI itself is only constructed inside a live game session, so layout, gray-out states and
dialog behavior are **not** covered by tests — verify those by running the game.
