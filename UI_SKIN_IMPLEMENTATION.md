# UI Skin Implementation

## Overview
Implemented centralized UI skin system for PitHero using Nez.UI skinning capabilities. All UI windows now use a custom NinePatch window background and consistent brown font color for readability.

## Font Color
All UI text uses a custom brown color that provides good contrast against the NinePatch background:
- **RGB**: (71, 36, 7)
- **Hex**: #472407

Applied to all UI elements:
- Labels
- Text Buttons (normal, hover, and pressed states)
- Checkboxes
- Radio Buttons

## Changes Made

### 1. Created PitHeroSkin.cs
- **Location**: `PitHero/UI/PitHeroSkin.cs`
- **Purpose**: Centralized skin factory that provides consistent styling across all UI elements
- **Key Features**:
  - Extends Nez's `Skin.CreateDefaultSkin()` with custom window background
  - Uses NinePatchDrawable with "NinepatchWindowBackground" texture from UI.atlas
  - NinePatch bounds: 24px (left, right, top, bottom)
  - Caches skin instance for performance
  - Provides `ClearCache()` method for content reloading scenarios

### 2. Updated UI Files to Use PitHeroSkin
Replaced all instances of `Skin.CreateDefaultSkin()` with `PitHeroSkin.CreateSkin()`:

#### Updated Files:
1. **HeroUI.cs**
   - `InitializeUI()` method
   - `ShowRemoveStencilConfirmation()` method
   - `CreateHeroWindow()` - Changed window title to empty string (tabs provide context)

2. **TitleMenuUI.cs**
   - `InitializeUI()` method

3. **SettingsUI.cs**
   - `InitializeUI()` method
   - `CreateSettingsWindow()` - Changed window title to empty string (tabs provide context)

4. **FastFUI.cs**
   - `InitializeUI()` method

5. **StencilLibraryPanel.cs**
   - `BuildUI()` method (removed redundant skin creation for title label)

### Window Title Changes
To prevent visual clutter with the new NinePatch backgrounds, window titles were removed from:
- **HeroUI**: Empty title (tabs clearly show Inventory/Hero Crystal/Behavior)
- **SettingsUI**: Empty title (tabs clearly show Window/Session)
- **StencilLibraryPanel**: Retains "Stencil Library" title (no tabs, title provides context)

## Technical Details

### NinePatch Configuration
```csharp
var ninePatch = new NinePatchSprite(ninepatchSprite, 24, 24, 24, 24);
var windowBackground = new NinePatchDrawable(ninePatch);
```
- **Left**: 24px
- **Right**: 24px
- **Top**: 24px
- **Bottom**: 24px

### Window Style
```csharp
var windowStyle = new WindowStyle
{
    Background = windowBackground
};
skin.Add("default", windowStyle);
```

### CheckBox Style
```csharp
var checkboxStyle = new CheckBoxStyle
{
    CheckboxOff = new SpriteDrawable(checkboxUnchecked),
    CheckboxOn = new SpriteDrawable(checkboxChecked),
    CheckboxOver = new SpriteDrawable(checkboxUncheckedHover) // Hover when UNCHECKED only
};
skin.Add("default", checkboxStyle);
```
- **Unchecked**: UICheckbox_Unchecked (16x16px)
- **Checked**: UICheckbox_Checked (16x16px)
- **Hover (unchecked only)**: UICheckbox_Unchecked_Highlight (16x16px) - falls back to checked sprite if not available

**Note**: Nez's CheckBox only supports hover visuals for the unchecked state. When the checkbox is already checked, hovering over it shows no visual change (it stays in the checked state).

### Radio Button Style
```csharp
var radioButtonStyle = new CheckBoxStyle
{
    CheckboxOff = new SpriteDrawable(radioButtonUnselected),
    CheckboxOn = new SpriteDrawable(radioButtonSelected),
    CheckboxOver = new SpriteDrawable(radioButtonUnselectedHover) // Hover when UNSELECTED only
};
skin.Add("radio", radioButtonStyle);
```
- **Unselected**: UIRadioButton_Unselected (16x16px)
- **Selected**: UIRadioButton_Selected (16x16px)
- **Hover (unselected only)**: UIRadioButton_Unselected_Highlight (16x16px) - falls back to selected sprite if not available
- Used for mutually exclusive options (e.g., window size selection)

**Note**: Nez's CheckBox only supports hover visuals for the unselected state. When the radio button is already selected, hovering over it shows no visual change (it stays in the selected state).

### ScrollPane Style
```csharp
var scrollPaneStyle = new ScrollPaneStyle
{
    VScroll = vScrollDrawable,       // NinePatchScroll (3,3,3,3), minWidth=10, minHeight=0
    VScrollKnob = vScrollKnobDrawable, // NinePatchScrollKnob (1,1,1,1), minWidth=4, minHeight=25, centered with LeftWidth=3, RightWidth=3
    HScroll = hScrollDrawable,       // NinePatchScroll (3,3,3,3), minWidth=0, minHeight=10
    HScrollKnob = hScrollKnobDrawable  // NinePatchScrollKnob (1,1,1,1), minWidth=25, minHeight=4, centered with TopHeight=3, BottomHeight=3
};
skin.Add("default", scrollPaneStyle);
```
- **VScroll**: NinePatchScroll texture, bounds (3,3,3,3), minWidth=10, minHeight=0
- **VScrollKnob**: NinePatchScrollKnob texture, bounds (1,1,1,1), minWidth=4, minHeight=25
  - Centered horizontally with LeftWidth=3, RightWidth=3 (knob width 4px centered in 10px scroll bar)
- **HScroll**: NinePatchScroll texture, bounds (3,3,3,3), minWidth=0, minHeight=10
- **HScrollKnob**: NinePatchScrollKnob texture, bounds (1,1,1,1), minWidth=25, minHeight=4
  - Centered vertically with TopHeight=3, BottomHeight=3 (knob height 4px centered in 10px scroll bar)
- Applied to all ScrollPane instances using PitHeroSkin

### Creating Hover Sprites

To add proper hover graphics, create these sprites in your UI atlas (16x16px each):
1. **UICheckbox_Unchecked_Highlight** - A lighter/brighter/outlined version of the unchecked checkbox
2. **UIRadioButton_Unselected_Highlight** - A lighter/brighter/outlined version of the unselected radio button

The code automatically detects if these sprites exist and uses them; otherwise it falls back to using the checked/selected sprites for hover feedback.

## Benefits

1. **Consistency**: All windows use the same styled background
2. **Maintainability**: Single point of change for UI styling
3. **Performance**: Skin is cached and reused across all UI components
4. **Scalability**: Easy to add more custom styles in the future

## Future Enhancements

The PitHeroSkin system can be extended to include:
- Custom button styles
- Custom text field styles
- Custom scroll pane styles
- Theme variations (light/dark modes)
- Color schemes based on game state

## Build Status

? Build successful
? All UI components compile correctly
?? One pre-existing test failure (unrelated to skin changes)

## Testing

All UI windows (Hero, Settings, Title Menu, Stencil Library, FastF button) now display with the custom NinePatch window background when opened in-game.
