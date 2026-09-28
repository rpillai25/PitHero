# Replay System

PitHero records every play session and can replay it exactly: **Settings → Replay** lists saved
recordings, saves the current session, or replays it from the start, and a bottom scrubber lets the
player play, pause, rewind, change speed and seek both ways while the camera stays free. Two things
are recorded, for two different jobs:

1. **The recording** (`ReplayRecorder`, `replay_*.bin`): the seed, the player commands and the
   tripwire hashes. It is the source of truth and the only thing the simulation ever needs. A replay
   **re-simulates the session** from it whenever the world itself must exist again (Time Travel Here,
   or a saved replay without a frame cache), so the game must be deterministic. That determinism is a
   project-wide contract: **every feature must be replay-friendly** (see the "Replay Determinism" rules
   in `AGENTS.md`).
2. **The frame stream** (`FrameRecorder`, `*.frames`): what was on screen at every tick — sprites,
   text, HUD numbers, tile changes, console lines, sounds, particle emitters. It is a derived cache of
   the simulation with one producer (the end of every tick). *Watching* a replay draws these frames:
   no simulation runs, seeking either way is instant, and nothing can diverge. If the cache is
   missing or stale the replay falls back to re-simulation and rebuilds it as it plays.

This document explains the model, the invariants and the recipes for staying inside them.

Code lives in `PitHero/Services/Replay/` (the frame stream under `Frames/`, its renderers under
`PitHero/Rendering/`), plus `Services/GameRandom.cs`, `Services/SimulationClock.cs`, the UI in
`UI/ReplayTab.cs` and `UI/ReplayScrubberPanel.cs`, and the fixed-step loop in the Nez fork
(`Nez/Nez.Portable/Core.cs`, `Utils/FixedStepScheduler.cs`, `Utils/Time.cs`, `ECS/Scene.cs`; plus
`ECS/InternalUtils/ComponentList.cs` + `Utils/Collections/FastList.StableSort` for history-independent
update order, `Debug/QuietLogHandler.cs` for logs that cost nothing during seeks,
`ParticleRandom` / `ParticleEmitter` for particles off the sim stream, `Scene.RenderableFilter` for the
frame viewer, and `RenderableComponent.CaptureSlot` + `SpriteAtlasLoader` texture names for capture).

## The model in one line

**fixed 60 Hz simulation tick + seeded RNG streams + recorded player commands = re-simulation.**

Nothing else is needed to rebuild the world. The hero's GOAP plans, battles, loot, kitchen FSMs, merc
spawns and every other decision are pure functions of world state, the RNG streams and the tick count,
so they fall out identically on playback. Hero decisions and periodic state fingerprints are recorded
only as a **divergence tripwire** that tells you when (and in which part of the state) a replay drifted.
The frame stream sits beside this as a picture of the past; it never feeds the simulation.

## The simulation tick

The Nez fork runs a fixed-step accumulator loop when `Core.UseFixedTimeStep` is on (`Game1`
enables it with `GameConfig.SimulationFixedStepSeconds`, 1/60 s):

| `Core` static | Meaning |
|---|---|
| `UseFixedTimeStep`, `FixedStepSeconds` | Opt-in and step length |
| `SimulationSpeed` | Steps-per-wall-second multiplier, picked from the shared `GameConfig.SpeedSteps` ladder (`1 / 2.5 / 4 / 8`, shown to the player as `1X / 2X / 4X / 8X` via `GameConfig.SpeedStepLabels`) by both the live fast-forward button and the replay scrubber. **Never use `Time.TimeScale`** for speed: more steps of the same length keep the trajectory identical; a scaled delta does not |
| `MaxStepsPerFrame` | Catch-up cap after a hitch or occlusion; the backlog is dropped (the sim slows, it never desyncs). Raised from `GameConfig.SimulationMaxStepsPerFrame` to `GameConfig.HighSpeedMaxStepsPerFrame` whenever the sim runs above 1x, so the top rung is not silently clamped |
| `SimulationSuspended` | Zero steps this frame (replay paused / at end) |
| `PendingExtraSteps` + `ExtraStepWallBudgetSeconds` + `ExtraStepDutyCycle` | Seek: one burst of extra steps per frame, as many as fit the wall budget, then (duty cycle below 1) a `Thread.Sleep` proportional to the burst so a long seek holds that share of one core instead of pegging it |
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

## Simulation version

Every recording is stamped with `GameConfig.SimulationVersion` (file format v4; older files read as 0).
The stamp is a hand-bumped integer, not the assembly version: a UI or art build must not orphan
recordings, and in development every local build would. **Bump it whenever a change alters what the
simulation does from the same seed and commands** — balance numbers, GOAP actions, RNG calls added or
removed, command handlers, the load path. A recording whose stamp differs still plays: the Replay tab
row and the scrubber say "Older game version: may differ from the original"
(`ReplayPlaybackService.IsOlderSimulation`). Time Travel Here stays available — the world on screen is
a valid state this build computed, so continuing from it cannot corrupt anything — but its confirmation
adds a warning that names whether the replay has matched the original so far or already diverged,
because the player is committing to the world they watched, not the game as they played it. Replay
Current Session is always current by construction. The tripwire hashes remain the safety net for a
change nobody stamped.

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

