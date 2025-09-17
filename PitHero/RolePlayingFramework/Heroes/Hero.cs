using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Heroes
{
    /// <summary>Runtime hero instance with equipment and derived stats.</summary>
    public sealed class Hero
    {
        public string Name { get; }
        public IJob Job { get; }
        public int Level { get; private set; }
        public int Experience { get; private set; }
        public StatBlock BaseStats { get; private set; }

        public int MaxHP { get; private set; }
        public int MaxMP { get; private set; }
        public int CurrentHP { get; private set; }
        public int CurrentMP { get; private set; }

        public Hero(string name, IJob job, int level, in StatBlock baseStats)
        {
            Name = name;
            Job = job;
            Level = level < 1 ? 1 : level;
            BaseStats = baseStats;
            RecalculateDerived();
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
        }

        /// <summary>Adds experience and levels up linearly.</summary>
        public bool AddExperience(int amount)
        {
            if (amount <= 0) return false;
            Experience += amount;
            var needed = Level * 100; // simple linear progression
            var leveled = false;
            while (Experience >= needed)
            {
                Experience -= needed;
                Level++;
                leveled = true;
                // Base stats grow linearly with level independent of job
                BaseStats = new StatBlock(
                    BaseStats.Strength + 1,
                    BaseStats.Agility + 1,
                    BaseStats.Vitality + 1,
                    BaseStats.Magic + 1);
                RecalculateDerived();
                needed = Level * 100;
            }
            return leveled;
        }

        /// <summary>Recomputes HP/MP and caps.</summary>
        public void RecalculateDerived()
        {
            var jobStats = Job.GetJobContributionAtLevel(Level);
            var total = BaseStats.Add(jobStats);
            MaxHP = 50 + total.Vitality * 10; // simple linear formula
            MaxMP = 10 + total.Magic * 5;     // simple linear formula
            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
            if (CurrentMP > MaxMP) CurrentMP = MaxMP;
        }

        /// <summary>Returns current total stats (base + job).</summary>
        public StatBlock GetTotalStats()
        {
            var jobStats = Job.GetJobContributionAtLevel(Level);
            return BaseStats.Add(jobStats);
        }

        /// <summary>Inflicts damage, returns true if hero died.</summary>
        public bool TakeDamage(int amount)
        {
            if (amount <= 0) return false;
            CurrentHP -= amount;
            if (CurrentHP < 0) CurrentHP = 0;
            return CurrentHP == 0;
        }

        /// <summary>Consumes MP if available.</summary>
        public bool SpendMP(int amount)
        {
            if (amount <= 0) return true;
            if (CurrentMP < amount) return false;
            CurrentMP -= amount;
            return true;
        }

        /// <summary>Heals HP up to max.</summary>
        public void Heal(int amount)
        {
            if (amount <= 0) return;
            CurrentHP += amount;
            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
        }
    }
}
