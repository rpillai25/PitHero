namespace PitHero.VirtualGame
{
    /// <summary>
    /// Per-battle statistics accumulated by <see cref="VirtualBattleSink"/> during a single
    /// headless battle run.  One instance is created per <see cref="VirtualBattleRunner.RunAdjacentBattle"/>
    /// call and is consumed by the caller to build higher-level aggregates.
    /// </summary>
    public sealed class VirtualBattleMetrics
    {
        /// <summary>Pit level the battle took place on.</summary>
        public int PitLevel;

        /// <summary>Number of complete rounds resolved in this battle.</summary>
        public int Rounds;

        /// <summary>Total damage dealt by allies to monsters this battle.</summary>
        public int DamageDealt;

        /// <summary>Total damage dealt by monsters to allies this battle.</summary>
        public int DamageTaken;

        /// <summary>Total HP restored to allies this battle (skills + consumables).</summary>
        public int HealingDone;

        /// <summary>Number of healing actions applied (skill heals + consumable heals).</summary>
        public int HealsCount;

        /// <summary>Number of consumable items used from the bag this battle.</summary>
        public int PotionsConsumed;

        /// <summary>True when the hero died during this battle.</summary>
        public bool HeroDied;

        /// <summary>Number of mercenaries that died during this battle.</summary>
        public int MercDeaths;

        /// <summary>Number of monsters defeated this battle.</summary>
        public int MonstersDefeated;

        /// <summary>True when the monster roster for this battle contained a boss.</summary>
        public bool IsBossBattle;

        /// <summary>Total experience yielded by all defeated monsters this battle.</summary>
        public int XpEarned;

        /// <summary>Total gold yielded by all defeated monsters this battle.</summary>
        public int GoldEarned;
    }
}
