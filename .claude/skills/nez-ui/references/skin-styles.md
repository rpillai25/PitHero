# PitHeroSkin, Styles, Fonts, Drawables

## Getting the Skin

```csharp
var skin = PitHeroSkin.CreateSkin();  // Cached singleton — safe to call repeatedly
```

`PitHeroSkin.CreateSkin()` returns a cached `Skin` instance. All styles are registered under `"ph-default"`. Nez's `Skin` stores styles by **type**, so multiple `Add("ph-default", ...)` calls with different style types don't conflict.

## Registered "ph-default" Styles

| Style Type | Notes |
|---|---|
| `LabelStyle` | Brown font color `(71, 36, 7)`, FontScale = 1f |
| `TextButtonStyle` | NinePatch Up/Down/Over, brown font, PressedOffset = 1,1 |
| `WindowStyle` | NinePatch background, brown title font |
| `CheckBoxStyle` | Checkbox sprites (Unchecked/Checked/Over) |
| `RadioButtonStyle` | Radio button sprites (Unselected/Selected) |
| `TabButtonStyle` | NinePatch Active/Inactive/Hover states |
| `TabWindowStyle` | Contains TabButtonStyle for TabPane |
| `ScrollPaneStyle` | VScroll/HScroll with NinePatch knobs |
| `SliderStyle` | PrimitiveDrawable background, knob sprites with states |

## Retrieving Styles

```csharp
var labelStyle  = skin.Get<LabelStyle>("ph-default");
var buttonStyle = skin.Get<TextButtonStyle>("ph-default");
var windowStyle = skin.Get<WindowStyle>("ph-default");
var tabStyle    = skin.Get<TabWindowStyle>("ph-default");
```

## Creating Elements with the Skin

```csharp
var label    = new Label("Text", skin);                    // uses default LabelStyle
var label    = new Label("Text", skin, "ph-default");      // explicit (preferred)
var button   = new TextButton("Click", skin);              // uses default TextButtonStyle
var checkbox = new CheckBox("Option", skin, "ph-default");
```

## Custom Styles (When Unique Colors Are Needed)

**Correct pattern** — create a new style instance:

```csharp
var defaultStyle = skin.Get<LabelStyle>("ph-default");
var blueLabelStyle = new LabelStyle
{
    Font       = defaultStyle.Font,
    FontColor  = new Color(0, 80, 156),
    FontScaleX = 1f,
    FontScaleY = 1f
};
var blueLabel = new Label("Special Text", blueLabelStyle);
```

**WRONG** — never mutate the shared style:

```csharp
// DO NOT — mutates style for ALL labels using "ph-default"
label.GetStyle().FontColor = Color.Red;
```

## Common Font Colors

| Color | RGB | Usage |
|---|---|---|
| Brown (default) | `(71, 36, 7)` | Standard UI text |
| Blue (detail) | `(37, 80, 112)` | Detail/info text |
| Green | `Color.Green` | Positive stat changes |
| Red | `Color.Red` | Negative stat changes |
| Gray | `Color.Gray` | Disabled/unavailable text |

## Font Paths (GameConfig)

| Constant | Path | Usage |
|---|---|---|
| `FontMainUI` | `Content/Fonts/Express.fnt` | Main UI font |
| `FontPathHud` | `Content/Fonts/Skullboy.fnt` | HUD font (normal) |
| `FontPathHud2x` | `Content/Fonts/Skullboy2x.fnt` | HUD font (2x for half-size mode) |
| `FontPathHudSmall` | `Content/Fonts/CratesSmall.fnt` | Small HUD font |

## Drawable System

### IDrawable Implementations

| Type | Usage | Example |
|---|---|---|
| `PrimitiveDrawable` | Colored rectangles | `new PrimitiveDrawable(Color.Black)` |
| `NinePatchDrawable` | Scalable bordered panels | `new NinePatchDrawable(sprite, left, right, top, bottom)` |
| `SpriteDrawable` | Static sprites | `new SpriteDrawable(sprite)` |

### NinePatch from Atlas (PitHero pattern)

```csharp
var uiAtlas    = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
var sprite     = uiAtlas.GetSprite("NinePatchButton_Up");
var ninePatch  = new NinePatchSprite(sprite, 4, 4, 4, 4);  // left, right, top, bottom margins
var drawable   = new NinePatchDrawable(ninePatch);
drawable.SetPadding(0, 0, 25, 25);   // force text centering
```

### PrimitiveDrawable for Simple Backgrounds

```csharp
// Button from colors (set min size — texture is 1×1)
var button = new Button(ButtonStyle.Create(Color.Black, Color.DarkGray, Color.Green));
table.Add(button).SetMinWidth(100).SetMinHeight(30);
```

## NinePatch Window Background

The standard `WindowStyle` uses the `NinepatchWindowBackground` sprite from `UI.atlas` with bounds **(24, 24, 24, 24)**:

```csharp
var ninePatch       = new NinePatchSprite(ninepatchSprite, 24, 24, 24, 24);
var windowBackground = new NinePatchDrawable(ninePatch);
```

## TabPane Padding Rule

When a `TabPane` is the only child of a `Window`, kill padding at **both** levels so tabs sit flush with the window edge:

```csharp
_window.Pad(0);                                       // Remove window's internal Table padding
_window.Add(_tabPane).Expand().Fill().Pad(0);         // Remove cell padding
```

Otherwise the default Window padding pushes the tabs in by several pixels and they look misaligned.

## Window Title Convention

For tabbed windows (`HeroUI`, `SettingsUI`), use an **empty title** — the tabs themselves provide context ("Inventory / Hero Crystal / Behavior", "Window / Session"). For untabbed panels (`StencilLibraryPanel`), keep the title.

```csharp
_window = new Window("", windowStyle);   // tabbed window — no title
```

Empty title avoids visual clutter against the NinePatch background.
