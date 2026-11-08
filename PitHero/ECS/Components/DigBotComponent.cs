using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// The DigBot artifact's robot (issue #452): crawls across the Digging Field one tile every
    /// <see cref="GameConfig.DigBotSecondsPerTile"/> seconds in a zigzag (the top row left to right, the next
    /// right to left, and so on; from the bottom left it heads back up to the top left) and digs every
    /// tile it reaches that is not resting, exactly as a player click would
    /// (<see cref="DiggingFieldService.TryDig"/>, shared pity, gems pay gold).
    ///
    /// Simulation: it runs in the fixed step, times itself with the step's Time.DeltaTime, rolls
    /// the session's sim stream (Nez.Random) only for a dig that happens, and reads nothing but sim
    /// state, so a replay re-creates every bot dig without a command. Its walk is not saved: a load
    /// or a seek restarts it at the top left, which is also where the recording's start state has it.
    /// </summary>
    public class DigBotComponent : Component, IUpdatable, IPausableComponent
    {
        public const string MoveAnimation = "DigBotMoveRight";
        public const string FindAnimation = "DigBotSlideTop";

        private enum Phase { Travel, Pause }

        private SpriteAnimator _animator;
        private PauseService _pauseService;
        private DiggingFieldService _field;
        private GameStateService _gameState;

        private int _fieldX = int.MinValue, _fieldY = int.MinValue;
        private int _scanIndex;
        private Phase _phase;
        private Vector2 _target;
        private float _pauseLeft;

        public bool ShouldPause => true;

        /// <summary>The field tile (0..31, zigzag order) the bot is heading to or standing on.</summary>
        public int ScanIndex => _scanIndex;

        /// <summary>
        /// Field-local column and row of a zigzag scan index: index 0 is the top-left tile, the top row
        /// runs left to right, the next row right to left, and so on, so with the field's four rows the
        /// last index is the bottom-left tile and every step is to a neighboring tile.
        /// </summary>
        public static Point GetScanTile(int scanIndex)
        {
            int w = BuildingConfig.DiggingFieldWidth;
            int row = scanIndex / w;
            int col = scanIndex % w;
            return new Point(row % 2 == 0 ? col : w - 1 - col, row);
        }

        /// <summary>The scan index after this one, wrapping from the bottom left back to the top left.</summary>
        public static int NextScanIndex(int scanIndex)
        {
            return (scanIndex + 1) % DiggingFieldService.TileCount;
        }

        /// <summary>World tile of a scan index inside a field anchored at (anchorX, anchorY).</summary>
        public static Point GetWorldTile(int anchorX, int anchorY, int scanIndex)
        {
            var local = GetScanTile(scanIndex);
            return new Point(anchorX + BuildingConfig.DiggingFieldDxMin + local.X,
                             anchorY + BuildingConfig.DiggingFieldDyMin + local.Y);
        }

        private static Vector2 TileCenter(Point tile)
        {
            return new Vector2(tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f,
                               tile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f);
        }

        public override void OnAddedToEntity()
        {
            _animator = Entity.GetComponent<SpriteAnimator>();
            _pauseService = Core.Services.GetService<PauseService>();
            _field = Core.Services.GetService<DiggingFieldService>();
            _gameState = Core.Services.GetService<GameStateService>();
        }

        public void Update()
        {
            if (_pauseService?.IsPaused == true)
                return;
            var field = _field?.GetField();
            if (field == null)
                return;

            // Placed for the first time, or the field was moved: start over at its top-left tile
            if (field.TileX != _fieldX || field.TileY != _fieldY)
            {
                _fieldX = field.TileX;
                _fieldY = field.TileY;
                _scanIndex = 0;
                _target = TileCenter(GetWorldTile(_fieldX, _fieldY, 0));
                Entity.Transform.Position = _target;
                _phase = Phase.Travel; // already there: arrives (and digs) this step
                PlayMove();
            }

            if (_phase == Phase.Pause)
            {
                _pauseLeft -= Time.DeltaTime;
                if (_pauseLeft <= 0f)
                    HeadToNextTile();
                return;
            }

            var pos = Entity.Transform.Position;
            var delta = _target - pos;
            float distance = delta.Length();
            float step = GameConfig.TileSize / GameConfig.DigBotSecondsPerTile * Time.DeltaTime;
            if (distance > step)
            {
                Entity.Transform.Position = pos + delta / distance * step;
                return;
            }
            Entity.Transform.Position = _target;
            Arrive();
        }

        /// <summary>Digs the tile the bot stands on when it is ready, then moves on (after a pause for a find).</summary>
        private void Arrive()
        {
            var tile = GetWorldTile(_fieldX, _fieldY, _scanIndex);
            int index = _field.GetTileIndex(tile.X, tile.Y);
            if (index >= 0 && !_field.IsCooling(index) && Artifacts.LocalArtifactEffects.HasDigBot(_gameState))
            {
                // Roll the sim stream only for a dig that happens
                long roll = Nez.Random.RNG.NextInt64(Config.GemDigConfig.RollRange);
                if (_field.TryDig(_gameState, tile.X, tile.Y, roll, "digbot_dig", out var find))
                {
                    if (Core.Scene is Scenes.MainGameScene scene)
                        scene.SpawnDigEffect(find, tile.X, tile.Y, GameConfig.DigBotFindStartOffsetY);
                    if (find.Kind != Config.DigFindKind.Nothing)
                    {
                        // The find pops up out of the bot: its lid slides open while it waits
                        _animator?.Play(FindAnimation, SpriteAnimator.LoopMode.ClampForever);
                        _phase = Phase.Pause;
                        _pauseLeft = GameConfig.DigBotFindPauseSeconds;
                        return;
                    }
                }
            }
            HeadToNextTile();
        }

        private void HeadToNextTile()
        {
            _scanIndex = NextScanIndex(_scanIndex);
            _target = TileCenter(GetWorldTile(_fieldX, _fieldY, _scanIndex));
            _phase = Phase.Travel;
            float dx = _target.X - Entity.Transform.Position.X;
            if (_animator != null && dx != 0f)
                _animator.FlipX = dx < 0f; // the art faces right; flipped while heading left
            PlayMove();
        }

        private void PlayMove()
        {
            if (_animator != null && !_animator.IsAnimationActive(MoveAnimation))
                _animator.Play(MoveAnimation, SpriteAnimator.LoopMode.Loop);
        }
    }
}
