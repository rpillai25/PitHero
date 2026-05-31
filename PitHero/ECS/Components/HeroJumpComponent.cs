using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using System.Collections.Generic;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero jump logic and shadow rendering during pit jumping actions
    /// </summary>
    public class HeroJumpComponent : Component, IUpdatable
    {
        private const string HERO_SHADOW_SPRITE = "HeroShadow";
        private const float MAX_JUMP_HEIGHT_PX = 32f; // peak vertical offset during jump (matches previous discrete peak)

        private MultiSpriteAnimator _multiSpriteAnimator;
        private List<HeroAnimationComponent> _heroAnimators;
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

            InitializeAnimators();

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

        private void InitializeAnimators()
        {
            _multiSpriteAnimator = Entity?.GetComponent<MultiSpriteAnimator>();

            // Keep individual animator list for SetColor and any legacy fallback
            _heroAnimators = new List<HeroAnimationComponent>
            {
                Entity?.GetComponent<HeroHand2AnimationComponent>(),
                Entity?.GetComponent<HeroBodyAnimationComponent>(),
                Entity?.GetComponent<HeroPantsAnimationComponent>(),
                Entity?.GetComponent<HeroShirtAnimationComponent>(),
                Entity?.GetComponent<HeroHeadAnimationComponent>(),
                Entity?.GetComponent<HeroEyesAnimationComponent>(),
                Entity?.GetComponent<HeroHairAnimationComponent>(),
                Entity?.GetComponent<HeroHand1AnimationComponent>()
            };
            _heroAnimators.RemoveAll(animator => animator == null);

            if (_multiSpriteAnimator == null && _heroAnimators.Count == 0)
                Debug.Warn("[HeroJumpComponent] No animation components found on entity");
        }

        /// <summary>Sets hero color for all layers</summary>
        public void SetColor(Color color)
        {
            // Initialize _heroAnimators if null (may happen in tests)
            if (_heroAnimators == null)
            {
                InitializeAnimators();
            }

            foreach (var animator in _heroAnimators)
            {
                animator?.SetColor(color);
            }
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

            // Update facing
            var facing = Entity.GetComponent<ActorFacingComponent>();
            facing?.SetFacing(direction);

            // Initialize _heroAnimators if null (may happen in tests)
            if (_heroAnimators == null)
            {
                InitializeAnimators();
            }

            if (_multiSpriteAnimator == null && (_heroAnimators.Count == 0 || _actorsAtlas == null))
            {
                Debug.Warn("[HeroJumpComponent] StartJump called but graphics components not available (this is normal in tests)");
                return;
            }

            if (_multiSpriteAnimator != null)
            {
                // MultiSpriteAnimator path: arc lives on the composite's LocalOffset
                _initialYOffset = _multiSpriteAnimator.LocalOffset.Y;
                _multiSpriteAnimator.PlayJumpAnimation(direction);
            }
            else
            {
                // Legacy path: arc applied per-layer
                _initialYOffset = _heroAnimators[0].LocalOffset.Y;
                foreach (var animator in _heroAnimators)
                    animator?.PlayJumpAnimation(direction);
            }

            if (_shadowRenderer != null)
                _shadowRenderer.SetEnabled(true);

            Debug.Log($"[HeroJumpComponent] Started jump animation for direction {direction} with duration {duration}s");
        }

        /// <summary>Ends current jump</summary>
        public void EndJump()
        {
            if (!_isJumping) return;
            _isJumping = false;

            // Initialize _heroAnimators if null (may happen in tests)
            if (_heroAnimators == null)
            {
                InitializeAnimators();
            }

            if (_multiSpriteAnimator != null)
            {
                _multiSpriteAnimator.LocalOffset = new Vector2(0f, _initialYOffset);
                _multiSpriteAnimator.UpdateAnimationForDirection(_jumpDirection);
            }
            else
            {
                foreach (var animator in _heroAnimators)
                {
                    if (animator != null)
                        animator.SetLocalOffset(new Vector2(animator.LocalOffset.X, _initialYOffset));
                }
                foreach (var animator in _heroAnimators)
                {
                    if (animator != null)
                    {
                        animator.UpdateAnimationForDirection(_jumpDirection);
                        animator.UnpauseAnimation();
                    }
                }
            }

            if (_shadowRenderer != null)
                _shadowRenderer.SetEnabled(false);

            Debug.Log("[HeroJumpComponent] Ended jump animation");
        }

        /// <summary>Updates vertical offset along parabolic arc</summary>
        private void UpdateJumpVerticalOffset(float progress)
        {
            if (_heroAnimators == null)
                InitializeAnimators();

            if (progress < 0f) progress = 0f;
            if (progress > 1f) progress = 1f;

            var heightFactor = 4f * progress * (1f - progress);
            var yOffset = _initialYOffset - (MAX_JUMP_HEIGHT_PX * heightFactor);

            if (_multiSpriteAnimator != null)
            {
                _multiSpriteAnimator.LocalOffset = new Vector2(0f, yOffset);
            }
            else
            {
                if (_heroAnimators.Count == 0) return;
                foreach (var animator in _heroAnimators)
                {
                    if (animator != null)
                        animator.SetLocalOffset(new Vector2(animator.LocalOffset.X, yOffset));
                }
            }
        }

        /// <summary>Returns true if jumping</summary>
        public bool IsJumping => _isJumping;
    }
}