using RolePlayingFramework.Heroes;
using System;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores moderate HP and AP.</summary>
    public sealed class MixPotion : Consumable
    {
        public MixPotion() : base("MixPotion", ItemRarity.Normal, "Restores 100 HP and 100 AP", 30, 100, 100) { }
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
                
                if (APRestoreAmount < 0)
                    apRestored = hero.RestoreAP(APRestoreAmount);
                else
                    apRestored = hero.RestoreAP(Math.Min(hero.MaxAP, hero.CurrentAP + APRestoreAmount));
                
                return hpRestored || apRestored;
            }
            return false;
        }
    }
}
