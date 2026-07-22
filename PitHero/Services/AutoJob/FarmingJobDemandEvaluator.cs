using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services.AutoJob
{
    /// <summary>
    /// Farming demand: the larger of a burst signal (outstanding till/plant/water/harvest tasks — spikes
    /// during watering and harvest waves) and a baseline signal (crops + plans under care — keeps a
    /// skeleton crew during quiet growth periods).
    /// </summary>
    public sealed class FarmingJobDemandEvaluator : IJobDemandEvaluator
    {
        private readonly FarmTaskCoordinator _coordinator;
        private readonly CropGrowthService _cropGrowth;
        private readonly CropPlantingService _cropPlanting;

        /// <summary>All dependencies are optional so the evaluator can run headless in tests.</summary>
        public FarmingJobDemandEvaluator(FarmTaskCoordinator coordinator,
            CropGrowthService cropGrowth, CropPlantingService cropPlanting)
        {
            _coordinator = coordinator;
            _cropGrowth = cropGrowth;
            _cropPlanting = cropPlanting;
        }

        /// <inheritdoc/>
        public MonsterJob Job => MonsterJob.Farming;

        /// <inheritdoc/>
        public JobDemandEntry EvaluateDemand(int rosterSize)
        {
            int outstanding = _coordinator != null ? _coordinator.OutstandingTaskCount : 0;
            int careLoad = (_cropGrowth != null ? _cropGrowth.CropCount : 0)
                + (_cropPlanting != null ? _cropPlanting.PlanCount : 0);
            return ComputeDemand(outstanding, careLoad, rosterSize);
        }

        /// <summary>Pure demand math: max of the burst and baseline worker counts, clamped to the roster.</summary>
        public static JobDemandEntry ComputeDemand(int outstandingTasks, int careLoad, int rosterSize)
        {
            int burst = CeilDiv(outstandingTasks, GameConfig.AutoJobFarmTasksPerWorker);
            int baseline = CeilDiv(careLoad, GameConfig.AutoJobFarmCropsPerWorkerBaseline);
            int desired = burst > baseline ? burst : baseline;
            if (desired > rosterSize)
                desired = rosterSize;

            return new JobDemandEntry
            {
                Job = MonsterJob.Farming,
                MinWorkers = desired > 0 ? 1 : 0,
                DesiredWorkers = desired,
                Sticky = false,
            };
        }

        private static int CeilDiv(int value, int per) => (value + per - 1) / per;
    }
}
