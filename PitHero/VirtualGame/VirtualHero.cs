using Microsoft.Xna.Framework;
using PitHero.AI;
using System;
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
        /// Move to a specific tile position - now requires pathfinding, no teleportation
        /// </summary>
        public void MoveTo(Point targetTile)
        {
            var currentPos = Position;
            
            // Only allow single-step movement (adjacent tiles) to enforce pathfinding
            var distance = Math.Abs(targetTile.X - currentPos.X) + Math.Abs(targetTile.Y - currentPos.Y);
            if (distance > 1)
            {
                System.Console.WriteLine($"[VirtualHero] ERROR: Attempted teleportation from ({currentPos.X},{currentPos.Y}) to ({targetTile.X},{targetTile.Y}), distance={distance}");
                throw new InvalidOperationException($"MoveTo only allows adjacent tile movement. Use pathfinding for longer moves. Distance={distance}");
            }
            
            // Check if target is passable
            if (_world.IsCollisionTile(targetTile))
            {
                System.Console.WriteLine($"[VirtualHero] Cannot move to collision tile ({targetTile.X},{targetTile.Y})");
                return;
            }
            
            _world.MoveHeroTo(targetTile);
            UpdatePositionStates();
            
            System.Console.WriteLine($"[VirtualHero] Moved to ({targetTile.X},{targetTile.Y}), states updated");
        }

        /// <summary>
        /// Testing method - allows teleportation for test scenarios
        /// </summary>
        public void TeleportTo(Point targetTile)
        {
            _world.MoveHeroTo(targetTile);
            UpdatePositionStates();
            
            System.Console.WriteLine($"[VirtualHero] Teleported to ({targetTile.X},{targetTile.Y}) for testing");
        }

        /// <summary>
        /// Move via pathfinding to target (for longer distances)
        /// </summary>
        public bool MoveViaPath(Point targetTile, VirtualPathfinder pathfinder)
        {
            var path = pathfinder.CalculatePath(Position, targetTile);
            if (path == null || path.Count == 0)
            {
                System.Console.WriteLine($"[VirtualHero] No path found to ({targetTile.X},{targetTile.Y})");
                return false;
            }
            
            SetMovementPath(path);
            System.Console.WriteLine($"[VirtualHero] Set path to ({targetTile.X},{targetTile.Y}) with {path.Count} steps");
            return true;
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
            
            // Validate movement step is adjacent (protect against bad path calculations)
            var currentPos = Position;
            var distance = Math.Abs(nextTile.X - currentPos.X) + Math.Abs(nextTile.Y - currentPos.Y);
            if (distance > 1)
            {
                System.Console.WriteLine($"[VirtualHero] WARNING: Path contains non-adjacent step from ({currentPos.X},{currentPos.Y}) to ({nextTile.X},{nextTile.Y}), using TeleportTo");
                TeleportTo(nextTile); // Fallback for bad paths
            }
            else
            {
                MoveTo(nextTile);
            }
            
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

            // Update InsidePit based on current position
            InsidePit = pitBounds.Contains(pos);
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
            
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Reset wizard orb workflow states (when exiting pit)
        /// </summary>
        public void ResetWizardOrbStates()
        {
            ActivatedWizardOrb = false;
            
            System.Console.WriteLine("[VirtualHero] Reset wizard orb workflow states");
        }

        /// <summary>
        /// Check if hero is adjacent to pit boundary from outside
        /// </summary>
        public bool AdjacentToPitBoundaryFromOutside()
        {
            var pos = Position;
            var pitBounds = _world.PitBounds;
            
            if (pitBounds.Contains(pos))
                return false; // Inside pit, not outside
                
            var distance = CalculateDistanceToPitBoundary(pos);
            return distance <= GameConfig.PitAdjacencyRadiusTiles;
        }

        /// <summary>
        /// Check if hero is adjacent to pit boundary from inside
        /// </summary>
        public bool AdjacentToPitBoundaryFromInside()
        {
            var pos = Position;
            var pitBounds = _world.PitBounds;
            
            if (!pitBounds.Contains(pos))
                return false; // Outside pit, not inside
                
            // Check if adjacent to pit boundary from inside
            return pos.X == pitBounds.X || pos.X == pitBounds.Right - 1 ||
                   pos.Y == pitBounds.Y || pos.Y == pitBounds.Bottom - 1;
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