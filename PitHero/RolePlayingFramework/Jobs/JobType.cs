using System;

namespace RolePlayingFramework.Jobs
{
    /// <summary>
    /// Bitflag enum identifying which job classes can equip a piece of gear.
    /// Supports bitwise operations to combine multiple jobs (e.g. JobType.Knight | JobType.Mage).
    /// </summary>
    [Flags]
    public enum JobType
    {
        None = 0,
        Knight = 1 << 0,
        Monk = 1 << 1,
        Mage = 1 << 2,
        Priest = 1 << 3,
        Thief = 1 << 4,
        Archer = 1 << 5,

        /// <summary>All primary jobs combined.</summary>
        All = Knight | Monk | Mage | Priest | Thief | Archer
    }
}
