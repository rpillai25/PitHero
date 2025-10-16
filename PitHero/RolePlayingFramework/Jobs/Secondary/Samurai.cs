using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>Samurai (Knight + Monk) - Elite warrior with high physical stats and counterattack abilities.</summary>
    public sealed class Samurai : BaseJob
    {
        public Samurai() : base(
            name: "Samurai",
            baseBonus: new StatBlock(strength: 4, agility: 2, vitality: 3, magic: 1),
            growthPerLevel: new StatBlock(strength: 2, agility: 2, vitality: 2, magic: 1))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new BushidoPassive());
            list.Add(new IronWillPassive());
            list.Add(new IaidoSlashSkill());
            list.Add(new DragonKickSkill());
        }
    }
}
