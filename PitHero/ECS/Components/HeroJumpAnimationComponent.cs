using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero jump animations and shadow rendering during pit jumping actions
    /// </summary>
    public class HeroJumpAnimationComponent : Component, IUpdatable
    {
        // Jump animation names that correspond to the atlas
        private const string JUMP_ANIM_DOWN = "BlueHairHeroJumpDown";
        private const string JUMP_ANIM_LEFT = "BlueHairHeroJumpLeft";
        private const string JUMP_ANIM_RIGHT = "BlueHairHeroJumpRight";
        private const string JUMP_ANIM_UP = "BlueHairHeroJumpUp";
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
        private string _currentJumpAnimationName;

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            // Get reference to the main hero animator
            _heroAnimator = Entity.GetComponent<HeroAnimationComponent>();
            if (_heroAnimator == null)
            {
                Debug.Warn("[HeroJumpAnimationComponent] HeroAnimationComponent not found on entity");
                return;
            }

            // Load the Actors atlas
            try
            {
                _actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
                if (_actorsAtlas == null)
                {
                    Debug.Warn("[HeroJumpAnimationComponent] Failed to load Actors.atlas - atlas is null");
                    return;
                }

                // Create shadow renderer
                CreateShadowRenderer();

                Debug.Log("[HeroJumpAnimationComponent] Initialized successfully");
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[HeroJumpAnimationComponent] Failed to load Actors.atlas: {ex.Message}");
            }
        }

        private void CreateShadowRenderer()
        {
            if (_actorsAtlas == null) return;

            // Try to get the HeroShadow sprite from the atlas
            var shadowSprite = _actorsAtlas.GetSprite(HERO_SHADOW_SPRITE);
            if (shadowSprite == null)
            {
                Debug.Warn($"[HeroJumpAnimationComponent] {HERO_SHADOW_SPRITE} sprite not found in atlas");
                return;
            }

            // Create shadow renderer and add it to the entity
            _shadowRenderer = Entity.AddComponent(new SpriteRenderer(shadowSprite));
            _shadowRenderer.SetRenderLayer(GameConfig.RenderLayerActors + 1); // Below hero
            _shadowRenderer.SetLocalOffset(new Vector2(0, GameConfig.TileSize / 2)); // At bottom of hero
            _shadowRenderer.SetEnabled(false); // Hidden by default

            Debug.Log($"[HeroJumpAnimationComponent] Created shadow renderer with sprite {HERO_SHADOW_SPRITE}");
        }

        public void Update()
        {
            if (!_isJumping) return;

            var elapsed = Time.TotalTime - _jumpStartTime;
            var progress = elapsed / _jumpDuration;

            if (progress >= 1.0f)
            {
                // Jump completed
                EndJump();
                return;
            }

            // Update jump animation frame and Y offset based on progress
            UpdateJumpAnimation(progress);
        }

        /// <summary>
        /// Start a jump animation in the specified direction for the given duration
        /// </summary>
        public void StartJump(Direction direction, float duration)
        {
            _jumpDirection = direction;
            _jumpDuration = duration;
            _jumpStartTime = Time.TotalTime;
            _isJumping = true;

            // Only proceed with graphics if components are available
            if (_heroAnimator == null || _actorsAtlas == null)
            {
                Debug.Warn("[HeroJumpAnimationComponent] StartJump called but graphics components not available (this is normal in tests)");
                return;
            }

            // Store the initial Y offset
            _initialYOffset = _heroAnimator.LocalOffset.Y;

            // Get the jump animation name for this direction
            _currentJumpAnimationName = GetJumpAnimationNameForDirection(direction);

            // Show shadow
            if (_shadowRenderer != null)
            {
                _shadowRenderer.SetEnabled(true);
            }

            Debug.Log($"[HeroJumpAnimationComponent] Started jump animation for direction {direction} with duration {duration}s");
        }

        /// <summary>
        /// End the current jump animation and reset everything
        /// </summary>
        public void EndJump()
        {
            if (!_isJumping) return;

            _isJumping = false;

            // Reset hero animator Y offset
            if (_heroAnimator != null)
            {
                _heroAnimator.SetLocalOffset(new Vector2(_heroAnimator.LocalOffset.X, _initialYOffset));
            }

            // Hide shadow
            if (_shadowRenderer != null)
            {
                _shadowRenderer.SetEnabled(false);
            }

            // Clear jump state
            _currentJumpAnimationName = null;

            // Restore walking animation in the same direction and ensure animator is running
            if (_heroAnimator != null)
            {
                // Switch back to directional walk animation
                _heroAnimator.UpdateAnimationForDirection(_jumpDirection);
                // Ensure animator is not paused
                _heroAnimator.UnPause();
            }

            Debug.Log("[HeroJumpAnimationComponent] Ended jump animation");
        }

        /// <summary>
        /// Updates the jump animation frame and smoothly interpolates the vertical offset using a parabolic arc
        /// </summary>
        private void UpdateJumpAnimation(float progress)
        {
            if (_heroAnimator == null || string.IsNullOrEmpty(_currentJumpAnimationName)) return;

            _heroAnimator.Play(_currentJumpAnimationName, SpriteAnimator.LoopMode.Once);
            _heroAnimator.Pause();

            // Clamp progress to [0,1]
            if (progress < 0f) progress = 0f;
            if (progress > 1f) progress = 1f;

            // Choose which frame to show based on progress (keep existing 2-frame look)
            int frameIndex;
            if (progress <= 0.25f)
            {
                frameIndex = 0;
            }
            else if (progress <= 0.75f)
            {
                frameIndex = 1;
            }
            else
            {
                frameIndex = 0;
            }

            _heroAnimator.SetFrame(frameIndex);

            // Smooth parabolic vertical offset: 0 at start/end, peak at mid
            // heightFactor = 4t(1-t) in [0,1]; multiply by MAX_JUMP_HEIGHT_PX for pixels
            var t = progress;
            var heightFactor = 4f * t * (1f - t);
            var yOffset = _initialYOffset - (MAX_JUMP_HEIGHT_PX * heightFactor);

            // Apply Y offset
            _heroAnimator.SetLocalOffset(new Vector2(_heroAnimator.LocalOffset.X, yOffset));
        }

        private string GetJumpAnimationNameForDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Up => JUMP_ANIM_UP,
                Direction.Down => JUMP_ANIM_DOWN,
                Direction.Left => JUMP_ANIM_LEFT,
                Direction.Right => JUMP_ANIM_RIGHT,
                Direction.UpLeft => JUMP_ANIM_LEFT, // Use left for diagonal up-left
                Direction.UpRight => JUMP_ANIM_RIGHT, // Use right for diagonal up-right
                Direction.DownLeft => JUMP_ANIM_LEFT, // Use left for diagonal down-left
                Direction.DownRight => JUMP_ANIM_RIGHT, // Use right for diagonal down-right
                _ => JUMP_ANIM_DOWN // Default fallback
            };
        }

        /// <summary>
        /// Check if currently jumping
        /// </summary>
        public bool IsJumping => _isJumping;
    }
}