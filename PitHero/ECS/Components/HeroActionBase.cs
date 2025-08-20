using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.GOAP;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Base class for all Hero GOAP actions
    /// </summary>
    public abstract class HeroActionBase : Action
    {
        protected HeroActionBase(string name, int cost = 1) : base(name, cost)
        {
        }

        public abstract bool Execute(HeroComponent hero);

        private static int TileSize => GameConfig.TileSize; // ensure single source of truth

        public static Vector2 TileToWorldPosition(Point tileCoords)
        {
            return new Vector2(tileCoords.X * TileSize + TileSize / 2f, tileCoords.Y * TileSize + TileSize / 2f);
        }

        public static Vector2 GetPitCenterWorldPosition()
            => TileToWorldPosition(new Point(GameConfig.PitCenterTileX, GameConfig.PitCenterTileY));

        public static Vector2 GetMapCenterWorldPosition()
            => TileToWorldPosition(new Point(GameConfig.MapCenterTileX, GameConfig.MapCenterTileY));
    }
}