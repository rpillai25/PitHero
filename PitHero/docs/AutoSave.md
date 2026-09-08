# AutoSave System

Issue #409. The running session is written to a dedicated `autosave.bin` every
`GameConfig.AutoSaveIntervalSeconds` (30 s of **wall-clock** time) without stalling gameplay. This
document is the reference for how the pieces fit, the rules that keep it safe, and the decisions
that are not obvious from the code.

## At a glance

| Piece | Where | Role |
|---|---|---|
| `AutoSaveService` | `Services/AutoSaveService.cs`, registered in `Game1` (global) | Wall-clock countdown, gathers the snapshot on the main thread, writes it on a worker, publishes completion by polling |
| `SaveLoadService.SaveAllowed` | `Services/SaveLoadService.cs` | The single save gate, written every presentation frame by `MainGameScene.ComputeSaveAllowed`; read by the autosave and the Session → Save button |
| Autosave store | second `FileDataStore` passed to `SaveLoadService` in `Game1` | Dedicated writer for the worker thread; the slot store stays on the main thread |
| `AutoSaveIndicator` | `UI/AutoSaveIndicator.cs`, on `_uiStage` | Pulsing `SaveIcon` in the lower-right corner while a write is in flight |
| Load UI row | `UI/SaveLoadUI.cs` (`AutoSaveSlot = -1`) | First row in **Load** mode only; tinted row, Skullboy "AutoSave" tag |
| Session → Save button | `UI/SettingsUI.RefreshSaveButtonState` | Greyed while `!SaveAllowed` or autosaving; "Autosave in progress" tooltip in the latter case |
| Nez `FileDataStore.Save` | `Nez/Nez.Persistence/Binary/DataStore/FileDataStore.cs` | Crash-atomic: stale tmp deleted, then `File.Move(tmp, dest, overwrite: true)` |
| Constants | `GameConfig.cs` → `// AutoSave (issue #409)` | Interval, file name, icon margin / linger / pulse |
| Strings | `UITextKey` + `UI.txt` → `# AutoSave (issue #409)` | `SaveLoadAutoSave`, `SettingsSaveAutosaveInProgressTooltip`, `ConfirmLoadAutoSave` |

## The sequence, once per rendered frame

All of this runs in `MainGameScene.PresentationUpdate`, **never** in the simulation step
(`MainGameScene.Update`). The replay system re-simulates sessions from a seed plus recorded player
commands; a timer that read the wall clock inside a step would break determinism, and saving mutates
nothing the simulation reads, so it needs no `PlayerCommand`.

1. `ComputeSaveAllowed(replayActive)` decides whether the session may be saved right now (see the
   gate below) and stores the answer in `SaveLoadService.SaveAllowed`.
2. `AutoSaveService.Tick(Time.UnscaledDeltaTime, canSave)`:
   - polls the worker first (`PollCompletion`);
   - if `!canSave`, **resets** the countdown to zero and returns;
   - otherwise accumulates wall seconds and, at the interval with no write in flight, calls
     `TryStartAutoSave()` and restarts the countdown.
3. `TryStartAutoSave()` calls `SaveLoadService.GatherCurrentState()` **on the calling (main) thread**.
   That method walks live entities and services and must never run off-thread. It returns a fully
   detached `SaveData` (strings, ints, lists of structs, freshly allocated), which is safe to hand to
   a worker.
4. `Task.Run` executes `SaveLoadService.WriteAutoSave(data)`: `Persist` + file write through the
   dedicated autosave store. The worker body is `try { write } catch { record exception }` and
   **never logs or touches `Core`**.
5. `PollCompletion()` (called from `Tick`, from the `IsSaving` getter and from `WaitForCompletion`)
   observes `Task.IsCompleted` on the main thread: clears `IsSaving`, then either `Debug.Warn`s and
   raises `Failed`, or calls `SaveLoadService.SetAutoSavePreview(data)` so the Load UI preview is the
   in-memory snapshot. **The disk is read only by the `SaveLoadService` constructor**
   (`RefreshAutoSavePreview`), never after a write.
6. `AutoSaveIndicator.Update(autoSaveService.IsSaving)` shows the icon while in flight and for
   `AutoSaveIconMinVisibleSeconds` afterwards, with a `Time.TotalTime` alpha pulse (wall clock is fine
   here: presentation only).

There is no `SynchronizationContext` on the main thread, so nothing is ever marshalled back from the
worker. Completion is a flag the main thread reads. This is the BeamFace `SaveGameManager` idea
(`Task.Run` + callback) adapted to a polling model.

## The save gate

`MainGameScene.ComputeSaveAllowed` returns false when any of these hold:

| Condition | Why saving then captures a broken state |
|---|---|
| `!_isInitializationComplete` | `Begin()` has not finished building the world |
| Replay playback active (`ReplayPlaybackService.Current.IsActive`) | The scene is a re-simulation; an autosave would overwrite the real session with replayed state |
| `IsIntroActive` (new-game intro) | Scripted opening, hero not yet in a normal state |
| `HeroPromotionService.IsGrantingCrystal` | Crystal ceremony mid-flight |
| No hero entity, or `LinkedHero == null` | The 2 s gap between death and respawn: the hero entity is destroyed and its crystal already vaulted |
| `LinkedHero.CurrentHP <= 0` | Death animation playing |
| `HeroComponent.NeedsCrystal` | Statue walk after respawn |

The same flag drives the Session → Save button, so **manual saves obey the identical rules**. Before
#409 the manual gate was UI-only (`SettingsUI.SetSaveEnabled`, now deleted) and did not cover the
death animation or the respawn gap. `MainGameScene.Unload` sets `SaveAllowed = false` so nothing
can save between scenes.

