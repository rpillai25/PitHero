using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts;
using Nez.Sprites;
using Nez.Textures;
using PitHero.ECS.Components;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// Graphical HUD component that renders the hero's HP, MP, and Level using sprites and dynamic text
    /// </summary>
    public class GraphicalHUD : RenderableComponent
    {
        private Sprite _hudTemplateHeroSprite;
        private Sprite _hudTemplateMercenarySprite;
        private Sprite _hpUnitSprite;
        private Sprite _mpUnitSprite;
        private BitmapFont _hudFont;
        private TextService _textService;
        
        private Entity _heroEntity;
        private bool _isMercenary;
        
        /// <summary>
        /// Gets the active HUD template sprite based on whether this is for a hero or mercenary
        /// </summary>
        private Sprite ActiveHudTemplate => _isMercenary ? _hudTemplateMercenarySprite : _hudTemplateHeroSprite;
        
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
        /// <summary>
        /// Safely retrieves TextService. Returns null if Core is not initialized (e.g., in unit tests).
        /// </summary>
        private TextService GetTextService()
        {
            if (_textService == null && Core.Services != null)
            {
                _textService = Core.Services.GetService<TextService>();
            }
            return _textService;
        }

        /// <summary>
        /// Gets localized text or falls back to key name if TextService unavailable.
        /// </summary>
        private string GetText(DialogueType type, TextKey key)
        {
            var service = GetTextService();
            return service?.DisplayText(type, key) ?? key.ToString();
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            // Load sprites from UI atlas
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            _hudTemplateHeroSprite = uiAtlas.GetSprite("HudTemplate");
            _hudTemplateMercenarySprite = uiAtlas.GetSprite("HudTemplateMercenary");
            _hpUnitSprite = uiAtlas.GetSprite("HPUnit");
            _mpUnitSprite = uiAtlas.GetSprite("MPUnit");

            // Load HUD font (normal size only)
            _hudFont = Core.Content.LoadBitmapFont(GameConfig.FontPathHud);
            
            // Get TextService
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
        /// Set the hero entity reference for rendering hero sprites
        /// </summary>
        public void SetHeroEntity(Entity heroEntity)
        {
            _heroEntity = heroEntity;
            
            // Detect if this entity is a mercenary or hero
            if (heroEntity != null)
            {
                _isMercenary = heroEntity.HasComponent<MercenaryComponent>();
            }
        }

        public override void Render(Batcher batcher, Camera camera)
        {
            // Get entity position in screen space (this should be top-left of HUD)
            var position = Entity.Transform.Position;

            // Render HUD template background with origin at top-left (0,0) instead of center
            batcher.Draw(ActiveHudTemplate, position, Color.White, 0f, Vector2.Zero, 1f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);

            // Use constant offsets without scaling
            int hpUnitXOffset = HP_UNIT_X_OFFSET;
            int hpUnitYOffset = HP_UNIT_Y_OFFSET;
            int mpUnitXOffset = MP_UNIT_X_OFFSET;
            int mpUnitYOffset = MP_UNIT_Y_OFFSET;
            int hpTextXOffset = HP_TEXT_X_OFFSET;
            int hpTextYOffset = HP_TEXT_Y_OFFSET;
            int mpTextXOffset = MP_TEXT_X_OFFSET;
            int mpTextYOffset = MP_TEXT_Y_OFFSET;
            int levelTextXOffset = LEVEL_TEXT_X_OFFSET;
            int levelTextYOffset = LEVEL_TEXT_Y_OFFSET;
            int barWidth = HP_MP_BAR_WIDTH;

            // Now all child elements use position directly as the top-left corner
            // Render HP bar (filled from right to left based on HP percentage)
            RenderBar(batcher, position, _hpUnitSprite, _currentHp, _maxHp, hpUnitXOffset, hpUnitYOffset, barWidth);

            // Render MP bar (filled from right to left based on MP percentage)
            RenderBar(batcher, position, _mpUnitSprite, _currentMp, _maxMp, mpUnitXOffset, mpUnitYOffset, barWidth);

            // Render dynamic numbers
            RenderText(batcher, position, _currentHp.ToString(), hpTextXOffset, hpTextYOffset);
            RenderText(batcher, position, _currentMp.ToString(), mpTextXOffset, mpTextYOffset);
            
            // Render hero body and hair sprites at level position (32x31 cropped from 32x46)
            RenderHeroSprites(batcher, position, levelTextXOffset-7, levelTextYOffset-15);
            
            // Render level text on top of hero sprites
            RenderText(batcher, position, GetText(DialogueType.UI, TextKey.HudLevelPrefix)+_level.ToString(), levelTextXOffset-10, levelTextYOffset+14);
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
            // Add padding for single-digit levels to center them
            int adjustedXOffset = xOffset;
            if (xOffset == LEVEL_TEXT_X_OFFSET && text.Length == 1)
            {
                adjustedXOffset += 4;
            }

            var textPosition = hudPosition + new Vector2(adjustedXOffset, yOffset);
            batcher.DrawString(_hudFont, text, textPosition, Color.White);
        }

        /// <summary>
        /// Render hero body and hair sprites at the specified position (32x36 cropped from 32x46)
        /// </summary>
        private void RenderHeroSprites(Batcher batcher, Vector2 hudPosition, int xOffset, int yOffset)
        {
            if (_heroEntity == null)
                return;

            var headAnimComponent = _heroEntity.GetComponent<HeroHeadAnimationComponent>();
            var eyesAnimComponent = _heroEntity.GetComponent<HeroEyesAnimationComponent>();
            var hairAnimComponent = _heroEntity.GetComponent<HeroHairAnimationComponent>();

            if (headAnimComponent == null || eyesAnimComponent == null || hairAnimComponent == null)
                return;

            // Get the walk down animation from each component's Animations dictionary
            if (headAnimComponent.Animations == null || eyesAnimComponent.Animations == null || hairAnimComponent.Animations == null)
                return;

            var headAnimName = headAnimComponent.WalkDownAnimationName;
            var hairAnimName = hairAnimComponent.WalkDownAnimationName;
            var eyesAnimName = eyesAnimComponent.WalkDownAnimationName;

            if (!headAnimComponent.Animations.ContainsKey(headAnimName) || 
                !hairAnimComponent.Animations.ContainsKey(hairAnimName) ||
                !eyesAnimComponent.Animations.ContainsKey(eyesAnimName))
                return;

            var headAnimation = headAnimComponent.Animations[headAnimName];
            var hairAnimation = hairAnimComponent.Animations[hairAnimName];
            var eyesAnimation = eyesAnimComponent.Animations[eyesAnimName];
            // Get frame 0 sprite from each animation
            if (headAnimation.Sprites == null || headAnimation.Sprites.Length == 0 ||
                hairAnimation.Sprites == null || hairAnimation.Sprites.Length == 0 ||
                eyesAnimation.Sprites == null || eyesAnimation.Sprites.Length == 0)
                return;

            var headSprite = headAnimation.Sprites[0];
            var hairSprite = hairAnimation.Sprites[0];
            var eyesSprite = eyesAnimation.Sprites[0];
            var renderPosition = hudPosition + new Vector2(xOffset, yOffset);

            // Define the source rectangle to crop the top 32x31 pixels from the head sprite
            var headCroppedSourceRect = new Rectangle(
                headSprite.SourceRect.X,
                headSprite.SourceRect.Y,
                32,
                31
            );

            // Render head sprite first (cropped) with head color from animation component
            batcher.Draw(
                headSprite.Texture2D,
                renderPosition,
                headCroppedSourceRect,
                headAnimComponent.ComponentColor,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0f
            );

            // Define the source rectangle to crop the top 32x31 pixels from the eyes sprite
            var eyesCroppedSourceRect = new Rectangle(
                eyesSprite.SourceRect.X,
                eyesSprite.SourceRect.Y,
                32,
                31
            );
            // Render eyes sprite on top (cropped) with eyes color from animation component
            batcher.Draw(
                eyesSprite.Texture2D,
                renderPosition,
                eyesCroppedSourceRect,
                eyesAnimComponent.ComponentColor,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0f
            );

            // Define the source rectangle to crop the top 32x31 pixels from the hair sprite
            var hairCroppedSourceRect = new Rectangle(
                hairSprite.SourceRect.X,
                hairSprite.SourceRect.Y,
                32,
                31
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
