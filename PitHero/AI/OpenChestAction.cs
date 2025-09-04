using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.AI.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to open an adjacent chest
    /// Hero faces the chest, opens it, and the chest is removed from the scene
    /// </summary>
    public class OpenChestAction : HeroActionBase
    {
        public OpenChestAction() : base(GoapConstants.OpenChest, 51)
        {
            // Preconditions: Hero must be adjacent to a chest
            SetPrecondition(GoapConstants.AdjacentToChest, true);
            
            // Postconditions: Chest is opened, recalculate adjacency
            SetPostcondition(GoapConstants.AdjacentToChest, false);
        }

        public override bool Execute(HeroComponent hero)
        {
            Debug.Log("[OpenChest] Starting chest opening");

            // Find the nearest adjacent chest
            var chestEntity = FindNearestAdjacentChest(hero);
            if (chestEntity == null)
            {
                Debug.Warn("[OpenChest] Could not find adjacent chest");
                // Recalculate if there are still chests adjacent to hero
                hero.AdjacentToChest = hero.CheckAdjacentToChest();
                return true; // Complete as failed
            }

            // Face the chest
            FaceTarget(hero, chestEntity.Transform.Position);

            // Open the chest (simulate by removing from scene)
            chestEntity.Destroy();
            Debug.Log("[OpenChest] Chest opened and removed from scene");

            // Recalculate if there are still chests adjacent to hero
            hero.AdjacentToChest = hero.CheckAdjacentToChest();

            Debug.Log("[OpenChest] Chest opening completed successfully");
            return true;
        }

        public override bool Execute(IGoapContext context)
        {
            context.LogDebug("[OpenChest] Starting chest opening with interface-based context");

            // Get current tile position
            var heroTile = context.HeroController.CurrentTilePosition;
            context.LogDebug($"[OpenChest] Hero at tile ({heroTile.X},{heroTile.Y})");

            // Note: Virtual implementation would handle chest removal from virtual world state
            context.LogDebug("[OpenChest] Chest opening completed in virtual context");
            return true;
        }

        /// <summary>
        /// Find the nearest adjacent chest to the hero
        /// </summary>
        private Entity FindNearestAdjacentChest(HeroComponent hero)
        {
            var heroTile = GetCurrentTilePosition(hero);
            var scene = Core.Scene;
            if (scene == null) return null;

            var chestEntities = scene.FindEntitiesWithTag(GameConfig.TAG_TREASURE);
            Entity nearestChest = null;
            float nearestDistance = float.MaxValue;

            foreach (var chest in chestEntities)
            {
                var chestTile = GetTileCoordinates(chest.Transform.Position);
                if (IsAdjacent(heroTile, chestTile))
                {
                    float distance = Vector2.Distance(hero.Entity.Transform.Position, chest.Transform.Position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestChest = chest;
                    }
                }
            }

            return nearestChest;
        }

        /// <summary>
        /// Make hero face the target position
        /// </summary>
        private void FaceTarget(HeroComponent hero, Vector2 targetPosition)
        {
            // For now, just log the direction. Could extend to update sprite direction later
            var direction = targetPosition - hero.Entity.Transform.Position;
            Debug.Log($"[OpenChest] Hero facing direction: ({direction.X},{direction.Y})");
        }

        /// <summary>
        /// Check if two tile positions are adjacent (8-directional adjacency)
        /// </summary>
        private bool IsAdjacent(Point tile1, Point tile2)
        {
            int deltaX = System.Math.Abs(tile1.X - tile2.X);
            int deltaY = System.Math.Abs(tile1.Y - tile2.Y);
            return deltaX <= 1 && deltaY <= 1 && (deltaX + deltaY > 0);
        }

        /// <summary>
        /// Get current tile position from hero component
        /// </summary>
        private Point GetCurrentTilePosition(HeroComponent hero)
        {
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                return tileMover.GetCurrentTileCoordinates();
            }
            
            // Fallback to manual calculation
            return GetTileCoordinates(hero.Entity.Transform.Position);
        }

        /// <summary>
        /// Get the tile coordinates from a world position
        /// </summary>
        private Point GetTileCoordinates(Vector2 worldPosition)
        {
            return new Point((int)(worldPosition.X / GameConfig.TileSize), (int)(worldPosition.Y / GameConfig.TileSize));
        }
    }
}