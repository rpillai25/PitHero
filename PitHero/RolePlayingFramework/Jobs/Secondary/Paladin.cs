using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>Paladin (Knight + Priest) - Holy tank and healer.</summary>
    public sealed class Paladin : BaseJob
    {
        public Paladin() : base(
            name: "Paladin",
            // BaseBonus: Emphasizing Strength and Vitality
            // Target Stats at L99: Str 75, Agi 48, Vit 85, Mag 60
            baseBonus: new StatBlock(strength: 4, agility: 1, vitality: 3, magic: 2),
            // GrowthPerLevel: Str: 4 + (0.724 * 98) ≈ 75, Agi: 1 + (0.480 * 98) ≈ 48
            // Vit: 3 + (0.837 * 98) ≈ 85, Mag: 2 + (0.592 * 98) ≈ 60
            growthPerLevel: new StatBlock(strength: 0.724f, agility: 0.480f, vitality: 0.837f, magic: 0.592f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new KnightsHonorPassive());
            list.Add(new DivineShieldPassive());
            list.Add(new HolyStrikeSkill());
            list.Add(new AuraHealSkill());
        }
    }
}
