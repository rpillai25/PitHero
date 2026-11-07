using Microsoft.Xna.Framework;

namespace PitHero.Util
{
    /// <summary>Placeable building kinds. The ordinal is saved and recorded in commands: append only.</summary>
    public enum BuildingType { MonsterHouse = 0, CropStorage = 1, DiggingField = 2 }

    /// <summary>Static configuration for each building type: footprint, sprite, cost, and description.</summary>
    public static class BuildingConfig
    {
        private static readonly (int dx, int dy)[] MonsterHouseFootprint = BuildFootprint(-2, 2, -2, 2);
        private static readonly (int dx, int dy)[] CropStorageFootprint  = BuildFootprint(-1, 1, -2, 1);
        private static readonly (int dx, int dy)[] DiggingFieldFootprint = BuildFootprint(
            DiggingFieldDxMin, DiggingFieldDxMin + DiggingFieldWidth - 1,
            DiggingFieldDyMin, DiggingFieldDyMin + DiggingFieldHeight - 1);

        /// <summary>Digging Field width in tiles (issue #452): the 256x128 sprite is 8 tiles wide.</summary>
        public const int DiggingFieldWidth = 8;
        /// <summary>Digging Field height in tiles: the 256x128 sprite is 4 tiles high.</summary>
        public const int DiggingFieldHeight = 4;
        /// <summary>Leftmost footprint column relative to the anchor tile.</summary>
        public const int DiggingFieldDxMin = -4;
        /// <summary>Top footprint row relative to the anchor tile.</summary>
        public const int DiggingFieldDyMin = -2;

        public static (int dx, int dy)[] GetFootprint(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => MonsterHouseFootprint,
            BuildingType.DiggingField => DiggingFieldFootprint,
            _                         => CropStorageFootprint,
        };

        /// <summary>
        /// Inclusive tile-offset bounds of a building's footprint relative to its anchor tile.
        /// Used to compute the world-space rectangle for the hover outline.
        /// </summary>
        public static (int dxMin, int dxMax, int dyMin, int dyMax) GetFootprintBounds(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => (-2, 2, -2, 2),
            BuildingType.DiggingField => (DiggingFieldDxMin, DiggingFieldDxMin + DiggingFieldWidth - 1,
                                          DiggingFieldDyMin, DiggingFieldDyMin + DiggingFieldHeight - 1),
            _                         => (-1, 1, -2, 1),
        };

        /// <summary>
        /// World position for the entity pivot given the anchor tile.
        /// MonsterHouse (5×5, odd×odd): pivot at tile centre.
        /// CropStorage  (3×4, odd×even): pivot X at tile centre, pivot Y at tile TOP edge so the
        /// 128 px sprite occupies exactly 4 rows (anchorY-2 … anchorY+1).
        /// DiggingField (8×4): pivot at the footprint's top-left corner (columns anchorX-4 … anchorX+3,
        /// rows anchorY-2 … anchorY+1); the sprite is offset by half its size (GetSpriteOffset).
        /// </summary>
        public static Vector2 GetWorldPos(int anchorTileX, int anchorTileY, BuildingType t)
        {
            if (t == BuildingType.DiggingField)
                return new Vector2((anchorTileX + DiggingFieldDxMin) * 32f, (anchorTileY + DiggingFieldDyMin) * 32f);
            float wx = anchorTileX * 32 + 16f;
            float wy = t == BuildingType.MonsterHouse
                ? anchorTileY * 32 + 16f   // odd height: centre of tile
                : anchorTileY * 32f;        // even height: top edge for clean 4-tile alignment
            return new Vector2(wx, wy);
        }

        /// <summary>
        /// Offset from the entity position to the sprite's center. Building sprites are drawn centered
        /// on the entity; the Digging Field's entity sits at its top-left corner instead (so its tile
        /// overlay can draw from there), so its sprite is shifted by half its size.
        /// </summary>
        public static Vector2 GetSpriteOffset(BuildingType t) => t == BuildingType.DiggingField
            ? new Vector2(DiggingFieldWidth * 16f, DiggingFieldHeight * 16f)
            : Vector2.Zero;

        /// <summary>
        /// Tile a farm worker walks to in order to enter/deliver at the building.
        /// MonsterHouse: the doorway at the bottom-centre of the 5×5 footprint (anchorY+2).
        /// CropStorage: the passable approach tile directly below the 3×4 footprint
        /// (anchorY+2; the footprint ends at anchorY+1, so this tile is outside it).
        /// DiggingField: nobody enters it; the anchor tile.
        /// </summary>
        public static Point GetDoorTile(BuildingType t, Point anchorTile) => t switch
        {
            BuildingType.MonsterHouse => new Point(anchorTile.X, anchorTile.Y + 2),
            BuildingType.DiggingField => anchorTile,
            _                         => new Point(anchorTile.X, anchorTile.Y + 2),
        };

