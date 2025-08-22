# PauseGame Functionality

## Overview
The PauseGame functionality allows all ECS components in the `PitHero.ECS.Components` namespace to be paused when the settings UI is shown. This provides a way to pause all game action while the user interacts with the settings.

## Architecture

### PauseService
The `PauseService` class provides global pause state management:
- Registered as a service using `Core.Services.AddService()` in `Game1.Initialize()`
- Accessible from anywhere using `Core.Services.GetService<PauseService>()`
- Provides `IsPaused` property and helper methods (`Pause()`, `Unpause()`, `Toggle()`)

### IPausableComponent Interface
Components that support pausing implement the `IPausableComponent` interface:
```csharp
public interface IPausableComponent
{
    bool ShouldPause { get; }
}
```

### Component Pause Pattern
All `IUpdatable` components in the `PitHero.ECS.Components` namespace check pause state:
```csharp
public void Update()
{
    // Check if game is paused
    var pauseService = Core.Services.GetService<PauseService>();
    if (pauseService?.IsPaused == true && ShouldPause)
        return;

    // Normal update logic...
}
```

## UI Integration
- Settings UI automatically sets `PauseGame = true` when the gear button is clicked and settings are shown
- Settings UI sets `PauseGame = false` when settings are hidden
- A transparent overlay (Color: 255,255,255,150) appears during pause using `PrototypeSpriteRenderer`
- Overlay is positioned at top-left corner spanning the entire window on `GameConfig.TransparentPauseOverlay` render layer

## Adding Pause Support to New Components

To add pause support to a new component:

1. Implement `IPausableComponent` interface
2. Add pause check at the beginning of `Update()` method
3. Set `ShouldPause` to `true` for game logic components, `false` for UI components

Example:
```csharp
public class MyGameComponent : Component, IUpdatable, IPausableComponent
{
    public bool ShouldPause => true; // Set to false for UI components

    public void Update()
    {
        var pauseService = Core.Services.GetService<PauseService>();
        if (pauseService?.IsPaused == true && ShouldPause)
            return;

        // Your update logic here
    }
}
```

## Current Component Support
- ✅ `HeroGoapAgentComponent` - Pauses game logic (ShouldPause = true)
- ✅ `TileByTileMover` - Pauses movement (ShouldPause = true)  
- ✅ `CameraControllerComponent` - Continues during pause for UI navigation (ShouldPause = false)

## Testing
Unit tests verify:
- `PauseService` basic functionality
- Component pause behavior with mock components
- Interface implementation correctness