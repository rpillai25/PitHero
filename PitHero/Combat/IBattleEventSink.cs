using PitHero.AI;
using RolePlayingFramework.Enemies;
using System.Collections;
using System.Collections.Generic;

namespace PitHero.Combat
{
    /// <summary>
    /// Callback interface that separates display, timing, and side-effect concerns
    /// from the pure battle-round logic inside <see cref="BattleEngine"/>.
    ///
    /// Display/timing methods return <see cref="IEnumerator"/>. The engine yields
    /// the returned value if it is non-null, and skips the yield when null is returned.
    /// This allows the headless sink to return null everywhere (instant execution)
    /// while the live sink returns real coroutines for animation and pacing.
    /// </summary>
    public interface IBattleEventSink
    {
        // ── Pacing / pause ────────────────────────────────────────────────────────

        /// <summary>Returns an enumerator that yields until the game is unpaused, or null.</summary>
        IEnumerator WaitWhilePaused();

        /// <summary>Returns an enumerator for the inter-turn delay (GameConfig.BattleTurnWait), or null.</summary>
        IEnumerator TurnDelay();

        /// <summary>Returns an enumerator for the digit-bounce delay (GameConfig.BattleDigitBounceWait), or null.</summary>
        IEnumerator DigitBounceDelay();

        // ── Battle lifecycle ──────────────────────────────────────────────────────

        /// <summary>Called once when the battle begins (before the first round).</summary>
        IEnumerator OnBattleStarted();

        /// <summary>Called at the start of each round (after turn values are calculated).</summary>
        IEnumerator OnRoundStarted();

        /// <summary>
        /// Called at the start of each round so the sink can scan for late-arriving
        /// mercenaries and add them to <paramref name="currentAllies"/> as new
        /// <see cref="IBattleAlly"/> entries.  The engine will pick up any additions
        /// before sorting participants for the round.
        /// </summary>
        void RecruitLateArrivingAllies(List<IBattleAlly> currentAllies);

        /// <summary>Called when an ally participant's turn begins (before action execution).</summary>
        IEnumerator OnTurnStarted(IBattleAlly ally);

        /// <summary>Called when a monster participant's turn begins (turn indicator with enemy styling).</summary>
        IEnumerator OnMonsterTurnStarted(IEnemy enemy);

        /// <summary>
        /// Called when a monster winds up an attack (before resolution):
        /// facing direction and attack animation belong here.
        /// </summary>
        IEnumerator OnMonsterWindup(IEnemy enemy, IBattleAlly target);

        // ── Visual feedback ───────────────────────────────────────────────────────

        /// <summary>Shows a damage number over a monster and waits for the animation.</summary>
        IEnumerator ShowDamageOnMonster(IEnemy enemy, int damage);

        /// <summary>
        /// Shows a damage number over a monster WITHOUT waiting (used by the multi-target
        /// skill path, which enables all digits first and then waits once).
        /// </summary>
        void ShowSkillDamageOnMonster(IEnemy enemy, int damage);

        /// <summary>Shows a damage number over an ally and waits for the animation.</summary>
        IEnumerator ShowDamageOnAlly(IBattleAlly ally, int damage);

        /// <summary>Shows a "Miss" label over a monster.</summary>
        IEnumerator ShowMissOnMonster(IEnemy enemy);

        /// <summary>Shows a "Miss" label over an ally.</summary>
        IEnumerator ShowMissOnAlly(IBattleAlly ally);

        /// <summary>Shows a "Deflect" label over an ally.</summary>
        IEnumerator ShowDeflectOnAlly(IBattleAlly ally);

        /// <summary>
        /// Plays a cast visual for an attack skill — a projectile from caster to the primary
        /// target (mage Fire) or an area effect over the enemy group (mage Firestorm).
        /// Yielded by the engine BEFORE damage digits appear so the impact lands first.
        /// Return null when the skill has no visual.
        /// </summary>
        IEnumerator ShowSkillEffectOnMonsters(RolePlayingFramework.Combat.ICombatant caster,
            RolePlayingFramework.Skills.ISkill skill, IEnemy primaryTarget, List<IEnemy> surroundingTargets);

