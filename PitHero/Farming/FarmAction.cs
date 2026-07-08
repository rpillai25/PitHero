using Microsoft.Xna.Framework;

namespace PitHero.Farming
{
    /// <summary>Kinds of work a farming monster can perform.</summary>
    public enum FarmActionType
    {
        Till       = 0,
        Plant      = 1,
        Water      = 2,
        Harvest    = 3,
        PickupDrop = 4,
    }

    /// <summary>A single unit of farm work, e.g. "till the tile at TargetTile".</summary>
    public struct FarmAction
    {
        public FarmActionType Type;
        public Point TargetTile;
    }
}
