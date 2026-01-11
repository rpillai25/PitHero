using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts;
using Nez.Sprites;
using Nez.Textures;
using PitHero.ECS.Components;

namespace PitHero.UI
{
    /// <summary>
    /// Graphical HUD component that renders the hero's HP, MP, and Level using sprites and dynamic text
    /// </summary>
    public class GraphicalHUD : RenderableComponent
    {
        private Sprite _hudTemplateSprite;
        private Sprite _hudTemplateSprite2x;
        private Sprite _hpUnitSprite;
        private Sprite _hpUnitSprite2x;
        private Sprite _mpUnitSprite;
        private Sprite _mpUnitSprite2x;
        private BitmapFont _hudFont;
        private BitmapFont _hudFont2x;
        
        private bool _useDoubleSize;
        private Entity _heroEntity;
        
        // Active sprites and fonts (switch between normal and 2x)
        private Sprite ActiveHudTemplate => _useDoubleSize ? _hudTemplateSprite2x : _hudTemplateSprite;
        private Sprite ActiveHpUnit => _useDoubleSize ? _hpUnitSprite2x : _hpUnitSprite;
        private Sprite ActiveMpUnit => _useDoubleSize ? _mpUnitSprite2x : _mpUnitSprite;
        private BitmapFont ActiveFont => _useDoubleSize ? _hudFont2x : _hudFont;
        private int SizeMultiplier => _useDoubleSize ? 2 : 1;

        // Constants from issue description
        private const int HP_MP_BAR_WIDTH = 51;
        private const int HP_UNIT_X_OFFSET = 3;
        private const int HP_UNIT_Y_OFFSET = 19; // Shifted up 7 pixels from original 26
        private const int MP_UNIT_X_OFFSET = 106;
        private const int MP_UNIT_Y_OFFSET = 19; // Shifted up 7 pixels from original 26
        private const int HP_TEXT_X_OFFSET = 28;
        private const int HP_TEXT_Y_OFFSET = 2; // Shifted up 7 pixels from original 9
        private const int MP_TEXT_X_OFFSET = 131;
        private const int MP_TEXT_Y_OFFSET = 2; // Shifted up 7 pixels from original 9
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
        public override float Width => ActiveHudTemplate?.SourceRect.Width ?? 160;

        /// <summary>
        /// Override Height to return the HUD template sprite height (fixes StackOverflowException)
        /// </summary>
        public override float Height => ActiveHudTemplate?.SourceRect.Height ?? 33;

