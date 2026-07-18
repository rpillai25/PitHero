using RolePlayingFramework.Equipment.Daggers;
using RolePlayingFramework.Equipment.Hammers;
using RolePlayingFramework.Equipment.Swords;
using RolePlayingFramework.Jobs;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Maps each primary job to the weakest weapon it can equip, granted pre-equipped at new game start (issue #316).</summary>
    public static class JobStartingWeapons
    {
        /// <summary>Returns a new instance of the job's starting weapon, or null if the job has no equippable weapon.</summary>
        public static Gear CreateStartingWeapon(JobType jobFlag)
        {
            switch (jobFlag)
            {
                case JobType.Knight:
                    return RustyBlade.Create();
                case JobType.Thief:
                case JobType.Mage:
                    return RustyDagger.Create();
                case JobType.Priest:
                    return Mallet.Create();
                // TODO(issue #317): Monk (WeaponKnuckle) and Archer (WeaponBow) have no
                // equippable weapon items yet — map their weakest weapon here once one exists.
                default:
                    return null;
            }
        }
    }
}
