using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.GOAP;
using PitHero.ECS.Components;
using PitHero.AI.Interfaces;

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

        /// <summary>
        /// Execute action using traditional HeroComponent (for backward compatibility)
        /// </summary>
        public abstract bool Execute(HeroComponent hero);

        /// <summary>
        /// Execute action using interface-based context (new approach)
        /// </summary>
        public virtual bool Execute(IGoapContext context)
        {
            // Default implementation logs that this action hasn't been updated yet
            context.LogWarning($"[{GetType().Name}] Execute(IGoapContext) not implemented, falling back to legacy mode");
            return false;
        }

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

        /// <summary>
        /// Get pit center world position using dynamic PitWidthManager values, with GameConfig fallback
        /// </summary>
        public static Vector2 GetPitCenterWorldPosition()
        {
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            var centerX = pitWidthManager?.CurrentPitCenterTileX ?? GameConfig.PitCenterTileX;
            var centerY = GameConfig.PitCenterTileY;
            return TileToWorldPosition(new Point(centerX, centerY));
        }

        /// <summary>
        /// Get map center world position (static since map center doesn't change)
        /// </summary>
        public static Vector2 GetMapCenterWorldPosition()
            => TileToWorldPosition(new Point(GameConfig.MapCenterTileX, GameConfig.MapCenterTileY));

        /// <summary>
        /// Get pit center world position using dynamic PitWidthManager values (instance method)
        /// </summary>
        protected Vector2 GetDynamicPitCenterWorldPosition()
        {
            return GetPitCenterWorldPosition(); // Use the updated static method
        }
    }
}