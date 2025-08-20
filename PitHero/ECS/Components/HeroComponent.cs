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

        // Track whether hero is adjacent to or inside the pit
        public bool IsAdjacentToPit { get; set; }
        public bool IsInsidePit { get; set; }
        public bool JustJumpedOutOfPit { get; set; }
        public bool IsAtCenter { get; set; }

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
            // Initialize any hero-specific logic
        }

        /// <summary>
        /// Called when hero enters a trigger collider
        /// </summary>
        public override void OnTriggerEnter(Collider other, Collider local)
        {
            base.OnTriggerEnter(other, local);
            
            // Handle pit trigger separately from tilemap
            if (other.PhysicsLayer == GameConfig.PhysicsPitLayer)
            {
                HandlePitTriggerEnter();
                return;
            }
            
            // Handle tilemap triggers for position tracking
            if (!IsTileMapCollision(other))
                return;

            var tileCoords = GetTileCoordinates(Entity.Transform.Position, GameConfig.TileSize);

            var inside = _pitCollisionRect.Contains(tileCoords);
            IsInsidePit = inside;

            // Adjacent: NOT inside, but within radius from pit center
            if (!inside)
            {
                var dist = DistanceTiles(tileCoords, _pitCenter);
                IsAdjacentToPit = dist <= GameConfig.PitAdjacencyRadiusTiles;
            }

            if (inside)
            {
                // milestone + fog clear (as before)
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
            
            if (!IsTileMapCollision(other))
                return;

            var tileCoords = GetTileCoordinates(Entity.Transform.Position, GameConfig.TileSize);

            var inside = _pitCollisionRect.Contains(tileCoords);
            IsInsidePit = inside;

            if (!inside)
            {
                // Leaving pit -> just jumped out
                if (JustJumpedOutOfPit == false && IsInsidePit)
                {
                    JustJumpedOutOfPit = true;
                    var historian = Entity.GetComponent<Historian>();
                    historian?.RecordMilestone(MilestoneType.FirstJumpOutOfPit, Time.TotalTime);
                }

                var dist = DistanceTiles(tileCoords, _pitCenter);
                IsAdjacentToPit = dist <= GameConfig.PitAdjacencyRadiusTiles;
                if (dist > GameConfig.PitAdjacencyRadiusTiles)
                    IsAdjacentToPit = false;
            }
        }

        private void HandlePitTriggerEnter()
        {
            IsInsidePit = true;
            IsAdjacentToPit = false;
            
            var historian = Entity.GetComponent<Historian>();
            historian?.RecordMilestone(MilestoneType.FirstJumpIntoPit, Time.TotalTime);
            
            var tileCoords = GetTileCoordinates(Entity.Transform.Position, GameConfig.TileSize);
            ClearFogOfWarAroundPosition(tileCoords);
        }

        private void HandlePitTriggerExit()
        {
            IsInsidePit = false;
            JustJumpedOutOfPit = true;
            
            var historian = Entity.GetComponent<Historian>();
            historian?.RecordMilestone(MilestoneType.FirstJumpOutOfPit, Time.TotalTime);
            
            // Check if we're still adjacent
            var tileCoords = GetTileCoordinates(Entity.Transform.Position, GameConfig.TileSize);
            var dist = DistanceTiles(tileCoords, _pitCenter);
            IsAdjacentToPit = dist <= GameConfig.PitAdjacencyRadiusTiles;
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