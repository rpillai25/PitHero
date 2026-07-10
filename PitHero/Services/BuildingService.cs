using System.Collections.Generic;
using Nez;
using PitHero.Util;

namespace PitHero.Services
{
    public class PlacedBuilding
    {
        public BuildingType Type;
        public int TileX;
        public int TileY;
        public int UniqueId; // stable identifier, never changes even if moved
        public Entity WorldEntity; // runtime reference; null after scene reload until restored
    }

    /// <summary>Tracks all placed buildings and exposes per-type counts and tile-occupancy queries.</summary>
    public class BuildingService
    {
        private readonly List<PlacedBuilding> _buildings = new List<PlacedBuilding>();
        private int _nextId = 1;

        /// <summary>Fired after the set of placed buildings changes (placement or restore).</summary>
        public System.Action BuildingsChanged;

        /// <summary>
        /// Fired when an already-placed building is relocated (its TileX/TileY changed but UniqueId
        /// is preserved). Lets in-flight workers (e.g. a farming monster carrying a crop to a Crop
        /// Storage) reactively retarget the building's new location.
        /// </summary>
        public System.Action<PlacedBuilding> BuildingMoved;

        /// <summary>
        /// Fired when a building is removed from the map (e.g. a Crop Storage sold by the player).
        /// Lets in-flight workers targeting it react before the building object is discarded.
        /// </summary>
        public System.Action<PlacedBuilding> BuildingRemoved;

        /// <summary>
        /// Raises <see cref="BuildingsChanged"/> then <see cref="BuildingMoved"/> for a building whose
        /// tile position just changed. BuildingsChanged must fire first: it rebuilds the farm
        /// pathfinder's walls, so workers re-pathing from BuildingMoved see the new location instead
        /// of stale walls at the old one.
        /// </summary>
        public void NotifyBuildingMoved(PlacedBuilding b)
        {
            BuildingsChanged?.Invoke();
            BuildingMoved?.Invoke(b);
        }

        /// <summary>Next ID to allocate. Persisted in the save file so IDs are never reused.</summary>
        public int NextId { get => _nextId; set => _nextId = value; }

        /// <summary>Allocates and returns the next unique building ID.</summary>
        public int AllocateId() => _nextId++;

        public int MonsterHouseCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _buildings.Count; i++)
                    if (_buildings[i].Type == BuildingType.MonsterHouse) count++;
                return count;
            }
        }

        public int CropStorageCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _buildings.Count; i++)
                    if (_buildings[i].Type == BuildingType.CropStorage) count++;
                return count;
            }
        }

        public IReadOnlyList<PlacedBuilding> GetAll() => _buildings;

        public void AddBuilding(PlacedBuilding b)
        {
            _buildings.Add(b);
            BuildingsChanged?.Invoke();
        }

        /// <summary>
        /// Removes a placed building from the map. Fires <see cref="BuildingRemoved"/> (so in-flight
        /// workers can retarget) then <see cref="BuildingsChanged"/> (so subscribers like
        /// CropStorageInventoryService prune their per-building state). The caller is responsible for
        /// destroying the building's <see cref="PlacedBuilding.WorldEntity"/>.
        /// </summary>
        public void RemoveBuilding(PlacedBuilding b)
        {
            if (b == null || !_buildings.Remove(b))
                return;
            BuildingRemoved?.Invoke(b);
            BuildingsChanged?.Invoke();
        }

        public bool IsTileOccupied(int tileX, int tileY) => IsTileOccupied(tileX, tileY, null);

        /// <summary>
        /// True if any placed building (other than <paramref name="ignore"/>) covers the tile.
        /// The ignore parameter lets a building being moved overlap its own current footprint.
        /// </summary>
        public bool IsTileOccupied(int tileX, int tileY, PlacedBuilding ignore)
        {
            for (int i = 0; i < _buildings.Count; i++)
            {
                var b = _buildings[i];
                if (b == ignore)
                    continue;
                var fp = BuildingConfig.GetFootprint(b.Type);
                for (int j = 0; j < fp.Length; j++)
                {
                    if (b.TileX + fp[j].dx == tileX && b.TileY + fp[j].dy == tileY)
                        return true;
                }
            }
            return false;
        }

        /// <summary>Returns the placed building with the given UniqueId, or null if none exists.</summary>
        public PlacedBuilding GetBuildingById(int uniqueId)
        {
            for (int i = 0; i < _buildings.Count; i++)
                if (_buildings[i].UniqueId == uniqueId)
                    return _buildings[i];
            return null;
        }

        /// <summary>Returns the placed building whose footprint covers the given tile, or null.</summary>
        public PlacedBuilding GetBuildingAtTile(int tileX, int tileY)
        {
            for (int i = 0; i < _buildings.Count; i++)
            {
                var b = _buildings[i];
                var fp = BuildingConfig.GetFootprint(b.Type);
                for (int j = 0; j < fp.Length; j++)
                {
                    if (b.TileX + fp[j].dx == tileX && b.TileY + fp[j].dy == tileY)
                        return b;
                }
            }
            return null;
        }

        public void Clear() => _buildings.Clear();
    }
}
