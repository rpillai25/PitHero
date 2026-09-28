---
name: replay-determinism
description: "**DOMAIN SKILL** — Keeping PitHero features replay-safe. The game records every session and replays it by re-simulation (fixed 60 Hz tick + seeded RNG + recorded PlayerCommands), so determinism is a hard project rule. USE FOR: any feature that adds a player action (button, drag, slider, dialog, hotkey) that changes game state; any new randomness (Nez.Random, System.Random, shuffle bags, particles, sound variants); timers, cooldowns, timestamps, accumulators; new static or global state read by the simulation; new scene services; cosmetic components during seeks; HUD panels, labels or overlays that display world state (they must read the recorded frame during a replay); diagnosing a 'Diverged at' label or replay_divergence.log; touching PlayerCommandService/PlayerCommandHandlers/GameRandom/SimulationClock/ReplayPlaybackService; changes to MainGameScene.Update vs PresentationUpdate. DO NOT USE FOR: replay UI layout (nez-ui), GOAP action design (nez-ai), balance testing (pit-balance-test)."
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
   not depend on which window is open or hovered. Corollaries (each was a real divergence): no UI
   class computes or stores a value the sim reads (synergy passives → `HeroSynergyResolver`); a UI
   object a handler executes on is rebound and refreshed from the sim first
   (`InventoryGrid.SyncFromSimulation`) because the overlay is built before the hero exists and a
   never-opened window's grid is not connected at all.
6. **Cosmetic-only components** early-return or finish instantly on `Core.CosmeticUpdatesSuspended`;
   anything the sim waits on (sprite animators, `AnimationState`) must not skip.
7. **Speed is more fixed steps** (`Core.SimulationSpeed`), never `Time.TimeScale`.
8. **Validate with a real replay**: play the feature, Settings → Replay → Replay Current Session,
   seek across it, confirm **In sync**.
9. **A new `RenderableComponent` is stock, `IFrameCapturable` or `ILiveOnlyRenderable`.** Replays
   are *watched* from a recorded per-tick frame stream (`Services/Replay/Frames/FrameRecorder`), so an
   unclassified renderable is simply absent from every replay. Stock sprite renderers/animators,
   `SpriteCompositorBase` composites and `ParticleEffectManager` emitters need nothing; custom
   drawing implements `IFrameCapturable.CaptureFrame` (emit Sprite/Text/Rect/NinePatch ops through the
   context's interning, no allocation per tick) or is `ILiveOnlyRenderable` (draws live while viewing,
   only for what is not part of the recorded world). `FrameCaptureCoverageTests` fails by name until
   the type is classified and added to its list.
10. **A new simulation sound goes through `SoundEffectManager.PlaySound` / `PlaySoundAt`.** The frame
    stream records what the manager plays (type, rolled variant, position) so a replay hears it; a raw
    `SoundEffect.Play` is silent in replays. UI click sounds belong to the global button hooks
    (`ButtonClickCategory`) and are never recorded.
11. **While a recorded frame is on screen, presentation reads the record, never the live entities.**
    In FrameView the live world sits suspended elsewhere on the timeline; during a Time Travel rebuild
    it races through the session behind the frozen frame. HUD panels, labels and overlays showing
    world state take their values from the HUD record (`MainGameScene.ApplyRecordedHud`, gated by
    `recordedHud`) and must not react to live entities in either state (the party auto-hide parks
    during a seek). What the record does not carry is pinned at `ReplayFrameViewer.Freeze` (mercenary
    portraits) or added to the record with a `ReplayFrameFormatVersion` bump.

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
| A divergence that code sweeps cannot explain, or any UI class that computes something the sim reads | `references/case-study-2026-09-14.md` — the snapshot → trace → analytics method and the three UI-feeds-sim bugs it found |

## Quick Gotchas

