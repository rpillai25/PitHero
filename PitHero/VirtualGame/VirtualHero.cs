using Microsoft.Xna.Framework;
using PitHero.AI;
using System.Collections.Generic;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual hero that simulates hero behavior without Nez dependencies
    /// </summary>
    public class VirtualHero
    {
        private readonly IVirtualWorld _world;

        // GOAP state flags (simplified 7-state model)
        public bool HeroInitialized { get; set; } = true;
        public bool PitInitialized { get; set; } = true;
        public bool InsidePit { get; set; }
        public bool ExploredPit { get; set; }
        public bool FoundWizardOrb { get; set; }
        public bool ActivatedWizardOrb { get; set; }

        // Movement state
        public bool IsMoving { get; set; }
        public Queue<Point> MovementQueue { get; } = new Queue<Point>();
        public Point? TargetTilePosition { get; set; }

        // Current position in tiles
        public Point Position => _world.HeroPosition;
        
        /// <summary>
        /// Current tile position (for tests)
        /// </summary>
        public Point CurrentTilePosition 
        { 
            get => Position; 
            set => _world.MoveHeroTo(value); 
        }

        public VirtualHero(IVirtualWorld world)
        {
            _world = world;
            UpdatePositionStates();
        }

        /// <summary>
        /// Move to a specific tile position
        /// </summary>
        public void MoveTo(Point targetTile)
        {
            _world.MoveHeroTo(targetTile);
            UpdatePositionStates();
            
            System.Console.WriteLine($"[VirtualHero] Moved to ({targetTile.X},{targetTile.Y}), states updated");
        }

        /// <summary>
        /// Set movement path (for pathfinding simulation)
        /// </summary>
        public void SetMovementPath(List<Point> path)
        {
            MovementQueue.Clear();
            foreach (var point in path)
            {
                MovementQueue.Enqueue(point);
            }
            IsMoving = MovementQueue.Count > 0;
            
            System.Console.WriteLine($"[VirtualHero] Set movement path with {path.Count} tiles");
        }

        /// <summary>
        /// Execute one step of movement
        /// </summary>
        public bool ExecuteMovementStep()
        {
            if (MovementQueue.Count == 0)
            {
                IsMoving = false;
                return true; // Movement complete
            }

            var nextTile = MovementQueue.Dequeue();
            MoveTo(nextTile);
            
            if (MovementQueue.Count == 0)
            {
                IsMoving = false;
                return true; // Movement complete
            }
            
            return false; // Still moving
        }

        /// <summary>
        /// Update position-based states based on current location
        /// </summary>
        private void UpdatePositionStates()
        {
            var pos = Position;
            var pitBounds = _world.PitBounds;

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
            var pitBounds = _world.PitBounds;
            
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
            
            System.Console.WriteLine("[VirtualHero] Reset wizard orb workflow states");
        }

        /// <summary>
        /// Get current GOAP world state for planning - updated for simplified 7-state model
        /// </summary>
        public Dictionary<string, bool> GetWorldState()
        {
            var ws = new Dictionary<string, bool>();

            ws[GoapConstants.HeroInitialized] = true;
            ws[GoapConstants.PitInitialized] = PitInitialized;
            
            // Core position states
            if (InsidePit)
                ws[GoapConstants.InsidePit] = true;
            if (!_world.PitBounds.Contains(Position))
                ws[GoapConstants.OutsidePit] = true;

            // Wizard orb workflow states
            if (ActivatedWizardOrb)
                ws[GoapConstants.ActivatedWizardOrb] = true;

            // Check exploration status - MapExplored is now ExploredPit
            if (CheckMapExplored())
                ws[GoapConstants.ExploredPit] = true;

            // Check wizard orb status
            if (CheckWizardOrbFound())
                ws[GoapConstants.FoundWizardOrb] = true;

            return ws;
        }

        /// <summary>
        /// Check if map exploration is complete (no fog in pit)
        /// </summary>
        private bool CheckMapExplored()
        {
            var pitBounds = _world.PitBounds;
            
            // Check explorable area (inside pit boundary)
            for (int x = pitBounds.X + 1; x < pitBounds.Right - 1; x++)
            {
                for (int y = pitBounds.Y + 1; y < pitBounds.Bottom - 1; y++)
                {
                    if (_world.HasFogOfWar(new Point(x, y)))
                        return false; // Still has fog
                }
            }
            
            return true; // All fog cleared
        }

        /// <summary>
        /// Check if wizard orb has been found (fog cleared around it)
        /// </summary>
        private bool CheckWizardOrbFound()
        {
            var orbPos = _world.WizardOrbPosition;
            if (!orbPos.HasValue)
                return false;
            
            return !_world.HasFogOfWar(orbPos.Value);
        }

        /// <summary>
        /// Check if hero is at wizard orb position
        /// </summary>
        private bool CheckAtWizardOrb()
        {
            var orbPos = _world.WizardOrbPosition;
            if (!orbPos.HasValue)
                return false;
            
            return Position == orbPos.Value;
        }
    }
}