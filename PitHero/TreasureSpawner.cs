using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;

namespace PitHero
{
    /// <summary>
    /// Runtime treasure-chest spawner (issue #382). PitGenerator creates chests only during
    /// level generation; this mirrors its TAG_TREASURE branch for chests spawned mid-level
    /// with explicit contents — currently the biome main-boss epic drop. Does NOT call
    /// InitializeForPitLevel (contents are supplied, not rolled).
    /// </summary>
    public static class TreasureSpawner
    {
        /// <summary>
        /// Spawns a closed treasure chest at the given tile containing exactly
        /// <paramref name="item"/>. Chest color derives from the item's rarity (#337).
        /// </summary>
        public static Entity SpawnTreasureChestAtTile(Scene scene, Point tile, IItem item)
        {
            var worldPos = new Vector2(
                tile.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                tile.Y * GameConfig.TileSize + GameConfig.TileSize / 2);

            var entity = scene.CreateEntity("treasures");
            entity.SetTag(GameConfig.TAG_TREASURE);
            entity.SetPosition(worldPos);

            // TreasureComponent self-installs renderers, compositor, and FogHideableComponent
            // in OnAddedToEntity; the Level setter drives the wood color.
            var treasureComponent = entity.AddComponent(new TreasureComponent());
            treasureComponent.ContainedItem = item;
            treasureComponent.Level = item is Gear gear
                ? RarityUtils.GetTreasureLevelForRarity(gear.Rarity)
                : 1;

            var collider = entity.AddComponent(new BoxCollider(GameConfig.TileSize, GameConfig.TileSize));
            collider.IsTrigger = true;
            Flags.SetFlagExclusive(ref collider.PhysicsLayer, GameConfig.PhysicsHeroWorldLayer);

            var pitWidthManager = Core.Services?.GetService<PitWidthManager>();
            int currentPitLevel = pitWidthManager?.CurrentPitLevel ?? 1;
            Services.Analytics.AnalyticsService.LogChestSpawned(currentPitLevel, tile.X, tile.Y,
                treasureComponent.Level, treasureComponent.ContainedItem, null, 0, null);

            Debug.Log($"[TreasureSpawner] Spawned level {treasureComponent.Level} chest at tile ({tile.X},{tile.Y}) containing {item?.Name}");
            return entity;
        }
    }
}
