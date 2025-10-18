using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores HP and MP.</summary>
    public sealed class FullMixPotion : Consumable
    {
        public FullMixPotion() : base("FullMixPotion", ItemRarity.Epic, "Fully restores HP and MP", 900, -1, -1) { }
        /// <summary>Consume: fully restore HP and MP.</summary>
        public override bool Consume(object context)
        {
            if (context is Hero hero)
            {
                bool hpRestored = hero.RestoreHP(hero.MaxHP);
                bool mpRestored = hero.RestoreMP(MPRestoreAmount);
                return hpRestored || mpRestored;
            }
            return false;
        }
    }
}
