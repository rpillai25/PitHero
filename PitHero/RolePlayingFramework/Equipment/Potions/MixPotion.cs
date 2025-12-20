using RolePlayingFramework.Heroes;
using System;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores moderate HP and MP.</summary>
    public sealed class MixPotion : Consumable
    {
        public MixPotion() : base("MixPotion", ItemRarity.Normal, "Restores 100 HP and 100 MP", 30, 100, 100) { }
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

                if (MPRestoreAmount < 0)
                    mpRestored = hero.RestoreMP(MPRestoreAmount);
                else
                    mpRestored = hero.RestoreMP(Math.Min(hero.MaxMP, hero.CurrentMP + MPRestoreAmount));

                return hpRestored || mpRestored;
            }
            return false;
        }
    }
}
