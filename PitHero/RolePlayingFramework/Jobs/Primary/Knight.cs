using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Primary
{
    /// <summary>
    /// Durable frontliner specializing in swords and heavy armor.
    /// Role: High HP/Defense tank
    /// Target Stats at L99: HP ~415 (Vit 78), MP ~94 (Mag 28), Str 68, Agi 42, Vit 78, Mag 28
    /// </summary>
    public sealed class Knight : BaseJob
    {
        public Knight() : base(
            name: "Knight",
            // BaseBonus provides initial stats at level 1
            // Target: High Vitality for HP, moderate Strength, low Agility and Magic
            baseBonus: new StatBlock(strength: 8, agility: 5, vitality: 9, magic: 3),
            // GrowthPerLevel: Linear stat growth to reach L99 targets
            // Str: 8 + (0.612 * 98) ≈ 68, Agi: 5 + (0.378 * 98) ≈ 42
            // Vit: 9 + (0.704 * 98) ≈ 78, Mag: 3 + (0.255 * 98) ≈ 28
            growthPerLevel: new StatBlock(strength: 0.612f, agility: 0.378f, vitality: 0.704f, magic: 0.255f),
            tier: JobTier.Primary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new LightArmorPassive());
            list.Add(new HeavyArmorPassive());
            list.Add(new SpinSlashSkill());
            list.Add(new HeavyStrikeSkill());
        }
    }
}
