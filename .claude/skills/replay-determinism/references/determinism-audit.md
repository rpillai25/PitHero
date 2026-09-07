# Determinism Audit

Run this checklist over any feature before declaring it done. Each item names the invariant, why it
matters, and the fix. The invariants are numbered as in `PitHero/docs/ReplaySystem.md`.

## Checklist

### Input and time (invariant 1)
- [ ] No `Input.*`, `Mouse`, `Keyboard`, `GamePad` reads in `Update()` of an entity component, a
      service ticked from `MainGameScene.Update`, an AI action, a coroutine, or a battle callback.
      Input belongs in `PresentationUpdate` / UI stages / `CameraControllerComponent.PresentationUpdate`.
- [ ] No `Time.TotalTime`, `Time.FrameCount`, `Time.TimeSinceSceneLoad`, `DateTime.Now/UtcNow`,
      `Stopwatch`, `Environment.TickCount` in sim code. Stored timestamps use `SimulationClock.Now`
      (seconds) or `SimulationClock.CurrentTick`. `Time.DeltaTime` accumulators are fine.
- [ ] Wall-clock reads that remain are presentation-only (UI pulses, time-played counter, analytics).

### Player mutations (invariant 2)
- [ ] Every UI path that changes sim state dispatches a `PlayerCommand`
      (`references/player-command-recipe.md`). Grep the new UI file for direct calls into
      `HeroComponent`, `InventoryGrid.SetSlotItem`, `FundsService`, managers and services.
- [ ] Hotkeys and context menus dispatch the same command as the button.
- [ ] Handler resolves targets from the payload, re-validates, no-ops on failure.

### Randomness (invariants 3, 4)
- [ ] Sim decisions roll `Nez.Random` (Sim stream) only. Mid-battle loot that must not shift the
      battle roll count uses `GameRandom.Loot`.
- [ ] Nothing in UI, audio, particles, tooltips, hover, camera, or cosmetic components calls
      `Nez.Random`. Use `GameRandom.UiRange` / `GameRandom.AudioRange` / Nez `ParticleRandom`.
- [ ] No new `new System.Random()`, `Guid.NewGuid()`, `GetHashCode()`-ordered collections, or
      `OrderBy` on floats with ties in sim code.
- [ ] Conditional rolls: a roll that happens only when some condition holds is fine **if** the
      condition is itself deterministic. A roll gated on a window being open, a hover, or wall time
      changes the stream.
- [ ] Shuffle bags consumed by the sim: `ShuffleBag.NextFromRoll` is RNG-neutral; a **static** bag
      must register for reset at the reseed (see `SpeechBubbleDialogue.OptionBag` and `_allBags`).

### Static and global state (invariant 5)
- [ ] New `static` fields read by the sim are reset at the top of `MainGameScene.Begin` next to
      `GameRandom.InitializeSession`, or are scene-scoped.
- [ ] New scene services are registered in the scene and removed in `Unload` (services are keyed by
      type; a leftover registration throws on the replay scene swap).
- [ ] New global services (`Game1`) holding sim-relevant state either get captured in the new-game
      start blob (`CaptureSessionStartBlob` / `RestoreGlobalServicesFromSave`) or reset on playback
      restart (`PitLevelQueueService` is drained there).
- [ ] Lazily created singletons the sim reads count as static state.

### Iteration order (invariant 6)
- [ ] Collections enumerated by the sim are built in the same order on every run (`Dictionary` and
      `HashSet` enumerate in insertion order for identical operation sequences; keep it that way).
- [ ] Sorts use total, stable keys (add an index tiebreaker for float scores).

### Presentation feedback (invariant 7)
- [ ] The sim does not read UI element state (selected row, checkbox value, window visibility,
      scroll position). Settings the sim needs live in a service the command handler writes.
- [ ] Callbacks from sim → UI → sim (`OnInventoryChanged`, shortcut refresh) behave identically with
      the window closed, hidden, or never opened.

