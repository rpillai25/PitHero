using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores HP and AP.</summary>
    public sealed class FullMixPotion : Consumable
    {
        public FullMixPotion() : base("FullMixPotion", ItemRarity.Epic, -1, -1) { }
        /// <summary>Consume: fully restore HP and AP.</summary>
        public override bool Consume(object context)
        {
            if (context is Hero hero)
            {
                hero.RestoreHP(hero.MaxHP);
                hero.RestoreAP(APRestoreAmount);
                return true;
            }
            return false;
        }
    }
}
