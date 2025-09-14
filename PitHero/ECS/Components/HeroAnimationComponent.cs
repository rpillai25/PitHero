using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Abstract base class for hero sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public abstract class HeroAnimationComponent : SpriteAnimator, IUpdatable
    {
        private TileByTileMover _tileMover;
        private Direction? _lastDirection = Direction.Down; // Default to down
        
        // Abstract properties for animation names - each layer defines its own
        protected abstract string DefaultAnimation { get; }
        protected abstract string AnimDown { get; }
        protected abstract string AnimLeft { get; }
        protected abstract string AnimRight { get; }
        protected abstract string AnimUp { get; }
        protected abstract string JumpAnimDown { get; }
        protected abstract string JumpAnimLeft { get; }
        protected abstract string JumpAnimRight { get; }
        protected abstract string JumpAnimUp { get; }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            
            try
            {
                // Load the Actors atlas and add all animations
                var actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
                if (actorsAtlas != null)
                {
                    AddAnimationsFromAtlas(actorsAtlas);
                    
                    // Start with default animation
                    Play(DefaultAnimation, LoopMode.Loop);
                    
                    Debug.Log($"[HeroAnimationComponent] Loaded animations from Actors.atlas and started {DefaultAnimation}");
                }
                else
                {
                    Debug.Warn("[HeroAnimationComponent] Failed to load Actors.atlas - atlas is null");
                }
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[HeroAnimationComponent] Failed to load Actors.atlas: {ex.Message}");
                // Component will still function, just without animations
            }
            
            // Get reference to TileByTileMover
            _tileMover = Entity.GetComponent<TileByTileMover>();
        }

        public new void Update()
        {
            // Always let base update run so animation time advances
            base.Update();
            
            if (_tileMover == null) 
                return;

            // Update animation based on movement direction
            var currentDirection = _tileMover.CurrentDirection ?? _lastDirection;
            
            // Only change animation if direction has changed
            if (currentDirection != _lastDirection && currentDirection.HasValue)
            {
                _lastDirection = currentDirection;
                UpdateAnimationForDirection(currentDirection.Value);
            }
        }

        /// <summary>
        /// Plays the appropriate walking animation for the given direction
        /// </summary>
        public void UpdateAnimationForDirection(Direction direction)
        {
            string animationName = direction switch
            {
                Direction.Up => AnimUp,
                Direction.Down => AnimDown,
                Direction.Left => AnimLeft,
                Direction.Right => AnimRight,
                Direction.UpLeft => AnimLeft, // Use left for diagonal up-left
                Direction.UpRight => AnimRight, // Use right for diagonal up-right
                Direction.DownLeft => AnimLeft, // Use left for diagonal down-left
                Direction.DownRight => AnimRight, // Use right for diagonal down-right
                _ => AnimDown // Default fallback
            };

            // Determine desired flip based on direction (all left-ish directions flip)
            bool shouldFlip = direction == Direction.Left || direction == Direction.UpLeft || direction == Direction.DownLeft;
            if (FlipX != shouldFlip)
                SetFlipXAndAdjustLocalOffset(shouldFlip);

            // If the animation itself is already active we can skip re-playing it
            // but only after ensuring flip state above has been corrected.
            if (IsAnimationActive(animationName))
                return;

            // Only try to play if we have animations loaded
            if (Animations != null && Animations.ContainsKey(animationName))
            {
                Play(animationName, LoopMode.Loop);
                Debug.Log($"[HeroAnimationComponent] Switched to animation: {animationName} for direction: {direction}");
            }
            else
            {
                Debug.Warn($"[HeroAnimationComponent] Animation {animationName} not found - animations may not be loaded");
            }
        }

        /// <summary>
        /// Gets the appropriate jump animation name for the given direction
        /// </summary>
        public string GetJumpAnimationNameForDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Up => JumpAnimUp,
                Direction.Down => JumpAnimDown,
                Direction.Left => JumpAnimLeft,
                Direction.Right => JumpAnimRight,
                Direction.UpLeft => JumpAnimLeft, // Use left for diagonal up-left
                Direction.UpRight => JumpAnimRight, // Use right for diagonal up-right
                Direction.DownLeft => JumpAnimLeft, // Use left for diagonal down-left
                Direction.DownRight => JumpAnimRight, // Use right for diagonal down-right
                _ => JumpAnimDown // Default fallback
            };
        }

        /// <summary>
        /// Starts playing a jump animation for the given direction (plays naturally)
        /// </summary>
        public void PlayJumpAnimation(Direction direction)
        {
            string animationName = GetJumpAnimationNameForDirection(direction);
            
            // Set flip immediately based on direction
            bool shouldFlip = direction == Direction.Left || direction == Direction.UpLeft || direction == Direction.DownLeft;
            if (FlipX != shouldFlip)
                SetFlipXAndAdjustLocalOffset(shouldFlip);

            // Only try to play if we have animations loaded
            if (Animations != null && Animations.ContainsKey(animationName))
            {
                Play(animationName, LoopMode.Once);
                Debug.Log($"[HeroAnimationComponent] Started jump animation: {animationName} for direction: {direction}");
            }
            else
            {
                Debug.Warn($"[HeroAnimationComponent] Jump animation {animationName} not found - animations may not be loaded");
            }
        }

        /// <summary>
        /// Updates the jump animation flip state (called during jump updates)
        /// </summary>
        public void UpdateJumpAnimationFlip(Direction direction)
        {
            bool shouldFlip = direction == Direction.Left || direction == Direction.UpLeft || direction == Direction.DownLeft;
            if (FlipX != shouldFlip)
                SetFlipXAndAdjustLocalOffset(shouldFlip);
        }

        /// <summary>
        /// Sets a specific frame (kept for potential manual control usage)
        /// </summary>
        public void SetAnimationFrame(int frameIndex)
        {
            SetFrame(frameIndex);
        }

        /// <summary>
        /// Pauses the current animation
        /// </summary>
        public void PauseAnimation()
        {
            Pause();
        }

        /// <summary>
        /// Unpauses the current animation  
        /// </summary>
        public void UnpauseAnimation()
        {
            UnPause();
        }
    }
}