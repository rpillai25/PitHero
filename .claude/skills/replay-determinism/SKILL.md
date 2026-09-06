---
name: replay-determinism
description: "**DOMAIN SKILL** — Keeping PitHero features replay-safe. The game records every session and replays it by re-simulation (fixed 60 Hz tick + seeded RNG + recorded PlayerCommands), so determinism is a hard project rule. USE FOR: any feature that adds a player action (button, drag, slider, dialog, hotkey) that changes game state; any new randomness (Nez.Random, System.Random, shuffle bags, particles, sound variants); timers, cooldowns, timestamps, accumulators; new static or global state read by the simulation; new scene services; cosmetic components during seeks; diagnosing a 'Diverged at' label or replay_divergence.log; touching PlayerCommandService/PlayerCommandHandlers/GameRandom/SimulationClock/ReplayPlaybackService; changes to MainGameScene.Update vs PresentationUpdate. DO NOT USE FOR: replay UI layout (nez-ui), GOAP action design (nez-ai), balance testing (pit-balance-test)."
applyTo: "**/Services/Replay/**,**/GameRandom.cs,**/SimulationClock.cs,**/PauseService.cs,**/MainGameScene.cs,**/UI/**,**/AI/**,**/Services/**,**/ECS/Components/**"
---

# Replay Determinism — PitHero Conventions

A replay is **not a recording of what happened**; it is the same simulation run again from the same
seed with the same player commands injected on the same ticks. Anything that makes two runs differ
breaks every replay silently, and the only symptom is a "Diverged at m:ss" label on the scrubber.
Full reference: `PitHero/docs/ReplaySystem.md`. Rule summary: `AGENTS.md` → "Replay Determinism".

## CRITICAL RULES (Never Violate)

1. **Player → simulation goes through `PlayerCommand`.** A UI action that changes hero, party,
   inventory, gold, farm, buildings, automation flags or pause dispatches a command; it never mutates
   directly. `PlayerCommandType` is persisted: **append only, never renumber**.
2. **Simulation code never reads `Input`, `Time.TotalTime`, `Time.FrameCount`, `DateTime`,
   `Stopwatch`, `Environment.TickCount`.** Timestamps come from `SimulationClock.Now`; `Time.DeltaTime`
   inside a step is the fixed step and is fine.
3. **Only the simulation rolls `Nez.Random`.** UI → `GameRandom.Ui`, audio → `GameRandom.Audio`,
   particles → Nez `ParticleRandom`, mid-battle epic chest → `GameRandom.Loot`. No new `System.Random`,
   `Guid` or hash-order dependence in sim code.
4. **Static/global state the sim reads resets at the session reseed** (top of `MainGameScene.Begin`)
   or is scene-scoped and removed in `Unload`. Seeks restart the scene inside one process.
5. **Presentation never feeds back into the sim** except through commands; a handler's outcome must
   not depend on which window is open or hovered.
6. **Cosmetic-only components** early-return or finish instantly on `Core.CosmeticUpdatesSuspended`;
   anything the sim waits on (sprite animators, `AnimationState`) must not skip.
7. **Speed is more fixed steps** (`Core.SimulationSpeed`), never `Time.TimeScale`.
8. **Validate with a real replay**: play the feature, Settings → Replay → Replay Current Session,
   seek across it, confirm **In sync**.

## The two passes

| Pass | Runs | Owns | May read |
|---|---|---|---|
| Simulation step (`MainGameScene.Update`, entity/component `Update`, managers) | N times per frame at exactly 1/60 s | World, hero, party, economy, farm, kitchen, AI | `Nez.Random`, `SimulationClock`, `Time.DeltaTime`, commands drained at the end of the tick |
| Presentation (`MainGameScene.PresentationUpdate`, UI stages, `CameraControllerComponent.PresentationUpdate`) | Once per rendered frame, even when zero steps ran | UI, camera, HUD, overlays, hover/click, tooltips | `Input`, wall clock, `GameRandom.Ui/Audio`; writes to the sim only by dispatching commands |

## What to Read Next (Progressive Disclosure)

| If you are working on… | Read |
|---|---|
| Adding or changing a player action, the `Dispatch` / `ShouldApplyDirectly` pattern, handler rules, payload packing | `references/player-command-recipe.md` |
| Auditing a feature for determinism, RNG streams, timers, static state, cosmetics, scene services, and diagnosing "Diverged at" | `references/determinism-audit.md` |
| Architecture, file format, playback/seek internals, configuration, tests | `PitHero/docs/ReplaySystem.md` |

## Quick Gotchas

| Gotcha | Fix |
|---|---|
| "Diverged at" with only the `rng` part differing | An extra/missing `Nez.Random` roll: a UI/audio/particle consumer on the sim stream, or a sim roll gated on something non-deterministic |
| Diverges only after a backward seek, not on first play | Static state survived the scene restart — reset it at the reseed (`ShuffleBag.Reset`, service `Detach`, queue drain) |
| Feature works live, replay skips it | The mutation never became a `PlayerCommand`; playback has nothing to inject |
| Handler works live, no-ops on replay | Handler resolved its target through UI state (selected row, open window) instead of a stable index/id/name in the payload |
| One-shot animation plays late after a seek | Component froze under `CosmeticUpdatesSuspended` instead of finishing instantly |
| Timer drifts between live and replay | Stored `Time.TotalTime`; store `SimulationClock.Now` |
| Command applied one tick late in replay | Applied between steps with the wrong tick; use `PlayerCommandService.ApplyNow` (records at tick-1) only when the drain cannot run |
| Clicks ignored during playback | Expected: `RejectLiveEnqueues` drops live commands while a replay runs |
| New `MainGameScene` throws duplicate service key | Old scene still registered; scene swaps go through `ReplayBootScene`, services removed in `Unload` |

## File Reference

- `PitHero/Services/Replay/PlayerCommand.cs` — `PlayerCommandType` (payload comments per member), `PlayerCommand`, `SlotRefCodec`
- `PitHero/Services/Replay/PlayerCommandService.cs` — `Dispatch`, `ShouldApplyDirectly`, `ApplyNow`, `Drain`, `RejectLiveEnqueues`
- `PitHero/Services/Replay/PlayerCommandHandlers.cs` + `.Extended.cs` — the handler `switch`
- `PitHero/Services/GameRandom.cs`, `Services/Replay/SeedableRandom.cs` — streams
- `PitHero/Services/SimulationClock.cs` — sim timestamps
- `PitHero/Services/Replay/ReplayTripwire.cs`, `SimulationStateHasher.cs` — divergence detection
- `PitHero/Services/Replay/ReplayPlaybackService.cs` — playback, seek, exit, divergence log
- `PitHero/ECS/Scenes/MainGameScene.cs` — seed lifecycle in `Begin`, drain at the end of `Update`, `PresentationUpdate`
- `PitHero/GameConfig.cs` — "Simulation clock" / "Replay playback" constants
