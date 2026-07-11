using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Per-battle implementation of <see cref="IBattleContext"/>.
    /// Created once at the start of each battle and discarded in the finally block.
    /// All collections are pre-allocated; no heap allocation during battle rounds.
    /// </summary>
    public sealed class BattleContext : IBattleContext
    {
        // ── DoT entries ───────────────────────────────────────────────────────────────

        /// <summary>Internal record of an active DoT effect.</summary>
        public struct DoTEntry
        {
            public IEnemy Target;
            public int DamagePerTurn;
            public int RemainingTurns;
            public string SourceSkillId;
            /// <summary>Name of the combatant who registered this DoT (for analytics).</summary>
            public string ActorName;
            /// <summary>Actor type string for analytics (e.g. "hero" or "merc").</summary>
            public string ActorType;
        }

        /// <summary>Result produced by <see cref="TickDoTs"/> for each DoT tick that fires.</summary>
        public struct DoTTickResult
        {
            public IEnemy Target;
            public int Damage;
            public string SourceSkillId;
            public string ActorName;
            public string ActorType;
            public bool TargetDied;
        }

        private readonly List<DoTEntry> _dots = new List<DoTEntry>(8);
        private readonly List<ICombatant> _actedCombatants = new List<ICombatant>(8);
        private readonly List<DoTTickResult> _tickResults = new List<DoTTickResult>(8);

        // ── IBattleContext ────────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void RegisterDoT(IEnemy target, int damagePerTurn, int turns, string sourceSkillId, ICombatant actor)
        {
            string actorName = actor?.Name ?? string.Empty;
            string actorType = actor is Hero ? "hero" : "merc";
            RegisterDoTInternal(target, damagePerTurn, turns, sourceSkillId, actorName, actorType);
        }

        private void RegisterDoTInternal(IEnemy target, int damagePerTurn, int turns, string sourceSkillId,
            string actorName, string actorType)
        {
            // Refresh existing same-source DoT on same target rather than stacking
            for (int i = 0; i < _dots.Count; i++)
            {
                if (_dots[i].Target == target && _dots[i].SourceSkillId == sourceSkillId)
                {
                    var entry = _dots[i];
                    entry.DamagePerTurn = damagePerTurn;
                    entry.RemainingTurns = turns;
                    if (actorName != string.Empty) entry.ActorName = actorName;
                    if (actorType != string.Empty) entry.ActorType = actorType;
                    _dots[i] = entry;
                    return;
                }
            }
            _dots.Add(new DoTEntry
            {
                Target = target,
                DamagePerTurn = damagePerTurn,
                RemainingTurns = turns,
                SourceSkillId = sourceSkillId,
                ActorName = actorName,
                ActorType = actorType
            });
        }

        /// <inheritdoc/>
        public bool IsFirstOffensiveAction(ICombatant c)
        {
            for (int i = 0; i < _actedCombatants.Count; i++)
            {
                if (_actedCombatants[i] == c) return false;
            }
            return true;
        }

        /// <inheritdoc/>
        public void MarkActed(ICombatant c)
        {
            for (int i = 0; i < _actedCombatants.Count; i++)
            {
                if (_actedCombatants[i] == c) return;
            }
            _actedCombatants.Add(c);
        }

        // ── End-of-round ticking ─────────────────────────────────────────────────────

        /// <summary>
        /// Ticks all active DoT entries: applies damage to each living target, decrements
        /// remaining turns, and removes expired or orphaned entries.
        /// Returns a reused list (valid until the next call to this method) of tick results
        /// for display and analytics.
        /// </summary>
        public List<DoTTickResult> TickDoTs()
        {
            _tickResults.Clear();

            for (int i = _dots.Count - 1; i >= 0; i--)
            {
                var dot = _dots[i];

                // Skip dead targets and remove the entry
                if (dot.Target == null || dot.Target.CurrentHP <= 0)
                {
                    _dots.RemoveAt(i);
                    continue;
                }

                bool died = dot.Target.TakeDamage(dot.DamagePerTurn);
                _tickResults.Add(new DoTTickResult
                {
                    Target = dot.Target,
                    Damage = dot.DamagePerTurn,
                    SourceSkillId = dot.SourceSkillId,
                    ActorName = dot.ActorName,
                    ActorType = dot.ActorType,
                    TargetDied = died
                });

                dot.RemainingTurns--;
                if (dot.RemainingTurns <= 0)
                    _dots.RemoveAt(i);
                else
                    _dots[i] = dot;
            }

            return _tickResults;
        }

        /// <summary>Exposes the raw DoT list for testing purposes only.</summary>
        public List<DoTEntry> GetDots() => _dots;
    }
}
