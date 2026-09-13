using System.Collections.Generic;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Mercenaries;

namespace RolePlayingFramework.Equipment
{
    /// <summary>
    /// Party-wide upgrade test used by auto-sell (issue #411): gear that would improve the hero or any
    /// hired mercenary is the last thing auto-sell parts with. Pure, no Nez dependencies.
    /// </summary>
    public static class PartyGearUpgradeCheck
    {
        /// <summary>True when the gear beats what the hero or any listed mercenary has equipped in its category.</summary>
        public static bool IsUpgradeForAnyone(Hero hero, IReadOnlyList<Mercenary> mercenaries, IGear gear)
        {
            if (gear == null)
                return false;
            if (GearAutoEquipService.IsUpgradeFor(hero, gear))
                return true;
            if (mercenaries == null)
                return false;
            for (int i = 0; i < mercenaries.Count; i++)
            {
                if (GearAutoEquipService.IsUpgradeFor(mercenaries[i], gear))
                    return true;
            }
            return false;
        }
    }
}
