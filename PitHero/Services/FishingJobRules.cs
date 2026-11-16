using System.Collections.Generic;
using PitHero.Config;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services
{
    /// <summary>Why a monster can or cannot be given the Fishing job.</summary>
    public enum FishingAssignResult
    {
        Allowed = 0,
        NeedsPond = 1,
        CrewFull = 2,
    }

    /// <summary>
    /// The one rule for manually assigning Fishing, shared by the Monsters window (to grey out the
    /// button) and the SetMonsterJob command handler (the authoritative, replay-safe gate): a Fishing
    /// Pond must be placed, and the monster's shift (day or night) may hold at most
    /// <see cref="GameConfig.FishingMaxWorkersPerShift"/> fishers. A monster that already fishes is
    /// always allowed (re-selecting its own job). Reads simulation state only.
    /// </summary>
    public static class FishingJobRules
    {
        /// <summary>Checks whether <paramref name="monster"/> may be switched to Fishing.</summary>
        public static FishingAssignResult CanAssign(IReadOnlyList<AlliedMonster> roster, AlliedMonster monster, bool pondPlaced)
        {
            if (monster != null && monster.Job == MonsterJob.Fishing)
                return FishingAssignResult.Allowed;
            if (!pondPlaced)
                return FishingAssignResult.NeedsPond;
            bool nocturnal = MonsterScheduleConfig.IsNocturnal(monster?.MonsterTypeName);
            return CountFishers(roster, nocturnal) >= GameConfig.FishingMaxWorkersPerShift
                ? FishingAssignResult.CrewFull
                : FishingAssignResult.Allowed;
        }

        /// <summary>Monsters of one shift whose job is Fishing.</summary>
        public static int CountFishers(IReadOnlyList<AlliedMonster> roster, bool nocturnal)
        {
            int count = 0;
            if (roster == null)
                return 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var m = roster[i];
                if (m != null && m.Job == MonsterJob.Fishing && MonsterScheduleConfig.IsNocturnal(m.MonsterTypeName) == nocturnal)
                    count++;
            }
            return count;
        }
    }
}
