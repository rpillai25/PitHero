using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services.AutoJob
{
    /// <summary>
    /// Kitchen demand: a base crew of cook + server + runner, plus extra workers as backpressure
    /// grows (issue #375) — one per backlog block of tickets/patrons, plus one more when a patron
    /// has been waiting a long time (a long wait means the pipeline is behind even if the raw
    /// counts look modest). Extras scale up instantly with the live backlog and drain down one
    /// worker per drain interval through the backpressure tracker. The base crew is all-or-nothing
    /// when competing with farming: if higher-priority reservations leave fewer than the base crew
    /// available, the kitchen fields no one (a partial kitchen has no runner and the fridge runs
    /// dry — and without farm workers there'd be no ingredients anyway). The kitchen also fields
    /// no one while no dish is coverable from fridge + storage: servers refuse orders they can't
    /// cover, so staff would be dead weight until crops exist (e.g. a new game before any harvest).
    /// Sticky: kitchen workers are not reshuffled arbitrarily between solves — they leave only
    /// through the solver's one-per-solve trim or the starvation release.
    /// </summary>
    public sealed class KitchenJobDemandEvaluator : IJobDemandEvaluator
    {
        private readonly KitchenTaskCoordinator _coordinator;
        private readonly MercenaryManager _mercenaryManager;
        private readonly PartyDiningService _partyDining;
        // Kitchen crews drain on their own slower interval: a worker walking out of a service
        // area mid-rush is far more noticeable than a farmer leaving a field.
        private readonly BackpressureTracker _tracker =
            new BackpressureTracker(GameConfig.AutoJobKitchenScaleDownDrainIntervalSeconds);

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

        private int LiveBacklog =>
            (_coordinator != null ? _coordinator.ActiveTicketCount : 0)
            + (_mercenaryManager != null ? _mercenaryManager.CountSeatedPatrons() : 0)
            + (_partyDining != null ? _partyDining.CountPendingPartyDiners() : 0);

        private bool PatronWaitHigh =>
            _mercenaryManager != null
            && _mercenaryManager.MaxPatronWaitSeconds() >= GameConfig.AutoJobKitchenHighWaitSeconds;

        /// <inheritdoc/>
        public void SamplePressure(float nowSeconds)
        {
            // Integer block division, matching the live math in ComputeDemand, so the tracker's
            // granted extras and the live extras agree for the same backlog.
            int raw = LiveBacklog / GameConfig.AutoJobKitchenBacklogPerExtraWorker;
            if (PatronWaitHigh)
                raw++;
            _tracker.Sample(raw, nowSeconds);
        }

        /// <inheritdoc/>
        public void ResetPressure(float nowSeconds) => _tracker.Reset(nowSeconds);

        /// <inheritdoc/>
        public JobDemandEntry EvaluateDemand(int rosterSize, int availableWorkers, bool nocturnal)
        {
            // Kitchen only operates during the day shift (6 AM–10 PM).  On the nocturnal shift
            // (10 PM–6 AM) workers go home once in-flight dishes are delivered; field no one new.
            if (nocturnal)
            {
                return new JobDemandEntry
                {
                    Job = MonsterJob.Cooking,
                    MinWorkers = 0,
                    DesiredWorkers = 0,
                    Sticky = true,
                };
            }

            bool anyDishOrderable = _coordinator == null || _coordinator.HasAnyOrderableDish();
            return ComputeDemand(LiveBacklog, _tracker.GrantedWorkers, rosterSize, availableWorkers,
                anyDishOrderable, PatronWaitHigh);
        }

        /// <summary>
        /// Pure demand math: base crew plus extra workers, capped. Extras are the larger of the
        /// live signal (instant scale-up: one per backlog block, +1 on a long patron wait) and
        /// the tracker's granted count (slow scale-down memory of the recent rush).
        /// </summary>
        public static JobDemandEntry ComputeDemand(int backlog, int grantedExtras, int rosterSize,
            int availableWorkers, bool anyDishOrderable = true, bool patronWaitHigh = false)
        {
            int baseStaff = GameConfig.AutoJobKitchenBaseStaff;

            // A kitchen that can't cook anything is dead weight: servers only take orders whose
            // recipe is coverable from fridge + storage, so with no coverable dish no ticket can
            // ever be created. Field no one until crops exist (new game, or the larder ran dry).
            if (!anyDishOrderable)
            {
                return new JobDemandEntry
                {
                    Job = MonsterJob.Cooking,
                    MinWorkers = 0,
                    DesiredWorkers = 0,
                    Sticky = true,
                };
            }

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

            int liveExtras = backlog / GameConfig.AutoJobKitchenBacklogPerExtraWorker;
            if (patronWaitHigh)
                liveExtras++;
            int extras = liveExtras > grantedExtras ? liveExtras : grantedExtras;

            int desired = baseStaff + extras;
            if (desired > GameConfig.AutoJobKitchenMaxWorkers)
                desired = GameConfig.AutoJobKitchenMaxWorkers;

            // A firm minimum keeps the base crew staffed ahead of farming's desired extras, and doubles
            // as the floor neither the trim pass nor the StarvationReleasePass drops below.
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
