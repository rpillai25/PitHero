using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>Shadow Fist (Monk + Thief) - Stealthy martial artist with counterattacks and mobility.</summary>
    public sealed class ShadowFist : BaseJob
    {
        public ShadowFist() : base(
            name: "Shadow Fist",
            baseBonus: new StatBlock(strength: 2, agility: 4, vitality: 2, magic: 1),
            growthPerLevel: new StatBlock(strength: 1, agility: 3, vitality: 1, magic: 1))
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
