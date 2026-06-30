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

        /// <summary>
        /// Tile a farm worker walks to in order to enter/deliver at the building.
        /// MonsterHouse: the doorway at the bottom-centre of the 5×5 footprint (anchorY+2).
        /// CropStorage: the passable approach tile directly below the 3×4 footprint
        /// (anchorY+2; the footprint ends at anchorY+1, so this tile is outside it).
        /// </summary>
        public static Point GetDoorTile(BuildingType t, Point anchorTile) => t switch
        {
            BuildingType.MonsterHouse => new Point(anchorTile.X, anchorTile.Y + 2),
            _                         => new Point(anchorTile.X, anchorTile.Y + 2),
        };

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

        /// <summary>Localization key (UI text) for a building's display name. Resolve via TextService.</summary>
        public static string GetDisplayNameKey(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => UITextKey.BuildingNameMonsterHouse,
            _                         => UITextKey.BuildingNameCropStorage,
        };

        /// <summary>Localization key (UI text) for a building's description. Resolve via TextService.</summary>
        public static string GetDescriptionKey(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => UITextKey.BuildingDescMonsterHouse,
            _                         => UITextKey.BuildingDescCropStorage,
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
