# HUD UI Updates - Implementation Summary

## Overview
Updated the HUD layout to improve readability and positioning of UI elements.

## Changes Made

### 1. Pit Level Display - No Scaling & Bottom-Left Position

**Before:**
- Pit Level display was positioned at top-left
- Scaled font size based on window mode (Normal vs Half)
- Position shifted vertically based on window mode

**After:**
- Pit Level display now positioned at **bottom-left corner** (X: 10, Y: 330)
- **No scaling** - always uses normal HUD font regardless of window size
- Fixed position - does not shift with window mode changes

**Code Changes:**
- Updated `PitLabelBaseX` and `PitLabelBaseY` constants
- Removed font style switching in `UpdateHudFontMode()`
- Removed position offset logic for Pit Level label

### 2. Funds Display Added

**New Feature:**
- Added "Gold: {amount}" display next to Pit Level (X: 100, Y: 330)
- Shows current global Funds value from `GameStateService`
- Updates automatically when Funds change (no string allocation if value unchanged)
- Uses same font as Pit Level (normal HUD font, no scaling)

**Implementation:**
```csharp
// New fields
private Label _fundsLabel;
private int _lastDisplayedFunds = -1;

// New method
private void UpdateFundsLabel()
{
    if (_fundsLabel == null) return;
    var gameState = Core.Services.GetService<GameStateService>();
    if (gameState == null) return;
    
    var currentFunds = gameState.Funds;
    if (currentFunds != _lastDisplayedFunds)
    {
        _fundsLabel.SetText($"Gold: {currentFunds}");
        _lastDisplayedFunds = currentFunds;
    }
}
```

### 3. Hero & Mercenary HUDs Shifted Left

**Before:**
- Hero HUD at X: 110 (to avoid overlapping Pit Lv at top-left)
- Additional X offset of 110 in half-height mode
- Mercenary HUDs positioned relative to Hero HUD

**After:**
- Hero HUD now at X: 10 (leftmost position, filling space left by moved Pit Lv)
- Mercenary #1 HUD at X: 180 (10 + 170 spacing)
- Mercenary #2 HUD at X: 350 (10 + 170*2 spacing)
- **No additional offset** in half-height mode (set to 0)

**Code Changes:**
```csharp
// Updated constants
private const float GraphicalHudBaseX = 10f;  // Was 110f
private const float GraphicalHudHalfModeXOffset = 0f;  // Was 110f

// Updated positioning logic in UpdateHudFontMode()
float hudTargetX = GraphicalHudBaseX; // No extra offset needed
```

## Layout Diagram

### Before:
```
Top-Left Area:
???????????????????????????????????
? Pit Lv. 1    [Hero HUD at 110]  ?
?              [Merc1 at 280]     ?
?              [Merc2 at 450]     ?
???????????????????????????????????
```

### After:
```
Top Area (HUDs):
???????????????????????????????????
? [Hero HUD][Merc1 HUD][Merc2 HUD]?  (at 10, 180, 350)
?                                 ?
???????????????????????????????????

Bottom-Left Area (Labels):
???????????????????????????????????
?                                 ?
? Pit Lv. 1    Gold: 0           ?  (at Y: 330)
???????????????????????????????????
```

## Technical Details

### Position Constants (MainGameScene.cs)

| Element | X Position | Y Position | Notes |
|---------|-----------|-----------|-------|
| Pit Level Label | 10 | 330 | Fixed, no scaling |
| Funds Label | 100 | 330 | Fixed, no scaling |
| Hero HUD | 10 | 4 + offset* | Top-left |
| Mercenary #1 HUD | 180 | 4 + offset* | 170px spacing |
| Mercenary #2 HUD | 350 | 4 + offset* | 170px spacing |

\* offset = `TopUiYOffsetNormal` or `TopUiYOffsetHalf` based on window mode

### Performance Optimizations

1. **String Allocation Prevention:**
   - `_lastDisplayedPitLevel` and `_lastDisplayedFunds` track previous values
   - `SetText()` only called when values actually change
   - Avoids per-frame string allocations

2. **Removed Unnecessary Logic:**
   - Removed font style switching for Pit Level
   - Removed position offset calculations for Pit Level
   - Removed half-mode X offset for HUDs

### Files Modified

- `PitHero/ECS/Scenes/MainGameScene.cs`
  - Added `_fundsLabel` field
  - Added `_lastDisplayedFunds` field
  - Added `FundsLabelBaseX` and `FundsLabelBaseY` constants
  - Updated `PitLabelBaseX` and `PitLabelBaseY` constants
  - Updated `GraphicalHudBaseX` constant (110 ? 10)
  - Updated `GraphicalHudHalfModeXOffset` constant (110 ? 0)
  - Added `UpdateFundsLabel()` method
  - Modified `UpdateHudFontMode()` to remove Pit Lv scaling
  - Modified `SetupUIOverlay()` to create Funds label

## Testing Performed

? Build successful
? No compilation errors
? Follows existing code patterns (similar to UpdatePitLevelLabel)

## Integration Notes

The Funds display integrates with the existing GoldYield system:
- Automatically updates when monsters are defeated
- Shows global Funds from `GameStateService`
- Ready for shop/merchant purchasing features

## Visual Result

Players will now see:
- **Bottom-Left:** Pit Level and Gold amount (always visible, always same size)
- **Top-Left:** Hero and Mercenary HUDs (shifted left to use freed space)
- Cleaner separation between game state info (bottom) and character info (top)
