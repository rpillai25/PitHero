using Nez;
using PitHero.ECS.Components;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Jobs;

namespace PitHero.Services
{
    /// <summary>
    /// Auto-hires tavern mercenaries whose job matches one of the two configured slots (issue #350).
    /// Call-driven (no update loop): <see cref="MercenaryManager"/> invokes <see cref="TryAutoHire"/>
    /// when a mercenary arrives at the tavern, and SettingsUI invokes <see cref="TryHirePass"/> when
    /// the settings window closes so already-seated mercenaries are considered too.
    ///
    /// A slot set to <see cref="JobType.None"/> hires nothing; duplicate slots hire two of the same
    /// job. Every hire honors the shared Gold Buffer and the party cap.
    /// </summary>
    public class AutoHireMercenaryService
    {
        private readonly GameStateService _gameState;
        private readonly AutoSeedPurchaseService _goldBufferSource;
        private readonly MercenaryManager _mercenaryManager;

        /// <summary>Master toggle. Off by default.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Job to auto-hire for the first party slot. None disables the slot.</summary>
        public JobType Merc1Job { get; set; } = JobType.None;

        /// <summary>Job to auto-hire for the second party slot. None disables the slot.</summary>
        public JobType Merc2Job { get; set; } = JobType.None;

        /// <summary>Gold floor shared with all automated spending; no hire may take funds below it.</summary>
        public int GoldBuffer => _goldBufferSource?.GoldBuffer ?? 0;

        /// <summary>
        /// Initialises the service. <paramref name="goldBufferSource"/> owns the single shared
        /// Gold Buffer setting, so this service must be registered after it.
        /// </summary>
        public AutoHireMercenaryService(GameStateService gameState, AutoSeedPurchaseService goldBufferSource, MercenaryManager mercenaryManager)
        {
            _gameState = gameState;
            _goldBufferSource = goldBufferSource;
            _mercenaryManager = mercenaryManager;
        }

        /// <summary>
        /// Attempts to auto-hire one just-arrived tavern mercenary. Returns true if it was hired.
        /// Pre-checks the party cap (via CanHireMore, which also covers the hero-dead hiring block)
        /// and the Gold Buffer, since HireMercenary enforces neither.
        /// </summary>
        public bool TryAutoHire(Entity mercEntity)
        {
            if (!Enabled || mercEntity == null || _mercenaryManager == null || _gameState == null)
                return false;

            var comp = mercEntity.GetComponent<MercenaryComponent>();
            if (comp == null || comp.LinkedMercenary == null || comp.IsHired || comp.IsBeingRemoved || !comp.IsWaitingInTavern)
                return false;

            if (!_mercenaryManager.CanHireMore())
                return false;

            GetHiredJobs(out var hiredJob1, out var hiredJob2);
            if (!JobQualifies(comp.LinkedMercenary.Job.JobFlag, Merc1Job, Merc2Job, hiredJob1, hiredJob2))
                return false;

            var hireCost = BalanceConfig.CalculateMercenaryHireCost(comp.LinkedMercenary.Level);
            if (!CanAffordHire(_gameState.Funds, hireCost, GoldBuffer))
                return false;

            return _mercenaryManager.HireMercenary(mercEntity);
        }

        /// <summary>
        /// Sweeps mercenaries already seated in the tavern and hires every match, oldest first.
        /// Returns the number of hires made.
        /// </summary>
        public int TryHirePass()
        {
            if (!Enabled || _mercenaryManager == null || _gameState == null)
                return 0;

            var hires = 0;
            var hiredOne = true;
            while (hiredOne && _mercenaryManager.CanHireMore())
            {
                hiredOne = false;
                GetHiredJobs(out var hiredJob1, out var hiredJob2);

                // Oldest qualifying, affordable, seated mercenary wins this round
                var candidates = _mercenaryManager.GetUnhiredMercenaries();
                Entity best = null;
                var bestSpawnId = int.MaxValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var comp = candidates[i].GetComponent<MercenaryComponent>();
                    if (comp == null || comp.LinkedMercenary == null || comp.IsHired || comp.IsBeingRemoved || !comp.IsWaitingInTavern)
                        continue;
                    if (!JobQualifies(comp.LinkedMercenary.Job.JobFlag, Merc1Job, Merc2Job, hiredJob1, hiredJob2))
                        continue;
                    var hireCost = BalanceConfig.CalculateMercenaryHireCost(comp.LinkedMercenary.Level);
                    if (!CanAffordHire(_gameState.Funds, hireCost, GoldBuffer))
                        continue;
                    if (comp.SpawnId < bestSpawnId)
                    {
                        best = candidates[i];
                        bestSpawnId = comp.SpawnId;
                    }
                }

                if (best != null && _mercenaryManager.HireMercenary(best))
                {
                    hires++;
                    hiredOne = true;
                }
            }
            return hires;
        }

        /// <summary>Reads the jobs of the currently hired party mercenaries (None when a slot is empty).</summary>
        private void GetHiredJobs(out JobType hiredJob1, out JobType hiredJob2)
        {
            hiredJob1 = JobType.None;
            hiredJob2 = JobType.None;
            var hired = _mercenaryManager.GetHiredMercenaries();
            for (int i = 0; i < hired.Count; i++)
            {
                var comp = hired[i].GetComponent<MercenaryComponent>();
                if (comp == null || comp.LinkedMercenary == null)
                    continue;
                if (hiredJob1 == JobType.None)
                    hiredJob1 = comp.LinkedMercenary.Job.JobFlag;
                else
                    hiredJob2 = comp.LinkedMercenary.Job.JobFlag;
            }
        }

        /// <summary>
        /// Multiset matching: each hired mercenary satisfies at most one desired slot of its job;
        /// the candidate qualifies only if an unsatisfied slot of its job remains.
        /// </summary>
        public static bool JobQualifies(JobType candidateJob, JobType slot1, JobType slot2, JobType hiredJob1, JobType hiredJob2)
        {
            if (candidateJob == JobType.None)
                return false;

            var d1 = slot1 != JobType.None;
            var d2 = slot2 != JobType.None;
            if (hiredJob1 != JobType.None)
            {
                if (d1 && slot1 == hiredJob1) d1 = false;
                else if (d2 && slot2 == hiredJob1) d2 = false;
            }
            if (hiredJob2 != JobType.None)
            {
                if (d1 && slot1 == hiredJob2) d1 = false;
                else if (d2 && slot2 == hiredJob2) d2 = false;
            }
            return (d1 && slot1 == candidateJob) || (d2 && slot2 == candidateJob);
        }

        /// <summary>True when paying the hire cost leaves funds at or above the gold buffer.</summary>
        public static bool CanAffordHire(int funds, int hireCost, int goldBuffer)
        {
            return funds - hireCost >= goldBuffer;
        }

        /// <summary>Clamps a persisted job value to the supported single-job options (unknown values become None).</summary>
        public static JobType SanitizeJob(JobType job)
        {
            switch (job)
            {
                case JobType.Knight:
                case JobType.Monk:
                case JobType.Mage:
                case JobType.Priest:
                case JobType.Thief:
                case JobType.Archer:
                    return job;
                default:
                    return JobType.None;
            }
        }
    }
}
