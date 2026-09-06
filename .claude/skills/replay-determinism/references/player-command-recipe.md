# Adding a Player Command

Every way the player changes simulation state is a `PlayerCommand`. The UI enqueues it during the
presentation pass; `PlayerCommandService.Drain` applies it at the end of the simulation tick and the
recorder logs `(tick, command)`. On playback the same command is injected on the same tick.

## Is it a command?

| Player does… | Command? |
|---|---|
| Buys, sells, equips, drags, uses an item, places a stencil | Yes |
| Toggles an automation flag, moves a threshold slider, picks a tactic/priority | Yes |
| Hires/dismisses, sets a monster job, buys a monster | Yes |
| Tills, plants, places/moves/sells a building, fridge actions | Yes |
| Opens Settings (pauses the sim) | Yes — `PauseService` already routes `SetManualPause` |
| Pans/zooms the camera, resizes/docks the window, toggles fast-forward | No (view-only) |
| Hovers, opens a tooltip, scrolls the event console, opens a window that does not pause | No |
| Presses a hotkey that does any of the "Yes" rows | Yes — the hotkey handler dispatches the same command as the button |

## Steps

### 1. Append the enum member

`PitHero/Services/Replay/PlayerCommand.cs`, inside the matching group, **at a new number** (values are
stored in replay files; never renumber or reuse). Document the payload inline:

```csharp
SetCropKeepStacks = 116,        // A = stacks
```

Payload slots: `A,B,C,D` ints, `L` long, `F` float, `S` string. Use `S` only for names/ids that have no
stable index. Pack an inventory cell with `SlotRefCodec.Pack(slotType, x, y)`.

### 2. Write the handler

`PlayerCommandHandlers.cs` (`Apply` switch) or `PlayerCommandHandlers.Extended.cs` for the larger
groups. Handlers are static, resolve everything through services/scene accessors, and **re-validate**:

```csharp
case PlayerCommandType.RestoreGrassTile:
{
    var tile = new Point(cmd.A, cmd.B);
    Services?.GetService<WetTileService>()?.ClearWet(tile);
    Services?.GetService<TilledTileService>()?.RestoreGrassTile(tile);
    return true;
}
```

Rules for the handler body:
- Resolve targets by **stable identity** carried in the payload: index, tile, building id, merc name,
  item name. Never by "the currently selected row" or "the open dialog's item".
- Check what the UI checked (funds, slot empty, entity alive, index in range) and **no-op on
  failure**. Live and replay must take the same branch even if the UI would have greyed the button.
- Do not dispatch further commands from inside a handler (`IsApplying` is true; nested enqueues run
  on the next drain and shift ticks). Call the underlying methods directly.
- No UI side effects that the sim reads. Refreshing a grid is fine; deciding a stat from a label is not.

### 3. Dispatch from the UI

Replace the direct mutation:

```csharp
_button.OnClicked += (_) => PlayerCommandService.Dispatch(
    new PlayerCommand(PlayerCommandType.SetCropKeepStacks, stacks));
```

`Dispatch` queues during a session and applies immediately when no service exists (title/creation
scenes, headless tests), so the same UI code works everywhere.

When one method serves both the UI path and the handler path (the handler calls it, and headless
tests call it too), branch on `ShouldApplyDirectly`:

```csharp
if (PlayerCommandService.ShouldApplyDirectly)
    SetShortcutReference(index, source);                  // handler / headless path
else
    PlayerCommandService.Dispatch(new PlayerCommand(PlayerCommandType.SetShortcutItem, index, bagIndex));
```

`ShouldApplyDirectly` is true with no service or while a handler is running, so the handler calling
`SetShortcutReference` does not re-enqueue.

### 4. UI-time randomness

If the action's outcome is rolled at UI time (crystal-creation stats, random names), roll on
`GameRandom.Ui` **in the UI** and put the result in the payload (`CreateCrystal` carries B/C/D/L). The
handler never rolls.

### 5. Between-step application (rare)

`PlayerCommandService.ApplyNow` applies immediately and records at `CurrentTick - 1`. It exists for the
moments the normal drain can never run: releasing the settings pause right before playback restarts
the scene, and exit bookkeeping. Do not use it as a shortcut; a normal `Dispatch` lands on the very
next tick.

### 6. Verify

1. `dotnet build PitHero.sln` and `dotnet test PitHero.Tests/PitHero.Tests.csproj` (there is a
   `PlayerCommandServiceTests` suite; extend it if you added packing helpers).
2. Run the game, perform the action a few times, then **Settings → Replay → Replay Current Session**.
3. Watch past the action; seek backward over it and forward again. The scrubber must read **In sync**.
4. If it reads "Diverged at", open `replay_divergence.log` next to the replay files and see
   `references/determinism-audit.md`.

## Things that look like commands but are not

- **Sim-driven changes** (the hero sells loot, a merc levels, auto-hire fires): they happen inside the
  step and are already deterministic. Do not wrap them.
- **Automation "run now" side effects on window close** (`FarmRescan`, `AutoHirePass`): these ARE
  commands because the close click is a player action that changes when the sim does the work.
- **Window open/close** on its own: not a command, unless it pauses (then `PauseService` handles it).
