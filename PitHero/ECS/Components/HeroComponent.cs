using System.Linq;
using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component for heroes in the game
    /// </summary>
    public class HeroComponent : ActorComponent
    {
        public float MoveSpeed { get; set; } = GameConfig.HeroMoveSpeed;

        // GOAP-specific pit boundary flags
        public bool AdjacentToPitBoundaryFromOutside { get; set; }
        public bool AdjacentToPitBoundaryFromInside { get; set; }
        public bool EnteredPit { get; set; }
        public Direction? PitApproachDirection { get; set; }

        // Pit configuration - collision rectangle from (1,2) to (12,10), center at (6,6)
        private readonly Rectangle _pitCollisionRect = new Rectangle(
            GameConfig.PitRectX,
            GameConfig.PitRectY,
            GameConfig.PitRectWidth,
            GameConfig.PitRectHeight
        );
        private readonly Point _pitCenter = new Point(GameConfig.PitCenterTileX, GameConfig.PitCenterTileY);

        // Helper: distance in tiles
        private float DistanceTiles(Point a, Point b) =>
            Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            
            // Initialize GOAP flags to clean state
            AdjacentToPitBoundaryFromOutside = false;
            AdjacentToPitBoundaryFromInside = false;
            EnteredPit = false;
            PitApproachDirection = null;
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
            var inside = _pitCollisionRect.Contains(tileCoords);

            if (inside)
            {
                // milestone + fog clear when entering pit area via tilemap
                var historian = Entity.GetComponent<Historian>();
                historian?.RecordMilestone(MilestoneType.FirstJumpIntoPit, Time.TotalTime);
                ClearFogOfWarAroundPosition(tileCoords);
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
            
            // No additional tilemap exit handling needed
        }

        private void HandlePitTriggerEnter()
        {
            var currentTile = GetCurrentTilePosition();
            
            Debug.Log($"[HeroComponent] HandlePitTriggerEnter: currentTile={currentTile.X},{currentTile.Y}, " +
                      $"pitBounds=({_pitCollisionRect.X},{_pitCollisionRect.Y},{_pitCollisionRect.Width},{_pitCollisionRect.Height})");
            
            // Determine approach direction based on current position relative to pit boundaries
            PitApproachDirection = DetermineApproachDirection(currentTile);
            
            // Check if we're approaching from outside the pit boundary
            var wasOutside = !_pitCollisionRect.Contains(currentTile);
            
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
            ClearFogOfWarAroundPosition(tileCoords);
        }

        private void HandlePitTriggerExit()
        {
            // Reset all GOAP flags when leaving pit trigger
            AdjacentToPitBoundaryFromInside = false;
            AdjacentToPitBoundaryFromOutside = false;
            EnteredPit = false;
            PitApproachDirection = null;
            
            var historian = Entity.GetComponent<Historian>();
            historian?.RecordMilestone(MilestoneType.FirstJumpOutOfPit, Time.TotalTime);
        }

        /// <summary>
        /// Determine which direction the hero is approaching the pit from
        /// </summary>
        private Direction? DetermineApproachDirection(Point currentTile)
        {
            var pitBounds = _pitCollisionRect;
            
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
        /// Clear FogOfWar in the 4 cardinal directions around the given position
        /// This is a side effect when hero enters the pit
        /// </summary>
        private void ClearFogOfWarAroundPosition(Point centerTile)
        {
            // Find FogOfWar helper in the scene
            var scene = Entity.Scene;
            if (scene != null)
            {
                for (int i = 0; i < scene.Entities.Count; i++)
                {
                    var entity = scene.Entities[i];
                    var fogHelper = entity?.GetComponent<FogOfWarHelper>();
                    if (fogHelper != null)
                    {
                        fogHelper.ClearFogOfWarAroundTile(centerTile.X, centerTile.Y);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Check if hero is adjacent to the pit (collision tile within pit rectangle)
        /// </summary>
        public bool CheckAdjacentToPit(Vector2 position)
        {
            var tile = GetTileCoordinates(position, GameConfig.TileSize);
            if (_pitCollisionRect.Contains(tile))
                return false; // inside is not "adjacent"
            return DistanceTiles(tile, _pitCenter) <= GameConfig.PitAdjacencyRadiusTiles;
        }

        /// <summary>
        /// Check if hero is inside the pit
        /// </summary>
        public bool CheckInsidePit(Vector2 position)
        {
            var tile = GetTileCoordinates(position, GameConfig.TileSize);
            return _pitCollisionRect.Contains(tile);
        }

        /// <summary>
        /// Get the pit center coordinates
        /// </summary>
        public Point GetPitCenter()
        {
            return _pitCenter;
        }
    }
}