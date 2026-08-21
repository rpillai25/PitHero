# Crystal Ceremony

## Overview
When a hero dies (see [Permadeath.md](Permadeath.md) for the death animation and crystal-vault flow), a **new hero** is respawned without a crystal. The hero walks to the hero statue, a lightning strike animation plays, and the hero is imbued with a new crystal — from the crystal queue if one is available, otherwise a randomly generated one.

> **History:** An older system promoted a random unhired mercenary into the new hero. That feature was removed; heroes are now always created new and receive their crystal via this ceremony. The dead code path was deleted from `HeroPromotionService` (the service name is retained for continuity).

## Flow

1. **Hero death** — `HeroDeathComponent` plays the death animation and moves the hero's crystal to the vault (see Permadeath.md). `MainGameScene.RespawnHeroAfterDelay` → `RespawnHero()` runs afterward.
2. **Respawn** — `RespawnHero()` calls `CreateHeroEntity(34, 6, needsCrystal: true)`. The hero spawns with `HeroComponent.NeedsCrystal = true` and no `LinkedHero`. Saving is disabled during this transitional state. Hired mercenaries are unfrozen and reassigned to follow the new hero; the pit tier resets to 1 immediately (`PitWidthManager.ResetTierForNewCycle`), and the pit resets to level 1 once all mercenaries have exited (or a safety timeout elapses).
3. **Walk to statue** — the GOAP action `WalkToStatueForCrystalAction` (`PitHero/AI/WalkToStatueForCrystalAction.cs`) paths the hero to the statue and sets `HeroComponent.HasArrivedAtStatueForCrystal = true`. GOAP states: `GoapConstants.NeedsCrystal`, `GoapConstants.HasArrivedAtStatueForCrystal`. A random respawn speech bubble (`SpeechBubbleDialogue.SayRespawn`) fires as the walk begins — it can't fire inside `RespawnHero()` because Nez defers component init by a frame (see [SpeechBubbleSystem.md](SpeechBubbleSystem.md)).
4. **Ceremony** — `HeroPromotionService.CheckAndPromoteHeroIfNeeded()` (called every frame from `MainGameScene.Update()`) detects `NeedsCrystal && HasArrivedAtStatueForCrystal` and starts `ExecuteHeroCrystalCeremony`:
   - The ceremony prayer bubble ("Spirit of the Hero, grant me strength!") fires immediately; movement and AI are disabled and the hero faces the statue (`Direction.Up`) for a 4.0s dwell (0.5 + 3.5) — sized so the bubble's reveal + linger (~3.9s) completes before the lightning strike. Don't shorten the dwell without accounting for the bubble.
   - The "LightningStrike" animation from Actors.atlas plays (`PlayLightningStrikeAtHero`)
   - `GetNextCrystalForHero()` selects the crystal: dequeue from `CrystalCollectionService`, else `GenerateRandomHeroCrystal()` (random primary job, level 1, random 2–5 base stats)
   - A new `Hero` is created with the chosen crystal. Spawn level is `max(crystal level, TierBaseLevel)`; since hero death resets the pit cycle (`ResetTierForNewCycle` sets `TierBaseLevel` back to 1), this is effectively the crystal level
   - `NeedsCrystal`/`HasArrivedAtStatueForCrystal` are cleared, movement/AI re-enabled
   - `ReconnectUIToHero()` reconnects the ShortcutBar and InventoryGrid to the new hero (these reconnects are idempotent — the underlying events are static, see `ShortcutBar.ConnectToDragManager`)
   - The Save button is re-enabled

## Hero Statue

**Location:** statue sprite anchored at `GameConfig.HeroStatueTileX/Y` (112, 3); the 181px-tall sprite's base lands on row 6, so heroes stand at `GameConfig.HeroStatueStandTileX/Y` (112, 6)
**Sprite:** "HeroStatue" from Actors.atlas
**Render Layer:** `GameConfig.RenderLayerActors`

