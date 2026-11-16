using Microsoft.Xna.Framework;

namespace PitHero.Util
{
    /// <summary>
    /// The four places a fisher sits around a Fishing Pond, in the order the spots are offered:
    /// left edge, bottom edge, right edge, top edge. Pure geometry from the pond's anchor tile (the
    /// pond's 8x6 footprint is columns anchorX-4..anchorX+3, rows anchorY-3..anchorY+2, so its centre
    /// is the tile corner (anchorX*32, anchorY*32)). Each spot is a stand tile just outside the
    /// footprint, the world point inside that tile where the fisher sits (centred on its edge of the
    /// water), whether it faces right, and the bobber's resting point in the water.
    /// </summary>
    public static class FishingPondSpots
    {
        /// <summary>Number of fishing spots around one pond.</summary>
        public const int Count = 4;

        public const int Left = 0;
        public const int Bottom = 1;
        public const int Right = 2;
        public const int Top = 3;

        // Where each spot's bobber rests, in world px from the pond's centre (anchorX*32, anchorY*32).
        // Picked from the pond art (Content/Textures/CropsProps/FishingPond): the open water is an
        // off-centre oval, lower than the sprite's middle (reeds fill the top), ringed by 30-60 px of
        // rocks and reeds, so each point sits in clear water away from the rocks and lily pads. A
        // sprite pixel (sx, sy) of the 233x192 pond is at (sx - 117, sy - 96) from the centre.
        private static readonly Vector2[] BobberFromCenter =
        {
            new Vector2(-59f, 28f), // left:   sprite (58, 124)
            new Vector2(3f, 54f),   // bottom: sprite (120, 150)
            new Vector2(69f, 10f),  // right:  sprite (186, 106)
            new Vector2(7f, -22f),  // top:    sprite (124, 74)
        };

        // The top fisher sits down on the top rocks (inside the pond's first footprint row) instead of
        // hovering above the reeds; it steps back onto its stand tile when it gets up.
        private const float TopSitDrop = 46f;

        /// <summary>The tile the fisher paths to (the farm pathfinder checks it is passable).</summary>
        public static Point GetStandTile(int spot, int anchorX, int anchorY)
        {
            switch (spot)
            {
                case Left: return new Point(anchorX + BuildingConfig.FishingPondDxMin - 1, anchorY);
                case Right: return new Point(anchorX + BuildingConfig.FishingPondDxMin + BuildingConfig.FishingPondWidth, anchorY);
                case Top: return new Point(anchorX, anchorY + BuildingConfig.FishingPondDyMin - 1);
                default: return new Point(anchorX, anchorY + BuildingConfig.FishingPondDyMin + BuildingConfig.FishingPondHeight);
            }
        }

        /// <summary>
        /// Offset from the stand tile's centre to where the fisher sits: on the pond's centre line
        /// (the left/right spots move up half a tile, the top/bottom spots left half a tile), tucked in
        /// toward the rocks on the left/right edges, where the sprite is inset from the footprint, and
        /// down onto the top rocks for the top spot (that point lies inside the walled footprint: the
        /// fisher returns to its stand tile before walking anywhere).
        /// </summary>
        public static Vector2 GetSitOffset(int spot)
        {
            float half = GameConfig.TileSize / 2f;
            switch (spot)
            {
                case Left: return new Vector2(8f, -half);
                case Right: return new Vector2(-8f, -half);
                case Top: return new Vector2(-half, TopSitDrop);
                default: return new Vector2(-half, 0f);
            }
        }

        /// <summary>World point the fisher sits at.</summary>
        public static Vector2 GetSitPosition(int spot, int anchorX, int anchorY)
        {
            var tile = GetStandTile(spot, anchorX, anchorY);
            var center = new Vector2(
                tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f,
                tile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f);
            return center + GetSitOffset(spot);
        }

        /// <summary>
        /// True when the fisher faces right. Monsters only have left/right art, so the top and bottom
        /// spots face right and cast diagonally.
        /// </summary>
        public static bool FacesRight(int spot) => spot != Right;

        /// <summary>World point where the spot's bobber rests: in open water, clear of the rocks and pads.</summary>
        public static Vector2 GetBobberPosition(int spot, int anchorX, int anchorY)
            => new Vector2(anchorX * GameConfig.TileSize, anchorY * GameConfig.TileSize) + BobberFromCenter[spot];
    }
}
