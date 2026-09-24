# Feature: Replay Frame Recording (issue #424)

**Goal:** make replay scrubbing, rewinding and exiting cost near-zero CPU, so a replay never has to
re-simulate from tick 0 just to *show* a moment. Braid-style recorded world state for everything the
player watches; re-simulation only for the rare moments the player *resumes* the world.

**Branch:** all work for #424 lands on the long-lived branch `feature/replayOverhaul`, never directly
on master. Each sub-issue's PR bases on and merges into that branch, so the whole overhaul can be
abandoned by deleting the branch if it does not work out. Master is merged into it periodically.

This document is written for an implementer who has not seen the discussion. Every phase names its
files, interfaces, tests and acceptance criteria. Read `PitHero/docs/ReplaySystem.md` first for the
current system; this document changes the *playback* half of it and leaves the *recording* half
(seed + commands + tripwire hashes) untouched.

---

## 1. Why the current design is CPU-bound (measured)

The current replay is **pure re-simulation**: seed + recorded `PlayerCommand`s, replayed through the
live `MainGameScene`. Every backward seek restarts the scene at tick 0 and fast-forwards. Exiting a
saved replay re-simulates the set-aside live session to its end.

| Fact | Value | Source |
|---|---|---|
| Sim cost per fixed step | ~42 µs (~24k steps/s, ~400x real time) | seek profile 2026-09-07 |
| Where the time goes | Entities 68% spread thin (GOAP + A* only 4%), services 26% (crop growth ~15%) | same profile |
| Backward seek to hour H of a session | ~9 s per hour of play | derived |
| Exit from a saved replay after an 8-hour live session | ~70 s | derived |
| Optimisation attempts | service caching, crop scratch arrays: nothing measurable, reverted | same profile |

There is no hotspot to fix. The only way to make "show me tick T" cheap is to **store T**.

## 2. What carries over from Braid, and what does not

Braid records the world state of every frame as base frames + deltas (base every 120 frames, delta
against the base, so any seek decodes exactly two frames). Braid's entities are small structs
(position, sprite, facing, depth), so "world state" and "what is on screen" are the same thing.

In PitHero they are not:

- **What is on screen** is ~300 renderables (sprites, paperdoll layers, floating text, HP bars) plus
  four tile layers, three HUD panels, a clock, gold and an event console. That is small and flat.
- **What the simulation needs to resume** is spread across GOAP planners, 26 live coroutine sites
  (six jump/lerp routines, the inn stay, the crystal ceremony, the death sequence, the new-game
  intro, mercenary walk-ons/offs, monster wander, and the whole `BattleEngine`, a ~40-method nested
  `IEnumerator` tree with `LiveBattleAdapter` presentation delays *inside* the sim's control flow),
  three coordinator services with queues, timers and EMAs, 21 static speech `OptionBag`s, loot
  shuffle bags, the dish bag, the hairstyle queue, four RNG streams and the tick. C# iterator state
  is not reflectable, and the project is AOT (no reflection anyway). `SaveData` is a deliberate
  "town boundary" snapshot: it drops the hero position, the pit, all plans and any battle.

So the honest split is:

| Consumer | Needs | Design |
|---|---|---|
| **Watching** (play, pause, scrub, rewind, speed) — 99% of replay use | what was on screen at tick T | **Record it, Braid-style.** Zero simulation while watching. O(1) random access |
| **Resuming** (Time Travel Here, entering the future, coming back from a saved replay's rebuild) — rare, deliberate, confirmed | the full simulation at tick T | **Re-simulate** from the recording (today's path), now hidden behind a frozen recorded frame and a progress bar |

This is not "two systems" in the sense Blow warned about. The frame stream is a **derived cache** of
the deterministic simulation with exactly one producer (a capture hook at the end of each tick). It
is never the source of truth; if it is missing or stale it is rebuilt by re-simulating, which is
exactly what happens today. Worst case degrades to the current behaviour; it never gets worse.

A useful consequence: **watching a replay can no longer diverge.** No simulation runs, so the
tripwire is only exercised on the resume paths, where it already lives.

## 3. Architecture

```
 L0  Recording (unchanged)        seed + PlayerCommands + tripwire hashes      replay_<hero>_<stamp>.bin
 L1  Frame stream (new)           per-tick presentation state, base+delta      replay_<hero>_<stamp>.frames
 L2  Frame viewer (new)           draws any recorded tick; no simulation        RecordedFrameRenderer
 L3  Resume (existing, reframed)  re-simulate to T for Time Travel / future     ReplayPlaybackService "Simulated" mode
```

### 3.1 L1 — the frame stream

**One frame per simulation tick**, captured at the tail of `MainGameScene.Update`, immediately
before `_simulationClock.Advance()` (next to the tripwire hash: `MainGameScene.cs` ~line 3381).
Y-sort depths are final there (the `YSortManager` scene component runs inside `Scene.Update`).

A frame is a list of **entities**, each a stable `ushort` recorder id plus a byte string of **draw
ops**, followed by a **HUD record**. Tile changes and console lines are recorded as **events** with
their tick, not per frame.

**Draw ops** (all little-endian, written through a small `FrameWriter` over a pooled `byte[]`):

| Op | Payload | Produced by |
|---|---|---|
| `Sprite` | spriteId u16, x f32, y f32, layerDepth f32, renderLayer i16, color u32 (RGBA), flags u8 (flipX, flipY, screenSpace) | `SpriteRenderer`, all `SpriteAnimator` subclasses (`Sprite`, `Color`, `SpriteEffects`, `LayerDepth`, `RenderLayer`, entity position + `LocalOffset`) |
| `Composite` | count u8, then per layer: spriteId u16, dx f32, dy f32, color u32, flags u8; then x, y, layerDepth, renderLayer | `MultiSpriteAnimator` / `StaticSpriteCompositor` via `ICompositeLayer` (never their private RenderTexture) |
| `Text` | stringId u16, x i16, y i16, dx i16, dy i16, color u32, scale f32, fontId u8, flags u8, charStart u16, charCount u16 | `TextRenderComponent`, `RisingTextComponent`, `BouncyTextComponent`, `BouncyDigitComponent` (one op per character), `SpeechBubbleComponent` (wrapped text interned once; charStart/charCount = the revealed, unscrolled slice), `MonsterHPBarComponent` (name) |
| `Rect` | x i16, y i16, dx i16, dy i16, w i16, h i16, color u32, flags u8 (filled/outline) | `MonsterHPBarComponent` (bars), `BuildingOutlineRenderComponent`, `SelectBoxRenderComponent`, stock `PrototypeSpriteRenderer` (filled) |
| `NinePatch` | patchId u16, x i16, y i16, dx i16, dy i16, w i16, h i16, color u32, flags u8 | `SpeechBubbleComponent` bubble body (the viewer draws the tail for this patch) |

**As shipped in #426/#427:** positions are i16 pixels (below), and the overlay ops (Text, Rect,
NinePatch) carry a `dx/dy` offset plus the `ConstantScreenSize` flag: the anchor is a world point and
dx/dy/w/h are *screen* pixels applied after the world-to-screen transform, so damage numbers, HP bars
and speech bubbles keep their on-screen size at any zoom without baking the capture-time camera into
the stream. Text/Rect/NinePatch ops carry no render layer or depth: the viewer draws them after the
sprite ops (they are all overlays on layers 0–2 or screen space). `TiledMapRenderer`, `UICanvas`,
`ParticleEmitter` and anything on `TransparentPauseOverlay` are skipped silently; hero paperdoll
layers are captured through their `MultiSpriteAnimator`, whose tint is folded into each layer color.

