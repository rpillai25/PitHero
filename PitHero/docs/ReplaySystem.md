# Replay System

PitHero records every play session and can replay it exactly: **Settings → Replay** lists saved
recordings, saves the current session, or replays it from the start, and a bottom scrubber lets the
player play, pause, change speed and seek both ways while the camera stays free. This is not a video.
A replay **re-simulates the session** from the same seed with the same player commands, so the game
must be deterministic. That determinism is a project-wide contract: **every feature must be
replay-friendly** (see the "Replay Determinism" rules in `AGENTS.md`). This document explains the
model, the invariants and the recipes for staying inside them.

Code lives in `PitHero/Services/Replay/` plus `Services/GameRandom.cs`, `Services/SimulationClock.cs`,
the UI in `UI/ReplayTab.cs` and `UI/ReplayScrubberPanel.cs`, and the fixed-step loop in the Nez fork
(`Nez/Nez.Portable/Core.cs`, `Utils/FixedStepScheduler.cs`, `Utils/Time.cs`, `ECS/Scene.cs`).

## The model in one line

**fixed 60 Hz simulation tick + seeded RNG streams + recorded player commands = re-simulation.**

Nothing else is recorded. The hero's GOAP plans, battles, loot, kitchen FSMs, merc spawns and every
other decision are pure functions of world state, the RNG streams and the tick count, so they fall
out identically on playback. Hero decisions and periodic state fingerprints are recorded only as a
**divergence tripwire** that tells you when (and in which part of the state) a replay drifted.

## The simulation tick

The Nez fork runs a fixed-step accumulator loop when `Core.UseFixedTimeStep` is on (`Game1`
enables it with `GameConfig.SimulationFixedStepSeconds`, 1/60 s):

| `Core` static | Meaning |
|---|---|
| `UseFixedTimeStep`, `FixedStepSeconds` | Opt-in and step length |
| `SimulationSpeed` | Steps-per-wall-second multiplier. Fast-forward = `GameConfig.SimulationFastForwardSpeed` (2.5). **Never use `Time.TimeScale`** for speed: more steps of the same length keep the trajectory identical; a scaled delta does not |
| `MaxStepsPerFrame` | Catch-up cap after a hitch or occlusion; the backlog is dropped (the sim slows, it never desyncs) |
| `SimulationSuspended` | Zero steps this frame (replay paused / at end) |
| `PendingExtraSteps` + `ExtraStepWallBudgetSeconds` | Seek: run as many extra steps per frame as fit the wall budget |
| `IsInSimulationStep` | True inside a step, false during the presentation pass |
| `CosmeticUpdatesSuspended` | Set during seeks when `GameConfig.ReplaySeekSkipsCosmetics` is on |

Each frame: `Input.Update()` once, then N simulation steps (`Time.SimulationStepUpdate` sets
`Time.DeltaTime` to the fixed step; `Scene.Update()` runs entities, managers and
`MainGameScene.Update`), then exactly one **presentation pass** (`Time.PresentationUpdate` with the
wall delta; `Scene.PresentationUpdate()` updates every registered `UICanvas` stage, the camera, HUD,
overlays and hover/click handling). A paused replay still gets its presentation pass, which is why the
scrubber stays responsive on zero-step frames.

**Consequences for code:**
- `Time.DeltaTime` inside a step is always the fixed step. Accumulators and coroutine waits stay
  deterministic without changes.
- `Time.TotalTime`, `Time.FrameCount` and `Time.TimeSinceSceneLoad` are **wall-clock / per rendered
  frame**. They are fine for UI pulses and the time-played counter and **wrong for anything the
  simulation reads**. Sim timestamps use `SimulationClock.Now` / `SimulationClock.CurrentTick`
  (kitchen ticket age, merc spawn time, hero jump timing already do).
- `Input` must only be read in the presentation pass (`CameraControllerComponent.PresentationUpdate`,
  `MainGameScene.PresentationUpdate`, UI stages). A step that reads input reads it N times per frame
  and differently on playback.

