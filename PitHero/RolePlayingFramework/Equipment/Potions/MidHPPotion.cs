using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of HP.</summary>
    public sealed class MidHPPotion : Consumable
    {
        public MidHPPotion() : base("MidHPPotion", ItemRarity.Rare, "Restores 500 HP", 100, 500, 0) { }
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
