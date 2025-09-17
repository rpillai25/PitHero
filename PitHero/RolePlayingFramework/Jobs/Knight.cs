using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Durable frontliner specializing in swords and heavy armor.</summary>
    public sealed class Knight : BaseJob
    {
        public Knight() : base(
            name: "Knight",
            baseBonus: new StatBlock(strength: 4, agility: 0, vitality: 3, magic: 0),
            growthPerLevel: new StatBlock(strength: 2, agility: 0, vitality: 2, magic: 0),
            abilities: new[] { JobAbility.SwordMastery, JobAbility.HeavyArmorTraining, JobAbility.Guard })
        { }
    }
}
