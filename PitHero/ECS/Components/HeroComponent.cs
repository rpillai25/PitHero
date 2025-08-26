using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component for heroes in the game
    /// </summary>
    public class HeroComponent : PathfindingActorComponent
    {
        // GOAP-specific pit boundary flags
        public bool PitInitialized { get; set; }
        public bool AdjacentToPitBoundaryFromOutside { get; set; }
        public bool AdjacentToPitBoundaryFromInside { get; set; }
        public bool InsidePit { get; set; }
        public bool OutsidePit => !InsidePit;
        public Direction? PitApproachDirection { get; set; }
        
        // GOAP-specific wizard orb workflow flags
        public bool ActivatedWizardOrb { get; set; }
        public bool MovingToInsidePitEdge { get; set; }
        public bool ReadyToJumpOutOfPit { get; set; }
        public bool MovingToPitGenPoint { get; set; }

        public bool AtPitGenPoint { get; set; }

        private PitWidthManager _pitWidthManager;

        // Dynamic pit collision rectangle computed from PitWidthManager (falls back to GameConfig)
        private Rectangle PitCollisionRect
        {
            get
            {
                var width = _pitWidthManager?.CurrentPitRectWidthTiles ?? GameConfig.PitRectWidth;
                var height = GameConfig.PitRectHeight;
                return new Rectangle(GameConfig.PitRectX, GameConfig.PitRectY, width, height);
            }
        }

        // Dynamic pit center point computed from PitWidthManager (falls back to GameConfig)
        private Point PitCenter
        {
            get
            {
                var centerX = _pitWidthManager?.CurrentPitCenterTileX ?? GameConfig.PitCenterTileX;
                var centerY = GameConfig.PitCenterTileY;
                return new Point(centerX, centerY);
            }
        }

        // Helper: distance in tiles
        private float DistanceTiles(Point a, Point b) =>
            Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            // Cache PitWidthManager service for dynamic pit sizing
            _pitWidthManager = Core.Services.GetService<PitWidthManager>();

            // Do not override PitInitialized here; it may be set by the spawner.
            // Initialize other GOAP flags to clean state
            AdjacentToPitBoundaryFromOutside = false;
            AdjacentToPitBoundaryFromInside = false;
            InsidePit = false;
            PitApproachDirection = null;
            
            // Initialize wizard orb workflow flags
            ActivatedWizardOrb = false;
            MovingToInsidePitEdge = false;
            ReadyToJumpOutOfPit = false;
            MovingToPitGenPoint = false;
        }

        /// <summary>
        /// Called when hero enters a trigger collider
        /// </summary>
        public override void OnTriggerEnter(Collider other, Collider local)
        {
            base.OnTriggerEnter(other, local);
            
            Debug.Log($"[HeroComponent] OnTriggerEnter: other.Entity.Name={other.Entity.Name}, " +
                      $"other.Entity.Tag={other.Entity.Tag}, " +
                      $"other.PhysicsLayer={other.PhysicsLayer}, " +
                      $"HeroPos={Entity.Transform.Position.X},{Entity.Transform.Position.Y}");
            
            // Handle pit trigger separately from tilemap
            if (other.Entity.Tag == GameConfig.TAG_PIT)
            {
                Debug.Log("[HeroComponent] Detected pit trigger entry");
                HandlePitTriggerEnter();
                return;
            }
            
            // Handle tilemap triggers for FogOfWar clearing
            if (!IsTileMapCollision(other))
                return;

            Debug.Log("[HeroComponent] Detected tilemap trigger entry");
            var tileCoords = GetTileCoordinates(Entity.Transform.Position, GameConfig.TileSize);
            var pitBounds = PitCollisionRect;
            var inside = pitBounds.Contains(tileCoords);

            if (inside)
            {
                // milestone + fog clear when entering pit area via tilemap
                var historian = Entity.GetComponent<Historian>();
                historian?.RecordMilestone(MilestoneType.FirstJumpIntoPit, Time.TotalTime);
            }
        }

        /// <summary>
        /// Called when hero exits a trigger collider
        /// </summary>
        public override void OnTriggerExit(Collider other, Collider local)
        {
            base.OnTriggerExit(other, local);
            
            // Handle pit trigger separately
            if (other.PhysicsLayer == GameConfig.PhysicsPitLayer)
            {
                HandlePitTriggerExit();
                return;
            }
        }

        private void HandlePitTriggerEnter()
        {
            var currentTile = GetCurrentTilePosition();
            var pitBounds = PitCollisionRect;
            
            Debug.Log($"[HeroComponent] HandlePitTriggerEnter: currentTile={currentTile.X},{currentTile.Y}, " +
                      $"pitBounds=({pitBounds.X},{pitBounds.Y},{pitBounds.Width},{pitBounds.Height})");
            
            // Determine approach direction based on current position relative to pit boundaries
            PitApproachDirection = DetermineApproachDirection(currentTile);
            
            // Check if we're approaching from outside the pit boundary
            var wasOutside = !pitBounds.Contains(currentTile);
            
            Debug.Log($"[HeroComponent] wasOutside={wasOutside}, approachDirection={PitApproachDirection}");
            
            // Reset conflicting states first
            AdjacentToPitBoundaryFromOutside = false;
            AdjacentToPitBoundaryFromInside = false;
            
            if (wasOutside)
            {
                AdjacentToPitBoundaryFromOutside = true;
                Debug.Log($"[HeroComponent] Set AdjacentToPitBoundaryFromOutside=true, direction: {PitApproachDirection}");
            }
            else
            {
                AdjacentToPitBoundaryFromInside = true;
                Debug.Log($"[HeroComponent] Set AdjacentToPitBoundaryFromInside=true (already inside pit boundary)");
            }
            
            var historian = Entity.GetComponent<Historian>();
            historian?.RecordMilestone(MilestoneType.FirstJumpIntoPit, Time.TotalTime);
            
            var tileCoords = GetTileCoordinates(Entity.Transform.Position, GameConfig.TileSize);
        }

        private void HandlePitTriggerExit()
        {
            var currentTile = GetCurrentTilePosition();
            var pitBounds = PitCollisionRect;
            
            Debug.Log($"[HeroComponent] HandlePitTriggerExit: currentTile={currentTile.X},{currentTile.Y}, " +
                      $"pitBounds=({pitBounds.X},{pitBounds.Y},{pitBounds.Width},{pitBounds.Height})");
            
            // Only reset flags if hero is actually outside the pit area 
            // This prevents spurious trigger exits from resetting state during normal pit exploration
            if (!pitBounds.Contains(currentTile))
            {
                Debug.Log("[HeroComponent] Hero truly exited pit area - resetting GOAP flags");
                
                // Reset all GOAP flags when actually leaving pit area
                AdjacentToPitBoundaryFromInside = false;
                AdjacentToPitBoundaryFromOutside = false;
                InsidePit = false;
                PitApproachDirection = null;
                
                // Reset wizard orb workflow flags when leaving pit
                ActivatedWizardOrb = false;
                MovingToInsidePitEdge = false;
                ReadyToJumpOutOfPit = false;
                MovingToPitGenPoint = false;
                
                var historian = Entity.GetComponent<Historian>();
                historian?.RecordMilestone(MilestoneType.FirstJumpOutOfPit, Time.TotalTime);
            }
            else
            {
                Debug.Log("[HeroComponent] Hero still inside pit area - ignoring spurious trigger exit");
            }
        }

        /// <summary>
        /// Determine which direction the hero is approaching the pit from
        /// </summary>
        private Direction? DetermineApproachDirection(Point currentTile)
        {
            var pitBounds = PitCollisionRect;
            
            // Check if at specific corner positions
            if (currentTile.X == pitBounds.Left && currentTile.Y == pitBounds.Top)
                return Direction.DownRight; // Upper left corner
            if (currentTile.X == pitBounds.Right - 1 && currentTile.Y == pitBounds.Top)
                return Direction.DownLeft; // Upper right corner
            if (currentTile.X == pitBounds.Right - 1 && currentTile.Y == pitBounds.Bottom - 1)
                return Direction.UpLeft; // Lower right corner
            if (currentTile.X == pitBounds.Left && currentTile.Y == pitBounds.Bottom - 1)
                return Direction.UpRight; // Lower left corner
            
            // Check cardinal directions
            if (currentTile.X < pitBounds.Left)
                return Direction.Right; // Approaching from left
            if (currentTile.X >= pitBounds.Right)
                return Direction.Left; // Approaching from right
            if (currentTile.Y < pitBounds.Top)
                return Direction.Down; // Approaching from above
            if (currentTile.Y >= pitBounds.Bottom)
                return Direction.Up; // Approaching from below
            
            return null; // Already inside
        }

        /// <summary>
        /// Get current tile position using TileByTileMover if available
        /// </summary>
        private Point GetCurrentTilePosition()
        {
            var tileMover = Entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                return tileMover.GetCurrentTileCoordinates();
            }
            
            // Fallback to manual calculation
            return GetTileCoordinates(Entity.Transform.Position, GameConfig.TileSize);
        }

        /// <summary>
        /// Check if hero is adjacent to the pit (collision tile within pit rectangle)
        /// </summary>
        public bool CheckAdjacentToPit(Vector2 position)
        {
            var tile = GetTileCoordinates(position, GameConfig.TileSize);
            if (PitCollisionRect.Contains(tile))
                return false; // inside is not "adjacent"
            return DistanceTiles(tile, PitCenter) <= GameConfig.PitAdjacencyRadiusTiles;
        }

        /// <summary>
        /// Check if hero is inside the pit
        /// </summary>
        public bool CheckInsidePit(Vector2 position)
        {
            var tile = GetTileCoordinates(position, GameConfig.TileSize);
            return PitCollisionRect.Contains(tile);
        }

        /// <summary>
        /// Get the pit center coordinates
        /// </summary>
        public Point GetPitCenter()
        {
            return PitCenter;
        }
    }
}