using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores MP.</summary>
    public sealed class FullMPPotion : Consumable
    {
        public FullMPPotion() : base("FullMPPotion", ItemRarity.Epic, "Fully restores MP", 500, 0, -1) { }
        /// <summary>Consume: fully restore MP.</summary>
        public override bool Consume(object context)
        {
            if (context is Hero hero)
            {
                return hero.RestoreMP(MPRestoreAmount);
            }
            return false;
        }
    }
}
