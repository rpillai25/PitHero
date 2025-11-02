using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>
    /// Dragon Fist (Monk + Mage) - Elemental martial artist with high burst damage.
    /// Role: Melee fighter with magical enhancement and elemental strikes
    /// Target Stats at L99 (job contribution): HP ~325 (Vit 60), MP ~235 (Mag 75), Str 77, Agi 80, Vit 60, Mag 75
    /// </summary>
    public sealed class DragonFist : BaseJob
    {
        public DragonFist() : base(
            name: "Dragon Fist",
            // BaseBonus: L1 stats emphasizing Strength and Agility
            // Target: 15-25% stronger than parent jobs (Monk Str 73, Mage Mag 88)
            baseBonus: new StatBlock(strength: 3, agility: 3, vitality: 2, magic: 2),
            // GrowthPerLevel: Linear growth to reach L99 targets
            // Str: 3 + (0.755 * 98) ≈ 77, Agi: 3 + (0.786 * 98) ≈ 80, Vit: 2 + (0.592 * 98) ≈ 60, Mag: 2 + (0.745 * 98) ≈ 75
            growthPerLevel: new StatBlock(strength: 0.755f, agility: 0.786f, vitality: 0.592f, magic: 0.745f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new ArcaneFuryPassive());
            list.Add(new KiBarrierPassive());
            list.Add(new DragonClawSkill());
            list.Add(new EnergyBurstSkill());
        }
    }
}
