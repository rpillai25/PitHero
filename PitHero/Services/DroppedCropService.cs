using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using PitHero.Farming;
using PitHero.Util;

namespace PitHero.Services
{
    /// <summary>A harvested-crop stack dropped on the farm ground, awaiting pickup by a monster.</summary>
    public class DroppedCrop
    {
        public CropType Type;
        public int Count;
        public Point Tile;
        public Entity GroundEntity;  // runtime entity; null until spawned/restored
        public bool Claimed;         // a worker is currently walking to pick this up
    }

    /// <summary>
    /// Tracks harvested crops dropped on the ground when a carrying monster has nowhere to deposit
    /// them (all Crop Storage buildings full, or the target storage was sold). Each drop is rendered
    /// as a ground entity; farming monsters pick drops back up and carry them to storage once a
    /// storage has room (see <see cref="FarmTaskCoordinator"/> pickup queue). Persisted across
    /// save/load (save version 12+).
    /// </summary>
    public class DroppedCropService
    {
        private readonly List<DroppedCrop> _drops = new List<DroppedCrop>();
        private Scene _scene;
        private SpriteAtlas _cropsAtlas;

        /// <summary>Provides the scene used to spawn dropped-crop ground entities.</summary>
        public void SetScene(Scene scene) => _scene = scene;

        private SpriteAtlas CropsAtlas
            => _cropsAtlas ??= Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");

        /// <summary>All current drops (for serialization and pickup-queue population).</summary>
        public IReadOnlyList<DroppedCrop> GetAll() => _drops;

        /// <summary>
        /// Drops <paramref name="count"/> units of <paramref name="crop"/> at (or near) the given
        /// tile. Merges into an existing same-type drop on that tile; if a different-type drop already
        /// occupies the tile, the new drop is placed on the nearest drop-free tile so per-tile lookups
        /// stay unambiguous.
        /// </summary>
        public void Drop(CropType crop, int count, Point tile)
        {
            if (count <= 0)
                return;

            var existing = FindAt(tile);
            if (existing != null && existing.Type == crop)
            {
                existing.Count += count;
                return;
            }
            if (existing != null)
                tile = FindNearestFreeTile(tile);

            var drop = new DroppedCrop { Type = crop, Count = count, Tile = tile };
            SpawnEntity(drop);
            _drops.Add(drop);
        }

        /// <summary>Returns the drop at a tile, or null.</summary>
        public bool TryGetAt(Point tile, out DroppedCrop drop)
        {
            drop = FindAt(tile);
            return drop != null;
        }

        /// <summary>Marks the drop at a tile as claimed by a worker (so others skip it).</summary>
        public void ClaimAt(Point tile)
        {
            var d = FindAt(tile);
            if (d != null)
                d.Claimed = true;
        }

        /// <summary>Un-claims the drop at a tile (worker gave up before pickup).</summary>
        public void ReleaseAt(Point tile)
        {
            var d = FindAt(tile);
            if (d != null)
                d.Claimed = false;
        }

        /// <summary>Removes the drop at a tile and destroys its ground entity (crop was picked up).</summary>
        public void RemoveAt(Point tile)
        {
            var d = FindAt(tile);
            if (d == null)
                return;
            d.GroundEntity?.Destroy();
            _drops.Remove(d);
        }

        /// <summary>Removes all drops and their entities (scene teardown / before loading a save).</summary>
        public void Clear()
        {
            for (int i = 0; i < _drops.Count; i++)
                _drops[i].GroundEntity?.Destroy();
            _drops.Clear();
        }

        /// <summary>Restores a drop from saved data (respawns its ground entity).</summary>
        public void Restore(CropType crop, int count, Point tile)
        {
            if (count <= 0)
                return;
            var drop = new DroppedCrop { Type = crop, Count = count, Tile = tile };
            SpawnEntity(drop);
            _drops.Add(drop);
        }

        private DroppedCrop FindAt(Point tile)
        {
            for (int i = 0; i < _drops.Count; i++)
                if (_drops[i].Tile == tile)
                    return _drops[i];
            return null;
        }

        private Point FindNearestFreeTile(Point origin)
        {
            // Search outward in square rings for the nearest tile with no existing drop.
            for (int r = 1; r < 8; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (System.Math.Abs(dx) != r && System.Math.Abs(dy) != r)
                            continue; // ring perimeter only
                        var t = new Point(origin.X + dx, origin.Y + dy);
                        if (FindAt(t) == null)
                            return t;
                    }
                }
            }
            return origin; // give up — overlap (extremely unlikely)
        }

        private void SpawnEntity(DroppedCrop drop)
        {
            if (_scene == null)
                return;
            var sprite = CropsAtlas?.GetSprite(CropConfig.GetHarvestSpriteName(drop.Type));
            if (sprite == null)
                return;

            float sprH = sprite.SourceRect.Height;
            float wx = drop.Tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f;
            float wy = drop.Tile.Y * GameConfig.TileSize + GameConfig.TileSize - sprH / 2f;

            var entity = _scene.CreateEntity("dropped-crop-" + drop.Tile.X + "-" + drop.Tile.Y);
            entity.SetPosition(wx, wy);
            var renderer = entity.AddComponent(new SpriteRenderer(sprite));
            renderer.SetRenderLayer(GameConfig.RenderLayerDroppedItems);
            drop.GroundEntity = entity;
        }
    }
}
