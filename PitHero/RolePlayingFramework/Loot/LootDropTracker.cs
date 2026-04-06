using RolePlayingFramework.Jobs;

namespace RolePlayingFramework.Loot
{
    /// <summary>
    /// Tracks how many job-matched equipment drops each job class has received during the current run.
    /// Used to compute deficit bonuses that dynamically increase drop weights for party members who are
    /// behind on gear relative to the rest of the party.
    /// </summary>
    public class LootDropTracker
    {
        /// <summary>
        /// The six primary job flags in bit-position order.
        /// Index 0 = Knight (bit 0), 1 = Monk (bit 1), 2 = Mage (bit 2),
        /// 3 = Priest (bit 3), 4 = Thief (bit 4), 5 = Archer (bit 5).
        /// </summary>
        private static readonly JobType[] _jobFlags = new JobType[]
        {
            JobType.Knight,
            JobType.Monk,
            JobType.Mage,
            JobType.Priest,
            JobType.Thief,
            JobType.Archer,
        };

        /// <summary>Number of primary job flag slots (6).</summary>
        public const int JobFlagCount = 6;

        /// <summary>Returns the job flag at the given index (0–5).</summary>
        public static JobType GetJobFlag(int index) => _jobFlags[index];

        /// <summary>Pre-allocated drop count array — one slot per primary job.</summary>
        private readonly int[] _dropCounts = new int[JobFlagCount];

        /// <summary>
        /// Resets all per-job drop counts to zero.
        /// Call at the start of each new pit run.
        /// </summary>
        public void Initialize()
        {
            for (int i = 0; i < _dropCounts.Length; i++)
                _dropCounts[i] = 0;
        }

        /// <summary>
        /// Records an equipment drop by incrementing the count for every job flag
        /// present in <paramref name="allowedJobs"/>.
        /// </summary>
        /// <param name="allowedJobs">Bitmask of jobs that can equip the dropped item.</param>
        public void RecordDrop(JobType allowedJobs)
        {
            for (int i = 0; i < _jobFlags.Length; i++)
            {
                if ((allowedJobs & _jobFlags[i]) != 0)
                    _dropCounts[i]++;
            }
        }

        /// <summary>
        /// Returns the drop count for a single job flag.
        /// </summary>
        /// <param name="singleJobFlag">A single <see cref="JobType"/> flag bit (e.g. <c>JobType.Knight</c>).</param>
        public int GetDropCount(JobType singleJobFlag)
        {
            for (int i = 0; i < _jobFlags.Length; i++)
            {
                if (_jobFlags[i] == singleJobFlag)
                    return _dropCounts[i];
            }
            return 0;
        }

        /// <summary>
        /// Returns the highest drop count recorded across all six job slots.
        /// Returns 0 when no drops have been recorded yet.
        /// </summary>
        public int GetMaxDropCount()
        {
            int max = 0;
            for (int i = 0; i < _dropCounts.Length; i++)
            {
                if (_dropCounts[i] > max)
                    max = _dropCounts[i];
            }
            return max;
        }

        /// <summary>
        /// Returns a direct reference to the internal drop-count array.
        /// The caller must not resize or store this reference beyond immediate use.
        /// Index mapping follows <see cref="_jobFlags"/> order.
        /// </summary>
        public int[] GetDropCountsArray()
        {
            return _dropCounts;
        }
    }
}
