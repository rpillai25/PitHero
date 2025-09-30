using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of HP.</summary>
    public sealed class MidHPPotion : Consumable
    {
        public MidHPPotion() : base("MidHPPotion", ItemRarity.Rare, 500, 0) { }
        /// <summary>Consume: restore HP.</summary>
        public override bool Consume(object context)
        {
            if (context is Hero hero)
            {
                if (HPRestoreAmount < 0) hero.RestoreHP(hero.MaxHP); else hero.RestoreHP(HPRestoreAmount);
                return true;
            }
            return false;
        }
    }
}
