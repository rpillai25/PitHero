using Microsoft.Xna.Framework;
using Nez.AI.GOAP;
using PitHero.ECS.Components;

namespace PitHero.AI
{
    /// <summary>
    /// Base class for all mercenary GOAP actions
    /// </summary>
    public abstract class MercenaryActionBase : Action
    {
        protected MercenaryActionBase(string name, int cost = 1) : base(name, cost)
        {
        }

        /// <summary>
        /// Execute the action for the given mercenary
        /// </summary>
        /// <returns>True if action is complete, false if still in progress</returns>
        public abstract bool Execute(MercenaryComponent mercenary);

        /// <summary>
        /// Convert tile coordinates to world position (center of tile)
        /// </summary>
        protected Vector2 TileToWorldPosition(Point tile)
        {
            return new Vector2(
                tile.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                tile.Y * GameConfig.TileSize + GameConfig.TileSize / 2
            );
        }
    }
}
