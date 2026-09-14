namespace RolePlayingFramework.AlliedMonsters
{
    /// <summary>
    /// A monster that has joined the party after being defeated in battle. Every job has a skill
    /// level (1–9) that rises by performing that job's tasks (issue #413): reaching the next level
    /// takes <c>current level × the job's tasks-per-level</c> completed tasks
    /// (GameConfig.MonsterJobFarmingTasksPerLevel for farming, MonsterJobTasksPerLevel otherwise).
    /// </summary>
    public sealed class AlliedMonster
    {
        /// <summary>Random first name assigned upon joining.</summary>
        public string Name { get; }

        /// <summary>The original monster type name (e.g. "Slime").</summary>
        public string MonsterTypeName { get; }

        private int _fishingLevel;
        private int _cookingLevel;
        private int _farmingLevel;
        private int _fishingTasks;
        private int _cookingTasks;
        private int _farmingTasks;

        /// <summary>Fishing skill level, 1–9.</summary>
        public int FishingProficiency => _fishingLevel;

        /// <summary>Cooking skill level, 1–9.</summary>
        public int CookingProficiency => _cookingLevel;

        /// <summary>Farming skill level, 1–9.</summary>
        public int FarmingProficiency => _farmingLevel;

        /// <summary>Fishing tasks completed toward the next fishing level.</summary>
        public int FishingTasks => _fishingTasks;

        /// <summary>Kitchen tasks completed toward the next cooking level.</summary>
        public int CookingTasks => _cookingTasks;

        /// <summary>Farm tasks completed toward the next farming level.</summary>
        public int FarmingTasks => _farmingTasks;

        /// <summary>Current job assignment for this monster.</summary>
        public MonsterJob Job { get; set; } = MonsterJob.None;

        /// <summary>UniqueId of the monster house this monster is assigned to (-1 if unassigned).</summary>
        public int MonsterHouseId { get; set; } = -1;

        /// <summary>Creates a new AlliedMonster with the given name, type, and job skill levels.</summary>
        public AlliedMonster(string name, string monsterTypeName, int fishing, int cooking, int farming,
            int monsterHouseId = -1)
        {
            Name = name;
            MonsterTypeName = monsterTypeName;
            _fishingLevel = ClampLevel(fishing);
            _cookingLevel = ClampLevel(cooking);
            _farmingLevel = ClampLevel(farming);
            MonsterHouseId = monsterHouseId;
        }

        /// <summary>Clamps a job skill level into the supported 1–9 range.</summary>
        public static int ClampLevel(int level)
        {
            return System.Math.Clamp(level, PitHero.GameConfig.MonsterJobLevelMin, PitHero.GameConfig.MonsterJobLevelMax);
        }

        /// <summary>Tasks per level for a job: farming needs more because its tasks are far more frequent.</summary>
        public static int TasksPerLevel(MonsterJob job)
        {
            return job == MonsterJob.Farming
                ? PitHero.GameConfig.MonsterJobFarmingTasksPerLevel
                : PitHero.GameConfig.MonsterJobTasksPerLevel;
        }

        /// <summary>Tasks that must be completed at <paramref name="level"/> of <paramref name="job"/> to reach the next level.</summary>
        public static int TasksRequiredForLevel(MonsterJob job, int level)
        {
            return level * TasksPerLevel(job);
        }

        /// <summary>Skill level for the given job (0 for None).</summary>
        public int GetLevel(MonsterJob job)
        {
            switch (job)
            {
                case MonsterJob.Farming: return _farmingLevel;
                case MonsterJob.Cooking: return _cookingLevel;
                case MonsterJob.Fishing: return _fishingLevel;
                default: return 0;
            }
        }

        /// <summary>Tasks completed toward the next level of the given job (0 for None).</summary>
        public int GetTasks(MonsterJob job)
        {
            switch (job)
            {
                case MonsterJob.Farming: return _farmingTasks;
                case MonsterJob.Cooking: return _cookingTasks;
                case MonsterJob.Fishing: return _fishingTasks;
                default: return 0;
            }
        }

        /// <summary>Tasks required to reach the next level of the given job at its current level.</summary>
        public int GetTasksRequired(MonsterJob job)
        {
            return TasksRequiredForLevel(job, GetLevel(job));
        }

        /// <summary>True when the job is at the maximum level and can no longer progress.</summary>
        public bool IsMaxLevel(MonsterJob job)
        {
            return GetLevel(job) >= PitHero.GameConfig.MonsterJobLevelMax;
        }

        /// <summary>
        /// Records one completed task for the job. Returns true when this task raised the job's level.
        /// Progress stops accumulating at the maximum level.
        /// </summary>
        public bool RecordTask(MonsterJob job)
        {
            switch (job)
            {
                case MonsterJob.Farming: return Advance(job, ref _farmingLevel, ref _farmingTasks);
                case MonsterJob.Cooking: return Advance(job, ref _cookingLevel, ref _cookingTasks);
                case MonsterJob.Fishing: return Advance(job, ref _fishingLevel, ref _fishingTasks);
                default: return false;
            }
        }

        private static bool Advance(MonsterJob job, ref int level, ref int tasks)
        {
            if (level >= PitHero.GameConfig.MonsterJobLevelMax)
            {
                tasks = 0;
                return false;
            }
            tasks++;
            int required = TasksRequiredForLevel(job, level);
            if (tasks < required)
                return false;
            tasks -= required;
            level++;
            if (level >= PitHero.GameConfig.MonsterJobLevelMax)
                tasks = 0;
            return true;
        }

        /// <summary>Restores task progress from a save (values are clamped to be non-negative).</summary>
        public void SetTaskProgress(int fishingTasks, int cookingTasks, int farmingTasks)
        {
            _fishingTasks = fishingTasks < 0 ? 0 : fishingTasks;
            _cookingTasks = cookingTasks < 0 ? 0 : cookingTasks;
            _farmingTasks = farmingTasks < 0 ? 0 : farmingTasks;
        }
    }
}
