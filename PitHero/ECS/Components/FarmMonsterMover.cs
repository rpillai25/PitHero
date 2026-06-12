using Microsoft.Xna.Framework;
using Nez;
using PitHero.Services;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Smooth point-to-point mover for farming monsters. Follows a list of tile waypoints in any
    /// direction (including diagonals) at a constant speed — no tile-by-tile snapping. Facing is
    /// quantized to 8 directions so directional animations and FlipX keep working.
    /// </summary>
    public class FarmMonsterMover : Component, IUpdatable, IPausableComponent
    {
        private const int MaxWaypoints = 64;

        private readonly Vector2[] _waypoints = new Vector2[MaxWaypoints];
        private int _count;
        private int _index;
        private ActorFacingComponent _facing;
        private PauseService _pauseService;

        /// <summary>Movement speed in pixels per second.</summary>
        public float MoveSpeed = GameConfig.HeroMovementSpeed;

        public bool ShouldPause => true;

        /// <summary>True while there are waypoints left to reach.</summary>
        public bool IsMoving => _index < _count;

        /// <summary>The tile the entity's position currently falls in.</summary>
        public Point CurrentTile => new Point(
            (int)(Entity.Transform.Position.X / GameConfig.TileSize),
            (int)(Entity.Transform.Position.Y / GameConfig.TileSize));

        public override void OnAddedToEntity()
        {
            _facing = Entity.GetComponent<ActorFacingComponent>();
            _pauseService = Core.Services.GetService<PauseService>();
        }

        /// <summary>Replaces the current path with the given tile waypoints (converted to tile centers).</summary>
        public void SetPath(System.Collections.Generic.List<Point> tilePath)
        {
            _count = tilePath.Count < MaxWaypoints ? tilePath.Count : MaxWaypoints;
            for (int i = 0; i < _count; i++)
            {
                _waypoints[i] = new Vector2(
                    tilePath[i].X * GameConfig.TileSize + GameConfig.TileSize / 2f,
                    tilePath[i].Y * GameConfig.TileSize + GameConfig.TileSize / 2f);
            }
            _index = 0;
        }

        /// <summary>Sets a single world-space destination (used for scripted door steps).</summary>
        public void SetSingleTarget(Vector2 worldPos)
        {
            _waypoints[0] = worldPos;
            _count = 1;
            _index = 0;
        }

        /// <summary>Discards any remaining waypoints, halting in place.</summary>
        public void Stop()
        {
            _count = 0;
            _index = 0;
        }

        public void Update()
        {
            if (_pauseService?.IsPaused == true)
                return;
            if (!IsMoving)
                return;

            var pos = Entity.Transform.Position;
            var target = _waypoints[_index];
            var delta = target - pos;
            float step = MoveSpeed * Time.DeltaTime;
            float distance = delta.Length();

            if (distance <= step)
            {
                Entity.Transform.Position = target;
                _index++;
            }
            else
            {
                delta /= distance;
                Entity.Transform.Position = pos + delta * step;
                UpdateFacing(delta);
            }
        }

        // Quantize the movement vector to the nearest of 8 directions for animation/FlipX purposes.
        private void UpdateFacing(Vector2 dir)
        {
            if (_facing == null)
                return;

            const float diag = 0.4142f;   // tan(22.5°) — boundary between cardinal and diagonal sectors
            bool east = dir.X > 0;
            bool south = dir.Y > 0;
            float ax = System.Math.Abs(dir.X);
            float ay = System.Math.Abs(dir.Y);

            Direction facing;
            if (ay <= ax * diag)
                facing = east ? Direction.Right : Direction.Left;
            else if (ax <= ay * diag)
                facing = south ? Direction.Down : Direction.Up;
            else if (east)
                facing = south ? Direction.DownRight : Direction.UpRight;
            else
                facing = south ? Direction.DownLeft : Direction.UpLeft;

            _facing.SetFacing(facing);
        }
    }
}
