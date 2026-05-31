using Microsoft.Xna.Framework;
using Nez;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Abstract base class for enemy sprite animations based on facing direction using the Actors.atlas
    /// </summary>
    public abstract class EnemyAnimationComponent : PausableSpriteAnimator, IUpdatable
    {
        private ActorFacingComponent _facing;
        private Direction _lastDirection = Direction.Down;
        private int _lastYSortRow = int.MinValue;
        private TileByTileMover _mover;
        private TiledMapService _tiledMapService;
        private PauseService _pauseService;

        // Wobble state for 1-frame movement animations
        private float _wobbleTimer;
        private bool _wasWobbling;

        // Abstract properties for animation names - each enemy type defines its own
        protected abstract string DefaultAnimation { get; }
        protected abstract string AnimDown { get; }
        protected abstract string AnimLeft { get; }
        protected abstract string AnimRight { get; }
        protected abstract string AnimUp { get; }

        /// <summary>Attack animation name, or null if none exists for this enemy.</summary>
        protected virtual string AnimAttack => null;

        private Color _componentColor = Color.White;
        /// <summary>Gets the tint color for this component (default: White)</summary>
        public Color ComponentColor
        {
            get => _componentColor;
            set { _componentColor = value; }
        }

        public EnemyAnimationComponent(Color color)
        {
            _componentColor = color;
        }

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            _pauseService = Core.Services.GetService<PauseService>();

            try
            {
                var actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
                if (actorsAtlas != null)
                {
                    AddAnimationsFromAtlas(actorsAtlas);
                    Play(DefaultAnimation, LoopMode.Loop);
                    Debug.Log($"[EnemyAnimationComponent] Loaded animations from Actors.atlas and started {DefaultAnimation}");
                }
                else
                {
                    Debug.Warn("[EnemyAnimationComponent] Failed to load Actors.atlas - atlas is null");
                }
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[EnemyAnimationComponent] Failed to load Actors.atlas: {ex.Message}");
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
            var row = (int)(Entity.Transform.Position.Y / GameConfig.TileSize);
            if (row != _lastYSortRow)
            {
                _lastYSortRow = row;
                SetLayerDepth(Mathf.Clamp01(1f - row * GameConfig.TileSize * GameConfig.YSortDepthScale));
            }
            if (_facing == null)
                return;
            var direction = _facing.Facing;
            if (direction != _lastDirection || _facing.ConsumeDirtyFlag())
            {
                _lastDirection = direction;
                UpdateAnimationForDirection(direction);
            }

            CheckFogVisibility();
            UpdateWobble();
        }

        /// <summary>
        /// Applies a subtle rotation wobble when the monster is moving and its animation has only one frame.
        /// </summary>
        private void UpdateWobble()
        {
            if (_mover == null)
                _mover = Entity?.GetComponent<TileByTileMover>();
            if (_mover == null)
                return;

            bool isMoving = _mover.IsMoving;
            bool singleFrame = IsSingleFrameAnimation();

            if (isMoving && singleFrame)
            {
                if (_pauseService?.IsPaused != true)
                    _wobbleTimer += Nez.Time.DeltaTime;
                Entity.Transform.LocalRotation =
                    (float)System.Math.Sin(_wobbleTimer * GameConfig.MonsterWobbleFrequency)
                    * GameConfig.MonsterWobbleAmplitude;
                _wasWobbling = true;
            }
            else if (_wasWobbling)
            {
                Entity.Transform.LocalRotation = 0f;
                _wasWobbling = false;
                _wobbleTimer = 0f;
            }
        }

        /// <summary>Returns true when the active animation consists of a single sprite frame.</summary>
        private bool IsSingleFrameAnimation()
        {
            if (CurrentAnimation == null) return false;
            return CurrentAnimation.Sprites.Length == 1;
        }

        /// <summary>
        /// Plays the named attack animation if it exists in the atlas, otherwise runs a placeholder
        /// that flips and jumps to simulate an attack. Yields until complete.
        /// </summary>
        public System.Collections.IEnumerator PlayAttackAnimation()
        {
            if (AnimAttack != null && Animations != null && Animations.ContainsKey(AnimAttack))
            {
                Play(AnimAttack, LoopMode.Once);
                while (AnimationState == State.Running)
                    yield return null;
                Play(DefaultAnimation, LoopMode.Loop);
            }
            else
            {
                yield return PlayAttackPlaceholder();
            }
        }

        /// <summary>
        /// Placeholder attack: flips the sprite 3 times and bounces it up/down over one second.
        /// Used when no real attack animation exists for this enemy.
        /// </summary>
        private System.Collections.IEnumerator PlayAttackPlaceholder()
        {
            const int flipCount = 3;
            bool originalFlipX = FlipX;
            float originalOffsetY = LocalOffset.Y;

            float elapsed = 0f;
            int lastFlipStage = 0;

            while (elapsed < GameConfig.MonsterAttackPlaceholderDuration)
            {
                if (_pauseService?.IsPaused == true)
                {
                    yield return null;
                    continue;
                }

                elapsed += Nez.Time.DeltaTime;
                float t = System.Math.Min(elapsed / GameConfig.MonsterAttackPlaceholderDuration, 1f);

                // Alternate FlipX across flipCount*2 equal stages
                int stage = (int)(t * flipCount * 2);
                if (stage != lastFlipStage)
                {
                    FlipX = (stage % 2 == 1) ? !originalFlipX : originalFlipX;
                    lastFlipStage = stage;
                }

                // Arc up and down flipCount times using |sin| — visual-only, entity position unchanged
                float jumpT = (float)System.Math.Abs(System.Math.Sin(t * System.Math.PI * flipCount));
                LocalOffset = new Vector2(LocalOffset.X, originalOffsetY - jumpT * GameConfig.MonsterAttackJumpHeight);

                yield return null;
            }

            FlipX = originalFlipX;
            LocalOffset = new Vector2(LocalOffset.X, originalOffsetY);
        }

        /// <summary>
        /// Hides the monster when its tile is fogged and movement is complete.
        /// Re-enabling is handled externally by TileByTileMover since Update() stops when this component is disabled.
        /// </summary>
        private void CheckFogVisibility()
        {
            if (_mover == null)
                _mover = Entity?.GetComponent<TileByTileMover>();
            if (_mover == null)
                return;

            if (_tiledMapService == null)
                _tiledMapService = Core.Services.GetService<TiledMapService>();
            if (_tiledMapService == null)
                return;

            if (_mover.IsMoving)
                return;

            var tile = _mover.GetCurrentTileCoordinates();
            if (_tiledMapService.IsFogOfWarTile(tile.X, tile.Y) && Enabled)
                SetEnabled(false);
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
                Debug.Log($"[EnemyAnimationComponent] Switched to animation: {animationName} for direction: {direction}");
            }
            else
            {
                Debug.Warn($"[EnemyAnimationComponent] Animation {animationName} not found - animations may not be loaded");
            }
        }
    }
}