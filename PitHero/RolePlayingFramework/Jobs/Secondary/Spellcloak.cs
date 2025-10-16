using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Secondary
{
    /// <summary>Spellcloak (Mage + Thief) - Stealthy spellcaster with evasion and magical attacks.</summary>
    public sealed class Spellcloak : BaseJob
    {
        public Spellcloak() : base(
            name: "Spellcloak",
            baseBonus: new StatBlock(strength: 2, agility: 3, vitality: 1, magic: 3),
            growthPerLevel: new StatBlock(strength: 1, agility: 3, vitality: 1, magic: 2))
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new MiragePassive());
            list.Add(new ArcaneStealthPassive());
            list.Add(new ShadowBoltSkill());
            list.Add(new FadeSkill());
        }
    }
}
