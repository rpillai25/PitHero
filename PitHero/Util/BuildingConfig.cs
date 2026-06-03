using Microsoft.Xna.Framework;

namespace PitHero.Util
{
    public enum BuildingType { MonsterHouse = 0, CropStorage = 1 }

    /// <summary>Static configuration for each building type: footprint, sprite, cost, and description.</summary>
    public static class BuildingConfig
    {
        private static readonly (int dx, int dy)[] MonsterHouseFootprint = BuildFootprint(-2, 2, -2, 2);
        private static readonly (int dx, int dy)[] CropStorageFootprint  = BuildFootprint(-1, 1, -2, 1);

        public static (int dx, int dy)[] GetFootprint(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => MonsterHouseFootprint,
            _                         => CropStorageFootprint,
        };

        /// <summary>
        /// World position for the entity pivot given the anchor tile.
        /// MonsterHouse (5×5, odd×odd): pivot at tile centre.
        /// CropStorage  (3×4, odd×even): pivot X at tile centre, pivot Y at tile TOP edge so the
        /// 128 px sprite occupies exactly 4 rows (anchorY-2 … anchorY+1).
        /// </summary>
        public static Vector2 GetWorldPos(int anchorTileX, int anchorTileY, BuildingType t)
        {
            float wx = anchorTileX * 32 + 16f;
            float wy = t == BuildingType.MonsterHouse
                ? anchorTileY * 32 + 16f   // odd height: centre of tile
                : anchorTileY * 32f;        // even height: top edge for clean 4-tile alignment
            return new Vector2(wx, wy);
        }

        public static string GetSpriteName(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => "MonsterHouse",
            _                         => "CropStorage",
        };

        public static int GetCost(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => 100,
            _                         => 50,
        };

        public static string GetDisplayName(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => "Monster House",
            _                         => "Crop Storage",
        };

        public static string GetDescription(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => "Houses 16 monsters",
            _                         => "Holds 32 stacks of crops",
        };

        private static (int dx, int dy)[] BuildFootprint(int dxMin, int dxMax, int dyMin, int dyMax)
        {
            int w = dxMax - dxMin + 1;
            int h = dyMax - dyMin + 1;
            var arr = new (int, int)[w * h];
            int i = 0;
            for (int dy = dyMin; dy <= dyMax; dy++)
                for (int dx = dxMin; dx <= dxMax; dx++)
                    arr[i++] = (dx, dy);
            return arr;
        }
    }
}
