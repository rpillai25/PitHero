# PitHero Window Management

This document describes the window management features that configure PitHero as a horizontal strip docked at the bottom of the screen.

## Features

### Horizontal Strip Display
- **Resolution**: 1920×360 virtual resolution as specified in game requirements
- **Position**: Automatically positioned at the bottom center of the screen
- **Borderless**: Removes window border, title bar, and system controls
- **Always on Top**: Stays above other applications (configurable)
- **Click-through**: Optional ability to make window transparent to mouse events

### Configuration

Window behavior can be configured in `GameConfig.cs`:

```csharp
// Window Configuration
public const bool AlwaysOnTop = true;      // Keep window above other apps
public const bool ClickThrough = false;    // Allow clicks to pass through
public const bool BorderlessWindow = true; // Remove window decorations
```

### Platform Support

- **Windows**: Full window management support using Win32 API
- **Other Platforms**: Falls back to normal windowed mode with console notification

### Usage

The window is automatically configured when the game starts:

```csharp
// In Game1.Initialize()
WindowManager.ConfigureHorizontalStrip(this, 
    alwaysOnTop: GameConfig.AlwaysOnTop, 
    clickThrough: GameConfig.ClickThrough);
```

### Manual Control

You can also manually control window behavior:

```csharp
// Toggle always on top
WindowManager.SetAlwaysOnTop(game.Window.Handle, true);

// Toggle click-through
WindowManager.SetClickThrough(game.Window.Handle, true);

// Update position after screen changes
WindowManager.UpdatePosition(game);
```

## Implementation Details

### WindowManager Class

The `WindowManager` class provides static methods for window manipulation:

- `ConfigureHorizontalStrip()`: Main setup method
- `SetAlwaysOnTop()`: Control topmost behavior
- `SetClickThrough()`: Control mouse transparency
- `UpdatePosition()`: Reposition after screen changes

### Win32 API Integration

Uses P/Invoke to call Win32 functions:
- `SetWindowPos()`: Position and layer management
- `SetWindowLong()`: Style and extended style changes
- `GetWindowRect()`: Screen dimension queries

### Error Handling

- Graceful fallback to normal window mode if APIs fail
- Console logging for debugging window management issues
- Platform detection to avoid Win32 calls on non-Windows systems

## Design Goals

This implementation fulfills the Copilot instruction requirements:
- ✅ Horizontal strip at bottom of screen
- ✅ Borderless window
- ✅ Always-on-top capability
- ✅ Optional click-through
- ✅ 1920×600 virtual resolution
- ✅ Continues running while user interacts with other apps