using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of MP.</summary>
    public sealed class MidMPPotion : Consumable
    {
        public MidMPPotion() : base("MidMPPotion", ItemRarity.Rare, "Restores 500 MP", 100, 0, 500) { }
        /// <summary>Consume: restore MP.</summary>
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
