# AutoSave System

Issue #409. The running session is written to that hero's own autosave file every
`GameConfig.AutoSaveIntervalSeconds` (30 s of **wall-clock** time) without stalling gameplay. There
is **one autosave per hero**, keyed by `HeroId`, so playing a second hero never overwrites the
first's — see "Retention and file naming". This document is the reference for how the pieces fit,
the rules that keep it safe, and the decisions that are not obvious from the code.

## At a glance

| Piece | Where | Role |
|---|---|---|
| `AutoSaveService` | `Services/AutoSaveService.cs`, registered in `Game1` (global) | Wall-clock countdown, gathers the snapshot on the main thread, writes it on a worker, publishes completion by polling |
| `SaveLoadService.SaveAllowed` | `Services/SaveLoadService.cs` | The single save gate, written every presentation frame by `MainGameScene.ComputeSaveAllowed`; read by the autosave and the Session → Save button |
| Autosave store | second `FileDataStore` passed to `SaveLoadService` in `Game1` | Dedicated writer for the worker thread; the slot store stays on the main thread |
| `AutoSaveEntry` + `AutoSaveEntries` | `Services/SaveLoadService.cs` | One entry per hero that has an autosave (`HeroId`, `Preview`, `LastWriteUtc`), most recently written first |
| `AutoSaveIndicator` | `UI/AutoSaveIndicator.cs`, on `_uiStage` | Pulsing `SaveIcon` in the lower-right corner while a write is in flight |
| Load UI rows | `UI/SaveLoadUI.cs` (`AutoSaveSlot = -1` + a hero id) | One row per hero's autosave, before the manual slots, in **Load** mode only; tinted row, Skullboy "AutoSave" tag |
| Session → Save button | `UI/SettingsUI.RefreshSaveButtonState` | Greyed while `!SaveAllowed` or autosaving; "Autosave in progress" tooltip in the latter case |
| Nez `FileDataStore.Save` | `Nez/Nez.Persistence/Binary/DataStore/FileDataStore.cs` | Crash-atomic: stale tmp deleted, then `File.Move(tmp, dest, overwrite: true)` |
| Constants | `GameConfig.cs` → `// AutoSave (issue #409)` | Interval, file prefix/extension, legacy file name, icon margin / linger / pulse. The retention cap is `SaveLoadService.MaxAutoSaves`, beside `MaxSlots` |
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
   dedicated autosave store, into the file named for `data.HeroId`. The worker body is
   `try { write } catch { record exception }` and **never logs, touches `Core`, or mutates the
   entry list** — file deletion for the retention cap happens on the main thread.
5. `PollCompletion()` (called from `Tick`, from the `IsSaving` getter and from `WaitForCompletion`)
   observes `Task.IsCompleted` on the main thread: clears `IsSaving`, then either `Debug.Warn`s and
   raises `Failed`, or calls `SaveLoadService.SetAutoSavePreview(data)`, which republishes that
   hero's snapshot in memory and enforces the retention cap. **A completed write is never read back
   from disk**; the disk is scanned only by `RefreshAutoSavePreviews()` (the constructor and when the
   Load window opens).
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
3. **Anything that reads an autosave file calls `AutoSaveService.WaitForCompletion()` first.** Today
   that is `SaveLoadUI.PerformLoad` and `BuildWindow`'s Load-mode scan, plus `Game1.Dispose` (so
   process exit cannot kill a write mid-flight). `File.Move` keeps the destination intact even
   without this; the wait avoids an orphaned tmp and a stale preview.
4. **Never call `FileDataStore.Clear()`** anywhere: it deletes every file in the persistent folder,
   including a tmp the worker is writing.
5. **Presentation only.** No `PlayerCommand`, no `Nez.Random`, nothing the sim reads. The replay
   audit (`.claude/skills/replay-determinism/references/determinism-audit.md`) treats the autosave
   as a wall-clock consumer that must stay out of `Update()`.
6. **Save format unchanged.** An autosave is an ordinary `SaveData` at `CurrentVersion` — the same
   bytes a manual slot holds, just in a different file — so every backwards-compatibility rule in
   AGENTS.md applies to it unchanged. An unreadable or old-version autosave is skipped, exactly like
   a slot. Ordering uses the file's mtime rather than a saved timestamp, so per-hero autosaves needed
   no version bump.
7. **Files are deleted only on the main thread**, in `SetAutoSavePreview` → `EnforceAutoSaveCap`.
   The worker writes and nothing else.

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
- **AutoSave rows appear only in Load mode.** Save mode keeps the five manual slots so a player can
  never overwrite an autosave by hand. When no hero has an autosave, no autosave row is shown at all
  — an "empty" autosave row is meaningless once the rows are per-hero.
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

## Retention and file naming

