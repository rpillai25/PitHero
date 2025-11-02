using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>
    /// Shadow Fist (Thief + Monk) - Unarmed stealth specialist.
    /// Role: Agile striker with critical hits and evasion
    /// Target Stats at L99 (job contribution): HP ~315 (Vit 58), MP ~205 (Mag 65), Str 72, Agi 88, Vit 58, Mag 65
    /// </summary>
    public sealed class ShadowFist : BaseJob
    {
        public ShadowFist() : base(
            name: "Shadow Fist",
            // BaseBonus: L1 stats emphasizing Agility and Strength
            // Target: 15-25% stronger than parent jobs (Thief Agi 82, Monk Str 73)
            baseBonus: new StatBlock(strength: 3, agility: 3, vitality: 2, magic: 1),
            // GrowthPerLevel: Linear growth to reach L99 targets
            // Str: 3 + (0.704 * 98) ≈ 72, Agi: 3 + (0.867 * 98) ≈ 88, Vit: 2 + (0.571 * 98) ≈ 58, Mag: 1 + (0.653 * 98) ≈ 65
            growthPerLevel: new StatBlock(strength: 0.704f, agility: 0.867f, vitality: 0.571f, magic: 0.653f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new ShadowCounterPassive());
            list.Add(new FastHandsPassive());
            list.Add(new SneakPunchSkill());
            list.Add(new KiCloakSkill());
        }
    }
}
