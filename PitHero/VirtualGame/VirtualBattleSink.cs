using PitHero.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Equipment;
using System.Collections;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Headless <see cref="BattleEventSinkBase"/> used during virtual simulation.
    ///
    /// <para>
    /// All display/timing methods return null (no-ops from the base class).
    /// The overridden methods accumulate combat metrics into a
    /// <see cref="VirtualBattleMetrics"/> instance and perform the one structural
    /// side-effect the virtual layer needs: removing defeated monsters from
    /// <see cref="VirtualWorldState"/> so that the GOAP monster-tracking stays consistent.
    /// </para>
    /// </summary>
    public sealed class VirtualBattleSink : BattleEventSinkBase
    {
        private readonly VirtualWorldState _world;
        private VirtualBattleMetrics _current;

        /// <summary>The metrics object for the battle currently in progress.</summary>
        public VirtualBattleMetrics CurrentMetrics => _current;

        /// <summary>
        /// Creates a sink bound to the given world state.
        /// </summary>
        /// <param name="world">World state to remove defeated monsters from.</param>
        public VirtualBattleSink(VirtualWorldState world)
        {
            _world = world;
        }

        // ── Battle lifecycle ──────────────────────────────────────────────────────

        /// <summary>
        /// Starts a new battle, resetting per-battle metrics.
        /// Must be called before <see cref="BattleEngine.Run"/> begins.
        /// </summary>
        /// <param name="pitLevel">Pit level the battle is taking place on.</param>
        /// <param name="isBoss">True when the monster roster contains a boss.</param>
        public void BeginBattle(int pitLevel, bool isBoss)
        {
            _current = new VirtualBattleMetrics
            {
                PitLevel    = pitLevel,
                IsBossBattle = isBoss
            };
        }

        /// <inheritdoc/>
        /// <remarks>Increments the round counter in <see cref="CurrentMetrics"/>.</remarks>
        public override IEnumerator OnRoundStarted()
        {
            if (_current != null) _current.Rounds++;
            return null;
        }

        // ── Analytics / side effects ──────────────────────────────────────────────

        /// <inheritdoc/>
        /// <remarks>
        /// Accumulates damage into <see cref="VirtualBattleMetrics.DamageDealt"/> (ally→monster)
        /// or <see cref="VirtualBattleMetrics.DamageTaken"/> (monster→ally).
        /// </remarks>
        public override void OnAttackResolved(in BattleAttackEvent evt)
        {
            if (_current == null || evt.Damage <= 0) return;

            if (evt.TargetType == "monster")
                _current.DamageDealt += evt.Damage;
            else if (evt.ActorType == "monster")
                _current.DamageTaken += evt.Damage;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Accumulates skill heal into <see cref="VirtualBattleMetrics.HealingDone"/> and
        /// <see cref="VirtualBattleMetrics.HealsCount"/>.
        /// </remarks>
        public override void OnHealApplied(in BattleHealEvent evt)
        {
            if (_current == null || evt.Amount <= 0) return;
            _current.HealingDone += evt.Amount;
            _current.HealsCount++;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Accumulates consumable heal into <see cref="VirtualBattleMetrics.HealingDone"/> and
        /// <see cref="VirtualBattleMetrics.PotionsConsumed"/>.
        /// </remarks>
        public override void OnConsumableHealApplied(Consumable consumable, in BattleHealEvent evt)
        {
            if (_current == null || evt.Amount <= 0) return;
            _current.HealingDone    += evt.Amount;
            _current.PotionsConsumed++;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Increments <see cref="VirtualBattleMetrics.MonstersDefeated"/>, accumulates
        /// XP and gold, and marks a boss victory in the world state when the enemy is a boss.
        /// </remarks>
        public override void OnEnemyDefeated(IEnemy enemy, bool heroKill)
        {
            if (_current == null || enemy == null) return;
            _current.MonstersDefeated++;
            _current.XpEarned   += enemy.ExperienceYield;
            _current.GoldEarned += enemy.GoldYield;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Marks <see cref="VirtualBattleMetrics.HeroDied"/> or increments
        /// <see cref="VirtualBattleMetrics.MercDeaths"/>.
        /// </remarks>
        public override void OnAllyKilled(IBattleAlly ally, IEnemy killer)
        {
            if (_current == null) return;
            if (ally.IsHero)
                _current.HeroDied = true;
            else
                _current.MercDeaths++;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Returns null (no animation), but removes the monster from <see cref="VirtualWorldState"/>
        /// so that GOAP monster-tracking remains consistent with the battle outcome.
        /// This mirrors the timing in <see cref="LiveBattleAdapter.ShowMonsterDeath"/> where
        /// the Nez entity is destroyed immediately after the death fade completes.
        /// </remarks>
        public override IEnumerator ShowMonsterDeath(IEnemy enemy)
        {
            if (enemy != null && _world != null)
                _world.RemoveMonster(enemy);
            return null;
        }
    }
}
