using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Mystic Avenger (Dragon Fist + Spellcloak) - Magic-infused martial artist with stealth.</summary>
    public sealed class MysticAvenger : BaseJob
    {
        public MysticAvenger() : base(
            name: "Mystic Avenger",
            baseBonus: new StatBlock(strength: 3, agility: 4, vitality: 2, magic: 4),
            growthPerLevel: new StatBlock(strength: 2, agility: 3, vitality: 1, magic: 2),
            tier: JobTier.Tertiary)
        { }

        protected override void DefineSkills(List<ISkill> list)
        {
            list.Add(new MysticCounterPassive());
            list.Add(new ArcaneCloakPassive());
            list.Add(new DragonBoltSkill());
            list.Add(new MysticFadeSkill());
        }
    }
}
