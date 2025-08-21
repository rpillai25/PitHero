using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.GOAP;
using PitHero.ECS.Components;

namespace PitHero.AI
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

        /// <summary>
        /// Position entity at tile corner adjusted for centered collider offset
        /// This ensures the collider aligns perfectly with tile boundaries
        /// </summary>
        public static Vector2 TileToWorldPosition(Point tileCoords)
        {
            // Position the entity so that when the centered collider is applied,
            // the collider edges align with tile boundaries
            var tileCorner = new Vector2(tileCoords.X * TileSize, tileCoords.Y * TileSize);
            var colliderCenterOffset = new Vector2(GameConfig.HeroWidth / 2f, GameConfig.HeroHeight / 2f);
            return tileCorner + colliderCenterOffset;
        }

        public static Vector2 GetPitCenterWorldPosition()
            => TileToWorldPosition(new Point(GameConfig.PitCenterTileX, GameConfig.PitCenterTileY));

        public static Vector2 GetMapCenterWorldPosition()
            => TileToWorldPosition(new Point(GameConfig.MapCenterTileX, GameConfig.MapCenterTileY));
    }
}