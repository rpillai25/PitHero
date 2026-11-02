using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Abstract base class for hero sprite animations based on facing direction using the Actors.atlas
    /// </summary>
    public abstract class HeroAnimationComponent : PausableSpriteAnimator, IUpdatable, ICompositeLayer
    {
        private ActorFacingComponent _facing;
        private Direction _lastDirection = Direction.Down; // Default to down
        private readonly HeroLayerNames _names;

        // Animation names come from the layer's HeroSpriteNames entry
        protected string DefaultAnimation => _names.Down;
        protected string AnimDown => _names.Down;
        protected string AnimLeft => _names.Right;   // Flipped in code
        protected string AnimRight => _names.Right;
        protected string AnimUp => _names.Up;
        protected string JumpAnimDown => _names.Jump;
        protected string JumpAnimLeft => _names.Jump;  // Flipped in code
        protected string JumpAnimRight => _names.Jump;
        protected string JumpAnimUp => _names.Jump;

        protected string SleepDown => _names.SleepDown;
        protected string SleepRight => _names.SleepRight;
        protected string SleepLeft => _names.SleepRight;   // Flipped in code, same sprite as SleepRight
        protected string SleepUp => _names.Up;

        private Color _componentColor = Color.White;
        /// <summary>Gets the tint color for this component (default: White)</summary>
        public Color ComponentColor
        {
            get => _componentColor;
            set { _componentColor = value; }
        }

        /// <summary>ICompositeLayer.LayerColor — wraps RenderableComponent.Color field.</summary>
        public Color LayerColor => Color;

        /// <summary>
        /// Set to true by MultiSpriteAnimator so this layer is composited rather than rendered directly.
        /// </summary>
        public bool OwnedByComposite { get; set; }

        public override bool IsVisibleFromCamera(Camera camera)
        {
            if (OwnedByComposite) return false;
            return base.IsVisibleFromCamera(camera);
        }

        /// <summary>Gets the move down animation name for this animation component</summary>
        public string MoveDownAnimationName => AnimDown;

        public HeroAnimationComponent(Color color, HeroLayerNames names)
        {
            _componentColor = color;
            _names = names;
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
            }
            else
            {
                Debug.Warn($"[HeroAnimationComponent] Jump animation {animationName} not found - animations may not be loaded");
            }
        }

        /// <summary>
        /// Plays the appropriate sleep animation for the given direction.
        /// Most layers hold their move animation; EyesBase shows the closed slits and Eyes is empty.
        /// Falls back to the move animation if a sleep animation is not found.
        /// </summary>
        public void PlaySleepAnimationForDirection(Direction direction)
        {
            string animationName = direction switch
            {
                Direction.Up => SleepUp,
                Direction.Down => SleepDown,
                Direction.Left => SleepLeft,
                Direction.Right => SleepRight,
                Direction.UpLeft => SleepLeft,
                Direction.UpRight => SleepRight,
                Direction.DownLeft => SleepLeft,
                Direction.DownRight => SleepRight,
                _ => SleepDown
            };

            bool shouldFlip = direction == Direction.Left || direction == Direction.UpLeft || direction == Direction.DownLeft;
            if (FlipX != shouldFlip)
                SetFlipXAndAdjustLocalOffset(shouldFlip);

            if (Animations != null && Animations.ContainsKey(animationName))
            {
                Play(animationName, LoopMode.Loop);
            }
            else
            {
                // Fall back to the regular walk animation for this direction
                UpdateAnimationForDirection(direction);
            }
        }

        /// <summary>
        /// Instantly freezes this layer in its sleep pose for the given direction. Syncs the
        /// direction cache so the next Update doesn't re-play the walk animation over the pose;
        /// callers must also consume the facing component's dirty flag.
        /// </summary>
        public void ForceSleepPose(Direction direction)
        {
            _lastDirection = direction;
            PlaySleepAnimationForDirection(direction);
            PauseAnimation();
        }

        /// <summary>Updates the jump animation flip state (called during jump updates)</summary>
        public void UpdateJumpAnimationFlip(Direction direction)
        {
            bool shouldFlip = direction == Direction.Left || direction == Direction.UpLeft || direction == Direction.DownLeft;
            if (FlipX != shouldFlip)
                SetFlipXAndAdjustLocalOffset(shouldFlip);
        }

        /// <summary>Sets a specific frame</summary>
        public void SetAnimationFrame(int frameIndex) => SetFrame(frameIndex);

        /// <summary>Pauses the current animation</summary>
        public void PauseAnimation() => Pause();

        /// <summary>Unpauses the current animation</summary>
        public void UnpauseAnimation() => UnPause();
    }
}