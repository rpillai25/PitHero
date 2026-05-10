using Microsoft.Xna.Framework;
using Nez;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Abstract base class for enemy sprite animations based on facing direction using the Actors.atlas
    /// </summary>
    public abstract class EnemyAnimationComponent : PausableSpriteAnimator, IUpdatable
    {
        private ActorFacingComponent _facing;
        private Direction _lastDirection = Direction.Down; // Default to down
        private TileByTileMover _mover;
        private TiledMapService _tiledMapService;

        // Abstract properties for animation names - each enemy type defines its own
        protected abstract string DefaultAnimation { get; }
        protected abstract string AnimDown { get; }
        protected abstract string AnimLeft { get; }
        protected abstract string AnimRight { get; }
        protected abstract string AnimUp { get; }

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
            if (_facing == null)
                return;
            var direction = _facing.Facing;
            if (direction != _lastDirection || _facing.ConsumeDirtyFlag())
            {
                _lastDirection = direction;
                UpdateAnimationForDirection(direction);
            }

            CheckFogVisibility();
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