`MainGameScene.Update` is the whole simulation tick. Its **last lines are the contract**:

```csharp
long tick = _simulationClock.Tick;
if (playback is active) playback.InjectDue(tick, _playerCommands);   // recorded commands for this tick
_playerCommands.Drain(tick);                                          // apply + record player commands
if (tick % GameConfig.ReplayHashIntervalTicks == 0)                    // 1 s tripwire fingerprint
    ReplayTripwire.ReportStateHash(SimulationStateHasher.Sample(tick));
_simulationClock.Advance();
```

## RNG streams (`Services/GameRandom.cs`)

| Stream | Backing | Who may use it |
|---|---|---|
| `GameRandom.Sim` | Installed as `Nez.Random.RNG` for the session | Everything the simulation rolls: battle, pit generation, loot tables, mercenaries, kitchen, farm, names. Existing `Nez.Random.*` call sites need no change |
| `GameRandom.Loot` | Separate `SeedableRandom` from the same master seed | The mid-battle epic-chest draw only (`LootShuffleService.SetEpicRng`), so it never shifts the battle stream |
| `GameRandom.Audio` | Wall-clock seeded | Sound variant picks (`GroupSoundEffect`) |
| `GameRandom.Ui` | Wall-clock seeded | Anything rolled at UI time: hero-creation randomize, crystal-creation stat rolls. The **result** travels in the command payload (`CreateCrystal` carries B/C/D/L) |
| Nez `ParticleRandom` | Private to the particle system | Particle emitters (they used to roll `Nez.Random` and desynced every seek) |
| `SpeechBubbleDialogue` private `System.Random` | Reseeded from `masterSeed ^ ReplaySpeechSeedSalt` | Cosmetic dialogue picks; reproducible but off the sim stream |

`SeedableRandom` is xoshiro128** with `GetState`/`SetState`, so the Sim stream's state is part of the
tripwire hash: **one extra or missing roll anywhere in the sim shows up as an `rng` divergence at the
next one-second sample.** That is by far the most common way a new feature breaks replays, and the
`replay_divergence.log` part hashes will name it.

The virtual game layer's "two sanctioned RNG streams" rule (`VirtualGameLogicLayer.md`) is the same
rule seen from the tests: seeded `Nez.Random` for the sim, nothing else.

## Session start and the seed lifecycle

Everything happens at the top of `MainGameScene.Begin`, before any world content exists:

1. `ReplaySessionBootstrap.Consume()` — a pending bootstrap (set by playback) supplies the master
   seed and the recording; otherwise `GameRandom.GenerateMasterSeed()` makes a fresh one.
2. `GameRandom.InitializeSession(masterSeed)` installs the Sim stream.
3. **Static state the sim reads is reset**: `HairstyleQueueService.ResetAndRefill()`,
   `SpeechBubbleDialogue.Reseed(...)` (which calls `ShuffleBag.Reset()` on every registered option bag),
   `LootShuffleService.SetEpicRng(GameRandom.Loot)`. Playback also drains the lazily created global
   `PitLevelQueueService` before restarting. **Any new static or global-service state that influences
   the simulation must be reset here too**, or the second run of a replay starts from different hidden
   inputs than the first.
4. `SimulationClock`, `PlayerCommandService` and `ReplayRecorder` are created (scene-scoped; each has a
   static `Current` that is null outside a game session and is detached in `Unload`).
5. The recorder captures the **start blob**: for a loaded game the exact `SaveData` bytes being
   loaded; for a new game a snapshot of the global services that New Game does not reset (funds,
   carry level, stencils, vaults, defeated monsters, hero design). Playback restores that blob before
   the scene restarts (`RestoreGlobalServicesFromSave`).

Tick 0 is the first `MainGameScene.Update` after `Begin`.

## Player commands (`Services/Replay/PlayerCommand*.cs`)

