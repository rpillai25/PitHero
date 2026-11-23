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

        /// <summary>
        /// Called at the end of a monster's turn, after its attack resolved (digits, deaths, counters): a monster
        /// that leapt in front of its target in <see cref="OnMonsterWindup"/> leaps back. Null when nothing moved.
        /// </summary>
        IEnumerator EndMonsterAttack(IEnemy enemy);

        /// <summary>
        /// Called when a monster's attack hits a party member, before the damage digits: the impact effect on
        /// the target. Presentation only; nothing waits on it.
        /// </summary>
        void ShowMonsterHitOnAlly(IEnemy enemy, IBattleAlly target);

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

        /// <summary>
        /// Plays a physical attack's visual (issue #458): a melee ally (anything but a bow) that stands away
        /// from the front line jumps beside the hero first, then the weapon's effect plays toward the target
        /// (<see cref="RolePlayingFramework.Combat.AttackVisuals.For"/>; null weapon = unarmed). Yielded after
        /// the attack is resolved and before its digits. Return null when there is nothing to show.
        /// </summary>
        IEnumerator ShowPhysicalAttack(IBattleAlly attacker, IEnemy target, RolePlayingFramework.Equipment.IItem weapon);

        /// <summary>
        /// Called after a physical attack's digits (and any death): a mercenary that jumped in for
        /// <see cref="ShowPhysicalAttack"/> jumps back to its spot. Return null when nothing moved.
        /// </summary>
        IEnumerator EndPhysicalAttack(IBattleAlly attacker);

        /// <summary>
        /// Called after an attack skill's digits (and any deaths): a mercenary that jumped beside the hero for
        /// a melee skill in <see cref="ShowSkillEffectOnMonsters"/> jumps back. Return null when nothing moved.
        /// </summary>
        IEnumerator EndSkillEffect(RolePlayingFramework.Combat.ICombatant caster);

        /// <summary>
        /// Called once when a support skill (heal, buff, self-buff) is cast, before its effects are shown.
        /// Starts a cast visual that plays by itself (Defense Up's shield, Vanish's smoke); nothing waits on it.
        /// </summary>
        void ShowSkillCast(RolePlayingFramework.Combat.ICombatant caster, RolePlayingFramework.Skills.ISkill skill, IBattleAlly target);

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

        /// <summary>
        /// Called every time an ally gains threat (damage, skill, heal, evasion).
        /// Live sink forwards to AnalyticsService.LogThreat; virtual sink may aggregate.
        /// </summary>
        void OnThreatGenerated(in BattleThreatEvent evt);

        /// <summary>
        /// Called when the threat target changes (evaluated at each monster turn).
        /// <paramref name="target"/> is null when nobody currently holds threat.
        /// Live sink tints the HUD and prints a console line; virtual sink tracks it for metrics.
        /// </summary>
        void OnThreatTargetChanged(IBattleAlly target);

        /// <summary>
        /// Called when a Knight-flagged ally casts Provoke (ThreatSystem.md), either as an out-of-turn
        /// reaction to a wounded ally or from the player's shortcut queue. Threat itself has already
        /// been logged through <see cref="OnThreatGenerated"/>. Live sink: speech bubble, floating
        /// label, console line, analytics; virtual sink counts it. Return null when nothing to wait for.
        /// </summary>
        IEnumerator OnProvoke(IBattleAlly tank, in BattleProvokeEvent evt);

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

        /// <summary>Requests a sound effect to be played. The source combatant's entity position
        /// attenuates the sound by distance from the camera view; null plays at full volume.</summary>
        void PlaySound(BattleSound sound, RolePlayingFramework.Combat.ICombatant source);
    }
}
