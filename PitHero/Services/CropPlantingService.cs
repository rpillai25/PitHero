using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.Farming;

namespace PitHero.Services
{
    /// <summary>Lightweight record of a placed crop planting plan.</summary>
    public struct PlacedCropPlan
    {
        /// <summary>The crop type designated for this tile.</summary>
        public CropType Type;
        /// <summary>Tile X coordinate of the plan.</summary>
        public int TileX;
        /// <summary>Tile Y coordinate of the plan.</summary>
        public int TileY;
        /// <summary>The world-space entity rendering the grayscale preview sprite.</summary>
        public Entity WorldEntity;
    }

    /// <summary>Tracks all crop planting plans placed by the player and the seed inventory counts.</summary>
    public class CropPlantingService
    {
        private readonly List<PlacedCropPlan> _plans = new List<PlacedCropPlan>();
        private readonly Dictionary<Point, int> _tileIndex = new Dictionary<Point, int>();

        /// <summary>
        /// Current seed inventory counts, indexed by (int)CropType.
        /// Initialized to null; the overlay sets this after construction.
        /// </summary>
        public int[] SeedInventory;

        /// <summary>Adds a new planting plan and indexes it by tile position.</summary>
        public void AddPlan(PlacedCropPlan plan)
        {
            _tileIndex[new Point(plan.TileX, plan.TileY)] = _plans.Count;
            _plans.Add(plan);
        }

        /// <summary>Returns true if a planting plan exists at the given tile.</summary>
        public bool HasPlan(Point tile) => _tileIndex.ContainsKey(tile);

        /// <summary>Number of crop plans currently placed (planted or awaiting planting).</summary>
        public int PlanCount => _plans.Count;

        /// <summary>Returns the crop type of the plan at a tile, or null if none exists.</summary>
        public CropType? GetPlanType(Point tile)
        {
            if (_tileIndex.TryGetValue(tile, out int idx))
                return _plans[idx].Type;
            return null;
        }

        /// <summary>Returns a read-only view of all currently registered plans.</summary>
        public IReadOnlyList<PlacedCropPlan> GetAllPlans() => _plans;

        /// <summary>
        /// Destroys all visual entities but keeps the plan data intact.
        /// Called when exiting seed mode so plans survive across mode toggles.
        /// </summary>
        public void DestroyPlanVisuals()
        {
            for (int i = 0; i < _plans.Count; i++)
            {
                _plans[i].WorldEntity?.Destroy();
                var p = _plans[i];
                p.WorldEntity = null;
                _plans[i] = p;
            }
        }

        /// <summary>Updates the world entity reference for an existing plan at the given tile.</summary>
        public void SetPlanEntity(Point tile, Entity entity)
        {
            if (!_tileIndex.TryGetValue(tile, out int idx))
                return;
            var p = _plans[idx];
            p.WorldEntity = entity;
            _plans[idx] = p;
        }

        /// <summary>
        /// Removes a single plan at the given tile, destroys its entity, and returns the crop type.
        /// Returns null if no plan exists there. Uses swap-remove for O(1) list mutation.
        /// </summary>
        public CropType? RemovePlan(Point tile)
        {
            if (!_tileIndex.TryGetValue(tile, out int idx))
                return null;

            var plan = _plans[idx];
            plan.WorldEntity?.Destroy();

            int last = _plans.Count - 1;
            if (idx != last)
            {
                var lastPlan = _plans[last];
                _plans[idx] = lastPlan;
                _tileIndex[new Point(lastPlan.TileX, lastPlan.TileY)] = idx;
            }
            _plans.RemoveAt(last);
            _tileIndex.Remove(tile);

            return plan.Type;
        }

        /// <summary>Returns true if at least one seed of the given type is in the inventory.</summary>
        public bool HasSeeds(CropType crop)
        {
            if (SeedInventory == null)
                return false;
            return SeedInventory[(int)crop] > 0;
        }

        /// <summary>
        /// Decrements the seed inventory by one and returns true. Returns false without
        /// modifying the inventory when there are no seeds or the array is not yet assigned.
        /// </summary>
        public bool ConsumeSeed(CropType crop)
        {
            if (SeedInventory == null)
                return false;
            int idx = (int)crop;
            if (SeedInventory[idx] <= 0)
                return false;
            SeedInventory[idx]--;
            return true;
        }

        /// <summary>
        /// Counts plans of the given type whose tile does NOT have a same-type growing crop
        /// (i.e. plans that still require a seed to be planted). A different-type crop on the
        /// same tile is excluded: it counts as needing a future seed for the swap.
        /// </summary>
        public int CountUnplantedPlans(CropType crop, CropGrowthService growth)
        {
            int count = 0;
            for (int i = 0; i < _plans.Count; i++)
            {
                if (_plans[i].Type != crop)
                    continue;
                var tile = new Microsoft.Xna.Framework.Point(_plans[i].TileX, _plans[i].TileY);
                // Skip tiles that already have the same-type crop growing
                if (growth != null && growth.GetCropType(tile) == crop)
                    continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Adds the given number of seeds for the specified crop to the seed inventory, clamped
        /// at GameConfig.SeedInventoryMaxPerCrop. Safe to call before the overlay assigns the
        /// array — allocates a fallback if needed.
        /// </summary>
        public void AddSeeds(CropType crop, int count)
        {
            if (SeedInventory == null)
                SeedInventory = new int[CropTypeInfo.Count];
            int next = SeedInventory[(int)crop] + count;
            if (next > GameConfig.SeedInventoryMaxPerCrop)
                next = GameConfig.SeedInventoryMaxPerCrop;
            SeedInventory[(int)crop] = next;
        }

        /// <summary>Destroys all world entities and clears the plan registry.</summary>
        public void Clear()
        {
            for (int i = 0; i < _plans.Count; i++)
                _plans[i].WorldEntity?.Destroy();
            _plans.Clear();
            _tileIndex.Clear();
        }
    }
}
