namespace RolePlayingFramework.Jobs
{
    /// <summary>Represents the tier of a job class.</summary>
    public enum JobTier
    {
        /// <summary>Primary/base job tier (e.g., Knight, Mage, Monk, Priest).</summary>
        Primary = 1,

        /// <summary>Secondary job tier (combinations of two primary jobs).</summary>
        Secondary = 2,

        /// <summary>Tertiary job tier (combinations of three primary jobs).</summary>
        Tertiary = 3
    }
}
