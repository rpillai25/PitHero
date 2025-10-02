using RolePlayingFramework.Heroes;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of HP and AP.</summary>
    public sealed class MidMixPotion : Consumable
    {
        public MidMixPotion() : base("MidMixPotion", ItemRarity.Rare, "Restores 500 HP and 500 AP", 180, 500, 500) { }
        /// <summary>Consume: restore HP and AP.</summary>
        public override bool Consume(object context)
        {
            if (context is Hero hero)
            {
                bool hpRestored = false;
                bool apRestored = false;
                
                if (HPRestoreAmount < 0)
                    hpRestored = hero.RestoreHP(hero.MaxHP);
                else
                    hpRestored = hero.RestoreHP(HPRestoreAmount);
                
                apRestored = hero.RestoreAP(APRestoreAmount);
                
                return hpRestored || apRestored;
            }
            return false;
        }
    }
}
