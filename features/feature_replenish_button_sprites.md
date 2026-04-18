# Feature: Replenish Button Dedicated Atlas Sprites

## 1. Feature Name
Replenish Button Dedicated Atlas Sprites

## 2. Objective
Replace the placeholder Gear atlas sprites on the Replenish UI button with the newly-added dedicated Replenish sprites so the button has its own visual identity.

## 3. Research Summary

### Relevant Files & Why
| File | Role |
|------|------|
| `PitHero/UI/ReplenishUI.cs` (lines 70-80) | **Only file that needs changes.** Contains `CreateButton()` which loads atlas sprites and builds `_normalStyle` / `_halfStyle`. |
| `PitHero/Content/Atlases/UI.atlas` | Already contains all six required sprites (confirmed). |
| `PitHero/UI/SettingsUI.cs` | Owns and positions `ReplenishUI`. No changes needed — it references the component, not its sprites. |

### Atlas Sprites Available (confirmed in UI.atlas)
| Sprite Name | Size | Purpose |
|-------------|------|---------|
| `UIReplenish` | Normal | Up state (idle) |
| `UIReplenishHighlight` | Normal | Over state (hover) |
| `UIReplenishInverse` | Normal | Down state (pressed) |
| `UIReplenish2x` | Half | Up state (half-window) |
| `UIReplenishHighlight2x` | Half | Over state (half-window) |
| `UIReplenishInverse2x` | Half | Down state (half-window) |

### Existing Pattern to Reuse
The `CreateButton()` method already follows the exact pattern: load sprites from atlas → build `ImageButtonStyle` with `ImageUp`/`ImageDown`/`ImageOver` → assign to `_normalStyle` and `_halfStyle`. Only the string keys change.

### Constraints & Rules
- Must use `HoverableImageButton` (already in use — no change).
- AOT compliance: no dynamic strings in game loop (sprite names are loaded once in `CreateButton()` — safe).
- No new styles, no font changes, no skin mutations needed.

### Risks / Edge Cases
- None significant. This is a 6-string substitution in a single method.

## 4. Scope

### In Scope
- Update sprite name strings in `ReplenishUI.CreateButton()`.

### Out of Scope
- No new monsters or equipment required — **skip**.
- No virtual game layer changes required — **skip**.
- No localization changes — button tooltip text is unchanged.
- No SettingsUI layout changes — button dimensions are read from the sprite's `SourceRect`, so if the new sprites are the same pixel size, positioning is automatic.

## 5. Constraints & Standards
- Follow Nez UI skill: use `HoverableImageButton`, `SpriteDrawable`, `ImageButtonStyle`.
- Comment rule: method already has `/// <summary>` — no changes needed.
- Remove the "Use Gear sprites as placeholder" comment (now incorrect).

## 6. Implementation Phases

### Phase 1: Update Sprite References (single task)

**Task 1.1 — Change sprite name strings in `ReplenishUI.CreateButton()`**

Target file: `PitHero/UI/ReplenishUI.cs`, lines 72-79.

Replace:
```csharp
// Use Gear sprites as placeholder
var sprite = uiAtlas.GetSprite("UIGear");
var sprite2x = uiAtlas.GetSprite("UIGear2x");
var highlight = uiAtlas.GetSprite("UIGearHighlight");
var highlight2x = uiAtlas.GetSprite("UIGearHighlight2x");
var inverse = uiAtlas.GetSprite("UIGearInverse");
var inverse2x = uiAtlas.GetSprite("UIGearInverse2x");
```

With:
```csharp
var sprite = uiAtlas.GetSprite("UIReplenish");
var sprite2x = uiAtlas.GetSprite("UIReplenish2x");
var highlight = uiAtlas.GetSprite("UIReplenishHighlight");
var highlight2x = uiAtlas.GetSprite("UIReplenishHighlight2x");
var inverse = uiAtlas.GetSprite("UIReplenishInverse");
var inverse2x = uiAtlas.GetSprite("UIReplenishInverse2x");
```

**Definition of done:** The six `GetSprite()` calls reference `UIReplenish*` names, the placeholder comment is removed, and the file compiles cleanly.

## 7. Test & Validation Plan

- **Build:** `dotnet build`
- **Unit tests:** `dotnet test PitHero.Tests/`
- **Manual smoke test:** Launch game, verify Replenish button renders with new artwork in both normal and half-height window modes; hover and click states display correct sprites.

No new unit tests required — no logic change, only cosmetic asset swap.

## 8. Risks & Mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| New sprites are a different pixel size than Gear sprites | Low | Button size is set from `sprite.SourceRect` dynamically, so it auto-adjusts. SettingsUI repositions based on `GetWidth()`. |
| Typo in sprite name causes runtime null ref | Low | Names confirmed present in atlas via grep. Build + run will surface any mismatch immediately. |

## 9. Open Questions / Decisions Needed
None — all information is available.

## 10. Acceptance Criteria
1. `ReplenishUI.CreateButton()` references `UIReplenish`, `UIReplenishHighlight`, `UIReplenishInverse` (and their `2x` variants).
2. No references to `UIGear*` remain in `ReplenishUI.cs`.
3. `dotnet build` succeeds.
4. `dotnet test PitHero.Tests/` passes.
5. Replenish button visually renders the new sprites in both normal and half-height modes.

## 11. Downstream Handoff Notes
This is a trivial cosmetic change. A single `replace_string_in_file` call on `ReplenishUI.cs` (lines 72-79) is sufficient. No other files, systems, or agents are affected.
