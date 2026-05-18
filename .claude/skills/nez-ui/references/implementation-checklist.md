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

## Common Patterns Worth Reusing

### Tabbed UI with per-tab state refresh

When a window owns multiple tabs that show *changing* data (JP, equipment, learned skills), refresh per-tab content on **window open**, not on tab switch. Example: `HeroUI.ToggleHeroWindow` calls `_heroCrystalTab.UpdateWithHero(hero)` whenever the window opens, and `Update()` refreshes the crystal-tab tooltip position each frame while shown. Click-to-purchase inside a tab triggers a local `RebuildSkillGrid` after the JP mutation, not a full window rebuild.

### Atlas-driven icon grid (skills, stencils)

Skills and synergy patterns load icons from `Content/Atlases/SkillsStencils.atlas` keyed by the **skill ID** or **pattern ID**:

```csharp
var atlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
var sprite = atlas.GetSprite(skill.Id);   // e.g. "knight.spin_slash", "synergy.holy_strike"
```

Naming convention: `synergy.*` for synergy-unlocked skills, `{job}.*` for job skills, `{job}.{pattern}` for pattern-only stencils.

### Conditional sprite selection with fallback

For elements that may render either of two related sprites (e.g., stencil shows skill icon if unlocked, pattern icon if not), prefer the more specific one with a chained fallback:

```csharp
string spriteName = _pattern.UnlockedSkill != null
    ? _pattern.UnlockedSkill.Id
    : _pattern.Id;
var sprite = atlas.GetSprite(spriteName);
if (sprite != null) drawable.Draw(...);
else DrawFallbackIcon(batcher);     // e.g. first item kind from pattern
```

Wrap the atlas load in `try/catch` and fall back silently — missing sprites or a missing atlas should never crash the UI.

### State-aware icon rendering

For grid icons with learned/unlearned states, render learned at full color and unlearned at **0.5 alpha** (don't use a separate grayscale sprite). Synergy skills are auto-learned (no JP click) — clicking an unlearned synergy skill should show an info message, not a purchase dialog. Only job skills route to the JP confirmation flow.
