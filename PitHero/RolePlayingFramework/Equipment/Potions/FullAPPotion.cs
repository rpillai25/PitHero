using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores AP.</summary>
    public sealed class FullAPPotion : Consumable
    {
        public FullAPPotion() : base("FullAPPotion", ItemRarity.Epic, 0, -1) { }
        /// <summary>Consume: fully restore AP.</summary>
        public override bool Consume(object context)
        {
            if (context is Hero hero)
            {
                hero.RestoreAP(APRestoreAmount);
                return true;
            }
            return false;
        }
    }
}
