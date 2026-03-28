using PitHero;
using System;

namespace RolePlayingFramework.Combat
{
    /// <summary>
    /// Tracks max and recent damage per party to compute expected damage for dynamic HP evaluation.
    /// Plain class (not a Nez Component), owned by HeroComponent.
    /// All party damage feeds one shared tracker (hero + mercs fight the same enemies).
    /// </summary>
    public class DamageTracker
    {
        /// <summary>Global persistent max damage (survives across battles, resets on hero death)</summary>
        public int MaxDamageTaken { get; private set; }

        /// <summary>Per-battle max damage (reset at battle start)</summary>
        public int BattleMaxDamageTaken { get; private set; }

        // Pre-allocated circular buffer for recent damage samples
        private readonly int[] _recentSamples;
        private int _sampleCount;
        private int _sampleIndex;

        /// <summary>
        /// Creates a new DamageTracker with a pre-allocated circular buffer.
        /// </summary>
        public DamageTracker(int sampleSize)
        {
            _recentSamples = new int[sampleSize];
            _sampleCount = 0;
            _sampleIndex = 0;
        }

        /// <summary>
        /// Resets BattleMaxDamageTaken to 0. Call at battle start.
        /// </summary>
        public void OnBattleStart()
        {
            BattleMaxDamageTaken = 0;
        }

        /// <summary>
        /// Records a damage value: updates BattleMaxDamageTaken and pushes to circular buffer.
        /// </summary>
        public void RecordDamage(int damage)
        {
            if (damage <= 0)
                return;

            BattleMaxDamageTaken = Math.Max(BattleMaxDamageTaken, damage);

            _recentSamples[_sampleIndex] = damage;
            _sampleIndex = (_sampleIndex + 1) % _recentSamples.Length;
            if (_sampleCount < _recentSamples.Length)
                _sampleCount++;
        }

        /// <summary>
        /// Call at battle end. Blends MaxDamageTaken with BattleMaxDamageTaken using decay weights.
        /// If BattleMaxDamageTaken > MaxDamageTaken, takes the new value directly.
        /// If MaxDamageTaken == 0, sets MaxDamageTaken directly from BattleMaxDamageTaken.
        /// </summary>
        public void OnBattleEnd()
        {
            if (MaxDamageTaken == 0)
            {
                MaxDamageTaken = BattleMaxDamageTaken;
            }
            else if (BattleMaxDamageTaken > MaxDamageTaken)
            {
                MaxDamageTaken = BattleMaxDamageTaken;
            }
            else
            {
                // Blend: decay old max toward battle max using integer math
                MaxDamageTaken = (int)(GameConfig.DamageTrackerDecayWeight * MaxDamageTaken +
                                       GameConfig.DamageTrackerNewWeight * BattleMaxDamageTaken);
            }
        }

        /// <summary>
        /// Returns expected damage during battle: Max(recentAverage, MaxDamageTaken * 7/10).
        /// Uses integer arithmetic only (AOT safe).
        /// </summary>
        public int GetExpectedDamageInBattle()
        {
            int recentAverage = GetRecentAverage();
            int scaledMax = MaxDamageTaken * 7 / 10;
            return Math.Max(recentAverage, scaledMax);
        }

        /// <summary>
        /// Returns expected damage outside of battle: MaxDamageTaken.
        /// </summary>
        public int GetExpectedDamageOutOfBattle()
        {
            return MaxDamageTaken;
        }

        /// <summary>
        /// Zeroes everything. Call on hero death/respawn.
        /// </summary>
        public void Reset()
        {
            MaxDamageTaken = 0;
            BattleMaxDamageTaken = 0;
            _sampleCount = 0;
            _sampleIndex = 0;
            for (int i = 0; i < _recentSamples.Length; i++)
            {
                _recentSamples[i] = 0;
            }
        }

        /// <summary>
        /// Computes the average of recent damage samples using integer arithmetic.
        /// Returns 0 if no samples recorded.
        /// </summary>
        private int GetRecentAverage()
        {
            if (_sampleCount == 0)
                return 0;

            int sum = 0;
            for (int i = 0; i < _sampleCount; i++)
            {
                sum += _recentSamples[i];
            }
            return sum / _sampleCount;
        }
    }
}
