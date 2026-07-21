using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Dining;

namespace PitHero.Services
{
    /// <summary>
    /// Spawns and despawns world-space dish entities (small 12×12 sprites on tables/stoves).
    /// Pattern mirrors DroppedCropService. Register in MainGameScene like DroppedCropService.
    /// </summary>
    public class DishEntityService
    {
        private Scene _scene;
        private SpriteAtlas _cropsAtlas;

        /// <summary>Provides the scene used to spawn dish entities.</summary>
        public void SetScene(Scene scene) => _scene = scene;

        private SpriteAtlas CropsAtlas
            => _cropsAtlas ??= Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");

        /// <summary>
        /// Spawns a small (12×12) dish sprite bottom-aligned at the given tile's center-bottom.
        /// Returns null when the scene is not set or the sprite is missing.
        /// </summary>
        public Entity SpawnDishAtTile(DishType dish, Point tile)
        {
            if (_scene == null)
                return null;

            var def = DishConfig.GetDefinition(dish);
            var sprite = CropsAtlas?.GetSprite(def.BaseSpriteName + "_Small");
            if (sprite == null)
                return null;

            float sprH = sprite.SourceRect.Height;
            float wx = tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f;
            float wy = tile.Y * GameConfig.TileSize + GameConfig.TileSize - sprH / 2f;

            return SpawnEntity(sprite, new Vector2(wx, wy), dish.ToString());
        }

        /// <summary>
        /// Spawns a small (12×12) dish sprite centered at the given world position.
        /// Returns null when the scene is not set or the sprite is missing.
        /// </summary>
        public Entity SpawnDishAtWorldPos(DishType dish, Vector2 centerPos)
        {
            if (_scene == null)
                return null;

            var def = DishConfig.GetDefinition(dish);
            var sprite = CropsAtlas?.GetSprite(def.BaseSpriteName + "_Small");
            if (sprite == null)
                return null;

            return SpawnEntity(sprite, centerPos, dish.ToString());
        }

        /// <summary>
        /// Spawns an EmptyPlate (12×12) sprite centered at the given world position.
        /// Returns null when the scene is not set or the sprite is missing.
        /// </summary>
        public Entity SpawnEmptyPlateAtWorldPos(Vector2 centerPos)
        {
            if (_scene == null)
                return null;

            var sprite = CropsAtlas?.GetSprite("EmptyPlate");
            if (sprite == null)
                return null;

            return SpawnEntity(sprite, centerPos, "EmptyPlate");
        }

        /// <summary>Destroys a spawned dish entity. Safe to call with null.</summary>
        public void Despawn(Entity entity)
        {
            entity?.Destroy();
        }

        private Entity SpawnEntity(Sprite sprite, Vector2 worldPos, string nameSuffix)
        {
            var entity = _scene.CreateEntity("dish-" + nameSuffix + "-" + worldPos.X + "-" + worldPos.Y);
            entity.SetPosition(worldPos);
            var renderer = entity.AddComponent(new SpriteRenderer(sprite));
            renderer.SetRenderLayer(GameConfig.RenderLayerDroppedItems);
            return entity;
        }
    }
}
