# Automation System

**Settings → Automation** is the single home for every "the game does this for me" toggle. This
document covers the tab as a whole: which service owns each control, when each automation actually
runs, how the settings persist, and the non-obvious constraints that are easy to break when adding
a new option.

Monster job assignment is the one automation with its own deep-dive — see
[AutoJobAssignmentSystem.md](AutoJobAssignmentSystem.md). This document does not repeat it.

## The tab at a glance

Built by `SettingsUI.PopulateAutomationTab` (`PitHero/UI/SettingsUI.cs`), which delegates the two
newest blocks to `PopulateAutoPurchaseControls` and `PopulateAutoEquipControls`. The whole tab is
wrapped in a vertical `ScrollPane` — it has outgrown the 450×350 settings window, so **new controls
just get appended; do not try to keep the tab short**.

| Control group | Owning service / state | Runs | Save section |
|---|---|---|---|
| Automate monster jobs | `AutoJobAssignmentService` | Ticked (cadence) | 34 (v19) |
| **Gold Buffer** (label + slider) | `AutoSeedPurchaseService.GoldBuffer` | — (read by others) | 30 (v15) |
| Auto-Purchase Seeds | `AutoSeedPurchaseService` | Ticked (1s throttle) | 30 (v15) |
| Auto-Sell Crops + "Choose Crops to Sell" | `AutoCropSellService` | Ticked | 31–32 (v16/v17) |
| Auto-Sell Excess Items + priority + "Gear Sell Options" | `AutoSellExcessItemsService` | Call-driven | 36–38 (v21/v22/v23) |
| Auto-Purchase Items + priority + merc opt-in + "Gear Purchase Options" + "Consumable Purchase Options" | `AutoItemPurchaseService` | Call-driven | 39 (v23) |
| Auto-Equip Options | `HeroComponent.AutoEquipHero` / `.AutoEquipMercenaries` (**no service**) | Call-driven | 40 (v23) |

Dialogs opened from the tab:

| Dialog | File | Notes |
|---|---|---|
| Auto-Sell Crop Types | `PitHero/UI/AutoSellCropTypesDialog.cs` | Per-crop checkboxes + keep-stacks slider |
| Gear Sell Options / Gear Purchase Options | `PitHero/UI/GearFilterOptionsDialog.cs` | **One class, two instances** — rarity + gear-type filters, parameterized by title/label keys and `Func<bool[]>` accessors |
| Consumable Purchase Options | `PitHero/UI/ConsumablePurchaseOptionsDialog.cs` | Sprite + checkbox + per-item 1–3 "Stacks" slider |

All dialogs follow the same shape: a plain class owning a `Nez.UI.Window`, built once in the
constructor, `SetVisible(false)`, added to the stage; `Show()` syncs from the service, packs,
centers, `ToFront()`. They are constructed lazily on first button click and reused thereafter.

## Ownership rules that are not obvious

**The Gold Buffer lives on `AutoSeedPurchaseService`.** It is the single shared gold floor for
*every* automated purchase, not just seeds — no auto-purchase may take `Funds` below it. The
setting predates item purchasing and kept its home, so `AutoItemPurchaseService` takes
`AutoSeedPurchaseService` by constructor injection and exposes a read-only
`GoldBuffer => _goldBufferSource?.GoldBuffer ?? 0`. Consequences:

- `AutoItemPurchaseService` **must be registered after** `AutoSeedPurchaseService` in
  `MainGameScene.Begin()`.
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
  `AutoSeedPurchaseService` (1-second throttle), `AutoCropSellService`, `AutoJobAssignmentService`.
  Each exposes a public, throttle-free pass method (`TryPurchasePass()`, `TrySellPass()`,
  `ReassessNow()`) so tests can drive it directly.
- **Call-driven** — no update loop; invoked from the game action that creates the situation.
  - `AutoSellExcessItemsService.TryMakeRoom(bag, incoming)` ← `OpenChestAction`, before adding a
    chest item to a full bag.
  - `AutoItemPurchaseService.TryPurchasePass(heroComp)` ← `JumpIntoPitAction`, in the first-frame
    branch **after** the landing tile is validated and **before** `StartJumpMovement`. Placing it
    after validation means an aborted jump never spends gold; the branch runs once per jump.
  - `PartyAutoEquipHelper.TryAutoEquipForParty(heroComp, item)` ← `OpenChestAction` (chest loot) and
    `AutoItemPurchaseService` (each purchased item).

## Persistence