`PlayerCommandService` is the **single doorway from the player into the simulation**. UI code never
mutates sim state directly; it dispatches a `PlayerCommand` (`Type` + `A,B,C,D` ints, `L` long, `F`
float, `S` string) and the service applies it in `Drain` at the end of the tick, raising
`OnCommandApplied` so the recorder logs `(tick, command)`.

- `PlayerCommandService.Dispatch(cmd)` — the one call UI code makes. Queues during a session; applies
  immediately when no service exists (title/creation scenes, headless tests).
- `PlayerCommandService.ShouldApplyDirectly` — true when there is no service or we are already inside a
  handler. Sites that share a method between the UI path and the handler path use it:

  ```csharp
  if (PlayerCommandService.ShouldApplyDirectly)
      SetShortcutReference(index, source);                       // handler / headless path
  else
      PlayerCommandService.Dispatch(new PlayerCommand(PlayerCommandType.SetShortcutItem, index, bagIndex));
  ```
- `ApplyNow(cmd)` — applies between steps and records at `CurrentTick - 1`. Only for moments when the
  normal drain will never run (releasing the settings pause right before playback restarts the scene).
- `RejectLiveEnqueues` — set during playback; live clicks are dropped with a `Debug.Warn` and the
  playback service injects the recorded commands for the tick instead.
- `PlayerCommandHandlers.Apply` (+ `.Extended.cs`) is one `switch` over `PlayerCommandType` into static
  handlers. **Handlers re-validate** (funds, slot emptiness, entity alive, index in range) and no-op on
  failure, so live and replay take the same branch even if UI state differed.
- `PlayerCommandType` values are **persisted in replay files: append only, never renumber or reuse**.
  The payload meaning per type is documented inline on the enum. `SlotRefCodec` packs an inventory
  cell (slot type, x, y) into one int.
- The pause is a command too (`PauseService` routes `SetManualPause` / `SetFarmModePause`). Ticks
  keep counting while paused, so a recorded pause replays faithfully; `ReplayPauseSpans` lets playback
  skip paused stretches of at least `ReplayPauseSkipMinTicks`.

**Not recorded (view-only):** camera, window size/dock, fast-forward, hover/tooltips, window
open/close (except the pause commands they trigger), event-console scrolling. If a feature adds
something the player does that changes the world and it is not one of these, it is a command.

## Recording and the file format

`ReplayRecorder` is always on. `ReplayData` (own format version, independent of `SaveData`) holds:

| Field | Content |
|---|---|
| `Kind` | `NewGame` or `Load` |
| `MasterSeed`, `StateBlob` | Enough to rebuild tick 0 exactly |
| `HeroName`, `JobName`, `PitLevelAtStart`, `RecordedAtUtcTicks`, `TotalTicks`, `BuildId` | List preview + a build-mismatch warning (never a block) |
| `Commands` | `(tick, PlayerCommand)` in order |
| `Decisions` | `(tick, hash)` of every successful hero GOAP plan (`ReplayTripwire.HashPlan`, reported from `HeroStateMachine`) |
| `StateHashes` | One `ReplayHashSample` per `ReplayHashIntervalTicks` with the combined hash and its `Rng` / `Hero` / `Party` / `World` parts |

`ReplayIO` serializes through Nez `IPersistable` (longs as two ints, blobs as base64).
`ReplayFileService` stores files as `replay_<hero>_<yyyyMMdd_HHmmss>.bin` under the persistent data
folder's `replays/` directory and enumerates them header-only. A few hours of play is a few hundred
KB. Bumping `ReplayData.CurrentVersion` is allowed to break old recordings (they are not user saves).

## Playback (`ReplayPlaybackService`, global)

`Start(data, isCurrentSession)` sets aside the live recording (`_returnSession`), restores the start
blob, sets a `ReplaySessionBootstrap` and swaps to `ReplayBootScene`, a trampoline whose `Begin`
constructs `MainGameScene.CreateForGameplay`. The old scene must fully unload first because services
are keyed by type (constructing a second `MainGameScene` while the first is registered throws).

