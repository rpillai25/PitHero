using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.Farming;
using PitHero.Util;

namespace PitHero.Services
{
    /// <summary>
    /// Manages all actively growing crops: tracks per-tile growth data, spawns and updates crop
    /// world entities, advances growth frames based on accumulated wet hours, and sets CropGrown
    /// when fully mature.
    /// </summary>
    public class CropGrowthService
    {
        private struct ActiveCropData
        {
            public CropType Type;
            public float AccumulatedHours;
            public int CurrentFrame;
            public Entity WorldEntity;
            /// <summary>Per-stage time multiplier; 1 for first growth, &gt;1 for slowed regrowth after a repeat harvest.</summary>
            public float RegrowthRateMultiplier;
        }

        private const float SecondsPerInGameHour = 60f;

        private readonly Dictionary<Point, ActiveCropData> _crops = new Dictionary<Point, ActiveCropData>();
        private readonly CropPlantingService _cropPlantingService;
        // When false, all crop world entities are hidden (farm UI is open)
        private bool _cropsVisible = true;

        public CropGrowthService(CropPlantingService cropPlantingService)
        {
            _cropPlantingService = cropPlantingService;
        }

        /// <summary>Returns true if a crop has been planted at the given tile.</summary>
        public bool HasCrop(Point tile) => _crops.ContainsKey(tile);

        /// <summary>Returns the type of the planted crop at the given tile, or null if none.</summary>
        public CropType? GetCropType(Point tile)
        {
            if (_crops.TryGetValue(tile, out var data))
                return data.Type;
            return null;
        }

        /// <summary>
        /// Plants a seed at the tile: creates the frame-1 crop entity and registers the crop for
        /// growth tracking. The plan-preview entity is NOT removed here — plans are permanent
        /// blueprints and their visuals are managed separately by the farm-mode gate.
        /// </summary>
        public void PlantCrop(Point tile, CropType type, Scene scene, SpriteAtlas atlas)
        {
            if (_crops.ContainsKey(tile))
                return;

            var sprite = atlas?.GetSprite(CropConfig.GetFrameSpriteName(type, 1));
            float sprH = sprite != null ? sprite.SourceRect.Height : GameConfig.TileSize;
            float wx = tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f;
            float wy = tile.Y * GameConfig.TileSize + GameConfig.TileSize - sprH / 2f;

            Entity entity = null;
            if (scene != null && sprite != null)
            {
                entity = scene.CreateEntity("crop-" + type + "-" + tile.X + "-" + tile.Y);
                entity.SetPosition(wx, wy);
                var renderer = entity.AddComponent(new SpriteRenderer(sprite));
                renderer.SetRenderLayer(GameConfig.RenderLayerSingleTileObject - 1);
                if (!_cropsVisible)
                    entity.SetEnabled(false);
            }

            _crops[tile] = new ActiveCropData
            {
                Type = type,
                AccumulatedHours = 0f,
                CurrentFrame = 1,
                WorldEntity = entity,
                RegrowthRateMultiplier = 1f,
            };
        }

        /// <summary>
        /// Removes a harvested regular crop: destroys its world entity and drops growth tracking.
        /// The caller is responsible for clearing the tile's CropGrown/CropGrowing flags.
        /// </summary>
        public void RemoveCrop(Point tile)
        {
            if (_crops.TryGetValue(tile, out var data))
            {
                data.WorldEntity?.Destroy();
                _crops.Remove(tile);
            }
        }

        /// <summary>
        /// Reverts a harvested repeat-harvest crop to an earlier frame so it regrows. Resets growth
        /// accumulation to match the revert frame at the (possibly slowed) regrowth rate and swaps the
        /// world sprite. The caller clears CropGrown / sets CropGrowing / clears Wet.
        /// </summary>
        public void RevertCropForRegrowth(Point tile, int revertFrame, float multiplier, SpriteAtlas atlas)
        {
            if (!_crops.TryGetValue(tile, out var data))
                return;

            int frame = revertFrame < 1 ? 1 : revertFrame;
            float effectiveHoursPerStage = CropConfig.GetHoursPerStage(data.Type) * multiplier;

            data.CurrentFrame = frame;
            data.AccumulatedHours = (frame - 1) * effectiveHoursPerStage;
            data.RegrowthRateMultiplier = multiplier;
            _crops[tile] = data;

            UpdateEntitySprite(data, tile, atlas);
        }

        /// <summary>
        /// Per-frame update: accumulates wet growth time and advances crop frames. Call only when
        /// not paused.
        /// </summary>
        public void Update(TileStateService tileState, SpriteAtlas atlas)
        {
            if (tileState == null || atlas == null)
                return;

            var tiles = new List<Point>(_crops.Keys);
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var data = _crops[tile];

                if ((data.CurrentFrame >= CropConfig.GetFrameCount(data.Type)) &&
                    tileState.HasFlag(tile, TileStateFlag.CropGrown))
                    continue;

                if (tileState.HasFlag(tile, TileStateFlag.Wet))
                    data.AccumulatedHours += Time.DeltaTime / SecondsPerInGameHour;

                int maxFrame = CropConfig.GetFrameCount(data.Type);
                float multiplier = data.RegrowthRateMultiplier <= 0f ? 1f : data.RegrowthRateMultiplier;
                float hoursPerStage = CropConfig.GetHoursPerStage(data.Type) * multiplier;
                int expectedFrame = 1 + (int)(data.AccumulatedHours / hoursPerStage);
                if (expectedFrame > maxFrame)
                    expectedFrame = maxFrame;

                if (expectedFrame != data.CurrentFrame)
                {
                    data.CurrentFrame = expectedFrame;
                    UpdateEntitySprite(data, tile, atlas);
                }

                if (expectedFrame >= maxFrame && !tileState.HasFlag(tile, TileStateFlag.CropGrown))
                {
                    tileState.SetFlag(tile, TileStateFlag.CropGrown);
                    tileState.ClearFlag(tile, TileStateFlag.CropGrowing);
                }

                _crops[tile] = data;
            }
        }

