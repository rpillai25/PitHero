using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// A Label whose characters each bob up and down on a sine wave with a per-character phase
    /// offset, so the wave travels across the word like a waving banner (issue #272 "Sleeping").
    /// Animates off Time.TotalTime (engine wall-clock) so it keeps waving even while the game is
    /// paused (e.g. the Monsters menu pauses gameplay); the UI redraws every frame.
    /// </summary>
    public class SineWaveLabel : Label
    {
        private const float Amplitude = 3f;   // vertical bob in pixels
        private const float Frequency = 6f;   // radians/second
        private const float PhaseStep = 0.6f; // phase offset between adjacent characters

        public SineWaveLabel(string text, LabelStyle style) : base(text, style)
        {
            SetAlignment(Align.Left);
        }

        public SineWaveLabel(string text, Skin skin, string styleName = null) : base(text, skin, styleName)
        {
            SetAlignment(Align.Left);
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            Validate();

            var style = GetStyle();
            var font = style.Font;
            var text = GetText();
            if (font == null || string.IsNullOrEmpty(text))
                return;

            var scale = new Vector2(style.FontScaleX, style.FontScaleY);
            float baseX = x;
            float baseY = y;
            float cursorX = 0f;

            for (int i = 0; i < text.Length; i++)
            {
                var ch = text[i].ToString();
                float yOffset = Mathf.Sin(Time.TotalTime * Frequency + i * PhaseStep) * Amplitude;

                batcher.DrawString(font, ch, new Vector2(baseX + cursorX, baseY + yOffset),
                    style.FontColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                cursorX += font.MeasureString(ch).X * style.FontScaleX;
            }
        }
    }
}
