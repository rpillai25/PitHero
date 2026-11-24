using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.Services;
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
        private float _initialXOffset;
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
                Entity?.GetComponent<HeroBodyAnimationComponent>(),
                Entity?.GetComponent<HeroArmorAnimationComponent>(),
                Entity?.GetComponent<HeroShirtAnimationComponent>(),
                Entity?.GetComponent<HeroHeadAnimationComponent>(),
                Entity?.GetComponent<HeroEyesBaseAnimationComponent>(),
                Entity?.GetComponent<HeroEyesAnimationComponent>(),
                Entity?.GetComponent<HeroHairAnimationComponent>()
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

            // A whole-body tint belongs on the composite, so the baked pose stays the same (issue #459)
            if (_multiSpriteAnimator != null)
            {
                _multiSpriteAnimator.SetColor(color);
                return;
            }

            for (int i = 0; i < _heroAnimators.Count; i++)
                _heroAnimators[i]?.SetColor(color);
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

            var elapsed = SimulationClock.Now - _jumpStartTime;
            var progress = elapsed / _jumpDuration;
            if (progress >= 1.0f)
            {
                EndJump();
                return;
            }

            if (progress < 0f) progress = 0f;
            var heightFactor = 4f * progress * (1f - progress);
            SetAirborneHeight(MAX_JUMP_HEIGHT_PX * heightFactor);
        }

        /// <summary>Starts a jump in a direction for duration</summary>
        public void StartJump(Direction direction, float duration)
        {
            _jumpDuration = duration;
            _jumpStartTime = SimulationClock.Now;
            _isJumping = true;

            if (BeginAirbornePose(direction))
                Debug.Log($"[HeroJumpComponent] Started jump animation for direction {direction} with duration {duration}s");
        }

        /// <summary>
        /// Puts the hero into the airborne look without any timing: faces the direction, plays the
        /// per-layer jump pose, shows the ground shadow and records the rest offset that
        /// <see cref="SetAirborneHeight"/> lifts from. Returns false when graphics are unavailable
        /// (headless tests). Used by StartJump and by scripted drops such as the new-game intro.
        /// </summary>
        public bool BeginAirbornePose(Direction direction) => BeginAirbornePose(direction, keepRestOffset: false);

        /// <summary>
        /// As <see cref="BeginAirbornePose(Direction)"/>; with <paramref name="keepRestOffset"/> the rest offset
        /// recorded by an earlier jump is kept, so a sprite that is still displaced (a mercenary standing beside
        /// the hero after its attack jump, issue #458) jumps home relative to its real rest position.
        /// </summary>
        public bool BeginAirbornePose(Direction direction, bool keepRestOffset)
        {
            _jumpDirection = direction;

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
                Debug.Warn("[HeroJumpComponent] BeginAirbornePose called but graphics components not available (this is normal in tests)");
                return false;
            }

            // Arc offset lives on the composite's LocalOffset when present; otherwise per-layer.
            if (!keepRestOffset)
            {
                _initialYOffset = _multiSpriteAnimator != null
                    ? _multiSpriteAnimator.LocalOffset.Y
                    : (_heroAnimators.Count > 0 ? _heroAnimators[0].LocalOffset.Y : 0f);
                _initialXOffset = _multiSpriteAnimator != null ? _multiSpriteAnimator.LocalOffset.X : 0f;
            }

            for (int i = 0; i < _heroAnimators.Count; i++)
                _heroAnimators[i]?.PlayJumpAnimation(direction);

            if (_shadowRenderer != null)
                _shadowRenderer.SetEnabled(true);

            return true;
        }

        /// <summary>
        /// Lifts the hero sprite heightPx above its rest offset. Render-only: the entity (collider,
        /// pathfinding, bubble anchor) does not move.
        /// </summary>
        public void SetAirborneHeight(float heightPx) => SetAirborneOffset(0f, 0f, heightPx);

        /// <summary>
        /// Moves the sprite (dx, dy) from its rest offset and lifts it heightPx above that point; the ground
        /// shadow follows (dx, dy). Render-only, like <see cref="SetAirborneHeight"/>. The sideways part
        /// applies to the composite (MultiSpriteAnimator) only.
        /// </summary>
        public void SetAirborneOffset(float dx, float dy, float heightPx)
        {
            if (_heroAnimators == null)
                InitializeAnimators();

            if (heightPx < 0f) heightPx = 0f;
            var yOffset = _initialYOffset + dy - heightPx;

            if (_shadowRenderer != null)
                _shadowRenderer.SetLocalOffset(new Vector2(dx, dy + GameConfig.TileSize / 4));

            if (_multiSpriteAnimator != null)
            {
                _multiSpriteAnimator.LocalOffset = new Vector2(_initialXOffset + dx, yOffset);
            }
            else
            {
                for (int i = 0; i < _heroAnimators.Count; i++)
                {
                    var animator = _heroAnimators[i];
                    if (animator != null)
                        animator.SetLocalOffset(new Vector2(animator.LocalOffset.X, yOffset));
                }
            }
        }

        /// <summary>
        /// Restores the grounded look: rest offset, walking animation for the airborne direction,
        /// shadow hidden.
        /// </summary>
        public void EndAirbornePose() => EndAirbornePose(0f, 0f);

        /// <summary>
        /// Lands the sprite (dx, dy) from its rest offset (0, 0 = home) with the grounded look: walking
        /// animation for the airborne direction, shadow hidden.
        /// </summary>
        public void EndAirbornePose(float dx, float dy)
        {
            // Initialize _heroAnimators if null (may happen in tests)
            if (_heroAnimators == null)
            {
                InitializeAnimators();
            }

            // Reset the arc offset — on the composite when present, per-layer in legacy path.
            if (_multiSpriteAnimator != null)
            {
                _multiSpriteAnimator.LocalOffset = new Vector2(_initialXOffset + dx, _initialYOffset + dy);
            }
            else
            {
                for (int i = 0; i < _heroAnimators.Count; i++)
                {
                    var animator = _heroAnimators[i];
                    if (animator != null)
                        animator.SetLocalOffset(new Vector2(animator.LocalOffset.X, _initialYOffset));
                }
            }

            for (int i = 0; i < _heroAnimators.Count; i++)
            {
                var animator = _heroAnimators[i];
                if (animator != null)
                {
                    animator.UpdateAnimationForDirection(_jumpDirection);
                    animator.UnpauseAnimation();
                }
            }

            if (_shadowRenderer != null)
            {
                _shadowRenderer.SetEnabled(false);
                _shadowRenderer.SetLocalOffset(new Vector2(0, GameConfig.TileSize / 4));
            }
        }

        /// <summary>Ends current jump</summary>
        public void EndJump()
        {
            if (!_isJumping) return;
            _isJumping = false;

            EndAirbornePose();

            Debug.Log("[HeroJumpComponent] Ended jump animation");
        }

        /// <summary>Returns true if jumping</summary>
        public bool IsJumping => _isJumping;
    }
}