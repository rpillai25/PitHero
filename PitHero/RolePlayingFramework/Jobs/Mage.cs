using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Glass cannon caster with strong magic and utility.</summary>
    public sealed class Mage : BaseJob
    {
        public Mage() : base(
            name: "Mage",
            baseBonus: new StatBlock(strength: 0, agility: 0, vitality: 0, magic: 5),
            growthPerLevel: new StatBlock(strength: 0, agility: 1, vitality: 0, magic: 3),
            abilities: new[] { JobAbility.ChannelMagic, JobAbility.StaffMastery })
        { }
    }
}
