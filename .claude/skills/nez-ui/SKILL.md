---
name: nez-ui
description: "**DOMAIN SKILL** — Nez UI framework best practices, PitHero UI conventions, and UI implementation workflow. USE FOR: building UI layouts with Table/Window/TabPane/ScrollPane, custom UI elements (HoverableImageButton, HoverableTextButton, HoverableLabel, EnhancedSlider), PitHeroSkin/style management, event handling (OnClicked, IInputListener), drag-and-drop with InventoryDragManager/DragDropOverlay, ConfirmationDialog and dialog patterns, ItemCardTooltip caching, periodic hover safety, gamepad navigation, drawable system (NinePatch/Primitive/Sprite), build/test validation for UI changes, and all Nez.UI work in PitHero. DO NOT USE FOR: non-UI game logic, rendering pipelines unrelated to UI, ECS components unrelated to UI."
applyTo: "**/*UI*.cs,**/UI/**,**/*Dialog*.cs,**/*Window*.cs,**/*Tab*.cs,**/*Skin*.cs,**/*Menu*.cs"
---

# Nez UI — PitHero Conventions

Nez UI is based on [TableLayout](https://github.com/EsotericSoftware/tablelayout) and libGDX Scene2D. Key terminology:
- libGDX `Actor` = Nez `Element`
- libGDX `Widget`/`WidgetGroup` don't exist in Nez — use `Element` and `Group`

## CRITICAL RULES (Never Violate)

1. **NEVER use `SetFontScale()`** on any UI element. To scale a font, load a larger bitmap font file instead.
2. **Always use the `"ph-default"` style** for `PitHeroSkin` elements unless a unique style is explicitly requested.
3. **Never mutate `"ph-default"` style properties** — do NOT do `element.GetStyle().FontColor = someColor`. If a unique color is needed, create a **new style instance** with the desired color.
4. **Use `HoverableImageButton`** instead of Nez `ImageButton` (tooltip hover support).
5. **Use `HoverableTextButton`** instead of Nez `TextButton` when a hover tooltip is needed (windowed tooltip following cursor, suppresses on click).
6. **Use `HoverableLabel`** instead of Nez `Label` when a hover tooltip is needed.
7. **Use `EnhancedSlider`** instead of Nez `Slider` (deferred value commit on mouse release).
8. **Use `PausableSpriteAnimator`** instead of `SpriteAnimator`.
9. **For new UI needs:** inherit and override Nez classes first; only duplicate Nez elements if inheritance is insufficient.
10. **AOT compliance:** No `foreach`, no LINQ in UI update loops. Use `for` loops. Pre-allocate collections. Avoid `new` during gameplay.

## UICanvas Setup

```csharp
SetDesignResolution(GameConfig.VirtualWidth, GameConfig.VirtualHeight, SceneResolutionPolicy.BestFit);

var screenSpaceRenderer = new ScreenSpaceRenderer(100, GameConfig.RenderLayerUI);
AddRenderer(screenSpaceRenderer);

var uiEntity = CreateEntity("ui-overlay");
var uiCanvas = uiEntity.AddComponent(new UICanvas());
uiCanvas.IsFullScreen = true;                    // true = screen-space; false = world-space
uiCanvas.RenderLayer = GameConfig.RenderLayerUI; // 998
_myUI.InitializeUI(uiCanvas.Stage);
```

### Render Layers

| Constant | Value | Purpose |
|---|---|---|
| `RenderLayerActionQueue` | 996 | Action queue (screen space) |
| `RenderLayerGraphicalHUD` | 997 | Graphical HUD |
| `RenderLayerUI` | 998 | UI layer (always on top) |
| `TransparentPauseOverlay` | 999 | Transparent overlay |

## What to Read Next (Progressive Disclosure)

| If you are working on… | Read |
|---|---|
| Tables, Windows, TabPanes, ScrollPanes, layout/alignment, cell config | `references/table-layout.md` |
| PitHeroSkin, styles, fonts, font colors, drawable system (NinePatch/Primitive/Sprite) | `references/skin-styles.md` |
| Drag-and-drop slots, `InventoryDragManager`, `DragDropOverlay`, cross-panel drops, hit-testing | `references/drag-drop.md` |
| Dialogs (`ConfirmationDialog`), tooltips (`HoverableLabel`/`HoverableTextButton`/`ItemCardTooltip`), tooltip caching, periodic hover safety | `references/dialogs-tooltips.md` |
| Event handling (`OnClicked`, `OnChanged`), `IInputListener`, custom element class list, gamepad navigation | `references/event-handling.md` |
| Validation workflow — build/test loop, inheritance preference, live strip integration | `references/implementation-checklist.md` |

## Quick UI Class Skeleton

```csharp
public class MyFeatureUI
{
    private Stage _stage;
    private Skin _skin;
    private Window _mainWindow;
    private Label _statusLabel;

    /// <summary>Initializes UI on the given stage.</summary>
    public void InitializeUI(Stage stage)
    {
        _stage = stage;
        _skin = PitHeroSkin.CreateSkin();
        CreateMainWindow();
    }

    private void CreateMainWindow()
    {
        var windowStyle = _skin.Get<WindowStyle>("ph-default");
        _mainWindow = new Window("My Feature", windowStyle);
        _mainWindow.SetSize(400, 300);

        var contentTable = new Table();
        contentTable.Pad(10f);

        _statusLabel = new Label("Ready", _skin, "ph-default");
        contentTable.Add(_statusLabel).Left().SetPadBottom(8f);
        contentTable.Row();

        var actionButton = new TextButton("Do Thing", _skin, "ph-default");
        actionButton.OnClicked += (btn) => HandleAction();
        contentTable.Add(actionButton).Width(100).Height(30);

        _mainWindow.Add(contentTable).Expand().Fill();

        _mainWindow.SetPosition(
            (_stage.GetWidth() - _mainWindow.GetWidth()) / 2f,
            (_stage.GetHeight() - _mainWindow.GetHeight()) / 2f);

        _stage.AddElement(_mainWindow);
    }
}
```

## Quick Gotchas

| Gotcha | Fix |
|---|---|
| Text too small / too large | Load a different font file — never `SetFontScale()` |
| Style colors bleeding across elements | Create a **new** style instance; never mutate `"ph-default"` |
| Button has no visible size | Set `.SetMinWidth()` / `.SetMinHeight()` on PrimitiveDrawable buttons |
| ScrollPane not scrolling | Ensure content is taller than ScrollPane; use `.SetScrollingDisabled(true, false)` for vertical-only |
| Window not centered | `(stage.GetWidth() - window.GetWidth()) / 2f` |
| Tab content not filling | Chain `.Expand().Fill()` on the TabPane cell |
| UI not responding to input | Check `SetTouchable(Touchable.Enabled)` and render-layer order |
| Drag source sprite disappears | Always call `InventoryDragManager.EndDrag()` or `CancelDrag()` |
| Item tooltip doesn't appear on hover | Add periodic hover check every 5 frames (see `references/dialogs-tooltips.md`) |
| Item tooltip slow / rebuilds every hover | `ItemCardTooltip.ShowItem()` already caches by reference — `InvalidateCache()` on window open / shop refresh |

## Element Lifecycle

- **Add to stage:** `stage.AddElement(element)`
- **Remove from stage:** `element.Remove()`
- **Hide but keep in hierarchy:** `element.SetVisible(false)`
- **Dialog self-removal:** call `Remove()` in button callbacks
