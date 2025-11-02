using System.Collections.Generic;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Jobs.Tertiary
{
    /// <summary>Mystic Avenger (Dragon Fist + Spellcloak) - Magic-infused martial artist with stealth.
    /// Target Stats at L99: Str 80, Agi 90, Vit 72, Mag 99</summary>
    public sealed class MysticAvenger : BaseJob
    {
        public MysticAvenger() : base(
            name: "Mystic Avenger",
            baseBonus: new StatBlock(strength: 3, agility: 4, vitality: 2, magic: 4),
            growthPerLevel: new StatBlock(strength: 0.786f, agility: 0.878f, vitality: 0.714f, magic: 0.969f),
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
