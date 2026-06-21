using PitHero.Services;

namespace PitHero.Config
{
    /// <summary>
    /// Single source of truth for each worker monster's day/night schedule (issue #272).
    /// Daytime monsters work 6:00 AM – 10:00 PM; Nocturnal monsters work 10:00 PM – 6:00 AM.
    /// Outside their work window a monster is "asleep" and retreats into its monster house.
    /// </summary>
    public static class MonsterScheduleConfig
    {
        /// <summary>
        /// True if the monster type works the night shift (10PM–6AM). Accepts either the bare
        /// type name ("Orc") or the localized key form ("Monster_Orc").
        /// </summary>
        public static bool IsNocturnal(string monsterTypeName)
        {
            if (string.IsNullOrEmpty(monsterTypeName))
                return false;

            var bare = monsterTypeName.StartsWith("Monster_")
                ? monsterTypeName.Substring("Monster_".Length)
                : monsterTypeName;

            switch (bare)
            {
                case "Orc":
                case "Skeleton":
                case "GhostMiner":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// True if the monster is currently outside its work window and should be sleeping.
        /// A null time service (e.g. unit tests without Core) is treated as awake.
        /// </summary>
        public static bool IsAsleep(string monsterTypeName, InGameTimeService time)
        {
            if (time == null)
                return false;

            return IsNocturnal(monsterTypeName) ? time.IsActiveHours : time.IsNighttime;
        }
    }
}
