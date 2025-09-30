using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of AP.</summary>
    public sealed class MidAPPotion : Consumable
    {
        public MidAPPotion() : base("MidAPPotion", ItemRarity.Rare, 0, 500) { }
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
