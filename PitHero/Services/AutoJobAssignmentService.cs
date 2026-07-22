using System.Collections.Generic;
using Nez;
using PitHero.Config;
using PitHero.Services.AutoJob;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services
{
    /// <summary>
    /// Automates allied-monster job assignment (issue #321). When enabled, reassesses per-job worker
    /// demand every GameConfig.AutoJobReassessIntervalSeconds of in-game time (60 in-game minutes)
    /// and reassigns monsters via JobAssignmentSolver. Day and nocturnal monsters are disjoint
    /// workforces (MonsterScheduleConfig: 6AM–10PM vs 10PM–6AM), so each shift is solved separately
    /// and every job gets both a day crew and a night crew. The coordinators reconcile worker
    /// entities off AlliedMonster.Job every frame, so writing the job field is the entire
    /// assignment action.
    /// </summary>
    public class AutoJobAssignmentService
    {
        private readonly AlliedMonsterManager _alliedMonsters;
        private readonly List<IJobDemandEvaluator> _evaluators = new List<IJobDemandEvaluator>(4);

        // Scratch lists reused across reassessments (roster is capped well below 64).
        private readonly List<MonsterJobSnapshot> _snapshots = new List<MonsterJobSnapshot>(64);
        private readonly List<JobDemandEntry> _demands = new List<JobDemandEntry>(4);
        private readonly List<MonsterJob> _resultJobs = new List<MonsterJob>(64);

        private float _lastAssessSeconds = -1f;

        /// <summary>Whether automatic job assignment is active.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Initialises the service with the roster and the initial demand evaluators, in priority order
        /// (kitchen first: its small sticky crew is staffed before farming absorbs the rest).
        /// </summary>
        public AutoJobAssignmentService(AlliedMonsterManager alliedMonsters,
            KitchenJobDemandEvaluator kitchenEvaluator, FarmingJobDemandEvaluator farmingEvaluator)
        {
            _alliedMonsters = alliedMonsters;
            if (kitchenEvaluator != null)
                _evaluators.Add(kitchenEvaluator);
            if (farmingEvaluator != null)
                _evaluators.Add(farmingEvaluator);
        }

        /// <summary>Registers an additional demand evaluator (future jobs, e.g. fishing).</summary>
        public void AddEvaluator(IJobDemandEvaluator evaluator)
        {
            if (evaluator != null)
                _evaluators.Add(evaluator);
        }

        /// <summary>
        /// Advances the reassessment cadence. Called once per game frame while the game is unpaused;
        /// keyed to InGameTimeService so pausing never advances the timer.
        /// </summary>
        public void Update()
        {
            if (!Enabled)
                return;

            var time = Core.Instance != null ? Core.Services.GetService<InGameTimeService>() : null;
            if (time == null)
                return;

            float now = time.AccumulatedSeconds;
            if (_lastAssessSeconds < 0f || now < _lastAssessSeconds)
            {
                // First tick after enable/load, or time rewound by a load — restart the interval.
                _lastAssessSeconds = now;
                return;
            }
            if (now - _lastAssessSeconds < GameConfig.AutoJobReassessIntervalSeconds)
                return;

            _lastAssessSeconds = now;
            ReassessNow();
        }

        /// <summary>
        /// Runs one demand evaluation + solve + apply pass immediately, bypassing the cadence gate.
        /// Called when the player first enables automation so assignments take effect at once.
        /// Solves the day shift and the night shift independently — the two groups never work at
        /// the same time, so each must staff every job on its own.
        /// </summary>
        public void ReassessNow()
        {
            if (_alliedMonsters == null)
                return;

            ReassessShift(nocturnal: false);
            ReassessShift(nocturnal: true);
        }

        /// <summary>Evaluates demand for one shift's roster and applies the solver's assignments to it.</summary>
        private void ReassessShift(bool nocturnal)
        {
            var roster = _alliedMonsters.AlliedMonsters;

            _snapshots.Clear();
            for (int i = 0; i < roster.Count; i++)
            {
                var m = roster[i];
                if (MonsterScheduleConfig.IsNocturnal(m.MonsterTypeName) != nocturnal)
                    continue;
                _snapshots.Add(new MonsterJobSnapshot
                {
                    RosterIndex = i,
                    CurrentJob = m.Job,
                    FarmingProficiency = m.FarmingProficiency,
                    CookingProficiency = m.CookingProficiency,
                    FishingProficiency = m.FishingProficiency,
                });
            }
            if (_snapshots.Count == 0)
                return;

            _demands.Clear();
            for (int i = 0; i < _evaluators.Count; i++)
                _demands.Add(_evaluators[i].EvaluateDemand(_snapshots.Count));

            JobAssignmentSolver.Solve(_snapshots, _demands, _resultJobs);

            for (int i = 0; i < _snapshots.Count; i++)
            {
                var monster = roster[_snapshots[i].RosterIndex];
                if (monster.Job != _resultJobs[i])
                    monster.Job = _resultJobs[i];
            }
        }
    }
}
