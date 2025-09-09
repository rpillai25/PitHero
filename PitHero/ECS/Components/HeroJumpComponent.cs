using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero jump logic and shadow rendering during pit jumping actions
    /// </summary>
    public class HeroJumpComponent : Component, IUpdatable
    {
        private const string HERO_SHADOW_SPRITE = "HeroShadow";
        private const float MAX_JUMP_HEIGHT_PX = 32f; // peak vertical offset during jump (matches previous discrete peak)

        private HeroAnimationComponent _heroAnimator;
        private SpriteRenderer _shadowRenderer;
        private SpriteAtlas _actorsAtlas;

        // Jump state tracking
        private bool _isJumping = false;
        private float _jumpStartTime;
        private float _jumpDuration;
        private float _initialYOffset;
        private Direction _jumpDirection;

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            _heroAnimator = Entity.GetComponent<HeroAnimationComponent>();
            if (_heroAnimator == null)
            {
                Debug.Warn("[HeroJumpComponent] HeroAnimationComponent not found on entity");
                return;
            }

            try
            {
                _actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
                if (_actorsAtlas == null)
                {
                    Debug.Warn("[HeroJumpComponent] Failed to load Actors.atlas - atlas is null");
                    return;
                }

                CreateShadowRenderer();
                Debug.Log("[HeroJumpComponent] Initialized successfully");
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[HeroJumpComponent] Failed to load Actors.atlas: {ex.Message}");
            }
        }

        /// <summary>Sets hero color</summary>
        public void SetColor(Color color)
        {
            _heroAnimator?.SetColor(color);
        }

        private void CreateShadowRenderer()
        {
            if (_actorsAtlas == null) return;

            var shadowSprite = _actorsAtlas.GetSprite(HERO_SHADOW_SPRITE);
            if (shadowSprite == null)
            {
                Debug.Warn($"[HeroJumpComponent] {HERO_SHADOW_SPRITE} sprite not found in atlas");
                return;
            }

            _shadowRenderer = Entity.AddComponent(new SpriteRenderer(shadowSprite));
            _shadowRenderer.SetRenderLayer(GameConfig.RenderLayerActors + 1);
            _shadowRenderer.SetLocalOffset(new Vector2(0, GameConfig.TileSize / 4));
            _shadowRenderer.SetEnabled(false);
            Debug.Log($"[HeroJumpComponent] Created shadow renderer with sprite {HERO_SHADOW_SPRITE}");
        }

        public void Update()
        {
            if (!_isJumping) return;

            var elapsed = Time.TotalTime - _jumpStartTime;
            var progress = elapsed / _jumpDuration;
            if (progress >= 1.0f)
            {
                EndJump();
                return;
            }

            UpdateJumpVerticalOffset(progress);
        }

        /// <summary>Starts a jump in a direction for duration</summary>
        public void StartJump(Direction direction, float duration)
        {
            _jumpDirection = direction;
            _jumpDuration = duration;
            _jumpStartTime = Time.TotalTime;
            _isJumping = true;

            if (_heroAnimator == null || _actorsAtlas == null)
            {
                Debug.Warn("[HeroJumpComponent] StartJump called but graphics components not available (this is normal in tests)");
                return;
            }

            _initialYOffset = _heroAnimator.LocalOffset.Y;
            _heroAnimator.PlayJumpAnimation(direction);

            if (_shadowRenderer != null)
                _shadowRenderer.SetEnabled(true);

            Debug.Log($"[HeroJumpComponent] Started jump animation for direction {direction} with duration {duration}s");
        }

        /// <summary>Ends current jump</summary>
        public void EndJump()
        {
            if (!_isJumping) return;
            _isJumping = false;

            if (_heroAnimator != null)
                _heroAnimator.SetLocalOffset(new Vector2(_heroAnimator.LocalOffset.X, _initialYOffset));

            if (_shadowRenderer != null)
                _shadowRenderer.SetEnabled(false);

            if (_heroAnimator != null)
            {
                _heroAnimator.UpdateAnimationForDirection(_jumpDirection);
                _heroAnimator.UnpauseAnimation(); // in case something paused externally
            }

            Debug.Log("[HeroJumpComponent] Ended jump animation");
        }

        /// <summary>Updates vertical offset along parabolic arc</summary>
        private void UpdateJumpVerticalOffset(float progress)
        {
            if (_heroAnimator == null) return;
            if (progress < 0f) progress = 0f;
            if (progress > 1f) progress = 1f;

            var t = progress;
            var heightFactor = 4f * t * (1f - t);
            var yOffset = _initialYOffset - (MAX_JUMP_HEIGHT_PX * heightFactor);
            _heroAnimator.SetLocalOffset(new Vector2(_heroAnimator.LocalOffset.X, yOffset));
        }

        /// <summary>Returns true if jumping</summary>
        public bool IsJumping => _isJumping;
    }
}