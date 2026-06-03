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
        public Entity WorldEntity; // runtime reference; null after scene reload until restored
    }

    /// <summary>Tracks all placed buildings and exposes per-type counts and tile-occupancy queries.</summary>
    public class BuildingService
    {
        private readonly List<PlacedBuilding> _buildings = new List<PlacedBuilding>();

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

        public void AddBuilding(PlacedBuilding b) => _buildings.Add(b);

        public bool IsTileOccupied(int tileX, int tileY)
        {
            for (int i = 0; i < _buildings.Count; i++)
            {
                var b = _buildings[i];
                var fp = BuildingConfig.GetFootprint(b.Type);
                for (int j = 0; j < fp.Length; j++)
                {
                    if (b.TileX + fp[j].dx == tileX && b.TileY + fp[j].dy == tileY)
                        return true;
                }
            }
            return false;
        }

        public void Clear() => _buildings.Clear();
    }
}