        public static string GetSpriteName(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => "MonsterHouse",
            BuildingType.DiggingField => "DiggingField",
            _                         => "CropStorage",
        };

        /// <summary>Price of the first building of this type the player actually pays for.</summary>
        public static int GetBaseCost(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => GameConfig.BuildingCostMonsterHouseBase,
            BuildingType.DiggingField => 0, // the Prospector's Map is the price of entry
            _                         => GameConfig.BuildingCostCropStorageBase,
        };

        /// <summary>Ceiling the escalating cost curve clamps to, however many are already placed.</summary>
        public static int GetMaxCost(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => GameConfig.BuildingCostMonsterHouseMax,
            BuildingType.DiggingField => 0,
            _                         => GameConfig.BuildingCostCropStorageMax,
        };

        /// <summary>Most buildings of this type that can be placed at once (the Digging Field is unique).</summary>
        public static int GetMaxCount(BuildingType t) => t == BuildingType.DiggingField ? 1 : int.MaxValue;

        /// <summary>False for a building with no Sell option (the Digging Field can only be moved).</summary>
        public static bool IsSellable(BuildingType t) => t != BuildingType.DiggingField;

        /// <summary>
        /// True for flat ground buildings farm workers walk across (the Digging Field); the others wall
        /// off their bottom two footprint rows in the farm pathfinder.
        /// </summary>
        public static bool IsWalkable(BuildingType t) => t == BuildingType.DiggingField;

        /// <summary>
        /// How many buildings of this type are gifted at new-game start and therefore excluded from
        /// the cost curve. Both the starter Monster House and the starter Crop Storage are free, so
        /// the first one the player buys is priced at <see cref="GetBaseCost"/>.
        /// </summary>
        public static int GetFreeStarterCount(BuildingType t) =>
            t == BuildingType.DiggingField ? 0 : GameConfig.BuildingFreeStarterCount;

        /// <summary>
        /// Purchase price of the NEXT building of this type, given how many of that type are already
        /// placed. Each paid building multiplies the price by GameConfig.BuildingCostGrowthFactor,
        /// rounded to the nearest BuildingCostRoundingStep gold and clamped at <see cref="GetMaxCost"/>.
        /// Selling a building lowers the count and therefore the next price back down, but since the
        /// refund is half of what was paid, churning buildings is always a net loss.
        /// </summary>
        public static int GetCost(BuildingType t, int existingCount)
        {
            int paidIndex = existingCount - GetFreeStarterCount(t);
            if (paidIndex < 0)
                paidIndex = 0;

            int maxCost = GetMaxCost(t);
            double raw = GetBaseCost(t) * System.Math.Pow(GameConfig.BuildingCostGrowthFactor, paidIndex);
            if (raw >= maxCost)
                return maxCost;

            int step = GameConfig.BuildingCostRoundingStep;
            int rounded = (int)System.Math.Round(raw / step, System.MidpointRounding.AwayFromZero) * step;
            return rounded > maxCost ? maxCost : rounded;
        }

        /// <summary>
        /// Gold refunded for selling a placed building: always the initial base price, however far up
        /// the cost curve the player has climbed. Sell the 6th Monster House you paid 760 G for and
        /// you still get 100 G back — you buy high and sell low, so expanding is a commitment.
        /// Independent of how many are placed, unlike <see cref="GetCost"/>.
        /// </summary>
        public static int GetSellPrice(BuildingType t) => GetBaseCost(t);

        /// <summary>Localization key (UI text) for a building's display name. Resolve via TextService.</summary>
        public static string GetDisplayNameKey(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => UITextKey.BuildingNameMonsterHouse,
            BuildingType.DiggingField => UITextKey.BuildingNameDiggingField,
            _                         => UITextKey.BuildingNameCropStorage,
        };

        /// <summary>Localization key (UI text) for a building's description. Resolve via TextService.</summary>
        public static string GetDescriptionKey(BuildingType t) => t switch
        {
            BuildingType.MonsterHouse => UITextKey.BuildingDescMonsterHouse,
            BuildingType.DiggingField => UITextKey.BuildingDescDiggingField,
            _                         => UITextKey.BuildingDescCropStorage,
        };

        /// <summary>
        /// Index (0..31) of a world tile inside a Digging Field anchored at (anchorX, anchorY), row by
        /// row from the top-left, or -1 when the tile is outside it.
        /// </summary>
        public static int GetDiggingFieldTileIndex(int anchorX, int anchorY, int tileX, int tileY)
        {
            int lx = tileX - (anchorX + DiggingFieldDxMin);
            int ly = tileY - (anchorY + DiggingFieldDyMin);
            if (lx < 0 || ly < 0 || lx >= DiggingFieldWidth || ly >= DiggingFieldHeight)
                return -1;
            return ly * DiggingFieldWidth + lx;
        }

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
