using Microsoft.Xna.Framework;
using Nez;
using PitHero.Services.Analytics;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services
{
    /// <summary>
    /// Single entry point for "a monster finished one job task" (issue #413). Advances the monster's
    /// job progress and, on a level-up, announces it in the event console and analytics. Called from
    /// the farming and kitchen state machines at their task-completion points; fishing has the same
    /// plumbing but no caller until the feature exists.
    /// </summary>
    public static class MonsterJobTaskRecorder
    {
        private static readonly Color LevelUpColor = new Color(255, 220, 90);

        /// <summary>
        /// Records completed work for the job and announces a level-up when it happens.
        /// <paramref name="credit"/> defaults to one whole task; tilling passes a fraction.
        /// </summary>
        public static void Record(AlliedMonster monster, MonsterJob job, float credit = 1f)
        {
            if (monster == null || job == MonsterJob.None)
                return;
            if (!monster.RecordTask(job, credit))
                return;

            int level = monster.GetLevel(job);
            AnalyticsService.LogMonsterJobLevelUp(monster.Name, monster.MonsterTypeName, job.ToString(), level);

            var services = Core.Instance != null ? Core.Services : null;
            var events = services?.GetService<GameEventService>();
            if (events == null)
                return;
            events.EmitLocalized(UITextKey.ConsoleMonsterJobLevelUp,
                (events.MonsterName(monster.MonsterTypeName), Color.White),
                (monster.Name, Color.White),
                (events.LocalizeUI(GetJobNameKey(job)), LevelUpColor),
                (level.ToString(), LevelUpColor));
        }

        /// <summary>UI text key for a job's display name.</summary>
        public static string GetJobNameKey(MonsterJob job)
        {
            switch (job)
            {
                case MonsterJob.Farming: return UITextKey.JobNameFarming;
                case MonsterJob.Cooking: return UITextKey.JobNameKitchen;
                case MonsterJob.Fishing: return UITextKey.JobNameFishing;
                default: return UITextKey.JobNameNone;
            }
        }
    }
}
