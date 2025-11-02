using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>Ninja (Knight + Thief) - Agile stealth warrior with evasion and burst.</summary>
    public sealed class Ninja : BaseJob
    {
        public Ninja() : base(
            name: "Ninja",
            // BaseBonus: Emphasizing Agility and Strength
            // Target Stats at L99: Str 72, Agi 92, Vit 60, Mag 62
            baseBonus: new StatBlock(strength: 3, agility: 3, vitality: 2, magic: 1),
            // GrowthPerLevel: Str: 3 + (0.704 * 98) ≈ 72, Agi: 3 + (0.908 * 98) ≈ 92
            // Vit: 2 + (0.592 * 98) ≈ 60, Mag: 1 + (0.622 * 98) ≈ 62
            growthPerLevel: new StatBlock(strength: 0.704f, agility: 0.908f, vitality: 0.592f, magic: 0.622f),
            tier: JobTier.Secondary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new EvasionMasteryPassive());
            list.Add(new TrapMasterPassive());
            list.Add(new ShadowSlashSkill());
            list.Add(new SmokeBombSkill());
        }
    }
}
