# Crystal Ceremony

## Overview
When a hero dies (see [Permadeath.md](Permadeath.md) for the death animation and crystal-vault flow), a **new hero** is respawned without a crystal. The hero walks to the hero statue, a lightning strike animation plays, and the hero is imbued with a new crystal — from the crystal queue if one is available, otherwise a randomly generated one.

> **History:** An older system promoted a random unhired mercenary into the new hero. That feature was removed; heroes are now always created new and receive their crystal via this ceremony. The dead code path was deleted from `HeroPromotionService` (the service name is retained for continuity).

## Flow

1. **Hero death** — `HeroDeathComponent` plays the death animation and moves the hero's crystal to the vault (see Permadeath.md). `MainGameScene.RespawnHeroAfterDelay` → `RespawnHero()` runs afterward.
2. **Respawn** — `RespawnHero()` calls `CreateHeroEntity(34, 6, needsCrystal: true)`. The hero spawns with `HeroComponent.NeedsCrystal = true` and no `LinkedHero`. Saving is disabled during this transitional state. Hired mercenaries are unfrozen and reassigned to follow the new hero; the pit resets to level 1 once all mercenaries have exited (or a safety timeout elapses).
3. **Walk to statue** — the GOAP action `WalkToStatueForCrystalAction` (`PitHero/AI/WalkToStatueForCrystalAction.cs`) paths the hero to the statue and sets `HeroComponent.HasArrivedAtStatueForCrystal = true`. GOAP states: `GoapConstants.NeedsCrystal`, `GoapConstants.HasArrivedAtStatueForCrystal`.
4. **Ceremony** — `HeroPromotionService.CheckAndPromoteHeroIfNeeded()` (called every frame from `MainGameScene.Update()`) detects `NeedsCrystal && HasArrivedAtStatueForCrystal` and starts `ExecuteHeroCrystalCeremony`:
   - Movement and AI are disabled; the hero faces the statue (`Direction.Up`) for 1 second
   - The "LightningStrike" animation from Actors.atlas plays (`PlayLightningStrikeAtHero`)
   - `GetNextCrystalForHero()` selects the crystal: dequeue from `CrystalCollectionService`, else `GenerateRandomHeroCrystal()` (random primary job, level 1, random 2–5 base stats)
   - A new `Hero` is created with the chosen crystal. Spawn level is `max(crystal level, TierBaseLevel)` so tier ≥ 2 heroes respawn at the recorded tier base level
   - `NeedsCrystal`/`HasArrivedAtStatueForCrystal` are cleared, movement/AI re-enabled
   - `ReconnectUIToHero()` reconnects the ShortcutBar and InventoryGrid to the new hero (these reconnects are idempotent — the underlying events are static, see `ShortcutBar.ConnectToDragManager`)
   - The Save button is re-enabled

## Hero Statue

**Location:** statue sprite at tile (112, 3); hero destination tile (112, 6)
**Sprite:** "HeroStatue" from Actors.atlas
**Render Layer:** `GameConfig.RenderLayerActors`

## Lightning Strike Animation

**Animation:** "LightningStrike" from Actors.atlas
**Play Mode:** Once (`LoopMode.Once`), 5-second safety timeout
**Render Layer:** `GameConfig.RenderLayerTop`

## Related Files

- `PitHero/Services/HeroPromotionService.cs` — `CheckAndPromoteHeroIfNeeded`, `ExecuteHeroCrystalCeremony`, `GetNextCrystalForHero`
- `PitHero/AI/WalkToStatueForCrystalAction.cs` — GOAP walk-to-statue action
- `PitHero/ECS/Scenes/MainGameScene.cs` — `RespawnHero`, `ReconnectUIToHero`, per-frame ceremony check
- `PitHero/ECS/Components/HeroComponent.cs` — `NeedsCrystal`, `HasArrivedAtStatueForCrystal`
- `PitHero/docs/Permadeath.md` — the death animation and crystal-vault half of the death→respawn cycle
