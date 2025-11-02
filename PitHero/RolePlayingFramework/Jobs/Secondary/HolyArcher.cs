using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>
    /// Holy Archer (Priest + Bowman) - Ranged support mixing healing and archery.
    /// Role: Ranged healer/support with holy arrows
    /// Target Stats at L99 (job contribution): HP ~325 (Vit 60), MP ~274 (Mag 88), Str 68, Agi 82, Vit 60, Mag 88
    /// </summary>
    public sealed class HolyArcher : BaseJob
    {
        public HolyArcher() : base(
            name: "Holy Archer",
            // BaseBonus: L1 stats emphasizing Magic and Agility
            // Target: 15-25% stronger than parent jobs (Priest Mag 78, Bowman Agi 72)
            baseBonus: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 3),
            // GrowthPerLevel: Linear growth to reach L99 targets
            // Str: 2 + (0.673 * 98) ≈ 68, Agi: 2 + (0.816 * 98) ≈ 82, Vit: 2 + (0.592 * 98) ≈ 60, Mag: 3 + (0.867 * 98) ≈ 88
            growthPerLevel: new StatBlock(strength: 0.673f, agility: 0.816f, vitality: 0.592f, magic: 0.867f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new DivineVisionPassive());
            list.Add(new BlessingArrowPassive());
            list.Add(new LightshotSkill());
            list.Add(new SacredVolleySkill());
        }
    }
}