### Cosmetics and seeks (invariant 8)
- [ ] Purely visual components (floating numbers, rising text, pickup arcs, Y-sort, indicators,
      building-fall tween) early-return or **finish instantly** when `Core.CosmeticUpdatesSuspended`
      (pattern: `BouncyTextComponent`, `ItemPickupAnimationComponent`). Freezing them makes the effect
      play late after the seek.
- [ ] Anything the sim waits on does NOT check the flag: sprite animators (`EnemyAnimationComponent`
      waits on `AnimationState`), "effect finished" callbacks that gate an action, coroutines that
      advance world state.
- [ ] Particle emitters use Nez `ParticleRandom` and complete instantly when suspended (already in the
      fork; new emitter code must not bypass it).

### Speed (invariant 9)
- [ ] No `Time.TimeScale` changes. Fast-forward is `Core.SimulationSpeed`.

### Update order (invariant 11)
- [ ] No `Array.Sort` / `List.Sort` on update lists whose keys can tie (both are unstable). Nez's
      `ComponentList` uses `FastList.StableSort`; new lists that drive update order need the same.
- [ ] Adding or removing a component/entity (effects, pickups, indicators) leaves the relative
      update order of everything else unchanged.
- [ ] `Debug.Log` arguments are pure: they are skipped under `QuietMode` and absent in Release.

### Persistence (invariant 10)
- [ ] `PlayerCommandType` members appended, never renumbered. If a member is retired, leave its
      number unused.

## Diagnosing "Diverged at"

The scrubber shows the first tick where a recorded fingerprint mismatched and whether it was a
`state` or `decision` sample. Playback keeps going; a diverged replay is still a valid game, just not
the recorded one.

1. Open `%LOCALAPPDATA%\FeedTheHero\replays\replay_divergence.log`. Each block lists the tick and the
   four part hashes (`rng`, `hero`, `party`, `world`) expected vs actual.
2. Read the parts:

   | Differs | Meaning | Look for |
   |---|---|---|
   | `rng` only | An extra or missing roll on the Sim stream | A non-sim consumer of `Nez.Random`; a sim roll gated on something non-deterministic; a new emitter/visual rolling the global RNG |
   | `hero` / `party` / `world` without `rng` | State changed without a roll | A missing `PlayerCommand`; a `Time.TotalTime` timestamp; an `Input` read in a step; a handler reading UI state; stale static state |
   | `decision` with matching state | Plan hash inputs changed | Renamed/reordered GOAP actions; a plan depending on presentation state; usually not real drift if state stays in sync afterwards |
   | Everything, from tick 0 | Session start differs | Start blob missing something (new global service), seed lifecycle reset missing, content/build change (`BuildId` warning) |
   | `hero` only, RNG equal, only right after a play-to-seek transition | Something mid-flight at the switch is finished differently | Update-order drift (invariant 11); A/B with `ReplaySeekSkipsCosmetics` then `ReplaySeekQuietLogging` |

3. Narrow the window: seek to just before the tick, play at 1x, watch what the hero/party did.
4. A/B a cosmetic suspect by flipping `GameConfig.ReplaySeekSkipsCosmetics`. If the divergence moves
   or disappears, a "cosmetic" component was feeding the sim.
5. Diverges only after a **backward** seek (first play was in sync): static state survived the scene
   restart. Reset it at the reseed.
6. Diverges only in a **loaded-save** replay: a value came from the pre-load scene or a global service
   that the start blob does not carry.

## Verification protocol (any feature touched by this audit)

1. Build + tests green.
2. New game, play 2–3 minutes exercising the feature (several times, with menus open and closed).
3. Settings → Replay → **Replay Current Session**. Let it run past the feature at 1x, then at 8x.
4. Seek backward to before the feature, forward past it. Status must stay **In sync** the whole time.
5. Exit the replay; live play must resume exactly where it left off.
6. If the feature involves saves, repeat from a loaded save (`ReplayKind.Load`).
