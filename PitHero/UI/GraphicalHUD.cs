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
        private const int HP_UNIT_X_OFFSET = 53;
        private const int HP_UNIT_Y_OFFSET = 26;
        private const int MP_UNIT_X_OFFSET = 156;
        private const int MP_UNIT_Y_OFFSET = 26;
        private const int HP_TEXT_X_OFFSET = 27;
        private const int HP_TEXT_Y_OFFSET = 7;
        private const int MP_TEXT_X_OFFSET = 130;
        private const int MP_TEXT_Y_OFFSET = 7;
        private const int LEVEL_TEXT_X_OFFSET = 71;
        private const int LEVEL_TEXT_Y_OFFSET = 13;

        // Current values for rendering
        private int _currentHp;
        private int _maxHp;
        private int _currentMp;
        private int _maxMp;
        private int _level;

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
            // Get entity position in screen space
            var position = Entity.Transform.Position;

            // Render HUD template background
            batcher.Draw(_hudTemplateSprite, position, Color.White);

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
        private void RenderBar(Batcher batcher, Vector2 hudPosition, Sprite unitSprite, int current, int max, int xOffset, int yOffset, int barWidth)
        {
            if (max <= 0) return;

            // Calculate percentage and number of pixels to fill
            float percentage = (float)current / max;
            int pixelsToFill = (int)(percentage * barWidth);

            // Render units from right to left
            // Starting position is at the right edge minus the pixels to fill
            int startX = xOffset + barWidth - pixelsToFill;

            for (int i = 0; i < pixelsToFill; i++)
            {
                var unitPosition = hudPosition + new Vector2(startX + i, yOffset);
                batcher.Draw(unitSprite, unitPosition, Color.White);
            }
        }

        /// <summary>
        /// Render text at the specified offset from HUD position
        /// </summary>
        private void RenderText(Batcher batcher, Vector2 hudPosition, string text, int xOffset, int yOffset)
        {
            var textPosition = hudPosition + new Vector2(xOffset, yOffset);
            batcher.DrawString(_hudFont, text, textPosition, Color.White);
        }
    }
}
