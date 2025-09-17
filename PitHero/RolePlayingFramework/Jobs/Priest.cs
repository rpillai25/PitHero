using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Support caster with healing and modest defenses.</summary>
    public sealed class Priest : BaseJob
    {
        public Priest() : base(
            name: "Priest",
            baseBonus: new StatBlock(strength: 0, agility: 0, vitality: 2, magic: 3),
            growthPerLevel: new StatBlock(strength: 0, agility: 1, vitality: 1, magic: 2),
            abilities: new[] { JobAbility.Heal, JobAbility.StaffMastery })
        { }
    }
}
