using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroAnimationComponent : SpriteAnimator, IUpdatable
    {
        private TileByTileMover _tileMover;
        private Direction? _lastDirection = Direction.Down; // Default to down
        private const string DEFAULT_ANIMATION = "BlueHairHeroDown";
        
        // Animation names that correspond to the atlas
        private const string ANIM_DOWN = "BlueHairHeroDown";
        private const string ANIM_LEFT = "BlueHairHeroLeft";
        private const string ANIM_RIGHT = "BlueHairHeroRight";
        private const string ANIM_UP = "BlueHairHeroUp";

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
                    Play(DEFAULT_ANIMATION, LoopMode.Loop);
                    
                    Debug.Log($"[HeroAnimationComponent] Loaded animations from Actors.atlas and started {DEFAULT_ANIMATION}");
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
            // Call base SpriteAnimator update first
            base.Update();
            
            if (_tileMover == null) 
                return;

            // Update animation based on movement direction
            var currentDirection = _tileMover.CurrentDirection ?? _lastDirection;
            
            // Only change animation if direction has changed
            if (currentDirection != _lastDirection)
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
                Direction.Up => ANIM_UP,
                Direction.Down => ANIM_DOWN,
                Direction.Left => ANIM_LEFT,
                Direction.Right => ANIM_RIGHT,
                Direction.UpLeft => ANIM_LEFT, // Use left for diagonal up-left
                Direction.UpRight => ANIM_RIGHT, // Use right for diagonal up-right
                Direction.DownLeft => ANIM_LEFT, // Use left for diagonal down-left
                Direction.DownRight => ANIM_RIGHT, // Use right for diagonal down-right
                _ => ANIM_DOWN // Default fallback
            };

            if (IsAnimationActive(animationName))
                return; // Already playing this animation

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
    }
}