Save version constants live in `PitHero/Services/SaveData.cs` (`CurrentVersion`,
`MinSupportedVersion`). The format is **strictly append-only**: older files are byte-exact prefixes
of newer ones, so a v17 file still loads into a v23 build with defaults for the missing tail.

Adding a persisted setting means, in order:

1. Add the field to `SaveData` with a default and a `// Feature name (vNN)` comment.
2. Append a **new numbered section at the very end** of `Persist`. Arrays are written as a count
   followed by the elements.
3. Append the mirrored read at the very end of `Recover`: **initialize defaults first, then**
   `if (fileVersion >= NN) { ... }`. Clamp array reads with `if (i < arr.Length)` so arrays that
   grow later still load. Defaults for "allowed" filters are all-true, so categories added after a
   save was written behave like a fresh enable.
4. Bump `CurrentVersion` and extend the `MinSupportedVersion` doc comment's section list.
5. Service → `SaveData` in `SaveLoadService` (the collect method); `SaveData` → service in
   `MainGameScene`'s load-apply path.
6. Extend `SaveLoadTests` with a round-trip test *and* a defaults test (a null array persists a
   zero count, which also exercises the "older file, section absent" path).

**Two indices are persisted identities and must stay append-only:**

- `ConsumableCatalog` (`PitHero/RolePlayingFramework/Equipment/ConsumableCatalog.cs`) — the order of
  its 9 entries is what `AutoPurchaseConsumableSelected` / `AutoPurchaseConsumableStacks` index
  into. Reordering silently remaps a player's selections onto the wrong potions.
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
`SetExcessItemControlsActive`, `SetItemPurchaseControlsActive`, `SetConsumablePurchaseControlsActive`.
`ReorderableTableList` has its own `SetGrayed(bool)`.

Gotchas:

- **Sliders have no `ph-grayed` style.** They use `DisabledBackground` / `DisabledKnob` on the shared
  `ph-default` `SliderStyle`, so deactivating one is `slider.Disabled = true` for the look — and
  Nez's `Slider` input listener **ignores `Disabled`**, so `SetTouchable(Touchable.Disabled)` is
  what actually stops dragging. You need both.
- **Nested gates compose.** "Consumable Purchase Options" is enabled only when *both*
  "Auto-Purchase Items" and "Auto-Purchase Consumables" are checked;
  `SetItemPurchaseControlsActive` ends by calling `SetConsumablePurchaseControlsActive` with the
  combined condition. Follow that pattern rather than gating on one checkbox.
- **Hide open dialogs when the parent turns off**, or a deactivated feature's dialog stays on screen.
- `PitHeroSkin.CreateSkin()` returns a **cached singleton** — never mutate a shared style; `Clone()`
  first if you need a variant (see `VaultBuyQuantityDialog`).

## Shared logic worth reusing

| Piece | File | Use it for |
|---|---|---|
| `GearCategoryUtils` | `RolePlayingFramework/Equipment/GearCategoryUtils.cs` | `ItemKind` → `GearCategory`, localization keys, `IsAllowed(bool[], ItemKind)` |
| `GearAutoEquipService` | `RolePlayingFramework/Equipment/GearAutoEquipService.cs` | `GetGearScore`, `IsNewGearBetter`, `TryGetSlotForGear`, `GetHeroItemInSlot` / `GetMercItemInSlot` |
| `PartyAutoEquipHelper` | `AI/PartyAutoEquipHelper.cs` | Hero → mercs equip cascade with hand-me-downs, honoring the auto-equip flags |
| `ExcessItemSellSelector` | `RolePlayingFramework/Equipment/ExcessItemSellSelector.cs` | Pure "which item should we sell" logic, filter delegates optional |
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
| Excess-item selling + gear filters | `PitHero.Tests/AutoSellExcessItemsTests.cs` |
| Item purchasing | `PitHero.Tests/AutoItemPurchaseServiceTests.cs` |
| Auto-equip | `PitHero.Tests/GearAutoEquipServiceTests.cs` |
| Save round-trips + defaults | `PitHero.Tests/SaveLoadTests.cs` |

`AutoItemPurchaseService` splits its entry point for this reason: `TryPurchasePass(HeroComponent)`
is the game-facing call, while `RunPurchasePass(hero, bag, mercenaries, purchasedOut)` takes plain
model objects and is what the tests drive.

The tab's UI itself is only constructed inside a live game session, so layout, gray-out states and
dialog behavior are **not** covered by tests — verify those by running the game.
