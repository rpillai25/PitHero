using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services.AutoJob
{
    /// <summary>
    /// Kitchen demand: a base crew of cook + server + runner, plus extra workers as the order backlog
    /// grows, capped at the coordinator's worker limit. The base crew is all-or-nothing when competing
    /// with farming: if higher-priority reservations leave fewer than the base crew available, the
    /// kitchen fields no one (a partial kitchen has no runner and the fridge runs dry — and without
    /// farm workers there'd be no ingredients anyway). Sticky: kitchen workers are never pulled away
    /// by the solver except when farming would otherwise have zero workers.
    /// </summary>
    public sealed class KitchenJobDemandEvaluator : IJobDemandEvaluator
    {
        private readonly KitchenTaskCoordinator _coordinator;
        private readonly MercenaryManager _mercenaryManager;
        private readonly PartyDiningService _partyDining;

        /// <summary>All dependencies are optional so the evaluator can run headless in tests.</summary>
        public KitchenJobDemandEvaluator(KitchenTaskCoordinator coordinator,
            MercenaryManager mercenaryManager, PartyDiningService partyDining)
        {
            _coordinator = coordinator;
            _mercenaryManager = mercenaryManager;
            _partyDining = partyDining;
        }

        /// <inheritdoc/>
        public MonsterJob Job => MonsterJob.Cooking;

        /// <inheritdoc/>
        public JobDemandEntry EvaluateDemand(int rosterSize, int availableWorkers)
        {
            int backlog = (_coordinator != null ? _coordinator.ActiveTicketCount : 0)
                + (_mercenaryManager != null ? _mercenaryManager.CountSeatedPatrons() : 0)
                + (_partyDining != null ? _partyDining.CountPendingPartyDiners() : 0);
            return ComputeDemand(backlog, rosterSize, availableWorkers);
        }

        /// <summary>Pure demand math: base crew plus one extra worker per backlog block, capped.</summary>
        public static JobDemandEntry ComputeDemand(int backlog, int rosterSize, int availableWorkers)
        {
            int baseStaff = GameConfig.AutoJobKitchenBaseStaff;

            // All-or-nothing when a higher-priority job (farming) has reserved workers: a kitchen crew
            // below cook+server+runner is nonfunctional, so cede the shortage to farming entirely.
            if (availableWorkers < baseStaff && availableWorkers < rosterSize)
            {
                return new JobDemandEntry
                {
                    Job = MonsterJob.Cooking,
                    MinWorkers = 0,
                    DesiredWorkers = 0,
                    Sticky = true,
                };
            }

            if (baseStaff > availableWorkers)
                baseStaff = availableWorkers;

            int desired = baseStaff + backlog / GameConfig.AutoJobKitchenBacklogPerExtraWorker;
            if (desired > GameConfig.AutoJobKitchenMaxWorkers)
                desired = GameConfig.AutoJobKitchenMaxWorkers;

            // A firm minimum keeps the base crew staffed ahead of farming's desired extras, and doubles
            // as the floor the StarvationReleasePass won't raid below.
            return new JobDemandEntry
            {
                Job = MonsterJob.Cooking,
                MinWorkers = baseStaff,
                DesiredWorkers = desired,
                Sticky = true,
            };
        }
    }
}