        /// <summary>Returns all crop data for serialization.</summary>
        public IEnumerable<KeyValuePair<Point, (CropType Type, float AccumulatedHours, int CurrentFrame, float RegrowthRateMultiplier)>> GetAllData()
        {
            foreach (var kvp in _crops)
                yield return new KeyValuePair<Point, (CropType, float, int, float)>(
                    kvp.Key,
                    (kvp.Value.Type, kvp.Value.AccumulatedHours, kvp.Value.CurrentFrame,
                     kvp.Value.RegrowthRateMultiplier <= 0f ? 1f : kvp.Value.RegrowthRateMultiplier));
        }

        /// <summary>Reconstructs crop entities and tracking data from a save. Call after tile states are restored.</summary>
        public void RestoreAll(List<SavedCropGrowthState> states, Scene scene, SpriteAtlas atlas)
        {
            _crops.Clear();
            if (states == null)
                return;

            for (int i = 0; i < states.Count; i++)
            {
                var s = states[i];
                var tile = new Point(s.TileX, s.TileY);
                var type = (CropType)s.CropTypeId;

                var sprite = atlas?.GetSprite(CropConfig.GetFrameSpriteName(type, s.CurrentFrame));
                float sprH = sprite != null ? sprite.SourceRect.Height : GameConfig.TileSize;
                float wx = tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f;
                float wy = tile.Y * GameConfig.TileSize + GameConfig.TileSize - sprH / 2f;

                Entity entity = null;
                if (scene != null && sprite != null)
                {
                    entity = scene.CreateEntity("crop-" + type + "-" + tile.X + "-" + tile.Y);
                    entity.SetPosition(wx, wy);
                    var renderer = entity.AddComponent(new SpriteRenderer(sprite));
                    renderer.SetRenderLayer(GameConfig.RenderLayerSingleTileObject - 1);
                    if (!_cropsVisible)
                        entity.SetEnabled(false);
                }

                _crops[tile] = new ActiveCropData
                {
                    Type = type,
                    AccumulatedHours = s.AccumulatedHours,
                    CurrentFrame = s.CurrentFrame,
                    WorldEntity = entity,
                    RegrowthRateMultiplier = s.RegrowthRateMultiplier <= 0f ? 1f : s.RegrowthRateMultiplier,
                };
            }
        }

        /// <summary>
        /// Returns the growth progress of a crop at the given tile as a value in [0, 1], where
        /// 0 is freshly planted and 1 is fully grown. Returns -1 when no crop exists at the tile.
        /// Uses the crop's regrowth-rate multiplier so post-harvest regrowth progress is accurate.
        /// </summary>
        public float GetGrowthProgress(Point tile)
        {
            if (!_crops.TryGetValue(tile, out var data))
                return -1f;

            int maxFrame = CropConfig.GetFrameCount(data.Type);
            float hoursPerStage = CropConfig.GetHoursPerStage(data.Type);
            float multiplier = data.RegrowthRateMultiplier <= 0f ? 1f : data.RegrowthRateMultiplier;
            float totalHours = (maxFrame - 1) * hoursPerStage * multiplier;
            if (totalHours <= 0f)
                return 1f;
            float progress = data.AccumulatedHours / totalHours;
            if (progress < 0f) progress = 0f;
            if (progress > 1f) progress = 1f;
            return progress;
        }

        /// <summary>
        /// Shows or hides all crop world entities. Called when the Farm UI opens (false) or closes
        /// (true) so real crops are not visible behind the translucent plan overlays. The state is
        /// persisted and applied to entities created by PlantCrop and RestoreAll.
        /// </summary>
        public void SetCropsVisible(bool visible)
        {
            _cropsVisible = visible;
            var keys = new List<Point>(_crops.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                if (_crops.TryGetValue(keys[i], out var data))
                    data.WorldEntity?.SetEnabled(visible);
            }
        }

        /// <summary>Returns all tile positions with planted crops, for water queue population.</summary>
        public IEnumerable<Point> GetAllCropTiles()
        {
            foreach (var kvp in _crops)
                yield return kvp.Key;
        }

        private void UpdateEntitySprite(ActiveCropData data, Point tile, SpriteAtlas atlas)
        {
            if (data.WorldEntity == null || data.WorldEntity.IsDestroyed)
                return;
            var renderer = data.WorldEntity.GetComponent<SpriteRenderer>();
            if (renderer == null)
                return;
            var sprite = atlas?.GetSprite(CropConfig.GetFrameSpriteName(data.Type, data.CurrentFrame));
            if (sprite != null)
            {
                renderer.Sprite = sprite;
                float sprH = sprite.SourceRect.Height;
                float wy = tile.Y * GameConfig.TileSize + GameConfig.TileSize - sprH / 2f;
                data.WorldEntity.SetPosition(data.WorldEntity.Position.X, wy);
            }
        }
    }
}
