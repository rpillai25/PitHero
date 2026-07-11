using PitHero.AI;
using PitHero.Combat;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Mercenaries;
using System.Collections.Generic;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Drives headless battles during virtual pit traversal.
    ///
    /// <para>
    /// Owns a <see cref="VirtualBattlePartyView"/>, a <see cref="VirtualBattleSink"/>,
    /// and a persistent <see cref="ActionQueue"/> (mirroring how
    /// <c>HeroComponent.BattleActionQueue</c> persists across live battles).
    /// Call <see cref="RunAdjacentBattle"/> after each hero movement step; the runner
    /// collects all living monsters in the 8 tiles adjacent to the virtual hero, runs a
    /// complete <see cref="BattleEngine"/> battle synchronously via
    /// <see cref="HeadlessCoroutineRunner.RunToCompletion"/>, and returns per-battle
    /// metrics.
    /// </para>
    /// </summary>
    public sealed class VirtualBattleRunner
    {
        private readonly VirtualWorldState       _world;
        private readonly VirtualBattlePartyView  _partyView;
        private readonly VirtualBattleSink       _sink;

        // Persistent across battles (mirrors live HeroComponent.BattleActionQueue)
        private readonly ActionQueue _heroActionQueue = new ActionQueue();

        // Ally wrappers — set once via SetHeroAlly / SetMercenaries
        private VirtualBattleAlly _heroAlly;

        // Merc ally list is mutable: the engine removes dead mercs in-place (mirrors live).
        private readonly List<IBattleAlly> _mercAllyInterfaces = new List<IBattleAlly>(2);

        // Pre-allocated buffer for adjacent-monster lookup (AOT: no per-call alloc)
        private readonly List<IEnemy> _adjacentBuffer = new List<IEnemy>(8);

        // Accumulated metrics for all battles run by this runner instance
        private readonly List<VirtualBattleMetrics> _allBattleMetrics = new List<VirtualBattleMetrics>(32);

        /// <summary>Read-only list of metrics from every battle run through this runner.</summary>
        public IReadOnlyList<VirtualBattleMetrics> AllBattleMetrics => _allBattleMetrics;

        // ── Construction ──────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a runner for the given world state and party view.
        /// </summary>
        public VirtualBattleRunner(VirtualWorldState world, VirtualBattlePartyView partyView)
        {
            _world     = world;
            _partyView = partyView;
            _sink      = new VirtualBattleSink(world);
        }

        // ── Configuration ─────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the hero ally wrapper.  Must be called before <see cref="RunAdjacentBattle"/>.
        /// </summary>
        public void SetHeroAlly(Hero hero)
        {
            _heroAlly = new VirtualBattleAlly(hero, isHero: true);
        }

        /// <summary>
        /// Replaces the mercenary roster with wrappers for the given mercenaries.
        /// Dead mercs removed by the engine during a battle remain removed for subsequent battles.
        /// </summary>
        public void SetMercenaries(IReadOnlyList<Mercenary> mercs)
        {
            _mercAllyInterfaces.Clear();
            for (int i = 0; i < mercs.Count; i++)
                _mercAllyInterfaces.Add(new VirtualBattleAlly(mercs[i], isHero: false));
        }

        // ── State ─────────────────────────────────────────────────────────────────

        /// <summary>True when the hero is still alive (CurrentHP > 0).</summary>
        public bool HeroAlive =>
            _partyView.Hero != null && _partyView.Hero.CurrentHP > 0;

        // ── Running a battle ──────────────────────────────────────────────────────

        /// <summary>
        /// Collects all living monsters in the 8 tiles adjacent to the virtual hero's
        /// current position.  If none are found, returns <c>null</c> immediately.
        /// Otherwise runs a complete headless battle and returns the accumulated
        /// per-battle metrics.
        /// </summary>
        /// <returns>
        /// Per-battle metrics, or <c>null</c> when no adjacent living monsters exist.
        /// </returns>
        public VirtualBattleMetrics RunAdjacentBattle()
        {
            if (_heroAlly == null) return null;

            _adjacentBuffer.Clear();
            _world.GetLivingMonstersAdjacentTo(_world.HeroPosition, _adjacentBuffer);
            if (_adjacentBuffer.Count == 0) return null;

            // Determine if this battle contains a boss
            bool isBoss = false;
            for (int i = 0; i < _adjacentBuffer.Count; i++)
            {
                if (_adjacentBuffer[i].IsBoss) { isBoss = true; break; }
            }

            // Reset burst flags so each battle starts clean
            _partyView.ResetBurstFlags();

            // Inform the sink of the incoming battle
            _sink.BeginBattle(_world.PitLevel, isBoss);

            // Build the monster list for this battle (copy so engine can mutate freely)
            var monsters = new List<IEnemy>(_adjacentBuffer.Count);
            for (int i = 0; i < _adjacentBuffer.Count; i++)
                monsters.Add(_adjacentBuffer[i]);

            // Run the battle synchronously
            var engine = new BattleEngine(_partyView, _sink);
            HeadlessCoroutineRunner.RunToCompletion(
                engine.Run(_heroAlly, _mercAllyInterfaces, monsters, _heroActionQueue));

            var metrics = _sink.CurrentMetrics;
            if (metrics != null) _allBattleMetrics.Add(metrics);
            return metrics;
        }

        /// <summary>
        /// Returns true when any party member (hero or hired mercenary) carries the
        /// TrapSense passive, mirroring the live <c>CheckTrapSenseDisarm</c> check
        /// in <c>TileByTileMover</c>.
        /// </summary>
        public bool PartyHasTrapSense()
        {
            if (_partyView.Hero?.TrapSense == true) return true;
            for (int i = 0; i < _mercAllyInterfaces.Count; i++)
            {
                if (_mercAllyInterfaces[i].Combatant is Mercenary m && m.TrapSense)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Applies trap damage to the hero, clamped so the hero always survives
        /// with at least 1 HP (mirrors <c>TrapComponent.Trigger</c> semantics).
        /// Does nothing when the hero is null or the damage is zero.
        /// </summary>
        /// <param name="rawDamage">Damage from <see cref="VirtualWorldState.TriggerTrap"/>.</param>
        public void ApplyTrapDamageToHero(int rawDamage)
        {
            var hero = _partyView.Hero;
            if (hero == null || rawDamage <= 0) return;
            // Clamp so hero cannot die from a trap (mirrors live TrapComponent.Trigger)
            int clampedDamage = System.Math.Min(rawDamage, hero.CurrentHP - 1);
            if (clampedDamage > 0)
                hero.TakeDamage(clampedDamage);
        }
    }
}
