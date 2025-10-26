using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>Dragon Fist (Mage + Monk) - Martial mage with physical and magical strikes.</summary>
    public sealed class DragonFist : BaseJob
    {
        public DragonFist() : base(
            name: "Dragon Fist",
            baseBonus: new StatBlock(strength: 3, agility: 2, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 2, agility: 2, vitality: 1, magic: 2),
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
