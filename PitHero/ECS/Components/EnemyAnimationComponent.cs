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
        private TileByTileMover _mover;
        private FarmMonsterMover _farmMover;
        private TiledMapService _tiledMapService;
        private PauseService _pauseService;

        // Wobble state for 1-frame movement animations
        private float _wobbleTimer;
        private bool _wasWobbling;

        // The single movement animation ("[MonsterName]MoveRight") - each enemy type defines its own.
        // Moving left plays it flipped; moving up/down keeps whichever look was last playing.
        protected abstract string AnimMoveRight { get; }

        /// <summary>Attack animation name, or null if none exists for this enemy.</summary>
        protected virtual string AnimAttack => null;

        private bool _tileAnchorApplied;

        // Animator speed multiplier that turns the atlas animation's full rate into the normal (half) rate
        private float _normalSpeedRatio = 1f;
        private GameStateService _gameState;

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
                    // Only this monster's own animations are registered - never the whole atlas
                    bool hasMove = TryAddAnimationFromAtlas(actorsAtlas, AnimMoveRight);
                    if (AnimAttack != null)
                        TryAddAnimationFromAtlas(actorsAtlas, AnimAttack);

                    if (hasMove)
                        Play(AnimMoveRight, LoopMode.Loop);
                    else
                        Debug.Warn($"[EnemyAnimationComponent] Animation {AnimMoveRight} not found in Actors.atlas");
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

            ApplyTileAnchorOffset();
        }

        /// <summary>The monster's portrait: frame 0 of its MoveRight animation, or null when it is not loaded.</summary>
        public Nez.Textures.Sprite PortraitSprite
        {
            get
            {
                if (Animations == null || !Animations.TryGetValue(AnimMoveRight, out var animation))
                    return null;
                return animation.Sprites != null && animation.Sprites.Length > 0 ? animation.Sprites[0] : null;
            }
        }

        /// <summary>Registers one named atlas animation on this animator. Returns false if the atlas lacks it.</summary>
        private bool TryAddAnimationFromAtlas(Nez.Sprites.SpriteAtlas atlas, string name)
        {
            int index = System.Array.IndexOf(atlas.AnimationNames, name);
            if (index < 0)
                return false;
            var animation = atlas.SpriteAnimations[index];
            // The atlas packs every animation at one fps; monsters derive theirs from the frame count.
            // The atlas animation is shared by every monster of the type, so this is idempotent.
            if (animation.FrameRates != null)
            {
                float frameRate = FrameRateForFrameCount(animation.FrameRates.Length);
                for (int i = 0; i < animation.FrameRates.Length; i++)
                    animation.FrameRates[i] = frameRate;
                if (name == AnimMoveRight)
                    _normalSpeedRatio = NormalFrameRateForFrameCount(animation.FrameRates.Length) / frameRate;
            }
            AddAnimation(name, animation);
            return true;
        }

        /// <summary>
        /// Monster animation speed in frames per second: twice the frame count, taken to the nearest
        /// multiple of 5 (4 frames → 8 → 10; 9 frames → 18 → 20), never below 5. Every animation
        /// therefore loops in roughly half a second however many frames it has.
        /// </summary>
        public static float FrameRateForFrameCount(int frameCount)
        {
            int rounded = (frameCount * 2 + 2) / 5 * 5; // 2n is even, so there are no ties to break
            return rounded < 5 ? 5 : rounded;
        }

        /// <summary>
        /// The normal (unboosted) monster animation speed: half of <see cref="FrameRateForFrameCount"/>,
        /// moved up to the nearest multiple of 5 (10 → 5; 15 → 7.5 → 10; 20 → 10). Pit monsters always
        /// animate at this speed; farm and kitchen workers do until the Hermes Boots double their
        /// movement, when they animate at the full rate.
        /// </summary>
        public static float NormalFrameRateForFrameCount(int frameCount)
        {
            int full = (int)FrameRateForFrameCount(frameCount);
            return (full + 9) / 10 * 5;
        }

        /// <summary>
        /// Plays the animation at the normal (half) rate, or at the full rate for a farm/kitchen worker
        /// whose movement the Hermes Boots have doubled. The atlas animation holds the full rate, so
        /// this only moves the animator's speed multiplier. Read every step: the boots can be bought
        /// mid-session.
        /// </summary>
        private void UpdateAnimationSpeed()
        {
            bool boosted = false;
            if (_farmMover != null)
            {
                _gameState ??= Core.Services.GetService<GameStateService>();
                boosted = PitHero.Artifacts.LocalArtifactEffects.GetWorkerMoveSpeedMultiplier(_gameState) > 1f;
            }
            Speed = boosted ? 1f : _normalSpeedRatio;
        }

        /// <summary>
        /// Shifts the sprite up so its lower 32px (one tile) aligns with the entity's tile, making
        /// medium/large monsters visually "stand" on their tile instead of floating centred on it
        /// (their upper body then reads correctly behind top-layer overhangs like the tavern wall).
        /// Pathfinding/collision are unaffected — they already use the entity-centred 32×32 tile.
        /// A no-op for 32px sprites. Y-only, so it survives the FlipX offset flip; the attack
        /// placeholder saves/restores LocalOffset.Y relative to this resting value.
        /// </summary>
        protected void ApplyTileAnchorOffset()
        {
            if (_tileAnchorApplied)
                return;
            var sprite = this.Sprite;
            if (sprite == null)
                return;
            float dy = (GameConfig.TileSize - sprite.SourceRect.Height) / 2f;
            if (dy != 0f)
                LocalOffset = new Vector2(LocalOffset.X, LocalOffset.Y + dy);
            _tileAnchorApplied = true;
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
            UpdateWobble();
            UpdateAnimationSpeed();
        }

        /// <summary>
        /// Applies a subtle rotation wobble when the monster is moving and its animation has only one frame.
        /// </summary>
        private void UpdateWobble()
        {
            if (_mover == null && _farmMover == null)
            {
                _mover = Entity?.GetComponent<TileByTileMover>();
                if (_mover == null)
                    _farmMover = Entity?.GetComponent<FarmMonsterMover>();
                if (_mover == null && _farmMover == null)
                    return;
            }

            bool isMoving = _mover != null ? _mover.IsMoving : _farmMover.IsMoving;
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
                Play(AnimMoveRight, LoopMode.Loop);
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
            // Farm monsters move with FarmMonsterMover and never enter fogged pit tiles
            if (_farmMover != null)
                return;

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
        /// Faces the looping MoveRight animation for the given direction: unflipped when heading right,
        /// flipped ("MoveLeft") when heading left. Up/Down have no animations of their own, so the
        /// current look just keeps playing. The animation itself is never restarted or stopped here.
        /// </summary>
        public void UpdateAnimationForDirection(Direction direction)
        {
            bool shouldFlip;
            switch (direction)
            {
                case Direction.Left:
                case Direction.UpLeft:
                case Direction.DownLeft:
                    shouldFlip = true;
                    break;
                case Direction.Right:
                case Direction.UpRight:
                case Direction.DownRight:
                    shouldFlip = false;
                    break;
                default:
                    return;
            }

            if (FlipX != shouldFlip)
                SetFlipXAndAdjustLocalOffset(shouldFlip);
        }
    }
}