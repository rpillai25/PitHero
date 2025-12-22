using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts;
using Nez.Sprites;
using Nez.Textures;

namespace PitHero.UI
{
    /// <summary>
    /// Graphical HUD component that renders the hero's HP, MP, and Level using sprites and dynamic text
    /// </summary>
    public class GraphicalHUD : RenderableComponent
    {
        private Sprite _hudTemplateSprite;
        private Sprite _hpUnitSprite;
        private Sprite _mpUnitSprite;
        private BitmapFont _hudFont;

        // Constants from issue description
        private const int HP_BAR_WIDTH = 51;
        private const int MP_BAR_WIDTH = 51;
        private const int HP_UNIT_X_OFFSET = 3;
        private const int HP_UNIT_Y_OFFSET = 26;
        private const int MP_UNIT_X_OFFSET = 106;
        private const int MP_UNIT_Y_OFFSET = 26;
        private const int HP_TEXT_X_OFFSET = 28;
        private const int HP_TEXT_Y_OFFSET = 9;
        private const int MP_TEXT_X_OFFSET = 131;
        private const int MP_TEXT_Y_OFFSET = 9;
        private const int LEVEL_TEXT_X_OFFSET = 71;
        private const int LEVEL_TEXT_Y_OFFSET = 13;

        // Current values for rendering
        private int _currentHp;
        private int _maxHp;
        private int _currentMp;
        private int _maxMp;
        private int _level;

        /// <summary>
        /// Override Width to return the HUD template sprite width (fixes StackOverflowException)
        /// </summary>
        public override float Width => _hudTemplateSprite?.SourceRect.Width ?? 160;

        /// <summary>
        /// Override Height to return the HUD template sprite height (fixes StackOverflowException)
        /// </summary>
        public override float Height => _hudTemplateSprite?.SourceRect.Height ?? 33;

        public GraphicalHUD()
        {
            // Set render layer to UI
            SetRenderLayer(GameConfig.RenderLayerUI);
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            // Load sprites from UI atlas
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            _hudTemplateSprite = uiAtlas.GetSprite("HudTemplate");
            _hpUnitSprite = uiAtlas.GetSprite("HPUnit");
            _mpUnitSprite = uiAtlas.GetSprite("MPUnit");

            // Load HUD font (16 point font)
            _hudFont = Core.Content.LoadBitmapFont("Content/Fonts/HUD.fnt");
        }

        /// <summary>
        /// Update the HUD values
        /// </summary>
        public void UpdateValues(int currentHp, int maxHp, int currentMp, int maxMp, int level)
        {
            _currentHp = currentHp;
            _maxHp = maxHp;
            _currentMp = currentMp;
            _maxMp = maxMp;
            _level = level;
        }

        public override void Render(Batcher batcher, Camera camera)
        {
            // Get entity position in screen space (this should be top-left of HUD)
            var position = Entity.Transform.Position;

            // Render HUD template background with origin at top-left (0,0) instead of center
            batcher.Draw(_hudTemplateSprite, position, Color.White, 0f, Vector2.Zero, 1f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);

            // Now all child elements use position directly as the top-left corner
            // Render HP bar (filled from right to left based on HP percentage)
            RenderBar(batcher, position, _hpUnitSprite, _currentHp, _maxHp, HP_UNIT_X_OFFSET, HP_UNIT_Y_OFFSET, HP_BAR_WIDTH);

            // Render MP bar (filled from right to left based on MP percentage)
            RenderBar(batcher, position, _mpUnitSprite, _currentMp, _maxMp, MP_UNIT_X_OFFSET, MP_UNIT_Y_OFFSET, MP_BAR_WIDTH);

            // Render dynamic numbers
            RenderText(batcher, position, _currentHp.ToString(), HP_TEXT_X_OFFSET, HP_TEXT_Y_OFFSET);
            RenderText(batcher, position, _currentMp.ToString(), MP_TEXT_X_OFFSET, MP_TEXT_Y_OFFSET);
            RenderText(batcher, position, _level.ToString(), LEVEL_TEXT_X_OFFSET, LEVEL_TEXT_Y_OFFSET);
        }

        /// <summary>
        /// Render a bar (HP or MP) filled from right to left
        /// </summary>
        private void RenderBar(Batcher batcher, Vector2 hudTopLeft, Sprite unitSprite, int current, int max, int xOffset, int yOffset, int barWidth)
        {
            if (max <= 0) return;

            // Calculate percentage and number of pixels to fill
            float percentage = (float)current / max;
            int pixelsToFill = (int)(percentage * barWidth);

            // Render units from right to left
            // The bar area starts at xOffset and extends barWidth pixels to the right
            // When filling from right to left, we start from the right edge and go left
            int rightEdge = xOffset + barWidth - 1;

            for (int i = 0; i < pixelsToFill; i++)
            {
                // Start from right edge and go left
                var unitPosition = hudTopLeft + new Vector2(rightEdge - i, yOffset);
                // Draw with origin at top-left (Vector2.Zero) for pixel-perfect positioning
                batcher.Draw(unitSprite, unitPosition, Color.White, 0f, Vector2.Zero, 1f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// Render text at the specified offset from HUD position
        /// </summary>
        private void RenderText(Batcher batcher, Vector2 hudPosition, string text, int xOffset, int yOffset)
        {
            // Add 8 pixels of padding for single-digit levels to center them in the level circle
            int adjustedXOffset = xOffset;
            if (xOffset == LEVEL_TEXT_X_OFFSET && text.Length == 1)
            {
                adjustedXOffset += 4;
            }

            var textPosition = hudPosition + new Vector2(adjustedXOffset, yOffset);
            batcher.DrawString(_hudFont, text, textPosition, Color.White);
        }
    }
}
