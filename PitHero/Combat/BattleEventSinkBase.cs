using PitHero.AI;
using RolePlayingFramework.Enemies;
using System.Collections;
using System.Collections.Generic;

namespace PitHero.Combat
{
    /// <summary>
    /// Abstract base that implements every <see cref="IBattleEventSink"/> method as a
    /// no-op or null return.  Subclasses override only the methods they care about.
    /// </summary>
    public abstract class BattleEventSinkBase : IBattleEventSink
    {
        /// <inheritdoc/>
        public virtual IEnumerator WaitWhilePaused() { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator TurnDelay() { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator DigitBounceDelay() { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator OnBattleStarted() { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator OnRoundStarted() { return null; }

        /// <inheritdoc/>
        public virtual void RecruitLateArrivingAllies(List<IBattleAlly> currentAllies) { }

        /// <inheritdoc/>
        public virtual IEnumerator OnTurnStarted(IBattleAlly ally) { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator OnMonsterTurnStarted(IEnemy enemy) { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator OnMonsterWindup(IEnemy enemy, IBattleAlly target) { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator ShowDamageOnMonster(IEnemy enemy, int damage) { return null; }

        /// <inheritdoc/>
        public virtual void ShowSkillDamageOnMonster(IEnemy enemy, int damage) { }

        /// <inheritdoc/>
        public virtual IEnumerator ShowDamageOnAlly(IBattleAlly ally, int damage) { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator ShowMissOnMonster(IEnemy enemy) { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator ShowMissOnAlly(IBattleAlly ally) { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator ShowDeflectOnAlly(IBattleAlly ally) { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator ShowCritOnMonster(IEnemy enemy) { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator ShowHealOnAlly(IBattleAlly ally, int amount) { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator ShowItemHealOnAlly(IBattleAlly ally, int amount, RolePlayingFramework.Equipment.Consumable consumable) { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator ShowBuffOnAlly(IBattleAlly ally, string label) { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator ShowMonsterDeath(IEnemy enemy) { return null; }

        /// <inheritdoc/>
        public virtual IEnumerator ShowAllyDeath(IBattleAlly ally, IEnemy killer) { return null; }

        /// <inheritdoc/>
        public virtual void OnAttackResolved(in BattleAttackEvent evt) { }

        /// <inheritdoc/>
        public virtual void OnHealApplied(in BattleHealEvent evt) { }

        /// <inheritdoc/>
        public virtual void OnBuffApplied(in BattleBuffEvent evt) { }

        /// <inheritdoc/>
        public virtual void OnItemConsumed() { }

        /// <inheritdoc/>
        public virtual void OnConsumableHealApplied(RolePlayingFramework.Equipment.Consumable consumable, in BattleHealEvent evt) { }

        /// <inheritdoc/>
        public virtual void OnMercenaryActionShown(IBattleAlly merc, QueuedAction action) { }

        /// <inheritdoc/>
        public virtual void FaceAllyToward(IBattleAlly ally, IEnemy target) { }

        /// <inheritdoc/>
        public virtual void OnEnemyDefeated(IEnemy enemy, bool heroKill) { }

        /// <inheritdoc/>
        public virtual void OnAllyKilled(IBattleAlly ally, IEnemy killer) { }

        /// <inheritdoc/>
        public virtual void PlaySound(BattleSound sound) { }
    }
}
