using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// The reusable color palette button (issue #467): the UIColorPalette icon (a brown painter's palette
    /// with a white-outlined brush) inside a standard nine-patch button. Clicking it is the caller's
    /// cue to open a <see cref="ColorPickerDialog"/>.
    /// </summary>
    public static class ColorPaletteButton
    {
        /// <summary>Button width, sized to sit beside the creation screen's 24 px tall arrow buttons.</summary>
        public const float Width = 28f;

        /// <summary>Button height.</summary>
        public const float Height = 24f;

        /// <summary>Name of the icon in the UI atlas.</summary>
        public const string IconSpriteName = "UIColorPalette";

        /// <summary>Makes a palette button with the given hover text.</summary>
        public static HoverableImageButton Create(string hoverText)
        {
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            // Own nine-patch drawables: the skin's ph-default button pads its Up drawable 25 px each side
            // to center text, which would push a small icon button wide.
            var style = new ImageButtonStyle
            {
                Up = new NinePatchDrawable(new NinePatchSprite(uiAtlas.GetSprite("NinePatchButton_Up"), 4, 4, 4, 4)),
                Down = new NinePatchDrawable(new NinePatchSprite(uiAtlas.GetSprite("NinePatchButton_Down"), 4, 4, 4, 4)),
                Over = new NinePatchDrawable(new NinePatchSprite(uiAtlas.GetSprite("NinePatchButton_Over"), 4, 4, 4, 4)),
                ImageUp = new SpriteDrawable(uiAtlas.GetSprite(IconSpriteName)),
                PressedOffsetX = 1,
                PressedOffsetY = 1
            };
            var button = new HoverableImageButton(style, hoverText);
            button.GetImage().SetScaling(Scaling.None);
            return button;
        }
    }
}
