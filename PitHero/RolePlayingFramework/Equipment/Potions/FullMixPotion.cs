using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores HP and AP.</summary>
    public sealed class FullMixPotion : Consumable
    {
        public FullMixPotion() : base("FullMixPotion", ItemRarity.Epic, "Fully restores HP and AP", 900, -1, -1) { }
        /// <summary>Consume: fully restore HP and AP.</summary>
        public override bool Consume(object context)
        {
            if (context is Hero hero)
            {
                bool hpRestored = hero.RestoreHP(hero.MaxHP);
                bool apRestored = hero.RestoreAP(APRestoreAmount);
                return hpRestored || apRestored;
            }
            return false;
        }
    }
}
