using System;

namespace PitHero.Util
{
    public static class TileBitmask
    {
        public static int Calculate(int x, int y, Func<int, int, bool> hasTileAt)
        {
            int v = 0;
            if (hasTileAt(x, y - 1)) v += 1;  // North
            if (hasTileAt(x - 1, y)) v += 2;  // West
            if (hasTileAt(x + 1, y)) v += 4;  // East
            if (hasTileAt(x, y + 1)) v += 8;  // South
            return v;
        }

        public static int GetTileIndex(int x, int y, int zerothTileIndex, Func<int, int, bool> hasTileAt)
            => zerothTileIndex + Calculate(x, y, hasTileAt);
    }
}
