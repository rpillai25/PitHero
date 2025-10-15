using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Ninja (Knight + Thief) - Agile warrior with evasion and stealth abilities.</summary>
    public sealed class Ninja : BaseJob
    {
        public Ninja() : base(
            name: "Ninja",
            baseBonus: new StatBlock(strength: 3, agility: 3, vitality: 2, magic: 1),
            growthPerLevel: new StatBlock(strength: 1, agility: 3, vitality: 1, magic: 1))
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
