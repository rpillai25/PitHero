using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts;

namespace PitHero.UI
{
    /// <summary>
    /// Shared helper for drawing inventory stack-size / count numbers with a dark outline so they
    /// stay readable against any item or crop sprite. Used by every inventory-style slot (hero
    /// inventory, shortcut bar, vault, seeds, harvested crops) to keep the look consistent.
    /// </summary>
    public static class StackCountText
    {
        /// <summary>Dark outline color drawn behind stack-count text.</summary>
        public static readonly Color OutlineColor = new Color(0, 20, 60, 220);

        /// <summary>Draws text with a 1px dark outline (4 directions), then the fill color on top.</summary>
        public static void Draw(Batcher batcher, BitmapFont font, string text, Vector2 position, Color color)
        {
            batcher.DrawString(font, text, position + new Vector2(-1f,  0f), OutlineColor);
            batcher.DrawString(font, text, position + new Vector2( 1f,  0f), OutlineColor);
            batcher.DrawString(font, text, position + new Vector2( 0f, -1f), OutlineColor);
            batcher.DrawString(font, text, position + new Vector2( 0f,  1f), OutlineColor);
            batcher.DrawString(font, text, position, color);
        }

        /// <summary>
        /// Scaled overload for call sites that render at a UI Scale factor. The outline offset scales
        /// with the text so it stays a consistent 1 logical pixel at any scale.
        /// </summary>
        public static void Draw(Batcher batcher, BitmapFont font, string text, Vector2 position, Color color, float scale)
        {
            float o = scale;
            batcher.DrawString(font, text, position + new Vector2(-o,  0f), OutlineColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            batcher.DrawString(font, text, position + new Vector2( o,  0f), OutlineColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            batcher.DrawString(font, text, position + new Vector2( 0f, -o), OutlineColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            batcher.DrawString(font, text, position + new Vector2( 0f,  o), OutlineColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            batcher.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
