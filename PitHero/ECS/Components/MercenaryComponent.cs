using Microsoft.Xna.Framework;
using Nez;
using RolePlayingFramework.Mercenaries;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component for mercenary entities that can be hired and follow the hero
    /// </summary>
    public class MercenaryComponent : Component, IUpdatable
    {
        /// <summary>Link to the Mercenary class from RolePlayingFramework</summary>
        public Mercenary LinkedMercenary { get; set; }

        /// <summary>True if this mercenary has been hired by the hero</summary>
        public bool IsHired { get; set; }

        /// <summary>True if this mercenary is waiting in the tavern</summary>
        public bool IsWaitingInTavern { get; set; }

        /// <summary>True if this mercenary is walking offscreen to be removed</summary>
        public bool IsBeingRemoved { get; set; }

        /// <summary>The entity this mercenary is following (hero or another mercenary)</summary>
        public Entity FollowTarget { get; set; }

        /// <summary>The tavern position this mercenary is assigned to</summary>
        public Point TavernPosition { get; set; }

        /// <summary>The time this mercenary was spawned (for tracking oldest unhired mercenary)</summary>
        public double SpawnTime { get; set; }

        /// <summary>Unique spawn ID for tracking oldest mercenary (lower ID = older)</summary>
        public int SpawnId { get; set; }

        /// <summary>The last tile position this mercenary was on (for chain following)</summary>
        public Point LastTilePosition { get; set; }

        /// <summary>True if this mercenary is being promoted to hero</summary>
        public bool IsBeingPromoted { get; set; }

    /// <summary>True if this mercenary has arrived at the hero statue during promotion</summary>
    public bool HasArrivedAtStatue { get; set; }

    /// <summary>True if this mercenary is inside the pit</summary>
    public bool InsidePit { get; set; }

    public void Update()
    {
        // Future: Update mercenary behavior here
    }
}
}
