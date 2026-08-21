using System.Collections.Generic;
using PitHero.Config;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.UI
{
    /// <summary>
    /// Aggregate headcounts for the Monster Info card (issue #394): roster totals per shift plus the
    /// farm/kitchen/idle split of whichever shift is currently awake. Pure and Core-free so it can be
    /// unit tested headless.
    /// </summary>
    public struct MonsterInfoStats
    {
        /// <summary>Monsters whose type works the day shift (6AM-10PM), asleep ones included.</summary>
        public int TotalDaytime;

        /// <summary>Monsters whose type works the night shift (10PM-6AM), asleep ones included.</summary>
        public int TotalNighttime;

        /// <summary>Awake monsters currently assigned to farming.</summary>
        public int FarmWorkers;

        /// <summary>Awake monsters currently assigned to the kitchen.</summary>
        public int KitchenWorkers;

        /// <summary>Awake monsters holding no job at all.</summary>
        public int IdleWorkers;

        /// <summary>
        /// Counts the roster. Totals span the whole roster; the three worker counts only cover the
        /// shift that is awake at <paramref name="isNighttime"/>. A monster assigned to Fishing (no
        /// work loop yet) counts in none of the three worker lines.
        /// </summary>
        public static MonsterInfoStats Compute(IReadOnlyList<AlliedMonster> roster, bool isNighttime)
        {
            var stats = new MonsterInfoStats();
            if (roster == null)
                return stats;

            for (int i = 0; i < roster.Count; i++)
            {
                var monster = roster[i];
                if (monster == null)
                    continue;

                bool nocturnal = MonsterScheduleConfig.IsNocturnal(monster.MonsterTypeName);
                if (nocturnal)
                    stats.TotalNighttime++;
                else
                    stats.TotalDaytime++;

                // Mirrors MonsterScheduleConfig.IsAsleep without needing an InGameTimeService.
                if (nocturnal != isNighttime)
                    continue;

                switch (monster.Job)
                {
                    case MonsterJob.Farming: stats.FarmWorkers++; break;
                    case MonsterJob.Cooking: stats.KitchenWorkers++; break;
                    case MonsterJob.None:    stats.IdleWorkers++;   break;
                }
            }

            return stats;
        }
    }
}
