using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to open an adjacent chest
    /// Hero faces the chest, opens it, and the chest is removed from the scene
    /// </summary>
    public class OpenChestAction : HeroActionBase
    {
        public OpenChestAction() : base(GoapConstants.OpenChest, 2)
        {
            // Preconditions: Hero must be adjacent to a chest
            SetPrecondition(GoapConstants.AdjacentToChest, true);
            
            // Postconditions: Chest is opened, recalculate adjacency
            SetPostcondition(GoapConstants.AdjacentToChest, false);
        }

        public override bool Execute(HeroComponent hero)
        {
            Debug.Log("[OpenChest] Starting chest opening");

            // Find the nearest adjacent CLOSED chest
            var chestEntity = FindNearestAdjacentClosedChest(hero);
            if (chestEntity == null)
            {
                Debug.Warn("[OpenChest] Could not find adjacent CLOSED chest");
                // Recalculate if there are still chests adjacent to hero
                hero.AdjacentToChest = hero.CheckAdjacentToChest();
                return true; // Complete as no-op
            }

            // Face the chest
            FaceTarget(hero, chestEntity.Transform.Position);

            // Open the chest by changing its state instead of removing it
            var treasureComponent = chestEntity.GetComponent<TreasureComponent>();
            if (treasureComponent != null)
            {
                treasureComponent.State = TreasureComponent.TreasureState.OPEN;
                Debug.Log("[OpenChest] Chest state changed to OPEN");
            }
            else
            {
                Debug.Warn("[OpenChest] Chest entity does not have TreasureComponent, falling back to removal");
                chestEntity.Destroy();
            }

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
        /// Find the nearest adjacent CLOSED chest to the hero (cardinal adjacency)
        /// </summary>
        private Entity FindNearestAdjacentClosedChest(HeroComponent hero)
        {
            var heroTile = GetCurrentTilePosition(hero);
            var scene = Core.Scene;
            if (scene == null) return null;

            var chestEntities = scene.FindEntitiesWithTag(GameConfig.TAG_TREASURE);
            Entity nearestChest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < chestEntities.Count; i++)
            {
                var chest = chestEntities[i];
                var treasureComponent = chest.GetComponent<TreasureComponent>();
                if (treasureComponent == null || treasureComponent.State != TreasureComponent.TreasureState.CLOSED)
                    continue;

                var chestTile = GetTileCoordinates(chest.Transform.Position);
                if (IsCardinalAdjacent(heroTile, chestTile))
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
            var direction = targetPosition - hero.Entity.Transform.Position;
            Debug.Log($"[OpenChest] Hero facing direction: ({direction.X},{direction.Y})");
        }

        /// <summary>
        /// Check if two tile positions are adjacent in cardinal directions (N/S/E/W only)
        /// </summary>
        private bool IsCardinalAdjacent(Point tile1, Point tile2)
        {
            int dx = System.Math.Abs(tile1.X - tile2.X);
            int dy = System.Math.Abs(tile1.Y - tile2.Y);
            return (dx + dy) == 1;
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