**Adding a new blocked state:** add one condition to `ComputeSaveAllowed`. Do not add a second flag
or touch the button directly.

## Rules

1. **Gather on the main thread, write on the worker.** `GatherCurrentState` reads live scene state;
   `WriteAutoSave` must stay thread-agnostic (no `Core.*`, no `Debug.*`, no `Time.*`).
2. **The autosave store is never shared.** Nez `FileDataStore` caches one `ReuseableBinaryWriter` and
   one reader per instance. The slot store is main-thread only; the autosave store is written by the
   worker and read by the main thread only when no write is in flight.
3. **Anything that reads `autosave.bin` calls `AutoSaveService.WaitForCompletion()` first.** Today
   that is `SaveLoadUI.PerformLoad` for the autosave row and `Game1.Dispose` (so process exit cannot
   kill a write mid-flight). `File.Move` keeps the destination intact even without this; the wait
   avoids an orphaned tmp and a stale preview.
4. **Never call `FileDataStore.Clear()`** anywhere: it deletes every file in the persistent folder,
   including a tmp the worker is writing.
5. **Presentation only.** No `PlayerCommand`, no `Nez.Random`, nothing the sim reads. The replay
   audit (`.claude/skills/replay-determinism/references/determinism-audit.md`) treats the autosave
   as a wall-clock consumer that must stay out of `Update()`.
6. **Save format unchanged.** `autosave.bin` is an ordinary `SaveData` at `CurrentVersion`; every
   backwards-compatibility rule in AGENTS.md applies to it unchanged. An unreadable or old-version
   autosave is treated as empty, exactly like a slot.

## Decisions that are not obvious from the code

- **Global service, not scene-scoped.** An in-flight write must survive Quit-to-Title, and the title
  screen's Load UI must be able to see it finish. Nothing in the service is sim-relevant, so the
  replay start blob does not need to capture it. `MainGameScene.Begin` calls `ResetTimer()` so every
  session (new game, load, replay exit) starts the countdown over.
- **A blocked stretch resets the countdown instead of holding it.** Otherwise the save would fire
  the instant a transitional state ended (ceremony complete, intro over), which is the moment the
  world is most likely mid-transition. The first autosave lands a full interval later.
- **Manual saves reset the countdown** (`SaveLoadUI.PerformSave`), so a manual save is never
  followed seconds later by a redundant autosave.
- **Fast-forward does not shorten the interval.** `Time.UnscaledDeltaTime` in the presentation pass is
  the raw frame delta; `Core.SimulationSpeed` only adds simulation steps per frame.
- **The autosave keeps running while the game is paused** (Settings, Hero window, shop open) and is
  deliberately **not** change-gated. Player commands still drain while paused, so inventory, gear,
  shop and automation changes made in menus do mutate persistent state. A "dirty flag" would add a
  second condition every mutation path must remember to set, and a missed autosave is the exact
  failure the feature exists to prevent; a redundant write of a small file costs nothing. Considered
  and rejected on 2026-09-07.
- **The AutoSave row appears only in Load mode.** Save mode keeps the five manual slots so a player
  can never overwrite the autosave by hand.
- **Concurrent manual save during an autosave is safe by construction** (different file, different
  store), but the Session → Save button is greyed anyway per the issue, so the two never overlap in
  practice.
- **The icon sits at the literal lower-right corner** over the `EventConsolePanel`'s corner and is
  added to the stage after the console so it draws above it. It is `ToFront()`'d once at creation,
  never per frame, so tooltips and dialogs that also `ToFront` still win.
- **1x / 2x art is re-checked every frame.** `WindowManager.IsHalfHeightMode()` flips to Normal
  while any UI window is open, so the indicator follows the same per-frame pattern as
  `FastFUI.UpdateButtonStyleIfNeeded`. Nez `Image.Draw` ignores scale, so the swap is
  `SetDrawable` + `SetSize`, with the 2x sprite coming from `ButtonSprite2xFactory.GetOrCreate2x`.

## Tests

| Area | Test file |
|---|---|
| Countdown, blocked reset, no overlapping write, thread affinity (gather/publish on caller, write on worker), faulting write recovery, null snapshot | `PitHero.Tests/AutoSaveServiceTests.cs` |
| Autosave round-trip from disk, incompatible-version autosave treated as empty, stale tmp replaced and no tmp left behind | `PitHero.Tests/SaveLoadTests.cs` (`SaveLoadService_AutoSave_*`, `FileDataStore_Save_ReplacesStaleTmpAndLeavesNoTmp`) |

The service takes its `gather` / `write` / `onSaved` delegates in the constructor, so tests run
without `Core`. Production wiring is in `Game1`.

## Manual verification checklist

1. New game, wait 30 s: the SaveIcon pulses lower-right for at least 1.5 s and
   `%LOCALAPPDATA%\FeedTheHero\autosave.bin` appears with no `.tmp` beside it. No frame hitch.
2. Settings → Session while the icon shows: Save is greyed and hovering it reads "Autosave in
   progress"; it re-enables when the icon goes away.
3. Window → Half size: the icon is the crisp 2x version and stays in the corner; opening any window
   swaps it to 1x live; Normal size restores 1x.
4. Quit to Title → Load: the first row is the tinted AutoSave row with the Skullboy tag and a correct
   hero preview; loading it restores the session. With no autosave file the row reads "- Empty -"
   and is disabled.
5. Hero death → respawn walk → ceremony: no autosave and Save stays greyed until the ceremony ends;
   the next autosave lands 30 s after that.
6. Replay: Settings → Replay → Replay Current Session, seek back and forth. Status stays **In sync**
   and no icon appears during playback. Repeat from a loaded autosave.
