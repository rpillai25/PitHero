# UI Implementation Workflow & Validation

When implementing UI features, follow this checklist alongside the design rules in `SKILL.md`.

## Implementation Approach

1. **Understand the requirements first.** Identify which existing UI panels are affected and what user interactions are added or changed.
2. **Reuse existing patterns.** Check `PitHero/UI/` for similar features before writing new code. Examples worth grepping for: `ConfirmationDialog`, `HoverableLabel`, `ItemCard`, `ReorderableTableList`, `InventoryDragManager`.
3. **Inherit before duplicating.** When a new custom element is needed, inherit and override the closest Nez class first. Only duplicate Nez source if inheritance is insufficient.
4. **One UI class per feature.** A `MyFeatureUI` class owns the stage/skin references and builds its own window(s).
5. **Wire localization.** All display text comes from `Content/Localization/en-US/UI.txt` via `TextService.GetText(TextKey.X)`. Add tooltip keys with a `*Tooltip` suffix.
6. **Wire AOT.** No `foreach`, no LINQ in update loops or per-frame code paths. Pre-allocate collections.

## Live Strip Considerations

The 1920×360 live strip renders the current `WorldState`. UI windows are added to the same stage via `UICanvas` at `RenderLayerUI = 998`, which always stays on top of game rendering.

- Use `IsFullScreen = true` for screen-space (raw mouse coordinates).
- Use `IsFullScreen = false` for world-space (mouse coordinates scaled by camera).

## Validation Requirements (mandatory before handoff)

```bash
dotnet build PitHero.sln
dotnet test PitHero.Tests/PitHero.Tests.csproj
```

Both must pass. UI work doesn't have automated UI tests in this repo, so build + test gates code-correctness; visual validation is manual (`dotnet run`).

## Manual Visual Validation

For any UI change, also run the game and verify:

1. The window opens and closes without leaving stray elements on the stage.
2. The window centers correctly when the design resolution changes (test at `BestFit`).
3. Buttons, tooltips, drag-and-drop, and keyboard/gamepad focus all work end-to-end.
4. Localization strings render (no `[UI.SomeKey]` placeholders visible).
5. No new `Debug.Log` spam in the console under normal use.

## Common Implementation Tasks

| Task | Key files |
|---|---|
| New dialog | `ConfirmationDialog.cs`, `*Dialog.cs` pattern |
| New tab in HeroUI | `HeroUI.cs`, `TabPane` setup |
| New shop panel | `SecondChanceShopUI.cs`, `*ShopUI.cs` pattern |
| New drag-and-drop slot | `InventorySlot.cs`, `InventoryDragManager.cs` |
| New tooltip | `HoverableLabel.cs`, `ItemCardTooltip.cs` |
| New skin element | `PitHeroSkin.cs` (add under `"ph-default"`) |

## Build/Test Loop

```
1. Edit code
2. dotnet build           # fix compiler errors
3. dotnet test            # fix failing tests
4. dotnet run             # visual verify
5. Repeat
```

Don't ship UI changes that compile but haven't been visually verified — UI is the one area where automated tests don't catch most regressions.
