using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a fixed amount of HP.</summary>
    public sealed class HPPotion : Consumable
    {
        public HPPotion() : base("HPPotion", ItemRarity.Normal, "Restores 100 HP", 20, 100, 0) { }
        /// <summary>Consume: restore HP.</summary>
        public override bool Consume(object context)
        {
            if (context is Hero hero)
            {
                if (HPRestoreAmount < 0)
                    return hero.RestoreHP(hero.MaxHP);
                else
                    return hero.RestoreHP(HPRestoreAmount);
            }
            return false;
        }
    }
}