**Measured (#425, §6.2): store every position as i16 pixels, not f32, and delta against the
previous tick.** That single change cuts the compressed stream by 37%; the layouts above are the
pre-measurement draft.

**Sprite identity.** Nez `Sprite` has no name. `SpriteKeyRegistry` maps a `Sprite` reference to a
`ushort` id on first sight and records the key `(textureName, sourceRect)` for it. `textureName` is
`Texture2D.Name`, which `NezContentManager.LoadTexture` sets to the asset path (`NezContentManager.cs:92`).
Sprites whose texture has no name (per-entity RenderTextures) are never captured; that is why
composites capture their layers. **Measured (#425): atlas textures are unnamed too**:
`SpriteAtlasLoader` never sets `Name`, so every sprite in the game has an empty `Texture2D.Name` until
the loader is fixed (see §6.1). **#427 owns the fix**: a one-line fork change in
`SpriteAtlasLoader.ParseSpriteAtlas` setting `texture.Name` to the atlas image path (details on the
issue). String and nine-patch keys are interned the same way. Tables are
persisted incrementally: each chunk carries the entries first seen in that chunk, so a reader
rebuilds the full table by scanning chunk headers once at open.

**Capture contract for renderables** (`PitHero/Services/Replay/Frames/IFrameCapturable.cs`):

```csharp
public interface IFrameCapturable          // custom RenderableComponents implement this
{
    void CaptureFrame(ref FrameWriter w, FrameCaptureContext ctx);  // emit 0..n ops for the current tick; no allocation
                                           // ctx interns sprites (by Texture2D.Name + rect), strings and nine-patch names
}
public interface ILiveOnlyRenderable { }   // keep drawing the LIVE component while viewing
```

- Stock `SpriteRenderer` / `SpriteAnimator` (and subclasses) need nothing: `FrameCaptureAdapters`
  handles them by type check (no reflection).
- `ILiveOnlyRenderable`: `CloudOverlayComponent` (scrolls on wall time), `TreeBandComponent`
  (constant), `GraphicalHUD` (fed from the recorded HUD record, see below), `ActionQueueVisualizationComponent`
  (v1: hidden while viewing; polish phase may capture it).
- `ParticleEmitter`: skipped in v1 (see §7 polish).
- Any renderable that is none of the above is skipped with a **one-time** `Debug.Warn` naming the
  type. This is the new rule for feature authors: *a new RenderableComponent is either stock,
  `IFrameCapturable`, or `ILiveOnlyRenderable`.* A test (`FrameCaptureCoverageTests`) enumerates
  every `RenderableComponent` subclass in the assembly by a hand-maintained list and asserts each is
  classified.

**HUD record** (fixed layout, ~40 bytes): hero hp/maxHp/mp/maxMp/level (5 × i32), merc1 and merc2
present flag + the same five, gold i64, pitLevel i32, pitTier i32, inGameSeconds f32, paused u8.
Source: the same reads `GraphicalHUD.UpdateValues`, `UpdatePitLevelLabel`, `UpdateFundsLabel`,
`UpdateClockLabel` make today.

**Tile events.** `TiledMapService.SetTile` / `RemoveTile` (`Util/TiledMapService.cs:44/27`) are the
single choke point for every runtime tile mutation (pit regeneration, fog clearing, tilling, wet
tiles, restore grass). The recorder subscribes there and appends `(tick, layerIndex u8, x u16, y u16,
gid i32)`. Every `ReplayTileKeyframeIntervalChunks` chunks (default 30 = 60 s) the chunk also stores a
full gid snapshot of the three mutable layers (Base, Detail, FogOfWar; Top never changes) so decoding
tile state at T is *nearest tile keyframe + events since*, never "from the start".

**Console events.** `GameEventService.Emit` (`Services/GameEventService.cs:90-110`) gets a
`(tick, ConsoleSegment[])` hook. The recorder interns the segment texts and stores
`(tick, segmentCount, [stringId, color u32, itemStringId]…)`.

**Encoding: chunks = GOPs.** A chunk covers `ReplayFrameChunkTicks` ticks (default 120 = 2 s):

```
ChunkHeader   { firstTick i64, tickCount u16, formatVersion u16, tableDeltaBytes u32, baseBytes u32, deltaBytes u32[tickCount-1], eventsBytes u32, tileKeyframeBytes u32 }
TableDelta    new sprite keys / strings / nine-patch keys first seen in this chunk
BaseFrame     every present entity: id u16, opsLen u16, ops[]; then HUD record
Delta[k]      vs BASE (Braid): for each entity whose ops bytes differ from the base or that appeared: id, opsLen, ops[];
              tombstone (id, opsLen = 0xFFFF) for entities gone since the base; then HUD record if changed (flag byte)
Events        tile events and console events for ticks in this chunk, tick-sorted
TileKeyframe  optional full gid arrays for Base/Detail/FogOfWar
```

The whole chunk is deflate-compressed as one unit (`System.IO.Compression.DeflateStream`, BCL,
AOT-safe). Decoding tick T = inflate its chunk (cached), apply `Base`, apply `Delta[T - firstTick]`.
Two frames, exactly as in Braid. Deltas are **entity-level** (an entity whose op bytes changed is
re-emitted whole); field-level masks are a later optimisation only if the size budget (§6) is missed.

**In memory.** `FrameStore` holds finished chunks in a ring keyed by chunk index, under
`ReplayFrameMemoryBudgetBytes` (default 192 MB). Chunks beyond the budget are evicted from RAM only
after they have been written to the session sidecar (below); reads of evicted chunks go to disk
(a 2 s chunk is tens of KB, sub-millisecond to inflate). Braid *forgets*; we *spill*.

**On disk, during the session.** Finished chunks are appended to
`replays/session_<startUtcTicks>.frames` from a worker thread (same shape as `AutoSaveService`:
the chunk bytes are immutable once finished, so the main thread hands them over and never waits).
The file is a sequence of `[chunkLen u32][chunk bytes]`; a **footer** with the chunk index (tick →
offset) and the identity header is written on `Save Session Replay` / quit, and the file is renamed
to match the `.bin` (`replay_<hero>_<stamp>.frames`; same volume, `File.Move`). A truncated tail
(crash) is tolerated: the reader rebuilds the index by scanning when the footer is missing.

**Identity.** The `.frames` header carries `MasterSeed`, `RecordedAtUtcTicks`, `SimulationVersion`,
`ReplayFrameFormatVersion` and `TotalTicks`. A sidecar whose identity does not match its `.bin`, or
whose frame format version is older, is ignored and rebuilt (§3.4).

**Lifecycle mirrors `ReplayRecorder` exactly.** `FrameRecorder.Current` is scene-scoped, created in
`MainGameScene.Begin` next to `ReplayRecorder`, detached in `Unload`. During a scene rebuild for a
resume (§3.3) it is *preloaded* with the existing stream and `IsRecording = false` for ticks that are
already recorded, resuming appends past `TotalTicks`; `TruncateAfter(tick)` drops frames for Time
Travel exactly as the command recorder does. Frames captured in the simulated future are dropped on
leaving it, again like the recorder.

### 3.2 L2 — the frame viewer

`ReplayPlaybackService` gains a **mode**: `FrameView` (new default) or `Simulated` (today's path).

**Entering FrameView does not swap scenes.** The live `MainGameScene` stays, its simulation is
suspended (`Core.SimulationSuspended = true`), `RejectLiveEnqueues` is set, the UI is closed by
`SettingsUI.EnterReplayMode` as today, the camera stays free. The player's live world sits frozen
underneath the viewer and is untouched.

**Drawing.** A `RecordedFrameRenderer : Nez.Renderer` is added to the scene with the world
`RenderOrder`. While viewing:

1. The Nez fork gets a per-scene `Func<IRenderable, bool> RenderableFilter` consulted by
   `DefaultRenderer`, `RenderLayerRenderer` and `ScreenSpaceRenderer` (one fork commit; the owner
   pushes the fork). The filter returns true only for `ILiveOnlyRenderable` components. No live
   component's `Enabled` flag is touched.
2. `RecordedFrameRenderer.Render` draws, in order: the live `Top` tile layer (constant), the viewer's
   three **shadow tile layers** (copies of Base/Detail/FogOfWar rebuilt from the tile keyframe +
   events; drawn with the same `TiledRendering.RenderLayer` call `TiledMapRenderer` uses, sharing
   the day/night material), then the decoded frame's world ops sorted by (renderLayer desc,
   layerDepth) exactly as `RenderableComparer` orders live renderables, then screen-space ops in a
   second pass with the screen-space matrix.
3. `GraphicalHUD` instances stay live but are fed from the recorded HUD record
   (`UpdateValues(...)`); the pit level, gold and clock labels are set from the same record. The
   hero portrait keeps reading the live hero's paperdoll (acceptable; noted in §7).
4. `EventConsolePanel` gets `ShowRecorded(IReadOnlyList<RecordedConsoleLine>, long upToTick)`: on
   every seek it rebuilds its 50-line view from the last 50 recorded lines at or before T; during
   play it appends lines as the cursor passes them. `GameEventService.Suppressed` stays true.

**Timeline.** The viewer owns a `long Cursor`. Play advances it by `wallDt × Speed × 60`
(accumulator, fractional carry). Pause holds it. Seek sets it. **Reverse play** is the same loop with
a negative sign; it is free and is exposed as a rewind button in the polish phase. Speeds are the
`GameConfig.SpeedSteps` ladder as today; the viewer may add view-only rungs above 8X because nothing
is simulated. Recorded pause spans are skipped by jumping the cursor (`ReplayPauseSpans` unchanged).

**Exit** from FrameView: remove the filter, unsuspend the simulation, clear `RejectLiveEnqueues`,
`ExitReplayMode`, restore the window size preference. No re-simulation. Instant.

### 3.3 L3 — resume paths (the only remaining O(T) work)

| Action | What happens |
|---|---|
| **Time Travel Here** at cursor T | Confirmation as today. Then `ReplayPlaybackService` switches to `Simulated`: `RestartScene(startAtTick: T)` through `ReplayBootScene` exactly as today, **while the viewer keeps drawing recorded frame T** over the rebuilding scene and the scrubber shows `SeekProgress`. When the seek lands, `ContinueFromHere` runs (truncate recorder + frame recorder at T), the viewer is removed, live play resumes. Cost: T / ~24k s (about 9 s per hour of session), paid once, after a deliberate confirmed action, behind a frozen picture instead of a blank world |
| **Entering the future** (Sphere of Foresight) from the current session | The live scene is already at `TotalTicks`. The viewer is removed, the simulation resumes with `InFuture` recording past the end, frames are captured as it goes; scrubbing backwards *inside the future* is then FrameView over those frames. Leaving the future without committing rebuilds to `TotalTicks` (today's path) |
| **Entering the future from a saved replay** | Needs the sim at `TotalTicks`: `Simulated` rebuild to the end (today's path), then as above |
| **Exit from a saved replay** | Instant when the live scene was never torn down (FrameView). Only if a resume path tore it down does `ReturnToLiveSession` re-simulate the set-aside live recording, as today |

A rebuild that **diverges** from the tripwire hashes (the world at T differs from the frames shown)
is reported exactly as today (`DivergenceTick`, `replay_divergence.log`); the player is told once,
after landing, that the rebuilt world drifted at m:ss. For the current session this can only mean a
determinism bug, which is what the tripwire is for.

### 3.4 Saved replays and the cache

| Case | Behaviour |
|---|---|
| `.bin` has a matching `.frames` | FrameView over a file-backed `FrameStore` (chunks inflated on demand, LRU under the memory budget). No scene swap. Exit instant |
| `.frames` missing, stale (identity/format mismatch) or damaged | **Transcode:** `Simulated` playback from tick 0 at maximum seek speed with the frame recorder producing chunks, while the viewer shows recorded frames and a "Buffering n%" status; the scrubber's usable range grows as the sim runs ahead (like a video buffering bar). When the sim reaches `TotalTicks` the sidecar is finalised and playback is pure FrameView. This path tears down the live scene, so Exit afterwards is `ReturnToLiveSession` as today. Recordings made before this feature ship take this path once |
| Disk budget | `ReplayFrameCacheDiskBudgetBytes` (default 4 GB) across `replays/`; when exceeded, the oldest `.frames` are deleted first. `.bin` files are never touched by the budget. The Replay tab shows a small "cached" mark per row |

## 4. Files

New, all under `PitHero/Services/Replay/Frames/` unless noted:

| File | Role |
|---|---|
| `FrameOps.cs` | Op codes, `FrameWriter` / `FrameReader` (ref structs over `byte[]`, no allocation on the hot path) |
| `FrameChunk.cs`, `FrameChunkCodec.cs` | Chunk model, encode (base + deltas vs base, events, tile keyframe, table delta) + deflate; decode to `DecodedFrame` |
| `FrameStore.cs` | In-memory chunk ring, memory budget, spill/evict policy, `TryGetFrame(tick, out DecodedFrame)` |
| `FrameSidecarFile.cs` | Append-only writer (worker thread), footer/index, identity header, scan-rebuild of a truncated file, reader with lazy chunk loads |
| `SpriteKeyRegistry.cs` | `Sprite` → id, `(textureName, rect)` keys; string and nine-patch interning; incremental table deltas |
| `IFrameCapturable.cs` | `IFrameCapturable`, `ILiveOnlyRenderable` |
| `FrameCaptureAdapters.cs` | Stock renderable capture by type; `ICompositeLayer` composites; one-time skip warning |
| `FrameRecorder.cs` | Scene-scoped recorder: per-tick capture walk over `Scene.RenderableComponents`, HUD record, tile/console subscriptions, `IsRecording`, `TruncateAfter`, preload, `Current`/`Detach` |
| `RecordedFrameRenderer.cs` (`PitHero/Rendering/`) | The Nez `Renderer` that draws a `DecodedFrame` + shadow tile layers |
| `ShadowTileLayers.cs` (`PitHero/Rendering/`) | Copies of the three mutable layers rebuilt from keyframe + events |
| `ReplayFrameViewer.cs` | Cursor, play/pause/reverse/speed, HUD + console feed, enter/exit (filter install, suspend) |

Changed: `ReplayPlaybackService` (mode, FrameView state machine, Time Travel overlay, transcode),
`ReplayScrubberPanel` (buffering status, rewind button), `ReplayTab` (cached mark),
`ReplayFileService` (sidecar naming, budget, delete both files), `MainGameScene` (recorder
lifecycle, capture hook, viewer hooks), `TiledMapService` (mutation event), `GameEventService`
(tick hook), `EventConsolePanel` (`ShowRecorded`), `SettingsUI.SaveSessionBeforeLeaving` (finalise
sidecar), `GameConfig` (§6 knobs). Nez fork: `Scene.RenderableFilter` + renderer checks.

## 5. Tests (all headless MSTest in `PitHero.Tests`, no `Core.Instance`)

| Test class | Pins |
|---|---|
| `FrameOpsTests` | writer/reader round-trip of every op, bounds, no-alloc after warm-up |
| `FrameChunkCodecTests` | encode N synthetic ticks with spawns, moves, despawns → decode every tick equals the captured input; delta-vs-base tombstones; HUD change flag; table deltas across chunks; deflate round-trip |
| `FrameStoreTests` | random-access equality, memory budget eviction only after spill, `TruncateAfter`, preload |
| `FrameSidecarFileTests` | append + footer + reopen; footer-less (truncated) file rebuilds its index and loses only the partial tail; identity mismatch rejected |
| `ShadowTileLayersTests` | keyframe + events reproduce a reference gid array at arbitrary ticks |
| `FrameCaptureCoverageTests` | every `RenderableComponent` subclass in `PitHero` is stock, `IFrameCapturable` or `ILiveOnlyRenderable` (hand-maintained list, fails loudly when a new one appears) |
| `ReplayFrameViewerTests` | cursor arithmetic: play, reverse, speed, pause-span skip, clamp to range |
| `FrameSizeBudgetTests` | a synthetic 1-hour stream with the census numbers from Phase 1 stays under the §6 budget |

Live validation for every phase: record a session that exercises the pit, farm and kitchen,
`Settings → Replay → Replay Current Session`, scrub both ways, exit, Time Travel Here, and confirm
the scrubber never says "Seeking" while watching.

## 6. Budgets and `GameConfig` knobs

| Knob | Default | Note |
|---|---|---|
| `ReplayFrameCaptureEnabled` | true | Kill switch; off = today's behaviour |
| `ReplayFrameChunkTicks` | 120 | Braid's 2 s GOP. Confirmed by #425: bases are ~4% of the bytes |
| `ReplayFrameCaptureEveryNTicks` | 1 | 1 = every tick (60 Hz, 33 MB/h measured), 2 = every 2nd tick (30 Hz, 21 MB/h). Decided in #428 on screen, see §6.2; the format is identical either way |
| `ReplayTileKeyframeIntervalChunks` | 1 | Full mutable-layer snapshot every chunk; ~1 KB deflated (#425), so cross-chunk event replay is not worth its complexity. Was 30 before measurement |
| `ReplayFrameMemoryBudgetBytes` | 192 MB | RAM ring; spill beyond |
| `ReplayFrameCacheDiskBudgetBytes` | 4 GB | Across `replays/`; oldest `.frames` first |
| `ReplayFrameFormatVersion` | 1 | Bump breaks old sidecars (they are caches; rebuilt) |
| `ReplayFrameViewSpeedSteps` | `SpeedSteps` + {16, 32} | View-only rungs, no artifact gate needed |

**Size target:** ≤ 30 MB per hour of play on disk (compressed), ≤ 40 µs added per tick during live
play at 1x. Estimate from the renderable inventory: ~300 tracked renderables, ~40 changing per tick
(movers, animators, fog), ~12 bytes per changed entity → ~500 B/tick raw, ~110 MB/h raw, 4–6x
deflate → ~25 MB/h; base frames add ~3 MB/h compressed. Braid spent 40 MB per 30–60 min on far fewer
entities without compression, so this is the same order of magnitude. **Phase 1 measures the real
numbers before anything is built on them**; if the budget is missed, the levers are (in order)
field-level deltas for `Sprite` position, capture every 2nd tick for 30 Hz viewing, shorter chunks.

### 6.1 Measured (issue #425 census, 2026-09-22)

Three live sessions at 1x (`GameConfig.ReplayFrameCensus`, log in `replays/frame_census.log`,
one report per 3,600 ticks). Session 1 (Debug): 50 minutes, 180,000 ticks, hero out of pit 113,692
(town, farm, kitchen), in pit 45,795, battle 20,165, paused 348. Session 2 (Debug): 25 minutes,
91,005 ticks, out of pit 58,211, in pit 24,145, battle 8,273, paused 376; it reproduced every
session 1 figure within a few percent and added the encoding variants in §6.2. Session 3
(**Release**): 12 minutes, 44,157 ticks, out of pit 18,781, in pit 19,098, battle 5,903; it
reproduced the sizes a third time (A 51.5 MB/h, C 32.9 MB/h) and supplied the cost figure. The census
builds the design's op stream in the exact §3.1 layout with a 120-tick base plus entity-level
deltas against that base, and deflates each 2 s chunk for real. Text/Rect payloads are
position + color with zero fill, so their compression is slightly optimistic. They are a small
share of the bytes.

| Quantity | Estimate (above) | Measured |
|---|---|---|
| Renderables in `Scene.RenderableComponents` | ~300 | mean 571, max 695. Of these, **366 mean / 415 max are actually drawn** (enabled, not composite-owned, not live-only). By type (max): `SpriteRenderer` 380, `HeroAnimationComponent` layers 104 (owned by composites), `PausableSpriteAnimator` 72, `SpeechBubbleComponent` 44 (one per speaker, mostly hidden), `EnemyAnimationComponent` 32, `YSortSpriteRenderer` 19, `MultiSpriteAnimator` 13, `BouncyText`/`BouncyDigit` 10 each, `StaticSpriteCompositor` 2, `TiledMapRenderer` 4, `GraphicalHUD` 3, `ActionQueueVisualization` 3, `TreeBand` 2, `Cloud`/`TextRender`/`BuildingOutline`/`SelectBox`/`MonsterHPBar`/`PrototypeSprite` 1, plus 1 unclassified (almost certainly the `UICanvas`) |
| Drawn renderables changed per tick vs previous tick (min / mean / max) | ~40 | out of pit 2 / 24.1 / 324, in pit 10 / 26.5 / 80, battle 9 / 26.2 / 67, paused 0 / 1.4 / 35 |
| Drawn renderables changed vs the chunk base (what a Braid delta stores) | — | out of pit 2 / 36.8 / 214, in pit 10 / 40.1 / 97, battle 11 / 42.2 / 80 |
| Delta frame bytes per tick (mean) | ~500 B | out of pit 1,583, in pit 1,845, battle 1,902 (max 14,242, on a floor regeneration). About half of this is `MultiSpriteAnimator` composites: 8 layers each, ~112 B, and every walking actor re-emits all 8 layers every tick |
| Base frame | — | ~8 KB raw, about 4% of all bytes at 120-tick chunks |
| Raw stream | ~110 MB/h | **392 MB/h** in the §3.1 layout (session 2; per-minute windows 340–520). Session 1 reported 361 MB/h using the issue's smaller op sizes (Composite 8 + 13/layer instead of the layout's 15 + 15/layer) |
| Deflate ratio | 4–6x | **7.5–7.7x optimal** (per-window 6.1–8.6x), 6.5x fastest. Higher than estimated because deltas vs base re-emit identical records tick after tick, which deflate collapses |
| Compressed stream, design as written | ~25 MB/h (+3 MB/h bases) | **52 MB/h optimal** (session 2, measured directly; per-window 45–65), ~60 MB/h fastest. **Misses the 30 MB/h target by ~1.7x.** Session 1's 47 MB/h applied the ratio to the smaller raw figure and is superseded |
| Deflate cost per 2 s chunk (optimal) | — | mean 1.4 ms, max 5.0 ms, done on the main thread in the census. Belongs on the worker |
| Tile mutations | "fog clears dominate" | 273/min. FogOfWar 75% (10,263), Base 12% (1,701), Collision 12% (1,694, never drawn, so no need to record), Detail 0. **55% are same-gid writes** (7,451) that change nothing |
| Tile keyframe (Base + Detail + FogOfWar gids) | — | 34,560 B raw, **~1 KB deflated** |
| Console lines | — | 6.1/min, 5.9 segments and 47 chars per line. Negligible |
| `Texture2D.Name` non-empty | yes (§3.1 assumption) | **No. 0 of 5 textures are named, and all 539 distinct sprites sit on unnamed textures.** Owners: composite layers 340, `SpriteRenderer` 98, `EnemyAnimationComponent` 84, `PausableSpriteAnimator` 7, `YSortSpriteRenderer` 5, `StaticSpriteCompositor` layers 4, `PrototypeSpriteRenderer` 1. Cause: `SpriteAtlasLoader` builds the texture with `Texture2D.FromStream` and never sets `Name`; only `NezContentManager.LoadTexture` names textures. 539 `Sprite` references map to 539 distinct `(texture, rect)` keys, so no two sprites share a key |
| Census walk cost per tick | ≤ 40 µs target for capture | **Release (session 3): mean 86 µs, min 29 µs** (per-window means 67–100), typical per-window max 0.6–3 ms, one 5.4 ms outlier. That is the census, not a capture: it walks all ~570 renderables with ~4 dictionary operations each and writes **five** streams plus a sprite-id lookup per op. Debug for comparison: 116 µs with one stream (session 1), 163 µs with five (session 2). A real single-stream capture without hashed lookups should land near the 29 µs floor; the 40 µs budget is reachable but not by accident |

### 6.2 Encoding variants (session 2)

Five streams written side by side from the same ticks, each in the §3.1 layout and deflated
per 120-tick chunk (758 chunks). Session totals, with the per-minute window range in brackets:

| Variant | Raw MB/h | Deflated MB/h | Ratio | Entities per delta frame |
|---|---|---|---|---|
| **A** design as written: deltas vs chunk base, f32 positions | 392 | **52.3** [45–65] | 7.5x | 35.6 |
| **B** deltas vs previous tick, f32 positions | 198 | **48.3** [40–60] | 4.1x | 24.2 |
| **C** = B with positions as i16 pixels | 156 | **33.0** [27–42] | 4.7x | 23.8 |
| **D** = C plus a composite "moved" op (position/depth only) | 138 | **32.4** [26–42] | 4.2x | 23.8 |
| **E** = C sampled every 2nd tick (30 Hz) | 99 | **20.7** [18–26] | 4.8x | 25.7 |

What the variants say:

- **Previous-tick deltas alone are worth only 8%** (A → B). They halve the raw bytes, but deflate
  was already collapsing the repeated records, so the ratio drops from 7.5x to 4.1x and most of the
  raw saving evaporates.
- **Integer-pixel positions are the real lever: −37% vs the design** (A → C, and B → C is −32%).
  The gain is not fewer changes: the change count at pixel precision equals the float count (23.9
  vs 24.2 per tick out of the pit, 24.3 vs 25.0 in the pit), because the movers are a minority of
  the changing entities and animators dominate. The gain is that 2-byte integer coordinates are
  smaller and far more compressible than 4-byte floats whose low bits churn every tick. It is
  lossless for the viewer: the game draws point-sampled pixel art and the compositors already
  round entity positions.
- **The composite "moved" op is worth ~1 MB/h** (C → D). Deflate already collapses the repeated
  layer records of a walking composite. Not worth an op code.
- **30 Hz sampling is worth another 37%** (C → E) and is the only variant under 30 MB/h.
- Deflate cost per chunk is unchanged (mean 1.4 ms, max 5.8 ms, main thread in the census).

**Recommendation for #426 (and the capture in #427).** Keep `ReplayFrameChunkTicks` at **120**: base frames are ~4% of the bytes and a chunk inflates in well under a millisecond, so shorter chunks buy nothing. Keep `ReplayFrameMemoryBudgetBytes` at **192 MB** with chunks held compressed in RAM, which is 5–6 hours at the recommended encoding before spilling. For the encoding, adopt variant **C**: entity-level deltas against the **previous tick** within the chunk, with every position (entity x/y, composite layer offsets, rect and text origins) stored as **i16 pixels** rather than f32. That is measured at **33 MB/h** (27–42 per minute) against 52 MB/h for the design as written, and the whole gain comes from the integer coordinates; previous-tick deltas alone are worth only 8% because deflate already collapses re-emitted records, and the composite "moved" op measured at ~1 MB/h, so **no field-level masks and no extra op codes are needed**. Decoding stays cheap: seeking to tick T inflates its chunk and replays up to 119 small deltas from the base, which is microseconds. **Capture every tick (60 Hz).** C is 10% above the 30 MB/h target, and the honest choice is to accept that and set `FrameSizeBudgetTests` at **≤ 40 MB/h** for the §6.2 traffic mix (a 4 GB disk budget still holds ~120 hours), rather than commit now to 30 Hz sampling (variant E, 20.7 MB/h). At 32–64 px/s an actor moves 0.5–1 px per tick, so a 30 Hz viewer would show 1–2 px steps at 1x: probably acceptable, but it is a visible change that #428 should judge on screen, so keep every-2nd-tick capture as a knob (`ReplayFrameCaptureEveryNTicks`, default 1) and decide it there. For tiles, store a keyframe **every chunk** (`ReplayTileKeyframeIntervalChunks = 1`): at ~1 KB it costs ~1.8 MB/h and removes cross-chunk event replay from the decoder; drop same-gid writes at the hook (55% of all writes) and ignore the Collision layer, which is never drawn. Run deflate on the worker thread that writes the sidecar, never on the main thread (1.4 ms mean, up to 6 ms per chunk). **`Texture2D.Name` is not a usable sprite key as-is.** The simplest fix is a one-line Nez fork change so that `SpriteAtlasLoader` sets `texture.Name` to the atlas image path, after which §3.1's `(textureName, sourceRect)` key works unchanged; if a fork change is unwanted, #427 builds a texture-to-atlas-path registry instead (only 5 textures are in use). Either way it keeps a one-time warning for unnamed textures. On **capture cost**, the Release census measured 86 µs per tick mean (29 µs floor) while doing far more than a capture would: five streams, ~4 dictionary operations per renderable over all ~570 of them, and a hashed sprite-id lookup per op. The 40 µs budget is therefore reachable, but only if #427 keeps per-renderable state without hash lookups (a slot index on the component or a parallel array), resolves sprite ids once per sprite rather than per op, skips live-only and composite-owned renderables before any other work, and profiles in Release against the budget before #428 builds on it.

## 7. Phases (one GitHub sub-issue each; each is independently shippable behind the kill switch)

| # | Issue | Title | Depends on | Size |
|---|---|---|---|---|
| 1 | #425 | Census spike: measure renderables, change rate and naive bytes per tick; verify `Texture2D.Name` keys | — | S |
| 2 | #426 | Frame model, chunk codec, store and sidecar file (headless, fully tested) | 1 | M |
| 3 | #427 | Capture: adapters, `SpriteKeyRegistry`, `FrameRecorder`, hooks (tick, tiles, console), session sidecar writes | 2 | M |
| 4 | #428 | Frame viewer for Replay Current Session: renderer, shadow tiles, HUD/console feed, cursor, Nez filter; Exit instant; Time Travel behind a frozen frame. **Decides `ReplayFrameCaptureEveryNTicks` on screen** (60 Hz vs 30 Hz at 1x; position interpolation between frames is the cheap way to make 30 Hz look like 60) | 3 | L |
| 5 | #429 | Saved replays: sidecar save/rename, identity, lazy loading, disk budget, Replay tab mark; FrameView for cached saved replays | 4 | M |
| 6 | #430 | Transcode for uncached or stale saved replays: buffering status, growing range, finalise sidecar | 5 | M |
| 7 | #431 | Polish and docs: rewind button + reverse play, view-only 16X/32X, particles as re-emitted effects, action-queue capture, `ReplaySystem.md` rewrite, `replay-determinism` skill + `AGENTS.md` rule for new renderables, remove dead code | 4–6 | M |
| 8 | #432 | *(Optional, go/no-go)* Simulation checkpoints to bound Time Travel rebuilds | 4 | XL — see §8 |

Phase 4 is the first phase the player feels. Phases 1–3 are invisible (capture runs, nothing reads it)
and are safe to merge one at a time.

## 8. Phase 8 — simulation checkpoints (optional, honest assessment)

A faithful snapshot every N minutes would bound the Time Travel rebuild to N minutes of
re-simulation (≤ 1 s for N = 5). It is the *only* remaining lever for that path. The survey of what
such a snapshot must capture:

- 26 `Core.StartCoroutine` sites in sim code (jumps ×6, inn stay, ceremony, death, intro,
  mercenary walk-on/off ×3, monster wander, pit reset waits, obstacle injection, spawn poses) plus the
  `BattleEngine` (15 nested `IEnumerator`s) and `LiveBattleAdapter` (~25). Iterator state cannot be
  serialised; each would become an explicit tick-driven FSM, or checkpoints would be restricted to
  ticks with **no live sim coroutine**, which with monster wander and kitchen walk-ons is rare while
  the hero is in the pit.
- Three coordinator services (`FarmTaskCoordinator`, `KitchenTaskCoordinator` with 16 tickets,
  station slots, three EMAs and a dish `ShuffleBag`, `PartyDiningService`), four actor FSMs
  (`HeroStateMachine` with planner + plan stack + path + latches, `MercenaryStateMachine`,
  `FarmingMonsterStateMachine`, `KitchenMonsterStateMachine`), `TavernPatronComponent`,
  `EnemyComponent` wander counters, `TreasureComponent`, `TrapComponent`, `TileByTileMover` sub-tile
  progress.
- Hidden state: 21 static speech `OptionBag`s, `LootShuffleService.Bags` + epic RNG,
  `HairstyleQueueService`, `PitLevelQueueService`, `HeroStateMachine.IsBattleInProgress` /
  `CurrentThreatTarget`, the four RNG stream states, `SimulationClock.Tick`.
- Tweens can be ignored (3 sites, all infinite cosmetic colour loops, nothing waits on them).

**Recommendation: do not build this unless Time Travel waits prove unacceptable after Phase 4.**
The wait is bounded (~9 s per hour of session), paid once, after a confirmation, behind a frozen
picture. If it is built, the go/no-go criterion is: the battle engine and the six jump routines are
first converted to tick FSMs (a separate refactor with its own replay validation), and checkpoints are
taken only at ticks with zero live sim coroutines, validated by a headless test that restores a
checkpoint and matches every subsequent tripwire hash for 10 minutes of simulation.

## 9. Non-goals

- Inspecting inventory or windows at a past tick (the UI is locked during replay today; nothing lost).
- Exact particles (cosmetic; re-emitted approximately in Phase 7).
- Changing the recording format, `PlayerCommand` pipeline, tripwire, or any determinism rule.
- Faster simulation. The profile says there is nothing cheap left.

## 10. Rules for the implementer

- **AOT** (`AGENTS.md`): `for` loops, no LINQ on hot paths, no reflection (type checks and hand lists
  only), no allocation per tick after warm-up (pooled buffers, pre-sized lists), `const` strings.
- **Determinism is untouched by design**: the recorder is read-only over the scene. It must never
  roll `Nez.Random`, read `Input`, or mutate any component. The Nez `RenderableFilter` is the only
  engine change and it is presentation-only.
- **Never delete a `.bin`** from a cache policy. The `.frames` file is always disposable.
- **Mirror `ReplayRecorder`** for lifecycle (`Current`, `Detach`, preload, `IsRecording`,
  `TruncateAfter`). Where the two would differ, the command recorder wins.
- **Every phase ends with the live validation in §5** and, for Phase 7, the documentation updates
  the `capture-determinism-lessons` practice requires: `ReplaySystem.md`, `AGENTS.md`, the
  `replay-determinism` skill.