- **Playing / Paused / AtEnd** drive `SimulationSpeed` / `SimulationSuspended` from the presentation
  side. Speed cycles `GameConfig.ReplaySpeedSteps`.
- **Seek forward** = `Seeking` with `PendingExtraSteps`. **Seek backward** = restart the scene from
  tick 0 and fast-forward. There are no keyframes: a `SaveData` snapshot is not faithful mid-pit, so
  re-simulation is the only exact path. Measured throughput is roughly 250x real time (an hour of play
  seeks in about 15 s); the Replay Info window carries the multi-day-session disclaimer instead of any
  session-length limit.
- During seeks: SFX muted, `Debug.QuietMode`, `CosmeticUpdatesSuspended`, camera view captured and
  restored (`CameraControllerComponent.CaptureView/RestoreView`), hero-follow never engages.
- `GameEventService.Suppressed` and analytics are off during playback; the recruit-notification queue
  is cleared on exit.
- **Exit** re-simulates the set-aside live recording to its end and returns to the exact pre-replay
  live state. **Continue Here** (confirmed) truncates the recording at the current tick
  (`ReplayRecorder.TruncateAfter`) and branches live play from there.
- `CheckDecision` / `CheckStateHash` set `DivergenceTick` on the first mismatch; the scrubber shows
  "Diverged at m:ss (state|decision)" and a diagnostic block is appended to
  `replay_divergence.log` next to the replay files, naming which part hash (`rng`, `hero`, `party`,
  `world`) drifted first. Playback continues: a diverged replay is still a valid game.

## Invariants (and why)

1. **Sim code never reads `Input`, `Time.TotalTime`, `Time.FrameCount`, `DateTime`, `Stopwatch` or
   `Environment.TickCount`.** They differ between live and replay. Use `SimulationClock.Now` for sim
   timestamps and keep input handling in the presentation pass.
2. **Every player-driven mutation of sim state is a `PlayerCommand`.** Otherwise playback lacks it
   and the world drifts the moment it should have happened.
3. **Nothing outside the simulation touches `Nez.Random`.** UI, audio, particles and other visuals use
   `GameRandom.Ui`, `GameRandom.Audio`, `ParticleRandom` or a private reseeded `System.Random`. One
   stray roll from a hover or a sound desyncs the whole Sim stream.
4. **Sim randomness comes only from `Nez.Random` (Sim) or `GameRandom.Loot`.** No new `System.Random`,
   no `Guid`, no hash-code ordering.
5. **Static or global state the sim reads is reset at the session reseed** (`MainGameScene.Begin`) or
   scene-scoped. Scene restarts for seeks happen inside one process, so leftovers survive.
6. **Iteration order is part of the state.** `Dictionary`/`HashSet` enumerate in insertion order for
   identical operation sequences; do not sort by anything unstable (float ties, reference hashes).
7. **Presentation never feeds back into the sim** except through commands. Whether a window is open,
   hovered or off-screen must not change what a handler does.
8. **Cosmetic-only components may honor `Core.CosmeticUpdatesSuspended`; anything the sim waits on
   may not.** One-shot effects finish instantly when suspended (they must not freeze and replay later).
   Sprite animators are NOT cosmetic: `EnemyAnimationComponent` waits on `AnimationState`.
9. **Fast-forward is more steps, never a scaled delta.** `Time.TimeScale` stays 1.
10. **`PlayerCommandType` is append-only.** Recorded files store the numeric value.

## Recipes

### Add a new player action

1. Append a `PlayerCommandType` member with a payload comment (`// A = ..., S = ...`).
2. Add a `case` in `PlayerCommandHandlers.Apply` (or the `.Extended.cs` partial) that resolves targets
   by stable identity (index, tile, id, name), re-validates and no-ops on failure.