Recordings are written by the Replay tab's "Save Session Replay" button (a new dated file each time)
and **automatically on Quit to Title / Exit Game** (`SettingsUI.SaveSessionBeforeLeaving`, issue #411:
synchronous autosave first, then the pause release and the replay snapshot; nothing is written while a
replay is playing back). The quit-time recording has **one static name per hero**,
`replay_auto_<HeroId as 8 hex digits>.bin` (`ReplayFileService.SaveAuto`, the autosave's naming), so
each session overwrites the previous one together with its `.frames` cache (issue #429: the caches are
tens of MB per hour, and a dated file per quit would eat the disk); the Replay tab marks it "Auto".
Manual saves are the player's to keep. The Replay tab lists only the `GameConfig.ReplayListMaxShown`
(10) newest recordings **after** the current-hero filter, with a "Showing the N most recent of M" note;
deleting one re-enumerates the folder, so the next most recent slides in.

## The frame stream (`Services/Replay/Frames`, issue #424)

Design history and measurements: `features/feature_replay_frame_recording_424.md`. The layers:

| Layer | What | Where |
|---|---|---|
| L0 recording | seed + commands + tripwire hashes (above) | `ReplayRecorder`, `replay_*.bin` |
| L1 frame stream | one presentation frame per tick, chunked and deflated | `FrameRecorder`, `FrameChunkBuilder/Codec`, `FrameStore`, `*.frames` |
| L2 viewer | draws any recorded tick over the untouched live scene | `ReplayFrameViewer`, `RecordedFrameRenderer`, `RecordedFrameScreenRenderer`, `RecordedParticlePool` |
| L3 resume | re-simulates to a tick when the world must exist again | `ReplayPlaybackService` in `Simulated` mode |

### Capture (L1)

`FrameRecorder.Current` is scene-scoped like `ReplayRecorder` (created in `MainGameScene.Begin`,
detached in `Unload`). `CaptureTick` runs at the tail of `MainGameScene.Update`, right before the clock
advances (Y-sort depths are final there), and walks `Scene.RenderableComponents` once. Each drawn
renderable becomes a byte string of **ops** under a stable `ushort` id (carried on the component in the
fork field `RenderableComponent.CaptureSlot`, so a re-sorted list costs no lookup):

| Op | From | Notes |
|---|---|---|
| `Sprite` | stock `SpriteRenderer` / every animator subclass | i16 pixel position, depth, layer, color, flags (flip, screen-space, `Graded` = drawn through the day-night material) |
| `Composite` | `MultiSpriteAnimator`, `StaticSpriteCompositor` | one record per layer with a sprite (never the render texture) |
| `Text`, `Rect`, `NinePatch` | `IFrameCapturable` components (floating text, HP bars, outlines, speech bubbles) | anchor + `dx/dy` + `ConstantScreenSize` for constant on-screen size at any zoom; no layer (drawn after every sprite) |
| `Particle` | `ParticleEmitter`s spawned by `ParticleEffectManager` | effect key, density, root, age in ticks, Emitting flag; the particles themselves are re-simulated by the viewer |

Everything else: `ILiveOnlyRenderable` (clouds, tree bands, `GraphicalHUD`) keeps drawing live;
`TiledMapRenderer` and `UICanvas` are skipped silently; any other type is skipped with a one-time
warning and **fails `FrameCaptureCoverageTests` by name**. That is the rule for feature authors: *a new
`RenderableComponent` is stock, `IFrameCapturable` or `ILiveOnlyRenderable`*. Anything per-rendered-frame
or read from a live service is wrong in a per-tick recording (the bouncy digits' spacing and bounce
curve, the day/night clock and the clouds were all found this way).

Beside the ops, every tick carries a **HUD record** (party hp/mp/level, gold, pit level/tier, in-game
seconds floored to whole seconds so it does not churn, pause flag, and the hero's static portrait: the
walk-down first frame of the head, eyes and hair layers as sprite ids with their tints, exactly what
the live HUD draws). **Events** arrive through hooks and are stored with their tick: tile mutations
(`TiledMapService.TileChanging`, same-gid writes and the undrawn Collision/Top layers dropped, plus a
full keyframe of Base/Detail/FogOfWar at every chunk start), console lines
(`GameEventService.OnEmitAny`, segments interned) and **sounds** (`SoundEffectManager.OnSoundPlayed`:
type, the variant a group sound rolled, world position, positional flag; UI click sounds are never
recorded, nothing is recorded while `Muted`). That is the second rule for feature authors: *a new
simulation sound goes through `SoundEffectManager`* or the frame stream never hears it.

Sprites are identified by `(Texture2D.Name, sourceRect)` in a `SpriteKeyRegistry` (strings and
nine-patch names likewise); each chunk carries the table entries first seen in it, so a reader rebuilds
the tables from the uncompressed chunk prefixes without inflating anything.

**Chunks** (`ReplayFrameChunkTicks` = 120 ticks, Braid's 2 s GOP): a base frame, then entity-level
deltas against the previous tick (an entity whose bytes changed is re-emitted whole; a tombstone
marks one gone), the HUD record only when it changed, then the events section (tile, console, sound)
and the tile keyframe, deflated as one unit on the sidecar worker. Chunks are contiguous (chunk *i*
starts at tick *i* × 120); only the last may be partial. Measured cost: ~36 MB per hour on disk,
40–70 µs per tick in Release (`replays/frame_recorder.log`).

**Lifecycle rules** (mirroring the command recorder): the capture rule is `IsRecording || tick >
Store.EndTick`, so a playback skips ticks that already have a frame and captures past the stream end;
`TruncateAfter` (Time Travel) cuts the stream, the file and the tables and continues the cut chunk;
`MainGameScene.Unload` hands the stream to the next scene when playback is `Starting` (a rebuild of the
same session adopts it; another session's stream is closed with its footer). Entering FrameView calls
`FlushPending` so the ticks still in the builder become frames.

### Files: the `.frames` sidecar

```
header    "PHFR", formatVersion i32, masterSeed i32, recordedAtUtcTicks i64, simulationVersion i32, chunkTicks i32
chunks    [len u32][chunk bytes]…      (a chunk = uncompressed prefix + table delta + deflated payload)
footer    "PHFX", totalTicks i64, count i32, {offset i64, firstTick i64, tickCount u16}…
          optional "PHFC", lineCount i32, {tick i64, segCount u8, {text, color u32, itemName}…}…   (console lines, plain text)
trailer   footerOffset i64, "PHFE"
```

- **During a session** the recorder appends to `replays/session_<recordedAtUtcTicks>.frames` from a
  worker thread (`FrameSessionSidecar`; the main thread never waits) and the file doubles as the reload
  source when the RAM ring (`ReplayFrameMemoryBudgetBytes`, 192 MB) evicts a chunk. A footer-less file
  (crash) is reindexed by scanning; stale `session_*` files are deleted when a genuinely new session
  starts.
- **Saving a replay** writes the cache beside the `.bin` with the same name (`ReplayFileService.
  SaveWithFrameCache`). Save Session Replay mid-session **copies** the session file (raw byte copy of
  header + chunk records, fresh footer; the session goes on in its file). The quit-time save
  (`SettingsUI.SaveSessionBeforeLeaving`) **finishes and moves** it (`FinishAndMove`; capture then
  continues in memory only). The console lines live in the footer in plain text rather than being
  rebuilt from the chunks' console events, which would inflate every chunk at open.
- **The quit-time recording is one static file per hero**, `replay_auto_<HeroId as 8 hex digits>.bin`
  + `.frames`, overwritten every session (a dated file per quit at tens of MB an hour would eat the
  disk); the Replay tab marks it "Auto". Manual saves are dated and unlimited.
- **Validity** (`TryOpenFrameCache`): identity header (seed, recording time, simulation version,
  `ReplayFrameFormatVersion`) + footer + `footer.TotalTicks == bin.TotalTicks` + frames up to the last
  tick. Anything else re-simulates. `Enumerate` opens every cache (header + footer only) for the
  "Cached" mark. Bumping `ReplayFrameFormatVersion` (any op layout, HUD record or events change)
  orphans every cache once; they come back through self-caching. **A `.bin` is never deleted by any
  cache policy.**
- **Disk budget** (`ReplayFrameCacheDiskBudgetBytes`, 4 GB): after every cache write the oldest
  `replay_*.frames` by last write time are deleted until the caches fit; `session_*.frames` and
  recordings are never touched.
- **Self-caching (issue #431).** A saved replay without a valid cache plays by re-simulation. The
  rebuilt scene's `FrameRecorder` runs under the replay's identity (`session_<recordedAt>.frames`) and
  captures every tick it simulates — seeks included, recorded ticks skipped, an earlier unfinished
  pass reopened and continued. On Exit (before the return rebuild) `ReplayPlaybackService.
  TrySelfCacheSimulatedReplay` flushes the builder and, if the stream reaches `TotalTicks - 1`,
  finishes the file with the console log and moves it beside the recording, then enforces the budget:
  the row gains "Cached" and the next play is instant FrameView. A replay left before its end stays
  uncached. Stretches that were seeked have no floating text or sounds in the cache (cosmetics skip and
  audio is muted during seeks); accepted.

### Viewer (L2)

`ReplayFrameViewer.AttachToScene` installs the Nez fork's `Scene.RenderableFilter` (only the `UICanvas`
and the `GraphicalHUD`s keep drawing through the stock renderers) and two renderers:

- `RecordedFrameRenderer` (world pass, right after the DefaultRenderer so it covers that renderer's
  world-camera ghost of the HUD panels): the shadow tile layers (`ShadowTileLayers`, Base/Detail/FogOfWar
  rebuilt from the chunk's keyframe + events), the live Top layer, the live-only world renderables
  (tree bands, clouds) and the frame's Sprite / Composite / Particle ops, merged in `RenderableComparer`
  order (layer desc, depth desc, material), then the overlay ops (Text/Rect) after every sprite.
  `Graded` ops and the terrain switch to the day-night material; a particle effect switches to its
  blend material.
- `RecordedFrameScreenRenderer` (after post-processing, before the live UI): screen-space sprites (the
  action queue icons) and speech bubbles (nine-patch body, tail, typewriter slice) anchored through the
  world camera.

`FrameSpriteResolver` maps ids back to atlas sprites (with their origins) or rebuilds a sprite from a
texture by name; scene-content textures that died with a rebuild are re-resolved. The HUD, pit level,
gold and clock labels are fed from the HUD record (`MainGameScene.ApplyRecordedHud`), the **portrait**
from the record's static head/eyes/hair sprites (`ApplyRecordedPortrait` →
`GraphicalHUD.SetRecordedPortrait`), day/night grading and the clouds from the recorded clock, the event
console from the console log (`EventConsolePanel.ShowRecorded` on a jump, appends while playing).
The mercenary portraits are not recorded and keep reading the live mercenary entities; a Time Travel
rebuild has none to read until the seek lands, so `Freeze` pins what the two panels were drawing
(`MainGameScene.CaptureMercenaryPortraits` / `ApplyMercenaryPortraits`) for the rebuild. While a recorded
frame feeds the HUD the party auto-hide keeps the panels up (`UpdateHudAutoHide(recordedHud)`): the live
party is not what is on screen, and during a rebuild it races through the session and made the panels
slide up and down for minutes.

**Particles** (`RecordedParticlePool`): each recorded emitter is re-simulated with Nez's own `Particle`
code at the fixed step from a `System.Random` seeded by (effect key, start tick); forward play is one
step per tick, any other move rebuilds from the start, so a seek or a rewind shows the same picture.
Approximate by design: a rebuilt effect spawns at the current root, and the Emitting flag only ends
emission from the current tick on.

**Sounds:** the viewer plays the sound events the playhead passes during forward play at 1X–2X
(`ReplayFrameViewSoundMaxSpeedIndex`) through `SoundEffectManager.PlayRecorded` — the exact variant,
with the live camera's falloff and pan for positional ones; `Muted` is honored and nothing is recorded
again. A move longer than `ReplayFrameViewSoundCatchupMaxTicks` (a seek, a skipped pause span), a
rewind, a pause or a higher rung is silent.

**Timeline:** `ReplayFrameCursor` advances by wall time × speed × 60 with fractional carry, in either
direction, clamped to `[0, TotalTicks]`, skipping recorded pause spans forward. Speeds come from
`ReplayFrameViewSpeedSteps` (the simulation ladder plus view-only 16X / 32X; no artifact gate).
**Rewind** is the cursor with `Direction = -1`: the `<<` button (pressing it again pauses) or
SHIFT + left arrow held (polled by the scrubber; releasing either key restores the state before the
hold; the camera ignores the arrow keys while SHIFT is down, so plain arrows still pan). Rewind stops
paused at tick 0. There is no simulated future: the timeline ends at the session
end (issue #438).

## Playback (`ReplayPlaybackService`, global)

The service has two **modes** (`ReplayPlaybackMode`):

- **FrameView** — *Replay Current Session* when the session's frame stream is complete, and any saved
  replay with a valid `.frames` cache (issue #429). The live `MainGameScene` is never torn down: its
  simulation is suspended (`Core.SimulationSuspended`), `PlayerCommandService.RejectLiveEnqueues` is
  set, the UI enters replay mode, and the viewer above draws the recorded tick at the playhead;
  `CurrentTick` is the cursor. Seeking either way is a cursor move: the scrubber never says "Seeking"
  inside the recording. **Exit** removes the viewer and un-suspends: instant, the world is exactly as it
  was (the live world never runs while watching). **Time Travel Here** freezes the current frame
  (`ReplayFrameViewer.Freeze` keeps a private copy that survives the stream truncation), switches to
  Simulated, rebuilds the world to the cursor with the frozen frame drawn over both the trampoline
  scene and the rebuilding scene while the scrubber shows the seek progress and a waving
  "Time Travelling..." banner (`ReplayTimeTravelBanner`, on `ReplayPlaybackService.IsTimeTravelling`)
  sits mid-screen so a minutes-long rebuild never reads as a hang, then commits
  (`CommitHere`: both recorders truncated). A cursor already at the live tick of the current session
  commits without a rebuild; a saved replay's live world underneath is another timeline, so it always
  rebuilds. A rebuild that diverged is reported once on the console. Kill switches:
  `ReplayFrameCaptureEnabled` and `ReplayFrameViewEnabled`; either off, or a gap in the stream, falls
  back to Simulated.
- **Simulated** — everything below: saved replays without a cache (which self-cache on exit), and
  every resume path.

`Start(data, isCurrentSession, startAtTick, fileName)` sets aside the live recording (`_returnSession`),
restores the start blob, sets a `ReplaySessionBootstrap` and swaps to `ReplayBootScene`, a trampoline
whose `Begin` constructs `MainGameScene.CreateForGameplay`. The old scene must fully unload first because
services are keyed by type (constructing a second `MainGameScene` while the first is registered throws).

- **Playing / Paused / AtEnd** drive `SimulationSpeed` / `SimulationSuspended` from the presentation
  side. Speed cycles `GameConfig.SpeedSteps` (a FrameView index above that ladder is clamped when the
  mode switches).
- **Seek forward** = `Seeking` with `PendingExtraSteps`. **Seek backward** = restart the scene from
  tick 0 and fast-forward. There are no simulation keyframes: a `SaveData` snapshot is not faithful
  mid-pit, so re-simulation is the only exact path. Unthrottled throughput is roughly 400x real time
  (~24k steps/s, an hour of play in about 9 s); the Replay Info window carries the disclaimer for
  Time Travel and uncached replays instead of any session-length limit.
- **Seeks are throttled** (issue #432, 2026-09-27). Every re-simulation runs through the `Seeking`
  state: Time Travel Here, Exit from an uncached saved replay (`ReturnToLiveSession`) and scrubs in
  Simulated mode. Unthrottled, that loop pegged one core for the whole rebuild (the 30 ms seek burst
  outlasted a 60 Hz frame, and the game runs vsync-paced with no sleep of its own), which a laptop
  answers with its fan. Now each rendered frame runs one burst of `ReplaySeekWallBudgetSeconds`
  (10 ms) and then the main thread sleeps so the burst is `ReplaySeekDutyCycle` (0.5) of the frame
  (`Core.ExtraStepDutyCycle`, `FixedStepScheduler.ComputeRestSeconds`): one core at about half load,
  a rebuild about twice as long, the frozen frame and progress bar updating at ~45 fps. The last
  burst of a seek never rests. Raise the duty cycle for faster seeks; 1 disables the rest. **Measured
  2026-09-27** on an 8-hour session with these values: Time Travel to the 8-hour mark took about
  5 min in Release (~37 s per hour of session, ~5.8k steps/s effective, so a Release step is now
  ~70 µs rather than the 42 µs of the September 7 profile) and 8–10 min in Debug, with the CPU quiet
  in both. Whether that wait is acceptable, or simulation checkpoints (#432, design §8) are wanted,
  is the owner's call.
- During seeks: SFX muted, `Debug.QuietMode`, `CosmeticUpdatesSuspended`, camera view captured and
  restored (`CameraControllerComponent.CaptureView/RestoreView`), hero-follow never engages.
- `GameEventService.Suppressed` and analytics are off during playback; the recruit-notification queue
  is cleared on exit.
- **Exit** self-caches the replay if it was watched to its end, then re-simulates the set-aside live
  recording to its end and returns to the exact pre-replay live state. **Time Travel Here**
  (`ContinueFromHere`, confirmed) truncates the recording to the ticks before the current one
  (`ReplayRecorder.TruncateAfter(tick - 1)`, `FrameRecorder.TruncateAfter(tick - 1)`: the clock sits
  at `tick`, which is simulated live next, so the old timeline's command, sample and frame at that tick
  must go too) and branches live play from there.
- `CheckDecision` / `CheckStateHash` set `DivergenceTick` on the first mismatch; the scrubber shows
  "Diverged at m:ss (state|decision)" and a diagnostic block is appended to
  `replay_divergence.log` next to the replay files, naming which part hash (`rng`, `hero`, `party`,
  `world`) drifted first. Playback continues: a diverged replay is still a valid game.
- Every playback transition is traced to `replays/replay_playback.log` in every build
  (`ReplayPlaybackTraceLog`), the only trail a Release freeze leaves.

## Time Travel Here (the past only)

The replay timeline ends at the recorded session end: the scrubber clamps there, Play stops there
(`End of replay`) and there is no simulated future. (A "Sphere of Foresight" future region existed
until 2026-09-26 and was removed by issue #438: every path through it re-simulated, and skipping ahead
cheapens play. The artifact's ordinal is retired, see "Artifacts" below.)

- **Ticks past the recorded end are recorded, not verified.** In Simulated mode a fast playback can
  step a few ticks past `TotalTicks` inside one rendered frame. `CheckDecision` / `CheckStateHash` and
  `InjectDue` flip the recorder back on for any tick above `TotalTicks` (`BeginRecordingPastEnd`), so
  the recording stays gap-free and a Time Travel commit there truncates nothing. A scene restart
  re-preloads the recorder from the recording and drops those ticks again.
- **Time Travel Here always goes to the past** and confirms with `ConfirmContinueHereMessage` ("time
  travel to the selected point in the past? Anything that happened since then will be lost").
- **Time travel needs the Chronos Timepiece artifact** (`ReplayPlaybackService.TimeTravelUnlocked`);
  without it the button is hidden in every replay.
- **Time travel is hero-gated.** Every playthrough has one hero, identified by `GameStateService.HeroId`
  (generated at new game, saved as `SaveData.HeroId` since v32, stamped into the replay header as
  `ReplayData.HeroId` since format v3). `ReplayPlaybackService.TimeTravelAllowed` is always true for
  Replay Current Session and otherwise only when the recording's id matches the hero that was live
  when playback started; the scrubber disables the button otherwise, so another hero's replay is
  watch-only. Saves older than v32 derive a stable id from the hero design
  (`SaveData.ComputeLegacyHeroId`); v2 replay files carry id 0 and never qualify.

## Artifacts (Global and Local)

`ArtifactType` / `ArtifactCatalog` (`PitHero/Artifacts/`) define one-time purchases in two scopes
(`ArtifactScope`, issue #411):

| Scope | Belongs to | Persisted in | Price | Read by |
|---|---|---|---|---|
| **Global** | the player — owned forever, across every hero and slot | the **system save** (`SystemSaveData`, `%LOCALAPPDATA%\PitHarvest\system.bin`, own format version), written the moment one is granted; New Game and loading a slot never touch it | **proof of wealth** — the merchant only needs to see the gold, nothing is deducted | presentation only (replay gates, speed rungs, tabs) |
| **Local** | the current hero | the regular session save (`SaveData.LocalArtifacts`, v34) — cleared by New Game (`TitleMenuUI.StartGame`), restored by `SaveLoadService.ApplyLoadedState` | **deducted** — a real purchase | the **simulation** (crop growth, worker speed) |

Global artifacts are granted rather than bought on purpose: they live outside the save, so deducting
gold would let the player reload an older save and keep both the gold and the artifact. Local
artifacts rewind with the save, so charging for them is safe.

| Artifact | Scope | Price constant | Effect | Prerequisites |
|---|---|---|---|---|
| Kairos Metronome | Global | `ArtifactKairosMetronomePrice` | The 4X and 8X live fast-forward rungs (`FastFUI.HighSpeedRungsUnlocked`) | none |
| Chronos Timepiece | Global | `ArtifactChronosTimepiecePrice` | Time Travel Here | Kairos Metronome |
| Fast Grow Fertilizer | Local | `ArtifactFastGrowFertilizerPrice` | Crops grow 2x (`FastGrowFertilizerCropGrowthMultiplier`) | none |
| Lightning Grow Fertilizer | Local | `ArtifactLightningGrowFertilizerPrice` | Crops grow 3x (wins over Fast; never stacks). Supersedes Fast in the owned grid (`ArtifactCatalog.GetSupersededBy`): Fast stays owned underneath, just not shown | Fast Grow Fertilizer |
| Hermes Boots | Local | `ArtifactHermesBootsPrice` | Farm and kitchen workers move 2x (`HermesBootsWorkerMoveSpeedMultiplier`; the runner sprint stacks on top) | none |

- **One query surface.** `ArtifactService` (global, Game1) answers `Owns` / `GetOwnedInOrder` /
  `IsAvailableInShop` / `Grant` for both scopes: Global from the system file, Local from the attached
  `GameStateService` (`AttachLocalStore`). Its `Version` is a composite of the two counters, so a Local
  grant, a load or a New Game refreshes every version-cached UI (Party tab, shop rows) for free.
- **Shop:** Second Chance → Artifacts tab lists what `IsAvailableInShop` allows (not owned, every
  prerequisite owned); rows disappear once granted. Clicking a slot opens `ArtifactInfoDialog` — title,
  a **Global / Local** line, sprite, description — with a Grant (Global) or Buy (Local) button that
  acts at once, grayed and dead when the hero cannot show that much gold.
- **Party → Artifacts tab:** a fixed `ArtifactGridColumns` x `ArtifactGridRows` grid: the player's
  Global artifacts, then this hero's Local ones; clicking one opens the same card without the button.
- **Replay-safe grant:** `PlayerCommandType.GrantArtifact` (A = ordinal). The handler re-checks the
  wealth and calls the idempotent `Grant`; for a Local artifact it then deducts the price. A Global
  grant changes no simulation state; a Local one does, but as a command it replays at its recorded tick.
- **Local effects are sim-read every fixed step**, never cached across ticks:
  `LocalArtifactEffects.GetCropGrowthMultiplier` is written into `CropGrowthService.GrowthSpeedMultiplier`
  right before its update in `MainGameScene.Update`, and `FarmMonsterMover` reads
  `GetWorkerMoveSpeedMultiplier` per update. A replay's start blob is a `SaveData` (so it carries
  `LocalArtifacts`), mid-session purchases are commands, and `SimulationStateHasher.HashWorld` hashes
  `GameStateService.LocalArtifactMask` — a drift shows up as a `world` divergence, not a silent speed
  mismatch. Recordings whose blob predates v34 read an empty list, which is correct.
- **Adding an artifact:** append to `ArtifactType` (persisted ordinal), pick its scope in
  `ArtifactCatalog.GetScope`, fill in the catalog switches (sprite name in the Items atlas,
  name/description/effect keys, price constant, prerequisites), bump `ArtifactCatalog.Count`. For a
  Local artifact with a simulation effect, add the read to `LocalArtifactEffects` and consume it inside
  the fixed step. Both stores keep unknown ordinals, so older builds never drop a purchase.
- **Retiring an artifact:** never renumber. Keep the enum member (ordinal 0, the Sphere of Foresight,
  is retired since issue #438), add it to `ArtifactCatalog.IsRetired`, let the catalog switches fall to
  their empty defaults, and leave `Count` and `IsValid` alone: a system save that owns the ordinal still
  loads and keeps it, `OwnedCount` / `GetOwnedInOrder` / `IsAvailableInShop` / `Grant` ignore it, and
  the `GrantArtifact` handler drops a recorded purchase of it as a no-op.

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
   hovered or off-screen must not change what a handler does — and no UI class computes or stores a
   value the sim later reads. Synergies were the precedent (2026-09-14): `InventoryGrid` detected
   patterns on every window refresh and wrote deflect/defense/stat passives onto the hero, so a hero
   fought with different passives depending on which windows had been opened, and a replay (which
   opens none) diverged at the first deflect roll. Detection now runs in the sim
   (`HeroSynergyResolver`, every tick a bag slot changes) and the grid only mirrors the result.
   Second precedent (2026-09-16): `AddMonsterDialog.ApplyPurchase` closed the dialog when the
   purchase filled the house, and `Close()` unpauses; because a handler was running,
   `PauseService` applied the unpause directly instead of queuing it, so nothing was recorded — the
   live game resumed while the replay (dialog never open) stayed paused: `rng` and `world` (pause
   flag) diverged 50 ticks after `PurchaseMonster`. Rule: a handler never closes, pauses or unpauses
   from inside itself. UI teardown a command triggers is deferred to the presentation pass, and
   `PauseService` requests always queue while a session exists (handlers use `ApplyManualPause`).
12. **A UI object used as a command executor must be rebound and refreshed from the sim first.** The
    UI overlay is constructed before the hero entity is spawned, so a grid whose window was never
    opened this session is not connected to the hero — in a replay that is every grid — and a swap or
    purchase recorded on it is a silent no-op. Even a connected grid keeps a cached slot picture that
    `PersistBagOrdering` writes back over the bag. `InventoryGrid.SyncFromSimulation()` (called by
    the handlers' `GetGrid` and by `SecondChanceShopUI.ApplyItemPurchase`) rebinds to the current hero
    entity (also after a respawn) and rebuilds the picture before the command applies.
14. **Settings controls are commands too, and syncing a control from the sim must not dispatch.**
    Precedent (2026-09-17): the Food tab wrote `PartyDiningService.FavoriteDishId` and `EatAtTavern`
    straight from its radio/checkbox handlers. A player who switched the favorite from Buttered Bread
    (35g) to Corn Chowder (200g) got a 200g dinner live, but the replay kept the saved favorite and
    paid 35g: `world` alone diverged (+165 gold) at the dinner order, `rng`/`hero`/`party` equal, and
    only in the one session where the tab was touched. The same audit found the fridge pre-stock
    slider (which also reran `RecomputePreStockDeficits` on every dialog open) and the sell/purchase
    priority lists (`ConsumablesFirst`) writing services directly. They now dispatch
    `SetFavoriteDish`, `SetEatAtTavern`, `SetPreStockStackSize` and `SetConsumablesFirst`. When a
    dialog syncs a control from the service on open (`SetValueAndCommit`, `IsChecked`), guard the
    control's handler (`_refreshing`, `_syncingSlider`) so the sync is not recorded as a player change.
    Audit grep: `GetService<...>()...X = ` or `svc.X = ` in `PitHero/UI`.
13. **The synergy grid is the bag only.** Hero and mercenary equipment cells never take part in
    pattern matching (`PartyGridLayout.FillSynergyGrid`). The old UI detection matched every cell, so
    mercenary gear that had been shown in the Party window could complete a pattern.
8. **Cosmetic-only components may honor `Core.CosmeticUpdatesSuspended`; anything the sim waits on
   may not.** One-shot effects finish instantly when suspended (they must not freeze and replay later).
   Sprite animators are NOT cosmetic: `EnemyAnimationComponent` waits on `AnimationState`.
9. **Fast-forward is more steps, never a scaled delta.** `Time.TimeScale` stays 1.
10. **`PlayerCommandType` is append-only.** Recorded files store the numeric value.
11. **Component update order must not depend on history.** Nez re-sorts an entity's updatable
    components on every add; the fork's `ComponentList` uses `FastList.StableSort` so equal
    `UpdateOrder`s keep insertion order. Before that fix, adding/removing a particle emitter on the
    hero at different ticks (live play vs a seek that finishes cosmetics instantly) reshuffled the
    hero's components and produced hero-only divergences with identical RNG. Never reintroduce an
    unstable sort into an update path, and never rely on `Array.Sort` for ties.

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
- A timer that must run in **real** time regardless of speed or pause, and whose effect the sim never
  reads, belongs in `PresentationUpdate` on `Time.UnscaledDeltaTime`. The autosave countdown
  (`AutoSaveService`, see `AutoSave.md`) is the reference example: ticked from
  `MainGameScene.PresentationUpdate`, gated off while `ReplayPlaybackService.Current.IsActive` so a
  replayed session never overwrites the real autosave, and dispatching no `PlayerCommand` because
  saving mutates nothing the simulation reads.

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

1. Open `%LOCALAPPDATA%\PitHarvest\replays\replay_divergence.log`: the block names the first tick and
   which of `rng` / `hero` / `party` / `world` differed.
2. `rng` alone with everything else equal = an extra or missing roll (invariant 3 or 4). Flip
   `GameConfig.ReplaySeekSkipsCosmetics` to A/B a cosmetic suspect and
   `GameConfig.ReplaySeekQuietLogging` to A/B the log path; a divergence that only appears right
   after a play-to-seek transition points at something whose finish timing feeds the sim.
   `hero` only with `rng` equal, appearing only during seeks, was the unstable component sort
   (invariant 11).
3. `hero`/`party`/`world` without `rng` = a timestamp, input read, missing command or stale static
   state (invariants 1, 2, 5, 7).
4. A `decision` divergence with matching state hashes usually means the plan hash inputs changed
   (renamed action) rather than a real drift.
5. With `GameConfig.ReplayDivergenceSnapshots` on (default) the block also carries the **last in-sync
   state** (`ReplayStateDescriber`: hero vitals, gear, deflect, active synergies, GOAP plan, mercs,
   nearby enemies with wander timers) and the **drifted state**, plus `ReplayBattleTrace`: the last 48
   battle/command events with ticks (round starts, monster target picks with roll and candidates,
   Provoke, attacks, buffs, threat, grid swaps). The in-sync sample IS the recorded state at that
   tick for everything the hash covers; unhashed fields (deflect, synergies, gear) are the replay's.
6. Cross-check against the **live session's analytics** (`%LOCALAPPDATA%\PitHarvest\analytics\
   session_*.jsonl`, the long `mode=load`/`new` one — playback writes a stub with no events). Wall
   time of a tick ≈ session start + tick/60 s. An attack the trace shows but analytics lacks was a
   deflect (deflects write no row). `party_snapshot` rows carry the live hero's skills and gear.
7. To read a recording offline, load it in a throwaway MSTest with `FileDataStore.Load(name, new
   ReplayData())` and dump `Commands` and `StateHashes`: seeing the recorded hero hash stay constant
   across a window, or equal the replay's post-command hash, tells you a command no-oped.
8. **`world` only?** Its inputs are few (`SimulationStateHasher.HashWorld`: funds, local artifact
   mask, pit level, pit tier, `InGameTimeService.AccumulatedSeconds`, pause flag), so recover which
   one drifted by brute force in the same throwaway test: solve the replay's own (actual) hash for the
   unknown mask/clock using the start blob (`ReplayIO.DeserializeSaveData(data.StateBlob)`) plus the
   un-paused tick count, then search funds/clock/pause around those values for the recorded hash. The
   gold delta usually matches a price in the live analytics (`gold_spent`/`gold_gained` rows).
   Remember paused ticks do not advance the clock when mapping a tick to the analytics `gt` time.
9. **`hero` only, and at tick 0?** The session never got off the ground: the new-game/load path built
   a different hero. `HashHero`'s inputs are just as few as `HashWorld`'s (tile, `CurrentHP`,
   `CurrentMP`, `Level`, `Experience`, `InsidePit`, `StoppedAdventure`, the FSM state *if the
   component exists*, `IsBattleInProgress`, then `Bag.Count` and every non-empty `(slotIndex,
   item.Name)`), so solve both hashes by brute force in a throwaway MSTest — build the candidates
   from **real objects** (`JobFactory.CreateJob` + `new Hero(...)` for vitals, a real `ItemBag` with
   real items for the slots) rather than hand-typed values: `item.Name` is the identity name
   (`"HPPotion"`), not the `Inv_*_Name` text key, and a hand-built model silently fails to reproduce
   even the side whose state you can already read in the log. Recovering both sides names the drift
   outright — 2026-09-17 resolved to *recorded = Thief (hp 75/mp 25), actual = Knight (95/22)*, which
   pointed straight at the start blob: `GatherCurrentState` read `data.JobName` off the hero entity
   only, a NewGame blob is captured before that entity exists, and `HeroDesign` turns an empty job
   into `"Knight"` (see "A NewGame replay re-runs the new-game path" in `AGENTS.md`). The same
   ordering catches scene-scoped **services**, which are registered later in `Begin` than the blob
   capture: gather from one and the null fallback must equal the new-game default, pinned by a test
   (`FarmTaskCoordinatorTests.DefaultOrder_MatchesSaveDataDefault`). A tick-0 `hero`
   divergence is always worth solving this way: it reproduces on every playback, needs no seeking,
   and the state space is tiny.

## Debug vs Release

`Debug.Log` is `[Conditional("DEBUG")]` in Nez, so a Release build contains none of the ~600 log
calls and runs the simulation (and seeks) markedly cooler; Debug builds additionally skip log
interpolation under `Debug.QuietMode` (seeks). Both are safe only because log arguments are pure.
Recordings replay across builds: the tripwire hashes simulation state only, and `BuildId` in the
header is informational. The VS Code launch entries `.NET: Launch (Release)` run the Release build
with the correct working directory (content paths are relative to it).

## Configuration (`GameConfig.cs`, "Simulation clock" and "Replay playback" blocks)

`SimulationFixedStepSeconds`, `SimulationMaxStepsPerFrame`, `HighSpeedMaxStepsPerFrame`,
`SimulationDefaultSpeedIndex`, `SpeedSteps`, `SpeedStepLabels`, `ReplaySeekWallBudgetSeconds`, `ReplaySeekDutyCycle`, `ReplayHashIntervalTicks`,
`ReplayPauseSkipMinTicks`, `ReplaySeekSkipsCosmetics`, `ReplaySeekQuietLogging`,
scrubber size constants, `ReplayDirectoryName` / `ReplayFilePrefix` / `ReplayFileExtension`,
`ReplaySpeechSeedSalt`; artifact prices,
grid size and `SystemSaveFileName` in the "Artifacts" block. Frame stream and viewer (issue #424):

| Knob | Default | Note |
|---|---|---|
| `ReplayFrameCaptureEnabled` | true | Kill switch for the frame stream; off = pure re-simulation playback |
| `ReplayFrameViewEnabled` | true | Kill switch for the viewer; off = Simulated playback over a captured stream |
| `ReplayFrameChunkTicks` | 120 | Ticks per chunk (base frame + previous-tick deltas, deflated as one unit) |
| `ReplayTileKeyframeIntervalChunks` | 1 | Full mutable tile-layer snapshot every N chunks (~1 KB deflated) |
| `ReplayFrameMemoryBudgetBytes` | 192 MB | Compressed chunks held in RAM; beyond it, spilled chunks are evicted |
| `ReplayFrameCacheDiskBudgetBytes` | 4 GB | All `replay_*.frames` caches; oldest deleted first, `.bin` never |
| `ReplayFrameFormatVersion` | 3 | Bump on any op / HUD record / events change; orphans every cache once |
| `ReplayFrameViewSpeedSteps` / `Labels` | `SpeedSteps` + 16X, 32X | View-only ladder, no artifact gate |
| `ReplayFrameViewSoundMaxSpeedIndex` | 1 (2X) | Recorded sounds play during forward play up to this rung |
| `ReplayFrameViewSoundCatchupMaxTicks` | 30 | A longer forward move (seek, skipped pause) plays no sounds |
| `ReplayFrameViewParticleRebuildMaxTicks` | 1200 | Oldest emitter age the viewer re-simulates |
| `ReplayFrameViewCullMarginPixels` | 128 | Off-screen margin for recorded sprites |
| `ReplayFrameSessionFilePrefix`, `ReplayFrameFileExtension`, `ReplayAutoFilePrefix` | | File naming |
| `ReplayFrameStatsLog`, `ReplayPlaybackTraceLog` | true | `frame_recorder.log` / `replay_playback.log` in every build |

## Tests

`FixedStepSchedulerTests`, `SeedableRandomTests`, `VirtualSimSeedableRandomTests`,
`PlayerCommandServiceTests`, `ReplayDataTests`, `ReplayRecorderTruncateTests`,
`ReplayPauseSpansTests`, `ReplayTimeFormatterTests`, `ShuffleBagResetTests`, `ArtifactServiceTests`,
`FastListStableSortTests` (update-order stability), `QuietLogHandlerTests` (log binding), the frame
stream suites (`FrameOps/ChunkCodec/Store/Sidecar/Recorder/CaptureAdapter/CaptureCoverage/SizeBudget`
tests; `FrameRecorderTests` also pins self-caching and the sound hook, `FrameChunkCodecTests` the sound
events' round-trip), the viewer's `ReplayFrameViewerTests` (cursor, reverse play), `ShadowTileLayersTests`
(keyframe + events at arbitrary ticks), `RecordedConsoleLogTests` and `ReplayFramesPolishTests`
(Particle op, speed ladders, the particle pool's seek/step equivalence), plus the
`SaveData_V32_HeroId` / `SaveData_V31_File_DerivesStableLegacyHeroId` layouts. The existing same-seed
determinism suites (`BattleEngineTests`, `VirtualBalanceTraversalTests`) guard the RNG call-order
contract. There is no headless end-to-end replay test; the live check is: record a session that
exercises the feature, replay it, scrub back and forth, and read the status label.
