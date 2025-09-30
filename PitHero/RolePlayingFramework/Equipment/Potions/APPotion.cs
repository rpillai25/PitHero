using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a fixed amount of AP.</summary>
    public sealed class APPotion : Consumable
    {
        public APPotion() : base("APPotion", ItemRarity.Normal, 0, 100) { }
        /// <summary>Consume: restore AP.</summary>
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
