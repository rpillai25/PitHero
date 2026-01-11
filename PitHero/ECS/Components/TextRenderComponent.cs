using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Renders text at an entity's position (used for mercenary name display)
    /// </summary>
    public class TextRenderComponent : RenderableComponent
    {
        private string _text = "";
        private BitmapFont _font;
        private Color _color = Color.White;
        private Vector2 _textSize;

        public override float Width => _textSize.X;
        public override float Height => _textSize.Y;

        public override void Render(Batcher batcher, Camera camera)
        {
            if (string.IsNullOrEmpty(_text) || _font == null)
                return;

            var position = Entity.Transform.Position;
            
            // Center the text horizontally
            var textOrigin = new Vector2(_textSize.X / 2, 0);
            
            batcher.DrawString(_font, _text, position, _color, 0f, textOrigin, 1f, SpriteEffects.None, LayerDepth);
        }

        /// <summary>Sets the text to display</summary>
        public void SetText(string text)
        {
            _text = text;
            UpdateTextSize();
        }

        /// <summary>Sets the font to use for rendering</summary>
        public void SetFont(BitmapFont font)
        {
            _font = font;
            UpdateTextSize();
        }

        /// <summary>Sets the text color</summary>
        public void SetColor(Color color)
        {
            _color = color;
        }

        private void UpdateTextSize()
        {
            if (_font != null && !string.IsNullOrEmpty(_text))
            {
                _textSize = _font.MeasureString(_text);
            }
            else
            {
                _textSize = Vector2.Zero;
            }
        }
    }
}
