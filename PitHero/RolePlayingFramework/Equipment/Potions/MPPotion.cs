using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a fixed amount of MP.</summary>
    public sealed class MPPotion : Consumable
    {
        public MPPotion() : base("MPPotion", ItemRarity.Normal, "Restores 100 MP", 20, 0, 100) { }
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
