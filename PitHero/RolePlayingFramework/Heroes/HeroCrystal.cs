using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Represents a stored hero template with job, level, and base stats.</summary>
    public sealed class HeroCrystal
    {
        public string Name { get; }
        public IJob Job { get; }
        public int Level { get; }
        public StatBlock BaseStats { get; }

        public HeroCrystal(string name, IJob job, int level, in StatBlock baseStats)
        {
            Name = name;
            Job = job;
            Level = level < 1 ? 1 : level;
            BaseStats = baseStats;
        }

        /// <summary>Combines two crystals by averaging level and summing scaled stats.</summary>
        public static HeroCrystal Combine(string combinedName, HeroCrystal a, HeroCrystal b)
        {
            var level = (a.Level + b.Level + 1) / 2;
            var stats = a.BaseStats.Add(b.BaseStats.Scale(1f));
            // Merge job bonuses by summing their contributions then mapping to a composite job wrapper
            var job = new CompositeJob(a.Job, b.Job);
            return new HeroCrystal(combinedName, job, level, stats);
        }
    }
}