| Gotcha | Fix |
|---|---|
| "Diverged at" with only the `rng` part differing | An extra/missing `Nez.Random` roll: a UI/audio/particle consumer on the sim stream, or a sim roll gated on something non-deterministic |
| Diverges only after a backward seek, not on first play | Static state survived the scene restart — reset it at the reseed (`ShuffleBag.Reset`, service `Detach`, queue drain) |
| Feature works live, replay skips it | The mutation never became a `PlayerCommand`; playback has nothing to inject |
| `hero` part diverges only during seeks, RNG equal | Update-order drift: something reorders components/entities by history. Nez `ComponentList` uses a stable sort for this reason; never sort update lists with `Array.Sort` on tied keys |
| Handler works live, no-ops on replay | Handler resolved its target through UI state (selected row, open window) instead of a stable index/id/name in the payload |
| `rng` + `world` diverge a few ticks after a command, hero/party equal, world differs only in the pause flag | The handler's UI executor closed itself and unpaused from inside the handler. Live: unpause applied directly (never recorded). Replay: dialog never open, stays paused. Defer UI teardown to the presentation pass (`AddMonsterDialog.Update`); `PauseService` requests always queue while a session exists |
| `hero` diverges a few ticks after a `SwapSlots`/`SellBagItem`/`BuyVaultItem`, RNG equal | A grid-executed handler ran on a stale slot picture and `PersistBagOrdering` wrote it over the bag. Live hides it because opening a window reconnects the grid. Handlers must call `InventoryGrid.SyncFromSimulation()` first (`GetGrid` does) |
| `world` alone diverges by a gold amount, RNG/hero/party equal, only in some sessions | A settings control (radio, checkbox, slider, reorder list) wrote a service directly, so the replay kept the saved value — e.g. Food tab favorite dish changed the dinner price (2026-09-17). Dispatch a command; guard the control's handler during open-time sync. Recover which world input drifted by brute-forcing `HashWorld` (see `ReplaySystem.md` → Diagnose, step 8) |
| Divergence **at tick 0** of a `kind=NewGame` replay, `hero` part only | The start blob did not carry something the new-game path reads, and a constructor defaulted it. The blob is captured at the top of `Begin`, before the hero entity exists **and before every scene-scoped service is registered**, so any `GatherCurrentState` field read off the live hero *or off a scene service* is empty in it. Precedent: `data.JobName` came only from the hero entity, `HeroDesign` defaults an empty job to `"Knight"`, and a Thief's new game replayed as a Knight. When gathering from a scene service, the null fallback must equal the new-game default and a test must pin the two together |
| One-shot animation plays late after a seek | Component froze under `CosmeticUpdatesSuspended` instead of finishing instantly |
| Timer drifts between live and replay | Stored `Time.TotalTime`; store `SimulationClock.Now` |
| Command applied one tick late in replay | Applied between steps with the wrong tick; use `PlayerCommandService.ApplyNow` (records at tick-1) only when the drain cannot run |
| Clicks ignored during playback | Expected: `RejectLiveEnqueues` drops live commands while a replay runs |
| New `MainGameScene` throws duplicate service key | Old scene still registered; scene swaps go through `ReplayBootScene`, services removed in `Unload` |
| Diverges in a battle at a deflect/crit/hit roll, same roll value both sides | A combat passive was set by UI code (window refresh, hover, grid rebuild). Synergies are the precedent: they now live in `HeroSynergyResolver` (sim, per tick) and `InventoryGrid` only mirrors them. Any stat the sim reads must be derived in the sim |
| Divergence report needs more than hashes | `GameConfig.ReplayDivergenceSnapshots`: the report prints the last in-sync state, the drifted state and `ReplayBattleTrace`; compare against the live analytics `.jsonl` by wall time (tick / 60 s after load) |
| A new visual is missing from replays (no divergence) | The renderable is not stock, `IFrameCapturable` or `ILiveOnlyRenderable` (`FrameCaptureCoverageTests` names it), or it draws per rendered frame / from a live service instead of per tick — a per-tick recording only sees what the sim step left behind |
| A new sound is silent in replays | It bypassed `SoundEffectManager` (raw `SoundEffect.Play`), or it is one of the UI click types, which are never recorded |
| Replay frame caches (`.frames`) all vanished after a change | `GameConfig.ReplayFrameFormatVersion` was bumped (any change to op layouts, the HUD record, the events section or the chunk codec): intended, caches are rebuilt by re-simulation and self-cached on exit; `.bin` recordings are never touched |
| A HUD panel or overlay flickers, slides or goes blank only while a replay seeks (fine while watching, fine live) | It reads live entities, which race through the session during a rebuild (or are absent until the seek lands). Feed it from the HUD record under `recordedHud`, park it while `ReplayPlaybackService.State` is Seeking/Starting, or pin what it shows at `ReplayFrameViewer.Freeze`. Precedent 2026-09-27: HUD auto-hide and mercenary portraits |

## File Reference

- `PitHero/Services/Replay/PlayerCommand.cs` — `PlayerCommandType` (payload comments per member), `PlayerCommand`, `SlotRefCodec`
- `PitHero/Services/Replay/PlayerCommandService.cs` — `Dispatch`, `ShouldApplyDirectly`, `ApplyNow`, `Drain`, `RejectLiveEnqueues`
- `PitHero/Services/Replay/PlayerCommandHandlers.cs` + `.Extended.cs` — the handler `switch`
- `PitHero/Services/GameRandom.cs`, `Services/Replay/SeedableRandom.cs` — streams
- `PitHero/Services/SimulationClock.cs` — sim timestamps
- `PitHero/Services/Replay/ReplayTripwire.cs`, `SimulationStateHasher.cs` — divergence detection
- `PitHero/Services/Replay/ReplayPlaybackService.cs` — playback (FrameView / Simulated), seek, rewind, exit, self-caching, divergence log
- `PitHero/Services/Replay/Frames/` — the frame stream: `FrameRecorder` (capture + tile/console/sound hooks), `FrameCaptureAdapters`, `IFrameCapturable`, `ReplayFrameViewer`; `PitHero/Rendering/RecordedFrameRenderer.cs`, `RecordedParticlePool.cs`
- `PitHero/ECS/Scenes/MainGameScene.cs` — seed lifecycle in `Begin`, drain at the end of `Update`, `PresentationUpdate`
- `PitHero/GameConfig.cs` — "Simulation clock" / "Replay playback" constants
