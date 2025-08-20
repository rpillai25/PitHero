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
        public bool IsAdjacentToPit { get; private set; }
        public bool IsInsidePit { get; private set; }
        public bool JustJumpedOutOfPit { get; set; }
        public bool IsAtCenter { get; set; }

        // Pit configuration - collision rectangle from (1,2) to (12,10), center at (6,6)
        private readonly Rectangle _pitCollisionRect = new Rectangle(1, 2, 12, 9); // width=12-1+1=12, height=10-2+1=9
        private readonly Point _pitCenter = new Point(6, 6);

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

            if (IsTileMapCollision(other))
            {
                var heroPosition = Entity.Transform.Position;
                var tileCoords = GetTileCoordinates(heroPosition);

                // Check if the collider's tile is within the pit collision rectangle
                if (_pitCollisionRect.Contains(tileCoords))
                {
                    IsAdjacentToPit = true;
                    
                    // If hero is exactly inside the pit area, mark as inside pit
                    if (tileCoords.X >= _pitCollisionRect.X && tileCoords.X < _pitCollisionRect.X + _pitCollisionRect.Width &&
                        tileCoords.Y >= _pitCollisionRect.Y && tileCoords.Y < _pitCollisionRect.Y + _pitCollisionRect.Height)
                    {
                        IsInsidePit = true;
                        
                        // Record milestone for first jump into pit
                        var historian = Entity.GetComponent<Historian>();
                        historian?.RecordMilestone(MilestoneType.FirstJumpIntoPit, Time.TotalTime);
                        
                        // Clear FogOfWar in 4 cardinal directions (side effect)
                        ClearFogOfWarAroundPosition(tileCoords);
                    }
                }
            }
        }

        /// <summary>
        /// Called when hero exits a trigger collider
        /// </summary>
        public override void OnTriggerExit(Collider other, Collider local)
        {
            base.OnTriggerExit(other, local);

            if (IsTileMapCollision(other))
            {
                var heroPosition = Entity.Transform.Position;
                var tileCoords = GetTileCoordinates(heroPosition);

                // Check if hero is leaving the pit area
                if (!_pitCollisionRect.Contains(tileCoords))
                {
                    if (IsInsidePit)
                    {
                        IsInsidePit = false;
                        JustJumpedOutOfPit = true;
                        
                        // Record milestone for first jump out of pit
                        var historian = Entity.GetComponent<Historian>();
                        historian?.RecordMilestone(MilestoneType.FirstJumpOutOfPit, Time.TotalTime);
                    }
                    IsAdjacentToPit = false;
                }
            }
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
            var tileCoords = GetTileCoordinates(position);
            return _pitCollisionRect.Contains(tileCoords);
        }

        /// <summary>
        /// Check if hero is inside the pit
        /// </summary>
        public bool CheckInsidePit(Vector2 position)
        {
            var tileCoords = GetTileCoordinates(position);
            return tileCoords.X >= _pitCollisionRect.X && tileCoords.X < _pitCollisionRect.X + _pitCollisionRect.Width &&
                   tileCoords.Y >= _pitCollisionRect.Y && tileCoords.Y < _pitCollisionRect.Y + _pitCollisionRect.Height;
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