        public GraphicalHUD()
        {
            // Set render layer to UI
            SetRenderLayer(GameConfig.RenderLayerUI);
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            // Load sprites from UI atlas (both normal and 2x versions)
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            _hudTemplateSprite = uiAtlas.GetSprite("HudTemplate");
            _hudTemplateSprite2x = uiAtlas.GetSprite("HudTemplate2x");
            _hpUnitSprite = uiAtlas.GetSprite("HPUnit");
            _hpUnitSprite2x = uiAtlas.GetSprite("HPUnit2x");
            _mpUnitSprite = uiAtlas.GetSprite("MPUnit");
            _mpUnitSprite2x = uiAtlas.GetSprite("MPUnit2x");

            // Load HUD fonts (normal and 2x)
            _hudFont = Core.Content.LoadBitmapFont("Content/Fonts/HUD.fnt");
            _hudFont2x = Core.Content.LoadBitmapFont("Content/Fonts/Hud2x.fnt");
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

        /// <summary>
        /// Set whether to use double-size (2x) sprites or normal sprites
        /// </summary>
        public void SetUseDoubleSize(bool useDoubleSize)
        {
            _useDoubleSize = useDoubleSize;
        }

        /// <summary>
        /// Set the hero entity reference for rendering hero sprites
        /// </summary>
        public void SetHeroEntity(Entity heroEntity)
        {
            _heroEntity = heroEntity;
        }

        public override void Render(Batcher batcher, Camera camera)
        {
            // Get entity position in screen space (this should be top-left of HUD)
            var position = Entity.Transform.Position;

            // Render HUD template background with origin at top-left (0,0) instead of center
            batcher.Draw(ActiveHudTemplate, position, Color.White, 0f, Vector2.Zero, 1f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);

            // Scale offsets based on size multiplier
            int scaledHpUnitXOffset = HP_UNIT_X_OFFSET * SizeMultiplier;
            int scaledHpUnitYOffset = HP_UNIT_Y_OFFSET * SizeMultiplier;
            int scaledMpUnitXOffset = MP_UNIT_X_OFFSET * SizeMultiplier;
            int scaledMpUnitYOffset = MP_UNIT_Y_OFFSET * SizeMultiplier;
            int scaledHpTextXOffset = HP_TEXT_X_OFFSET * SizeMultiplier;
            int scaledHpTextYOffset = HP_TEXT_Y_OFFSET * SizeMultiplier;
            int scaledMpTextXOffset = MP_TEXT_X_OFFSET * SizeMultiplier;
            int scaledMpTextYOffset = MP_TEXT_Y_OFFSET * SizeMultiplier;
            int scaledLevelTextXOffset = LEVEL_TEXT_X_OFFSET * SizeMultiplier;
            int scaledLevelTextYOffset = LEVEL_TEXT_Y_OFFSET * SizeMultiplier;
            int scaledBarWidth = HP_MP_BAR_WIDTH * SizeMultiplier;

            // Now all child elements use position directly as the top-left corner
            // Render HP bar (filled from right to left based on HP percentage)
            RenderBar(batcher, position, ActiveHpUnit, _currentHp, _maxHp, scaledHpUnitXOffset, scaledHpUnitYOffset, scaledBarWidth);

            // Render MP bar (filled from right to left based on MP percentage)
            RenderBar(batcher, position, ActiveMpUnit, _currentMp, _maxMp, scaledMpUnitXOffset, scaledMpUnitYOffset, scaledBarWidth);

            // Render dynamic numbers
            RenderText(batcher, position, _currentHp.ToString(), scaledHpTextXOffset, scaledHpTextYOffset);
            RenderText(batcher, position, _currentMp.ToString(), scaledMpTextXOffset, scaledMpTextYOffset);
            
            // Render hero body and hair sprites at level position (32x36 cropped from 32x46)
            RenderHeroSprites(batcher, position, scaledLevelTextXOffset-6, scaledLevelTextYOffset-20);
            
            // Render level text on top of hero sprites
            RenderText(batcher, position, "Lv "+_level.ToString(), scaledLevelTextXOffset-10, scaledLevelTextYOffset+14);
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
            // Add padding for single-digit levels to center them in the level circle (scaled based on size)
            int adjustedXOffset = xOffset;
            if (xOffset == LEVEL_TEXT_X_OFFSET * SizeMultiplier && text.Length == 1)
            {
                adjustedXOffset += 4 * SizeMultiplier;
            }

            var textPosition = hudPosition + new Vector2(adjustedXOffset, yOffset);
            batcher.DrawString(ActiveFont, text, textPosition, Color.White);
        }

        /// <summary>
        /// Render hero body and hair sprites at the specified position (32x36 cropped from 32x46)
        /// </summary>
        private void RenderHeroSprites(Batcher batcher, Vector2 hudPosition, int xOffset, int yOffset)
        {
            if (_heroEntity == null)
                return;

            var bodyAnimComponent = _heroEntity.GetComponent<HeroBodyAnimationComponent>();
            var hairAnimComponent = _heroEntity.GetComponent<HeroHairAnimationComponent>();

            if (bodyAnimComponent == null || hairAnimComponent == null)
                return;

            // Get the walk down animation from each component's Animations dictionary
            if (bodyAnimComponent.Animations == null || hairAnimComponent.Animations == null)
                return;

            var bodyAnimName = bodyAnimComponent.WalkDownAnimationName;
            var hairAnimName = hairAnimComponent.WalkDownAnimationName;

            if (!bodyAnimComponent.Animations.ContainsKey(bodyAnimName) || 
                !hairAnimComponent.Animations.ContainsKey(hairAnimName))
                return;

            var bodyAnimation = bodyAnimComponent.Animations[bodyAnimName];
            var hairAnimation = hairAnimComponent.Animations[hairAnimName];

            // Get frame 0 sprite from each animation
            if (bodyAnimation.Sprites == null || bodyAnimation.Sprites.Length == 0 ||
                hairAnimation.Sprites == null || hairAnimation.Sprites.Length == 0)
                return;

            var bodySprite = bodyAnimation.Sprites[0];
            var hairSprite = hairAnimation.Sprites[0];

            var renderPosition = hudPosition + new Vector2(xOffset, yOffset);

            // Define the source rectangle to crop the top 32x36 pixels from the body sprite
            var bodyCroppedSourceRect = new Rectangle(
                bodySprite.SourceRect.X,
                bodySprite.SourceRect.Y,
                32,
                36
            );

            // Render body sprite first (cropped) with body color from animation component
            batcher.Draw(
                bodySprite.Texture2D,
                renderPosition,
                bodyCroppedSourceRect,
                bodyAnimComponent.ComponentColor,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0f
            );

            // Define the source rectangle to crop the top 32x36 pixels from the hair sprite
            var hairCroppedSourceRect = new Rectangle(
                hairSprite.SourceRect.X,
                hairSprite.SourceRect.Y,
                32,
                36
            );

            // Render hair sprite on top (cropped) with hair color from animation component
            batcher.Draw(
                hairSprite.Texture2D,
                renderPosition,
                hairCroppedSourceRect,
                hairAnimComponent.ComponentColor,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0f
            );
        }
    }
}