## Lightning Strike Animation

**Helper:** `Util/LightningStrikeEffect.PlayAt(scene, worldPosition)` — a coroutine shared by the ceremony and the new-game intro
**Animation:** "LightningStrike" from Actors.atlas
**Play Mode:** Once (`LoopMode.Once`), 5-second safety timeout
**Render Layer:** `GameConfig.RenderLayerTop`
**Sound:** none (cosmetic only)

## New-Game Intro (issue #396)

A brand-new game (`SaveLoadService.PendingLoadData == null`, captured at the top of `MainGameScene.Begin()` because `ApplyPendingLoadData` clears it) opens with a scripted sequence run by `Services/NewGameIntroService`:

1. The hero is created at the statue's feet (`HeroStatueStandTileX/Y`) **without** a `HeroStateMachine` — Nez's `SimpleStateMachine.InitialState` setter runs `Idle_Enter` synchronously inside the deferred `OnAddedToEntity`, which would plan the first pit trip (and burn the one-shot pit bubble) immediately. Its `MultiSpriteAnimator` is disabled so no standing frame renders.
2. `MainGameScene.BeginIntroPresentation` hides the labels and graphical HUD, `SettingsUI.EnterIntroMode()` snaps the top bar / shortcut bar / event console off-screen (re-pinned every frame while active) and raises a transparent full-stage blocker (every stage hit-test succeeds → all world-interaction gates close), and `CameraControllerComponent.InputSuspended` + `CenterOnWorldPosition` park the camera on the statue (the centre is latched until the controller's deferred init). The farm/kitchen coordinator ticks are also held while `IsIntroActive`, so the starter Slime stays inside its Monster House and emerges only after the intro.
3. Once the paperdoll animators are loaded, the hero is posed with `HeroJumpComponent.BeginAirbornePose(Down)` and lifted above the visible top edge (`ComputeFallStartHeight` from the controller's render-target math — not `Camera.Bounds`, which reads the backbuffer from a coroutine), then dropped with a gravity ease (`ComputeFallHeight`, `GameConfig.IntroFallDurationSeconds`) and the Land sound.
4. `SetFacing(Up)`, `SpeechBubbleDialogue.SayIntro` (`HeroIntroDestiny`), and a dwell of `GameConfig.IntroPrayerDwellSeconds` sized to the bubble's reveal + linger (change them together), then `LightningStrikeEffect.PlayAt` — purely for show, the hero already has its crystal.
5. `MainGameScene.EndIntroPresentation` restores the HUD (bars slide back in), re-enables camera input and adds the `HeroStateMachine`, whose first plan is the normal pit trip from the statue.

No save-format change (the intro never applies to a loaded game) and no virtual-layer counterpart (presentation only).

## Related Files

- `PitHero/Services/HeroPromotionService.cs` — `CheckAndPromoteHeroIfNeeded`, `ExecuteHeroCrystalCeremony`, `GetNextCrystalForHero`
- `PitHero/Util/LightningStrikeEffect.cs` — shared lightning strike coroutine
- `PitHero/Services/NewGameIntroService.cs` — new-game intro sequence
- `PitHero/AI/WalkToStatueForCrystalAction.cs` — GOAP walk-to-statue action
- `PitHero/ECS/Scenes/MainGameScene.cs` — `RespawnHero`, `ReconnectUIToHero`, per-frame ceremony check, `BeginIntroPresentation` / `EndIntroPresentation`
- `PitHero/ECS/Components/HeroComponent.cs` — `NeedsCrystal`, `HasArrivedAtStatueForCrystal`
- `PitHero/ECS/Components/HeroJumpComponent.cs` — `BeginAirbornePose` / `SetAirborneHeight` / `EndAirbornePose` (airborne look without the jump timing)
- `PitHero/docs/Permadeath.md` — the death animation and crystal-vault half of the death→respawn cycle
