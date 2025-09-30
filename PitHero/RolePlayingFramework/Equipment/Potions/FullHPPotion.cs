using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores HP.</summary>
    public sealed class FullHPPotion : Consumable
    {
        public FullHPPotion() : base("FullHPPotion", ItemRarity.Epic, -1, 0) { }
        /// <summary>Consume: fully restore HP.</summary>
        public override bool Consume(object context)
        {
            if (context is Hero hero)
            {
                hero.RestoreHP(hero.MaxHP);
                return true;
            }
            return false;
        }
    }
}
