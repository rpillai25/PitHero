using System.Collections.Generic;
using System.IO;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Aggregated statistics for a single pit-level traversal, plus run-level summary
    /// fields.  Multiple instances are produced by successive <c>RunPitLevel</c> calls and
    /// can be accumulated into a cross-level report via <see cref="WriteCsv"/>.
    /// </summary>
    public sealed class VirtualRunMetrics
    {
        // ── Per-level aggregate fields ─────────────────────────────────────────────

        /// <summary>Pit level this metrics object covers.</summary>
        public int PitLevel;

        /// <summary>Total battles fought on this pit level.</summary>
        public int BattleCount;

        /// <summary>Total rounds resolved across all battles on this level.</summary>
        public int TotalRounds;

        /// <summary>Total ally-to-monster damage across all battles on this level.</summary>
        public int DamageDealt;

        /// <summary>Total monster-to-ally damage across all battles on this level.</summary>
        public int DamageTaken;

        /// <summary>
        /// HP-loss percentage: <see cref="DamageTaken"/> divided by the party maximum-HP pool
        /// at the start of the level.  Computed externally once the pool is known.
        /// </summary>
        public float HpLossPercent;

        /// <summary>Total HP restored via heals and consumables on this level.</summary>
        public int HealingConsumed;

        /// <summary>Total ally deaths (hero + mercs) across all battles on this level.</summary>
        public int PartyDeaths;

        /// <summary>True when the hero was wiped out before completing the level.</summary>
        public bool Wiped;

        /// <summary>Total treasure chests opened (items collected) on this pit level.</summary>
        public int TreasuresOpened;

        /// <summary>Total gear pieces auto-equipped (hero or mercenaries) on this pit level.</summary>
        public int GearEquipped;

        // ── Run summary fields (populated once per simulation run) ──────────────────

        /// <summary>RNG seed used for this run (placeholder for Phase C).</summary>
        public int RngSeed;

        /// <summary>Name of the hero's job used in this run.</summary>
        public string JobName;

        /// <summary>Lowest pit level traversed in this run.</summary>
        public int LevelRangeMin;

        /// <summary>Highest pit level traversed in this run.</summary>
        public int LevelRangeMax;

        // ── Accumulation ───────────────────────────────────────────────────────────

        /// <summary>
        /// Accumulates a single battle's metrics into this level's aggregate.
        /// </summary>
        public void AccumulateBattle(VirtualBattleMetrics battle)
        {
            if (battle == null) return;
            BattleCount++;
            TotalRounds    += battle.Rounds;
            DamageDealt    += battle.DamageDealt;
            DamageTaken    += battle.DamageTaken;
            HealingConsumed += battle.HealingDone;
            PartyDeaths    += battle.MercDeaths;
            if (battle.HeroDied) PartyDeaths++;
        }

        // ── CSV output ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes a single CSV header row to <paramref name="writer"/>.
        /// Call once before writing rows via <see cref="WriteRow"/>.
        /// </summary>
        public static void WriteCsvHeader(TextWriter writer)
        {
            writer.WriteLine("pitLevel,battles,rounds,dmgDealt,dmgTaken,hpLossPct,healing,deaths,wiped,treasures,gearEquipped");
        }

        /// <summary>
        /// Writes one CSV data row for this metrics object to <paramref name="writer"/>.
        /// </summary>
        public void WriteRow(TextWriter writer)
        {
            writer.WriteLine(
                $"{PitLevel},{BattleCount},{TotalRounds},{DamageDealt},{DamageTaken}," +
                $"{HpLossPercent:F4},{HealingConsumed},{PartyDeaths},{(Wiped ? 1 : 0)}," +
                $"{TreasuresOpened},{GearEquipped}");
        }

        /// <summary>
        /// Convenience overload: writes a header row followed by a single data row.
        /// Suitable for single-level reports and unit tests.
        /// </summary>
        public void WriteCsv(TextWriter writer)
        {
            WriteCsvHeader(writer);
            WriteRow(writer);
        }
    }
}
