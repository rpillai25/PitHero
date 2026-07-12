using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Mercenaries;

namespace PitHero.Combat
{
    /// <summary>
    /// Read-only (plus burst/exhaustion write-backs) view of the hero's party state
    /// consumed by the battle engine and the decision engine.
    /// Live implementation: wraps HeroComponent.
    /// Virtual implementation: wraps VirtualHero.
    /// </summary>
    public interface IBattlePartyView
    {
        /// <summary>The hero object (for reward math and skill context).</summary>
        Hero Hero { get; }

        /// <summary>Active battle tactic that controls AI decision-making.</summary>
        BattleTactic CurrentBattleTactic { get; }

        /// <summary>Hero's item bag (shared with mercenaries for consumable use).</summary>
        ItemBag Bag { get; }

        /// <summary>Returns the ordered array of heal priorities (length 3).</summary>
        HeroHealPriority[] GetHealPrioritiesInOrder();

        /// <summary>True when no healing item actions remain available this battle.</summary>
        bool HealingItemExhausted { get; set; }

        /// <summary>True when no healing skill actions remain available this battle.</summary>
        bool HealingSkillExhausted { get; set; }

        /// <summary>True when the hero permits consumable use on mercenaries.</summary>
        bool UseConsumablesOnMercenaries { get; }

        /// <summary>True when mercenaries are permitted to use consumable items.</summary>
        bool MercenariesCanUseConsumables { get; }

        /// <summary>
        /// Returns true if the hero's HP is below the effective critical threshold,
        /// including burst-damage and replenish-override checks.
        /// </summary>
        bool IsHeroHPCritical();

        /// <summary>
        /// Returns true if the given mercenary's HP is below the effective critical threshold,
        /// including burst-damage and replenish-override checks.
        /// </summary>
        bool IsMercenaryHPCritical(Mercenary merc);

        /// <summary>
        /// Registers a burst-damage event for the hero.
        /// Called immediately after applying damage so the heal decision on the next turn reacts.
        /// </summary>
        void RegisterHeroBurstDamage(int damage);

        /// <summary>
        /// Registers a burst-damage event for a mercenary.
        /// Called immediately after applying damage so the heal decision on the next turn reacts.
        /// </summary>
        void RegisterMercenaryBurstDamage(Mercenary merc, int damage);
    }
}
