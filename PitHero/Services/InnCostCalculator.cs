using Nez;
using PitHero.ECS.Components;
using RolePlayingFramework.Heroes;

namespace PitHero.Services
{
    /// <summary>
    /// Inn nap pricing: every party member (hero + hired mercenaries) pays
    /// <see cref="GameConfig.InnCostBaseGoldPerMember"/> plus
    /// <see cref="GameConfig.InnCostGoldPerTenLevels"/> per full 10 levels.
    /// Night sleep stays free.
    /// </summary>
    public static class InnCostCalculator
    {
        /// <summary>Total nap cost for the current live party (hero + hired mercenaries).</summary>
        public static int GetCurrentPartyCost(Hero hero)
        {
            int total = hero != null
                ? GameConfig.GetInnCostForMember(hero.Level)
                : GameConfig.InnCostBaseGoldPerMember;

            var mercManager = Core.Instance != null ? Core.Services.GetService<MercenaryManager>() : null;
            if (mercManager != null)
            {
                var hired = mercManager.GetHiredMercenaries();
                for (int i = 0; i < hired.Count; i++)
                {
                    var comp = hired[i].GetComponent<MercenaryComponent>();
                    if (comp?.LinkedMercenary != null)
                        total += GameConfig.GetInnCostForMember(comp.LinkedMercenary.Level);
                }
            }
            return total;
        }
    }
}
