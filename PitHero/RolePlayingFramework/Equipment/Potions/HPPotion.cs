using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a fixed amount of HP.</summary>
    public sealed class HPPotion : Consumable
    {
        public HPPotion() : base("HPPotion", ItemRarity.Normal, 100, 0) { }
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
