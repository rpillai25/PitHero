# Event Handling, Custom Elements, IInputListener, Gamepad

## Button Clicks

```csharp
button.OnClicked += (btn) => HandleClick();
```

## CheckBox / Toggle

```csharp
checkbox.OnChanged += (isChecked) => { /* handle toggle */ };
```

## Slider / EnhancedSlider

```csharp
// Continuous updates
slider.OnChanged += (value) => UpdateLabel(value);

// Deferred commit (EnhancedSlider only — fires on mouse release)
enhancedSlider.OnValueCommitted += (value) => ApplySetting(value);
```

## Custom Events (Delegate Pattern)

```csharp
public event System.Action<IItem, InventorySlot> OnItemHovered;
public event System.Action                       OnItemUnhovered;

// Fire events
OnItemHovered?.Invoke(item, slot);
```

## IInputListener Interface

Implement `IInputListener` for custom mouse/touch handling:

```csharp
public interface IInputListener
{
    void OnMouseEnter();
    void OnMouseExit();
    bool OnLeftMousePressed(Vector2 mousePos);   // return true to track
    bool OnRightMousePressed(Vector2 mousePos);
    void OnMouseMoved(Vector2 mousePos);
    void OnLeftMouseUp(Vector2 mousePos);
    void OnRightMouseUp(Vector2 mousePos);
    bool OnMouseScrolled(int mouseWheelDelta);   // return true to consume
}
```

## PitHero Custom Element Classes

PitHero extends Nez UI classes for custom behavior. **Preferred:** inherit and override first; only duplicate if inheritance is insufficient.

| PitHero Class | Base Class | Purpose |
|---|---|---|
| `HoverableImageButton` | `ImageButton` | ImageButton with tooltip hover (`Draw()` polling) |
| `HoverableTextButton` | `TextButton` | TextButton with windowed cursor tooltip; suppresses on click |
| `HoverableLabel` | `Label` + `IInputListener` | Label with windowed cursor tooltip |
| `ResettableTextButton` | `TextButton` | TextButton with state reset |
| `EnhancedSlider` | `ProgressBar` + `IInputListener` | Slider with deferred commit |
| `ConfirmationDialog` | `Window` | Yes/No dialog |
| `ItemCard` | `Window` | Item stats display |
| `StencilLibraryPanel` | `Window` | Scrollable stencil grid |
| `MercenaryHireDialog` | `Table` | Mercenary stats + hire button |
| `ReorderableTableList<T>` | `Table` | Generic reorderable list |
| `InventoryGrid` | `Group` | 20×9 slot grid with synergy |
| `ShortcutBar` | `Group` | 8-slot horizontal bar |
| `DragDropOverlay` | `Element` | Ghost sprite that follows cursor during drag |
| `ItemCardTooltip` | `Tooltip` | Cached item stat card that follows cursor |

## Gamepad Input

Enable gamepad navigation by setting the first focusable element:

```csharp
stage.SetGamepadFocusElement(firstButton);
```

For explicit focus control (required for Sliders):

```csharp
leftButton.ShouldUseExplicitFocusableControl = true;
leftButton.GamepadRightElement = middleSlider;
leftButton.GamepadLeftElement  = rightButton;   // optional wrap-around

middleSlider.ShouldUseExplicitFocusableControl = true;
middleSlider.GamepadLeftElement  = leftButton;
middleSlider.GamepadRightElement = rightButton;
```

Default action button: A (gamepad) / Enter (keyboard). Customizable via `stage.GamepadActionButton` and `stage.KeyboardActionKey`.
