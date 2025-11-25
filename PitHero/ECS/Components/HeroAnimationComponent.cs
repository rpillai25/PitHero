using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Abstract base class for hero sprite animations based on facing direction using the Actors.atlas
    /// </summary>
    public abstract class HeroAnimationComponent : PausableSpriteAnimator, IUpdatable
    {
        private ActorFacingComponent _facing;
        private Direction _lastDirection = Direction.Down; // Default to down

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

        private Color _componentColor = Color.White;
        /// <summary>Gets the tint color for this component (default: White)</summary>
        public Color ComponentColor
        {
            get => _componentColor;
            set { _componentColor = value; }
        }

        public HeroAnimationComponent(Color color)
        {
            _componentColor = color;
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            
            try
            {
                var actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
                if (actorsAtlas != null)
                {
                    AddAnimationsFromAtlas(actorsAtlas);
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
            }
            
            _facing = Entity.GetComponent<ActorFacingComponent>();
            if (_facing == null)
            {
                _facing = Entity.AddComponent(new ActorFacingComponent());
            }

            this.SetColor(ComponentColor);
        }

        public new void Update()
        {
            base.Update();
            if (_facing == null)
                return;
            var direction = _facing.Facing;
            if (direction != _lastDirection || _facing.ConsumeDirtyFlag())
            {
                _lastDirection = direction;
                UpdateAnimationForDirection(direction);
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
                Direction.UpLeft => AnimLeft,
                Direction.UpRight => AnimRight,
                Direction.DownLeft => AnimLeft,
                Direction.DownRight => AnimRight,
                _ => AnimDown
            };

            bool shouldFlip = direction == Direction.Left || direction == Direction.UpLeft || direction == Direction.DownLeft;
            if (FlipX != shouldFlip)
                SetFlipXAndAdjustLocalOffset(shouldFlip);

            if (IsAnimationActive(animationName))
                return;

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

        /// <summary>Gets the appropriate jump animation name for the given direction</summary>
        public string GetJumpAnimationNameForDirection(Direction direction)
        {
            return direction switch
            {
                Direction.Up => JumpAnimUp,
                Direction.Down => JumpAnimDown,
                Direction.Left => JumpAnimLeft,
                Direction.Right => JumpAnimRight,
                Direction.UpLeft => JumpAnimLeft,
                Direction.UpRight => JumpAnimRight,
                Direction.DownLeft => JumpAnimLeft,
                Direction.DownRight => JumpAnimRight,
                _ => JumpAnimDown
            };
        }

        /// <summary>Starts playing a jump animation for the given direction (plays naturally)</summary>
        public void PlayJumpAnimation(Direction direction)
        {
            string animationName = GetJumpAnimationNameForDirection(direction);
            bool shouldFlip = direction == Direction.Left || direction == Direction.UpLeft || direction == Direction.DownLeft;
            if (FlipX != shouldFlip)
                SetFlipXAndAdjustLocalOffset(shouldFlip);
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

        /// <summary>Unpauses the current animation</summary>
        public void UnpauseAnimation() => UnPause();
    }
}