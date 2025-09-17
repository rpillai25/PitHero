using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Martial artist focused on endurance and counters.</summary>
    public sealed class Monk : BaseJob
    {
        public Monk() : base(
            name: "Monk",
            baseBonus: new StatBlock(strength: 3, agility: 1, vitality: 3, magic: 0),
            growthPerLevel: new StatBlock(strength: 2, agility: 1, vitality: 2, magic: 0),
            abilities: new[] { JobAbility.FistMastery, JobAbility.Counter })
        { }
    }
}
