using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services.AutoJob
{
    /// <summary>
    /// Kitchen demand: a base crew of cook + server + runner whenever any monsters are available
    /// (the kitchen must be open before tickets or party dining can flow, and without a runner the
    /// fridge runs dry), plus extra workers as the order backlog grows, capped at the coordinator's
    /// worker limit. Sticky: kitchen workers are never pulled away by the solver.
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
        public JobDemandEntry EvaluateDemand(int rosterSize)
        {
            int backlog = (_coordinator != null ? _coordinator.ActiveTicketCount : 0)
                + (_mercenaryManager != null ? _mercenaryManager.CountSeatedPatrons() : 0)
                + (_partyDining != null ? _partyDining.CountPendingPartyDiners() : 0);
            return ComputeDemand(backlog, rosterSize);
        }

        /// <summary>Pure demand math: base crew plus one extra worker per backlog block, capped.</summary>
        public static JobDemandEntry ComputeDemand(int backlog, int rosterSize)
        {
            int baseStaff = GameConfig.AutoJobKitchenBaseStaff;
            if (baseStaff > rosterSize)
                baseStaff = rosterSize;

            int desired = baseStaff + backlog / GameConfig.AutoJobKitchenBacklogPerExtraWorker;
            if (desired > GameConfig.AutoJobKitchenMaxWorkers)
                desired = GameConfig.AutoJobKitchenMaxWorkers;

            int min = backlog > 0 ? (baseStaff < desired ? baseStaff : desired) : 0;

            return new JobDemandEntry
            {
                Job = MonsterJob.Cooking,
                MinWorkers = min,
                DesiredWorkers = desired,
                Sticky = true,
            };
        }
    }
}
