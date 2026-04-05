using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using PitHero;

namespace RolePlayingFramework.Jobs
{
    /// <summary>Creates job instances from job name strings for save/load reconstruction.</summary>
    public static class JobFactory
    {
        /// <summary>Creates a single primary job by name. Returns Knight as default for unknown names.</summary>
        private static IJob CreatePrimaryJob(string jobName)
        {
            switch (jobName)
            {
                case "Knight":
                case JobTextKey.Job_Knight_Name: return new Knight();
                case "Mage":
                case JobTextKey.Job_Mage_Name: return new Mage();
                case "Monk":
                case JobTextKey.Job_Monk_Name: return new Monk();
                case "Priest":
                case JobTextKey.Job_Priest_Name: return new Priest();
                case "Archer":
                case JobTextKey.Job_Archer_Name: return new Archer();
                case "Thief":
                case JobTextKey.Job_Thief_Name: return new Thief();
                default: return new Knight();
            }
        }

        /// <summary>
        /// Creates a job from a name string. Supports composite jobs in "JobA-JobB" format.
        /// Returns Knight as default for unknown job names.
        /// </summary>
        public static IJob CreateJob(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
            {
                return new Knight();
            }

            var dashIndex = jobName.IndexOf('-');
            if (dashIndex > 0 && dashIndex < jobName.Length - 1)
            {
                var nameA = jobName.Substring(0, dashIndex);
                var nameB = jobName.Substring(dashIndex + 1);
                var jobA = CreatePrimaryJob(nameA);
                var jobB = CreatePrimaryJob(nameB);
                return new CompositeJob(jobA, jobB);
            }

            return CreatePrimaryJob(jobName);
        }
    }
}
