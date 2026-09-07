using Microsoft.Xna.Framework;
using Nez;
using Nez.BitmapFonts;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// A <see cref="HoverableImageButton"/> that draws a short label centered over its icon — used by
    /// the fast-forward button to show the engaged speed rung (2X / 4X / 8X). Nez has no Stack widget
    /// and ImageButton has no text slot, so the label is drawn directly after base.Draw, the same way
    /// the inventory slots paint their stack counts.
    /// </summary>
    public class SpeedOverlayImageButton : HoverableImageButton
    {
        /// <summary>Text drawn centered on the button face; null or empty draws nothing.</summary>
        public string OverlayText { get; set; }

        private BitmapFont _fontNormal;
        private BitmapFont _fontHalf;
        private bool _fontsLoaded;

        public SpeedOverlayImageButton(ImageButtonStyle style, string hoverText) : base(style, hoverText)
        {
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            base.Draw(batcher, parentAlpha);

            if (string.IsNullOrEmpty(OverlayText))
                return;

            var font = GetFont();
            if (font == null)
                return;

            // The button is added straight to the stage and absolutely positioned, so its local
            // coordinates are already stage coordinates.
            var size = font.MeasureString(OverlayText);
            var position = new Vector2(
                GetX() + (GetWidth() - size.X) * 0.5f,
                GetY() + (GetHeight() - size.Y) * 0.5f);

            StackCountText.Draw(batcher, font, OverlayText, position, Color.White);
        }

        /// <summary>HUD font matching the current window mode; the half-height button is drawn at 2x.</summary>
        private BitmapFont GetFont()
        {
            if (!_fontsLoaded)
            {
                _fontsLoaded = true;
                if (Core.Instance != null)
                {
                    _fontNormal = Core.Content.LoadBitmapFont(GameConfig.FontPathHud);
                    _fontHalf = Core.Content.LoadBitmapFont(GameConfig.FontPathHud2x);
                }
            }

            try
            {
                if (WindowManager.IsHalfHeightMode())
                    return _fontHalf ?? _fontNormal ?? Graphics.Instance?.BitmapFont;
            }
            catch
            {
                // WindowManager is unavailable headless; fall through to the normal font
            }

            return _fontNormal ?? Graphics.Instance?.BitmapFont;
        }
    }
}