One autosave per **hero**, not per save slot: three manual saves of the same hero still share one
autosave, and a second hero gets its own file instead of overwriting the first's.

- **The key is `GameStateService.HeroId`**, an `int` minted once per playthrough
  (`GenerateHeroId()`, called only from `TitleMenuUI` on New Game), stored in `SaveData.HeroId` and
  restored by `ApplyLoadedState`. It is stable across death, respawn, the crystal ceremony, job
  changes and promotion — a "new hero" after permadeath is the *same* playthrough and keeps writing
  to the same file. Files therefore accumulate once per **New Game**, never per death.
- **Filename** `autosave_<8 hex digits>.bin` (`GameConfig.AutoSaveFilePrefix` + `((uint)heroId)
  .ToString("X8")` + `AutoSaveFileExtension`). Hex because `HeroId` is an opaque 32-bit value that is
  often negative, and `autosave_-1234567.bin` is a poor filename.
- **The file's contents are authoritative.** `RefreshAutoSavePreviews()` globs the folder, loads each
  file and takes `HeroId` from the loaded `SaveData` rather than parsing the name, so there is one
  source of truth and no format/parse asymmetry.
- **Retention: `SaveLoadService.MaxAutoSaves` (5), evicting the least recently written.** Enforced in
  `SetAutoSavePreview` after a successful write, never on the worker. The hero currently playing is
  never the one evicted. Since an autosave lands every 30 s of play, last-write time is a faithful
  "last played" proxy. Chosen over protecting heroes that lack a manual save because it is
  predictable; the trade-off is that a playthrough whose only copy was its autosave can age out.
- **The legacy `autosave.bin`** from the original single-file implementation is adopted on the first
  scan (rewritten under its hero's name) and then deleted — see `MigrateLegacyAutoSave`.
- **Pre-v32 saves derive their id from the hero's design** (`SaveData.ComputeLegacyHeroId`: name,
  gender, colors, hairstyle), so two old playthroughs with an identical name *and* appearance share
  one id, and therefore one autosave. Accepted: it only affects saves written before `HeroId` existed.
- **A `HeroId` of 0** ("unknown", only reachable through odd paths) is a bucket like any other rather
  than a reason to skip autosaving — preserving the session beats tidy identity.
- **The Load window re-scans on open.** In-memory publishing keeps the *current* hero's entry fresh
  but can never discover files written by other heroes or in an earlier run, so `BuildWindow` calls
  `RefreshAutoSavePreviews()` in Load mode, the way `ReplayTab.Refresh()` re-reads its directory.

## Tests

| Area | Test file |
|---|---|
| Countdown, blocked reset, no overlapping write, thread affinity (gather/publish on caller, write on worker), faulting write recovery, null snapshot | `PitHero.Tests/AutoSaveServiceTests.cs` |
| Per-hero round-trip and rediscovery from disk, a second hero not overwriting the first, cap eviction of the least recently written, legacy-file migration, incompatible-version autosave skipped, stale tmp replaced and no tmp left behind | `PitHero.Tests/SaveLoadTests.cs` (`SaveLoadService_AutoSave_*`, `SaveLoadService_LegacyAutoSaveFile_*`, `FileDataStore_Save_ReplacesStaleTmpAndLeavesNoTmp`) |

The service takes its `gather` / `write` / `onSaved` delegates in the constructor, so tests run
without `Core`. Production wiring is in `Game1`.

## Manual verification checklist

1. New game, wait 30 s: the SaveIcon pulses lower-right for at least 1.5 s and one
   `%LOCALAPPDATA%\FeedTheHero\autosave_<hex>.bin` appears with no `.tmp` beside it. No frame hitch.
2. Settings → Session while the icon shows: Save is greyed and hovering it reads "Autosave in
   progress"; it re-enables when the icon goes away.
3. Window → Half size: the icon is the crisp 2x version and stays in the corner; opening any window
   swaps it to 1x live; Normal size restores 1x.
4. Quit to Title → Load: the tinted AutoSave rows come first, each with the Skullboy tag and its own
   hero preview; loading one restores that hero. With no autosaves at all, only the manual slots show.
5. **One per hero:** play hero A past an autosave, Quit to Title, New Game as hero B, autosave again.
   Load lists **two** AutoSave rows (B on top) and loading A still gives A — before this change B's
   autosave replaced A's. Three manual saves of one hero still leave that hero one autosave file.
6. Hero death → respawn walk → ceremony: no autosave and Save stays greyed until the ceremony ends;
   the next autosave lands 30 s after that, into the **same** file (no new one appears).
7. Retention: with 5 autosaves on disk, start a 6th playthrough and let it autosave — the folder
   still holds 5 and the least recently played one is gone.
8. Replay: Settings → Replay → Replay Current Session, seek back and forth. Status stays **In sync**
   and no icon appears during playback. Repeat from a loaded autosave.
