using Microsoft.Xna.Framework;
using System.Collections.Generic;
using PitHero.AI.Interfaces;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual hero controller that implements IHeroController without Nez dependencies
    /// </summary>
    public class VirtualHeroController : IHeroController
    {
        private readonly IWorldState _worldState;
        
        // Movement simulation
        private readonly Queue<Point> _movementQueue = new Queue<Point>();
        private bool _isMoving = false;

        // GOAP state flags (matching HeroComponent)
        public bool PitInitialized { get; set; } = true;
        public bool AdjacentToPitBoundaryFromOutside { get; set; }
        public bool AdjacentToPitBoundaryFromInside { get; set; }
        public bool InsidePit { get; set; }
        public bool ActivatedWizardOrb { get; set; }
        public bool MovingToInsidePitEdge { get; set; }
        public bool ReadyToJumpOutOfPit { get; set; }
        public bool MovingToPitGenPoint { get; set; }
        public Direction? PitApproachDirection { get; set; }

        public VirtualHeroController(IWorldState worldState)
        {
            _worldState = worldState;
            UpdatePositionStates();
        }

        public Point CurrentTilePosition => _worldState.HeroPosition;

        public Vector2 CurrentWorldPosition
        {
            get
            {
                // Convert tile position to world position
                var tile = CurrentTilePosition;
                return new Vector2(
                    tile.X * GameConfig.TileSize + GameConfig.HeroWidth / 2f,
                    tile.Y * GameConfig.TileSize + GameConfig.HeroHeight / 2f
                );
            }
        }

        public bool IsMoving => _isMoving;

        public bool StartMoving(Direction direction)
        {
            if (_isMoving) return false;

            var currentPos = CurrentTilePosition;
            var targetPos = currentPos;

            switch (direction)
            {
                case Direction.Up:
                    targetPos.Y--;
                    break;
                case Direction.Down:
                    targetPos.Y++;
                    break;
                case Direction.Left:
                    targetPos.X--;
                    break;
                case Direction.Right:
                    targetPos.X++;
                    break;
                default:
                    return false;
            }

            // Check if target position is passable
            if (!_worldState.IsPassable(targetPos))
            {
                return false;
            }

            // Start movement
            _movementQueue.Enqueue(targetPos);
            _isMoving = true;
            
            // For virtual simulation, complete movement immediately
            ExecuteMovementStep();
            
            return true;
        }

        public void MoveTo(Point tilePosition)
        {
            if (_worldState is VirtualWorldState virtualWorld)
            {
                virtualWorld.MoveHeroTo(tilePosition);
                UpdatePositionStates();
            }
        }

        public void MoveTo(Vector2 worldPosition)
        {
            // Convert world position to tile coordinates
            var tileX = (int)(worldPosition.X / GameConfig.TileSize);
            var tileY = (int)(worldPosition.Y / GameConfig.TileSize);
            MoveTo(new Point(tileX, tileY));
        }

        public void SetMovementPath(List<Point> path)
        {
            _movementQueue.Clear();
            foreach (var point in path)
            {
                _movementQueue.Enqueue(point);
            }
            _isMoving = _movementQueue.Count > 0;
        }

        public bool HasReachedTarget(Point targetTile)
        {
            return CurrentTilePosition == targetTile;
        }

        /// <summary>
        /// Execute one step of movement for virtual simulation
        /// </summary>
        public bool ExecuteMovementStep()
        {
            if (_movementQueue.Count == 0)
            {
                _isMoving = false;
                return true; // Movement complete
            }

            var nextTile = _movementQueue.Dequeue();
            MoveTo(nextTile);
            
            if (_movementQueue.Count == 0)
            {
                _isMoving = false;
                return true; // Movement complete
            }
            
            return false; // Still moving
        }

        /// <summary>
        /// Update position-based states based on current location
        /// </summary>
        private void UpdatePositionStates()
        {
            var pos = CurrentTilePosition;
            var pitBounds = _worldState.PitBounds;

            // Reset all position states first
            AdjacentToPitBoundaryFromOutside = false;
            AdjacentToPitBoundaryFromInside = false;
            InsidePit = false;

            // Check if inside pit
            if (pitBounds.Contains(pos))
            {
                InsidePit = true;
                
                // Check if adjacent to pit boundary from inside
                if (pos.X == pitBounds.X || pos.X == pitBounds.Right - 1 ||
                    pos.Y == pitBounds.Y || pos.Y == pitBounds.Bottom - 1)
                {
                    AdjacentToPitBoundaryFromInside = true;
                }
            }
            else
            {
                // Check if adjacent to pit boundary from outside
                var distance = CalculateDistanceToPitBoundary(pos);
                if (distance <= GameConfig.PitAdjacencyRadiusTiles)
                {
                    AdjacentToPitBoundaryFromOutside = true;
                }
            }
        }

        /// <summary>
        /// Calculate distance from position to pit boundary
        /// </summary>
        private float CalculateDistanceToPitBoundary(Point pos)
        {
            var pitBounds = _worldState.PitBounds;
            
            // Calculate distance to nearest edge of pit rectangle
            int dx = 0;
            if (pos.X < pitBounds.X)
                dx = pitBounds.X - pos.X;
            else if (pos.X >= pitBounds.Right)
                dx = pos.X - (pitBounds.Right - 1);
            
            int dy = 0;
            if (pos.Y < pitBounds.Y)
                dy = pitBounds.Y - pos.Y;
            else if (pos.Y >= pitBounds.Bottom)
                dy = pos.Y - (pitBounds.Bottom - 1);
            
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Reset wizard orb workflow states (when exiting pit)
        /// </summary>
        public void ResetWizardOrbStates()
        {
            ActivatedWizardOrb = false;
            MovingToInsidePitEdge = false;
            ReadyToJumpOutOfPit = false;
            MovingToPitGenPoint = false;
        }
    }
}