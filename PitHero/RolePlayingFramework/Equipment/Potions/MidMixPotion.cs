using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of HP and MP.</summary>
    public sealed class MidMixPotion : Consumable
    {
        public MidMixPotion() : base("MidMixPotion", ItemRarity.Rare, "Restores 500 HP and 500 MP", 180, 500, 500) { }
        /// <summary>Consume: restore HP and MP.</summary>
        public override bool Consume(object context)
        {
            if (context is Hero hero)
            {
                bool hpRestored = false;
                bool mpRestored = false;
                
                if (HPRestoreAmount < 0)
                    hpRestored = hero.RestoreHP(hero.MaxHP);
                else
                    hpRestored = hero.RestoreHP(HPRestoreAmount);
                
                mpRestored = hero.RestoreMP(MPRestoreAmount);
                
                return hpRestored || mpRestored;
            }
            return false;
        }
    }
}
