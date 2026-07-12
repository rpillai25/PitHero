using PitHero.AI;
using PitHero.Combat;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Mercenaries;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual implementation of <see cref="IBattlePartyView"/> wrapping a real
    /// <see cref="Hero"/> instance and a simulation-owned <see cref="ItemBag"/>.
    ///
    /// <para>
    /// Critical-HP and burst-damage math is replicated from
    /// <c>HeroComponent</c> using the same <see cref="GameConfig"/> constants so
    /// the virtual decision engine behaves identically to live play.
    /// </para>
    ///
    /// <para>Defaults match HeroComponent defaults:</para>
    /// <list type="bullet">
    ///   <item><see cref="CurrentBattleTactic"/> = <see cref="BattleTactic.Strategic"/></item>
    ///   <item><see cref="UseConsumablesOnMercenaries"/> = <c>true</c></item>
    ///   <item><see cref="MercenariesCanUseConsumables"/> = <c>true</c></item>
    ///   <item>Heal priority order: Inn → HealingItem → HealingSkill</item>
    /// </list>
    /// </summary>
    public sealed class VirtualBattlePartyView : IBattlePartyView
    {
        private readonly Hero     _hero;
        private readonly ItemBag  _bag;

        // ── Burst-damage tracking (mirrors HeroComponent fields) ──────────────────
        // Hero burst: one bool flag cleared once HP recovers past recovery threshold.
        private bool _heroBurstDamageTriggered;

        // ── Construction ──────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a party view for the given hero and bag.
        /// </summary>
        /// <param name="hero">The real Hero instance driving reward math.</param>
        /// <param name="bag">The simulation's item bag (may be empty for balance runs).</param>
        /// <param name="tactic">Battle tactic the AI uses; defaults to Strategic.</param>
        public VirtualBattlePartyView(Hero hero, ItemBag bag,
            BattleTactic tactic = BattleTactic.Strategic)
        {
            _hero = hero;
            _bag  = bag;
            CurrentBattleTactic = tactic;
        }

        // ── IBattlePartyView ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Hero Hero => _hero;

        /// <inheritdoc/>
        public BattleTactic CurrentBattleTactic { get; set; }

        /// <inheritdoc/>
        public ItemBag Bag => _bag;

        /// <inheritdoc/>
        /// <remarks>Matches HeroComponent defaults: Inn → HealingItem → HealingSkill.</remarks>
        public HeroHealPriority[] GetHealPrioritiesInOrder() =>
            new[] { HeroHealPriority.Inn, HeroHealPriority.HealingItem, HeroHealPriority.HealingSkill };

        /// <inheritdoc/>
        public bool HealingItemExhausted { get; set; }

        /// <inheritdoc/>
        public bool HealingSkillExhausted { get; set; }

        /// <inheritdoc/>
        /// <remarks>Default: true (matches HeroComponent.UseConsumablesOnMercenaries default).</remarks>
        public bool UseConsumablesOnMercenaries { get; set; } = true;

        /// <inheritdoc/>
        /// <remarks>Default: true (matches HeroComponent.MercenariesCanUseConsumables default).</remarks>
        public bool MercenariesCanUseConsumables { get; set; } = true;

        /// <inheritdoc/>
        /// <remarks>
        /// Replicates <c>HeroComponent.IsHeroHPCritical()</c> exactly:
        /// <list type="number">
        ///   <item>HP% below <see cref="GameConfig.HeroCriticalHPPercent"/> (0.40)</item>
        ///   <item>Burst flag set AND HP% below <see cref="GetBurstDamageRecovery()"/></item>
        /// </list>
        /// </remarks>
        public bool IsHeroHPCritical()
        {
            if (_hero == null || _hero.MaxHP <= 0) return false;
            float hpPct = (float)_hero.CurrentHP / _hero.MaxHP;
            if (hpPct < GameConfig.HeroCriticalHPPercent) return true;
            if (_heroBurstDamageTriggered && hpPct < GetBurstDamageRecovery()) return true;
            return false;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Replicates <c>HeroComponent.IsMercenaryHPCritical()</c>:
        /// HP% below <see cref="GameConfig.HeroCriticalHPPercent"/>.
        /// Burst-damage tracking per-merc uses a simplified path (no Entity-based id set);
        /// the plain threshold is sufficient for balance simulation.
        /// </remarks>
        public bool IsMercenaryHPCritical(Mercenary merc)
        {
            if (merc == null || merc.MaxHP <= 0) return false;
            float hpPct = (float)merc.CurrentHP / merc.MaxHP;
            return hpPct < GameConfig.HeroCriticalHPPercent;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Replicates <c>HeroComponent.RegisterHeroBurstDamage()</c>:
        /// sets the burst flag when the hit exceeds <see cref="GetBurstDamageThreshold()"/>
        /// of the hero's max HP.
        /// </remarks>
        public void RegisterHeroBurstDamage(int damage)
        {
            if (_hero == null || _hero.MaxHP <= 0) return;
            float threshold = _hero.MaxHP * GetBurstDamageThreshold();
            if (damage >= threshold)
                _heroBurstDamageTriggered = true;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Simplified: no per-merc id tracking in the virtual layer.
        /// The base critical-HP threshold from <see cref="IsMercenaryHPCritical"/> is
        /// sufficient for balance simulation without Entity ids.
        /// </remarks>
        public void RegisterMercenaryBurstDamage(Mercenary merc, int damage)
        {
            // Simplified virtual implementation — no entity-id-based burst set.
            // The plain GameConfig.HeroCriticalHPPercent check in IsMercenaryHPCritical
            // covers most heal decisions without needing per-merc burst tracking.
        }

        /// <summary>
        /// Resets the hero burst-damage flag between battles.
        /// Called by <see cref="VirtualBattleRunner"/> at the start of each new battle.
        /// </summary>
        public void ResetBurstFlags()
        {
            _heroBurstDamageTriggered = false;
        }

        // ── Private helpers — replicate HeroComponent tactic-aware math ───────────

        /// <summary>
        /// Burst-damage trigger threshold as a fraction of max HP.
        /// Matches <c>HeroComponent.GetBurstDamageThreshold()</c>:
        /// Defensive tactic: 0.15; all others: 0.20.
        /// </summary>
        private float GetBurstDamageThreshold() =>
            CurrentBattleTactic == BattleTactic.Defensive
                ? GameConfig.BurstDamageThresholdPercentDefensive
                : GameConfig.BurstDamageThresholdPercent;

        /// <summary>
        /// HP recovery fraction at which the burst flag clears.
        /// Matches <c>HeroComponent.GetBurstDamageRecovery()</c>:
        /// Defensive tactic: 0.80; all others: 0.60.
        /// </summary>
        private float GetBurstDamageRecovery() =>
            CurrentBattleTactic == BattleTactic.Defensive
                ? GameConfig.BurstDamageRecoveryPercentDefensive
                : GameConfig.BurstDamageRecoveryPercent;
    }
}
