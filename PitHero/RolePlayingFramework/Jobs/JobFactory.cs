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
                // Legacy display name cases (kept for backward compat with saved games and HeroCreationUI)
                // TODO: Update callers to pass JobTextKey constants, then remove these display-name cases
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
        /// Creates a job from a name string. Supports composite jobs serialized as "JobA_NameKey-JobB_NameKey[-...]".
        /// Parses recursively so that 3+ job composites (Hero/Legend/Chosen One) are reconstructed correctly.
        /// Returns Knight as default for unknown job names.
        /// </summary>
        public static IJob CreateJob(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
            {
                return new Knight();
            }

            // Scan for the first dash where the left side is a valid primary-job key.
            // Primary job keys (e.g. "Job_Knight_Name") never contain a dash, so the
            // first dash that produces a non-empty right side is the correct split point.
            int searchFrom = 0;
            while (true)
            {
                var dashIndex = jobName.IndexOf('-', searchFrom);
                if (dashIndex < 0 || dashIndex >= jobName.Length - 1)
                    break;

                var nameA = jobName.Substring(0, dashIndex);
                var nameB = jobName.Substring(dashIndex + 1);

                // Only split here if the left part resolves to a known primary job.
                if (IsKnownPrimaryJobKey(nameA))
                {
                    var jobA = CreatePrimaryJob(nameA);
                    var jobB = CreateJob(nameB); // recursive — handles 3+ job composites
                    return new CompositeJob(jobA, jobB);
                }

                searchFrom = dashIndex + 1;
            }

            return CreatePrimaryJob(jobName);
        }

        /// <summary>Returns true if the given string corresponds to a known primary job key or display name.</summary>
        private static bool IsKnownPrimaryJobKey(string name)
        {
            switch (name)
            {
                case "Knight":
                case JobTextKey.Job_Knight_Name:
                case "Mage":
                case JobTextKey.Job_Mage_Name:
                case "Monk":
                case JobTextKey.Job_Monk_Name:
                case "Priest":
                case JobTextKey.Job_Priest_Name:
                case "Archer":
                case JobTextKey.Job_Archer_Name:
                case "Thief":
                case JobTextKey.Job_Thief_Name:
                    return true;
                default:
                    return false;
            }
        }
    }
}
