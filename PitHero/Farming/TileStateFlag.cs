using System;

namespace PitHero.Farming
{
    [Flags]
    public enum TileStateFlag
    {
        None        = 0,
        ReadyToTill = 1 << 0,
        Tilled      = 1 << 1,
        Wet         = 1 << 2,
        CropGrowing = 1 << 3,
        CropGrown   = 1 << 4,
    }
}