3. In the UI, replace the direct mutation with `PlayerCommandService.Dispatch(...)`. If the same method
   serves both paths, branch on `ShouldApplyDirectly` as above.
4. Never dispatch a command from inside a handler or from sim code; sim-driven changes just happen.
5. Verify: play the action, save the session replay, replay it and confirm the scrubber says
   **In sync** past that point; seek back over it.

### Add randomness

- Sim decision: `Nez.Random.*` (Sim stream) as usual. Mid-battle loot that must not shift the battle
  roll count: `GameRandom.Loot`.
- Anything visual, audible or UI-driven: `GameRandom.UiRange` / `GameRandom.AudioRange` /
  `ParticleRandom`. If a UI roll produces a gameplay result (a stat roll in a dialog), roll on `Ui`
  and put the result into the command payload.
- Drawing from a `ShuffleBag` that the sim consumes: fine (bags are RNG-neutral via `NextFromRoll`),
  but a static bag must register for reset at the reseed like `SpeechBubbleDialogue.OptionBag` does.

### Add a timer, cooldown or timestamp

- Per-step accumulators on `Time.DeltaTime` are deterministic. Coroutine `WaitForSeconds` is fine.
- A stored "when did this happen" timestamp uses `SimulationClock.Now` (or `CurrentTick`), never
  `Time.TotalTime`.

### Add a cosmetic component

- If nothing in the simulation reads its output (floating text, pickup arc, Y-sort, turn indicator):
  early-return or finish instantly when `Core.CosmeticUpdatesSuspended` is true, following
  `BouncyTextComponent` / `ItemPickupAnimationComponent`.
- If any sim code waits on it (animation state, "effect finished" callbacks that gate an action): do
  not skip it.

### Add a new scene-level service

- Register it in `MainGameScene.Initialize`/`Begin` and remove it in `Unload` (services are keyed by
  type; a leftover registration breaks the replay scene swap).
- If it is global (`Game1`) and holds sim-relevant state, reset that state at the reseed step or on
  playback restart.

### Diagnose "Diverged at"

1. Open `%LOCALAPPDATA%\FeedTheHero\replays\replay_divergence.log`: the block names the first tick and
   which of `rng` / `hero` / `party` / `world` differed.
2. `rng` alone with everything else equal = an extra or missing roll (invariant 3 or 4). Flip
   `GameConfig.ReplaySeekSkipsCosmetics` to A/B a cosmetic suspect.
3. `hero`/`party`/`world` without `rng` = a timestamp, input read, missing command or stale static
   state (invariants 1, 2, 5, 7).
4. A `decision` divergence with matching state hashes usually means the plan hash inputs changed
   (renamed action) rather than a real drift.

## Configuration (`GameConfig.cs`, "Simulation clock" and "Replay playback" blocks)

`SimulationFixedStepSeconds`, `SimulationMaxStepsPerFrame`, `SimulationFastForwardSpeed`,
`ReplayMaxStepsPerFrame`, `ReplaySeekWallBudgetSeconds`, `ReplayHashIntervalTicks`,
`ReplayPauseSkipMinTicks`, `ReplaySeekSkipsCosmetics`, `ReplaySpeedSteps`, scrubber size constants,
`ReplayDirectoryName` / `ReplayFilePrefix` / `ReplayFileExtension`, `ReplaySpeechSeedSalt`.

## Tests

`FixedStepSchedulerTests`, `SeedableRandomTests`, `VirtualSimSeedableRandomTests`,
`PlayerCommandServiceTests`, `ReplayDataTests`, `ReplayRecorderTruncateTests`,
`ReplayPauseSpansTests`, `ReplayTimeFormatterTests`, `ShuffleBagResetTests`. The existing same-seed
determinism suites (`BattleEngineTests`, `VirtualBalanceTraversalTests`) guard the RNG call-order
contract. There is no headless end-to-end replay test; the live check is: record a session that
exercises the feature, replay it, scrub back and forth, and read the status label.
