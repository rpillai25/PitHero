# PitHero Window Management

This document describes how PitHero configures its OS window as a horizontal strip docked at the
bottom of the screen, and how the strip can be re-docked, shrunk, moved between monitors and dragged
freely. All window work goes through `PitHero/WindowManager.cs`, which is pure **SDL3** (via the
SDL3-CS binding that ships with FNA) — there is no Win32 interop.

## The strip

- **Design resolution**: 1920×`GameConfig.VirtualHeight` (currently **264**; was 360, then 296).
  Scenes use `SceneResolutionPolicy.FixedHeight`, so the render target is always `VirtualHeight`
  tall and its width follows the window aspect (ultrawide monitors see more world).
- **Physical height**: `WindowManager.GetStripHeight(displayHeight)` scales the design height to
  the monitor — `displayHeight * VirtualHeight / ReferenceDisplayHeight` — so the strip renders 1:1
  at 1080p and 2× at 4K and the render target always maps to whole pixels. The width is the full
  display width.
- **Borderless, always-on-top** (`GameConfig.AlwaysOnTop`), so the game keeps running while the
  player works in other apps. The window is configured once from `Game1.LoadContent()`:

```csharp
WindowManager.ConfigureHorizontalStrip(this, alwaysOnTop: GameConfig.AlwaysOnTop);
```

### Why 264

`VirtualHeight` is the single knob for the strip's height. Every tall UI window fits itself to
`Stage.GetHeight()` at show time (`PitHero/UI/UILayout.cs`), and the Party inventory grid was
reshaped to 30 columns × 4 bag rows (still 120 slots, `InventoryGrid`) so the whole grid fits the tab
area without scrolling. See `PitHero/docs/SynergySystem.md` for what that means for stencils and
`BagLayoutMigration` for how older saves keep their item arrangement.

## Taskbar awareness

Vertical docking anchors to the display's **usable area** — `SDL_GetDisplayUsableBounds`, the
monitor bounds minus the OS taskbar/dock — rather than the raw monitor bounds:

- A bottom-docked strip sits on the top edge of the taskbar instead of covering it.
- A top-docked strip starts below a taskbar that has been moved to the top edge.
- Center docking centers inside the usable area.

`WindowManager.DockedY(mode, usableY, usableH, windowHeight, yOffset)` is the pure helper every
dock, shrink/restore and monitor-swap path uses (`PitHero.Tests/WindowDockTests.cs`). The strip's
width and height still come from the full display bounds, so `GetStripHeight` stays 1:1. If SDL
cannot report a usable area the full bounds are used, which reproduces the old behavior. An
auto-hide taskbar reports the full area, so the strip may cover its hidden edge.

## Docking, shrinking and monitors

`WindowManager` tracks a `DockMode` (`None`, `Top`, `Bottom`, `Center`) plus a fine-tuning Y offset
so later operations honor the player's choice:

| Method | What it does |
|---|---|
| `ConfigureHorizontalStrip(game, alwaysOnTop)` | Startup: full-width strip, `GetStripHeight` tall, docked bottom on the window's display |
| `DockTop / DockBottom / DockCenter(game, yOffset)` | Re-dock on the current display (Settings → Window tab) |
| `ShrinkToNextLevel(game)` / `RestoreOriginalSize(game)` | Toggle Normal ↔ Half size (both axes), keeping the dock anchor |
| `SwapToNextMonitor(game)` | Move to the next SDL display, re-sizing for that monitor and re-applying the dock |
| `ClearDockMode()` | Forget the dock so shrink/restore anchor to the window's current position (free-move mode) |
| `MoveWindowClampedToCurrentDisplay(game, x, y)` / `ClampRectToBounds(...)` | Move the window, clamped inside the current display's full bounds (display-origin aware) |
| `SetAlwaysOnTop(game, bool)` | Toggle topmost |

SDL3 window operations are asynchronous and DPI-context sensitive: when changing monitors, position
onto the target display first, `SDL_SyncWindow`, then resize, then re-assert the position.

### Free move mode

Settings → "Free Move Window" (issue #364) lets the player drag the strip anywhere with the mouse.
It clears the dock mode on entry, polls the global mouse position each frame, and clamps the rect to
the current display's full bounds — so a dragged window may cover the taskbar by choice. Docking or
swapping monitors afterwards re-applies taskbar-aware placement.

## Related

- `AGENTS.md` — project rules (design height, FixedHeight policy).
- `PitHero/docs/ReplaySystem.md` — window size/dock is view-only and is not recorded.
- `PitHero.Tests/UILayoutTests.cs`, `WindowClampTests.cs`, `WindowDockTests.cs` — the pure math.
