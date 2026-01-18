using RolePlayingFramework.Heroes;
using RolePlayingFramework.Mercenaries;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Base class for all HP/MP restoring potions.</summary>
    public abstract class BaseHPMPPotion : Consumable
    {
        protected BaseHPMPPotion(string name, ItemRarity rarity, string description, int price, int hpRestoreAmount, int mpRestoreAmount)
            : base(name, rarity, description, price, hpRestoreAmount, mpRestoreAmount)
        {
        }

        /// <summary>Consume: restore HP and/or MP.</summary>
        public override bool Consume(object context)
        {
            bool hpRestored = RestoreHP(context);
            bool mpRestored = RestoreMP(context);
            return hpRestored || mpRestored;
        }

        /// <summary>Restores HP for hero or mercenary. Returns true if HP was actually restored.</summary>
        private bool RestoreHP(object context)
        {
            if (HPRestoreAmount == 0) return false;

            if (context is Hero hero)
            {
                if (HPRestoreAmount < 0)
                    return hero.RestoreHP(hero.MaxHP);
                else
                    return hero.RestoreHP(HPRestoreAmount);
            }
            else if (context is Mercenary mercenary)
            {
                if (HPRestoreAmount < 0)
                    return mercenary.RestoreHP(mercenary.MaxHP);
                else
                    return mercenary.RestoreHP(HPRestoreAmount);
            }

            return false;
        }

        /// <summary>Restores MP for hero or mercenary. Returns true if MP was actually restored.</summary>
        private bool RestoreMP(object context)
        {
            if (MPRestoreAmount == 0) return false;

            if (context is Hero hero)
            {
                if (MPRestoreAmount < 0)
                    return hero.RestoreMP(MPRestoreAmount); // -1 is handled by Hero.RestoreMP
                else
                    return hero.RestoreMP(MPRestoreAmount);
            }
            else if (context is Mercenary mercenary)
            {
                if (MPRestoreAmount < 0)
                {
                    // Full MP restore for mercenary
                    int restoreAmount = mercenary.MaxMP - mercenary.CurrentMP;
                    if (restoreAmount > 0)
                    {
                        mercenary.RestoreMP(restoreAmount);
                        return true;
                    }
                    return false;
                }
                else if (MPRestoreAmount > 0)
                {
                    int beforeMP = mercenary.CurrentMP;
                    mercenary.RestoreMP(MPRestoreAmount);
                    return mercenary.CurrentMP > beforeMP;
                }
            }

            return false;
        }
    }
}
