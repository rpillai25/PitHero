using PitHero;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using System.Collections.Generic;

namespace PitHero.Combat
{
    /// <summary>
    /// Per-battle threat ledger (see PitHero/docs/ThreatSystem.md).
    /// Tracks how much attention each ally has drawn this battle; the single highest
    /// entry is the "threat target" monsters prefer. Parallel lists, pre-allocated —
    /// no heap allocation during battle rounds. Consumes no RNG.
    /// </summary>
    public sealed class ThreatTable
    {
        private readonly List<ICombatant> _combatants = new List<ICombatant>(4);
        private readonly List<float> _threat = new List<float>(4);
        private readonly List<int> _evasions = new List<int>(4);

        /// <summary>Number of allies with a ledger entry (including zero-threat ones).</summary>
        public int Count => _combatants.Count;

        private int IndexOf(ICombatant c)
        {
            for (int i = 0; i < _combatants.Count; i++)
                if (ReferenceEquals(_combatants[i], c)) return i;
            return -1;
        }

        private int EnsureIndex(ICombatant c)
        {
            int idx = IndexOf(c);
            if (idx >= 0) return idx;
            _combatants.Add(c);
            _threat.Add(0f);
            _evasions.Add(0);
            return _combatants.Count - 1;
        }

        /// <summary>Current threat for a combatant (0 if none recorded).</summary>
        public float Get(ICombatant c)
        {
            int idx = IndexOf(c);
            return idx >= 0 ? _threat[idx] : 0f;
        }

        /// <summary>Evasions recorded for a combatant this battle.</summary>
        public int GetEvasionCount(ICombatant c)
        {
            int idx = IndexOf(c);
            return idx >= 0 ? _evasions[idx] : 0;
        }

        /// <summary>
        /// Adds raw threat (already job-scaled by the caller, or use <see cref="AddScaled"/>).
        /// Returns the new total. Non-positive amounts are ignored.
        /// </summary>
        public float Add(ICombatant c, float amount)
        {
            if (c == null || amount <= 0f) return Get(c);
            int idx = EnsureIndex(c);
            _threat[idx] += amount;
            return _threat[idx];
        }

        /// <summary>Adds threat after applying the combatant's job multiplier. Returns the scaled amount added.</summary>
        public float AddScaled(ICombatant c, float rawAmount, out float newTotal)
        {
            float scaled = rawAmount * JobThreatMultiplier(c);
            newTotal = Add(c, scaled);
            return scaled;
        }

        /// <summary>
        /// Records an evasion: threat gained = ThreatEvasionBase × evasions-so-far (15, 30, 45 …),
        /// then job-scaled. Returns the scaled amount added.
        /// </summary>
        public float RegisterEvasion(ICombatant c, out float newTotal)
        {
            int idx = EnsureIndex(c);
            _evasions[idx]++;
            float raw = GameConfig.ThreatEvasionBase * _evasions[idx];
            return AddScaled(c, raw, out newTotal);
        }

        /// <summary>End-of-round decay: every entry × ThreatDecayPerRound, snapping to 0 below ThreatFloor.</summary>
        public void DecayRound()
        {
            for (int i = 0; i < _threat.Count; i++)
            {
                float t = _threat[i] * GameConfig.ThreatDecayPerRound;
                _threat[i] = t < GameConfig.ThreatFloor ? 0f : t;
            }
        }

        /// <summary>Drops a combatant (e.g. on death) so they can never be the threat target.</summary>
        public void Remove(ICombatant c)
        {
            int idx = IndexOf(c);
            if (idx < 0) return;
            _combatants.RemoveAt(idx);
            _threat.RemoveAt(idx);
            _evasions.RemoveAt(idx);
        }

        /// <summary>Clears the ledger (battle end).</summary>
        public void Clear()
        {
            _combatants.Clear();
            _threat.Clear();
            _evasions.Clear();
        }

        /// <summary>
        /// Returns the candidate with the highest threat (&gt; 0), or null when nobody has threat.
        /// Ties resolve to the earliest candidate (party order: hero first).
        /// </summary>
        public IBattleAlly HighestAmong(List<IBattleAlly> candidates)
        {
            IBattleAlly best = null;
            float bestThreat = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                var ally = candidates[i];
                if (ally?.Combatant == null) continue;
                float t = Get(ally.Combatant);
                if (t > bestThreat)
                {
                    bestThreat = t;
                    best = ally;
                }
            }
            return best;
        }

        /// <summary>Highest threat value among candidates other than <paramref name="exclude"/> (0 if none).</summary>
        public float HighestThreatExcluding(List<IBattleAlly> candidates, ICombatant exclude)
        {
            float best = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i]?.Combatant;
                if (c == null || ReferenceEquals(c, exclude)) continue;
                float t = Get(c);
                if (t > best) best = t;
            }
            return best;
        }

        // ── Static helpers ─────────────────────────────────────────────────────────

        /// <summary>Job-based multiplier on all threat a combatant generates (Knight = tank).</summary>
        public static float JobThreatMultiplier(ICombatant c)
        {
            IJob job = null;
            if (c is Hero hero) job = hero.Job;
            else if (c is Mercenary merc) job = merc.Job;
            if (job != null && (job.JobFlag & JobType.Knight) != 0)
                return GameConfig.ThreatKnightMultiplier;
            return 1f;
        }

        /// <summary>Flat threat for a skill use: explicit ThreatValue, else a target-type default.</summary>
        public static int SkillFlatThreat(ISkill skill)
        {
            if (skill == null) return 0;
            if (skill.ThreatValue >= 0) return skill.ThreatValue;
            bool support = skill.TargetType == SkillTargetType.Self ||
                           skill.TargetType == SkillTargetType.SingleAlly ||
                           skill.TargetType == SkillTargetType.AllAllies;
            return support ? GameConfig.ThreatSkillSupportDefault : GameConfig.ThreatSkillAttackDefault;
        }

        /// <summary>Threat for damage dealt, as percent of the victim's max HP (capped at 100%).</summary>
        public static float DamageThreat(int damage, int victimMaxHP)
        {
            if (damage <= 0 || victimMaxHP <= 0) return 0f;
            float pct = damage * 100f / victimMaxHP;
            if (pct > 100f) pct = 100f;
            return pct * GameConfig.ThreatPerDamagePercent;
        }

        /// <summary>Threat for HP restored, as percent of the recipient's max HP.</summary>
        public static float HealThreat(int healed, int recipientMaxHP)
        {
            if (healed <= 0 || recipientMaxHP <= 0) return 0f;
            return healed * 100f / recipientMaxHP * GameConfig.ThreatPerHealPercent;
        }
    }
}