        /// <summary>Shows a "Crit" label over a monster.</summary>
        IEnumerator ShowCritOnMonster(IEnemy enemy);

        /// <summary>Shows a heal number over an ally (skill/spell heals).</summary>
        IEnumerator ShowHealOnAlly(IBattleAlly ally, int amount);

        /// <summary>Shows a heal number over an ally for a consumable heal; the consumable
        /// lets the live sink pick a potion-specific visual (e.g. denser particles for
        /// stronger potions).</summary>
        IEnumerator ShowItemHealOnAlly(IBattleAlly ally, int amount, RolePlayingFramework.Equipment.Consumable consumable);

        /// <summary>Shows a buff label (e.g. "DEF+1") over an ally.</summary>
        IEnumerator ShowBuffOnAlly(IBattleAlly ally, string label);

        /// <summary>Plays a monster-death animation and destroys the entity.</summary>
        IEnumerator ShowMonsterDeath(IEnemy enemy);

        /// <summary>Plays an ally-death animation.</summary>
        IEnumerator ShowAllyDeath(IBattleAlly ally, IEnemy killer);

        // ── Analytics / side effects ─────────────────────────────────────────────

        /// <summary>
        /// Called after every attack is resolved.
        /// Live sink forwards to AnalyticsService.LogAttack.
        /// Virtual sink accumulates per-battle metrics.
        /// </summary>
        void OnAttackResolved(in BattleAttackEvent evt);

        /// <summary>
        /// Called after every heal is applied.
        /// Live sink forwards to AnalyticsService.LogHeal.
        /// Virtual sink accumulates per-battle metrics.
        /// </summary>
        void OnHealApplied(in BattleHealEvent evt);

        /// <summary>
        /// Called after every battle buff is applied (one call per granted buff;
        /// at-cap-skipped buffs fire nothing).
        /// Live sink forwards to AnalyticsService.LogBuff.
        /// Virtual sink accumulates per-battle metrics.
        /// </summary>
        void OnBuffApplied(in BattleBuffEvent evt);

        /// <summary>Called after a consumable item is used from the bag.</summary>
        void OnItemConsumed();

        /// <summary>
        /// Called when a consumable restores HP.
        /// Live sink emits the ConsoleBattleHealConsumable line (item name in rarity
        /// colour) and forwards to AnalyticsService.LogHeal; virtual sink aggregates.
        /// </summary>
        void OnConsumableHealApplied(RolePlayingFramework.Equipment.Consumable consumable, in BattleHealEvent evt);

        /// <summary>Shows a mercenary's chosen action in their action-queue visualization.</summary>
        void OnMercenaryActionShown(IBattleAlly merc, QueuedAction action);

        /// <summary>Turns an ally's sprite to face the given enemy.</summary>
        void FaceAllyToward(IBattleAlly ally, IEnemy target);

        /// <summary>
        /// Called after an enemy is defeated and pure reward math (XP/JP/SP) is applied.
        /// Sink handles: gold, InnExhausted reset, DefeatedMonsterService, AlliedMonsterManager,
        /// boss-orb tint, AnalyticsService.LogMonsterDefeated.
        /// </summary>
        void OnEnemyDefeated(IEnemy enemy, bool heroKill);

        /// <summary>
        /// Called when an ally is killed.
        /// Sink handles: death animation start, analytics LogCharacterKilled,
        /// vault transfer + follower reassignment for mercenaries.
        /// </summary>
        void OnAllyKilled(IBattleAlly ally, IEnemy killer);

        // ── Audio ─────────────────────────────────────────────────────────────────

        /// <summary>Requests a sound effect to be played.</summary>
        void PlaySound(BattleSound sound);
    }
}
