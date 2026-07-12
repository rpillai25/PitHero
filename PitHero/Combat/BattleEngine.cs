using Nez;
using PitHero.AI;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using System.Collections;
using System.Collections.Generic;

namespace PitHero.Combat
{
    /// <summary>
    /// Self-contained battle-round engine extracted from AttackMonsterAction.
    /// Operates on <see cref="IBattleAlly"/> and <see cref="IEnemy"/> interfaces rather
    /// than Nez entities, making it usable in both live (coroutine-driven) and headless
    /// (HeadlessCoroutineRunner) execution contexts.
    ///
    /// Display, audio, and Nez side-effects are delegated to <see cref="IBattleEventSink"/>.
    /// Party settings and burst-damage tracking are read from <see cref="IBattlePartyView"/>.
    /// </summary>
    public sealed class BattleEngine
    {
        // DEBUG multiplier applied to monster→ally damage only.  Keep at 1 for production.
        private const int DEBUG_DAMAGE_MULT = 1;

        private readonly IBattlePartyView _partyView;
        private readonly IBattleEventSink _sink;

        // Per-battle state (initialised in Run)
        private IBattleAlly _hero;
        private List<IBattleAlly> _mercenaries;
        private List<IEnemy> _monsters;
        private ActionQueue _heroActionQueue;

        // Pre-allocated temp buffers — reused across rounds (no per-round heap alloc)
        private readonly List<Participant> _participants = new List<Participant>(16);
        private readonly List<IEnemy>      _tempLivingEnemies = new List<IEnemy>(8);
        private readonly List<Mercenary>   _tempMercs         = new List<Mercenary>(4);
        private readonly List<IBattleAlly> _tempLivingAllies  = new List<IBattleAlly>(4);
        private readonly Dictionary<IEnemy, int> _monsterHPBefore = new Dictionary<IEnemy, int>(8);
        private readonly List<IEnemy>      _surroundingTargets = new List<IEnemy>(8);

        /// <summary>Battle outcome; <see cref="BattleOutcome.InProgress"/> until Run completes.</summary>
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.InProgress;

        /// <summary>
        /// Initialises the engine with a party view (settings / burst callbacks) and
        /// an event sink (display / audio / analytics).
        /// </summary>
        public BattleEngine(IBattlePartyView partyView, IBattleEventSink sink)
        {
            _partyView = partyView;
            _sink      = sink;
        }

        // ── Static helpers (testable without a full engine instance) ──────────────

        /// <summary>
        /// Calculates the turn-order value for a combatant with the given agility.
        /// Formula: (RAND(0,255) * (AGI − AGI/4)) / 256.
        /// Uses Nez.Random; seed before calling for deterministic results.
        /// </summary>
        public static float CalculateTurnValue(int agility)
        {
            int randomValue = Random.Range(0, 256); // 0-255 inclusive
            float turn = (randomValue * (agility - agility / 4f)) / 256f;
            return turn;
        }

        /// <summary>
        /// Reorders <paramref name="livingMonsters"/> so that <paramref name="preferred"/>
        /// (if present and alive) is at index 0.  All other entries shift right.
        /// When preferred is null, already at index 0, or not in the list, the list is unchanged.
        /// </summary>
        public static void SelectPrimaryTarget(List<IEnemy> livingMonsters, IEnemy preferred)
        {
            if (preferred == null || livingMonsters.Count == 0) return;
            for (int i = 1; i < livingMonsters.Count; i++)
            {
                if (livingMonsters[i] == preferred)
                {
                    var tmp = livingMonsters[0];
                    livingMonsters[0] = livingMonsters[i];
                    livingMonsters[i] = tmp;
                    return;
                }
            }
            // preferred not found (already [0], died, or not in list) — no change needed
        }

        // ── Main coroutine ───────────────────────────────────────────────────────

        /// <summary>
        /// Runs the full multi-participant battle sequence.
        /// Yield this from a Nez coroutine for live play, or pass to
        /// <see cref="HeadlessCoroutineRunner.RunToCompletion"/> for headless execution.
        /// </summary>
        /// <param name="hero">The hero ally (IBattleAlly wrapping Hero).</param>
        /// <param name="mercenaries">Mutable list of merc allies; late-arrivals may be appended by the sink.</param>
        /// <param name="monsters">Mutable list of initial monsters; entries are removed as they die.</param>
        /// <param name="heroActionQueue">The hero's live action queue (UI queue / AI pre-queue).</param>
        public IEnumerator Run(IBattleAlly hero, List<IBattleAlly> mercenaries,
                               List<IEnemy> monsters, ActionQueue heroActionQueue)
        {
            _hero            = hero;
            _mercenaries     = mercenaries;
            _monsters        = monsters;
            _heroActionQueue = heroActionQueue;

            var battleContext = new BattleContext();

            // Clear any leaked buff state from a previous battle
            hero.Combatant.ClearBattleState();
            for (int i = 0; i < _mercenaries.Count; i++)
                _mercenaries[i].Combatant.ClearBattleState();

            // Build initial participant list: hero + mercs + monsters
            _participants.Clear();
            _participants.Add(new Participant { IsAlly = true, Ally = hero });
            for (int i = 0; i < _mercenaries.Count; i++)
                _participants.Add(new Participant { IsAlly = true, Ally = _mercenaries[i] });
            for (int i = 0; i < _monsters.Count; i++)
                _participants.Add(new Participant { IsAlly = false, Enemy = _monsters[i] });

            var r = _sink.OnBattleStarted();
            if (r != null) yield return r;

            try
            {
                // ── Battle loop ────────────────────────────────────────────────────
                while (HasValidAlliesInPit() && HasLivingMonsters())
                {
                    r = _sink.WaitWhilePaused();
                    if (r != null) yield return r;

                    // Late-arriving allies: sink scans the scene and appends new IBattleAlly
                    // entries to _mercenaries; engine then syncs _participants.
                    _sink.RecruitLateArrivingAllies(_mercenaries);
                    SyncLateArrivingAllies();

                    // Calculate turn values for all participants
                    CalculateAllTurnValues(battleContext);

                    // Sort descending by TurnValue (highest goes first)
                    _participants.Sort(static (a, b) => b.TurnValue.CompareTo(a.TurnValue));

                    r = _sink.OnRoundStarted();
                    if (r != null) yield return r;

                    for (int ti = 0; ti < _participants.Count; ti++)
                    {
                        var p = _participants[ti];

                        // Participants with TurnValue < 0 are dead — skip without a turn wait
                        if (p.TurnValue < 0f) continue;

                        // Skip rules mirror the original loop exactly:
                        // hero — only when outside the pit (a hero that dies mid-round still acts);
                        // merc — dead, missing, or outside the pit;
                        // monster — dead.
                        if (p.IsAlly)
                        {
                            if (p.Ally.IsHero)
                            {
                                if (!p.Ally.IsPresent) continue;
                            }
                            else
                            {
                                if (!p.Ally.IsPresent || p.Ally.Combatant == null || p.Ally.Combatant.CurrentHP <= 0) continue;
                            }
                        }
                        else
                        {
                            if (p.Enemy.CurrentHP <= 0) continue;
                        }

                        r = _sink.WaitWhilePaused();
                        if (r != null) yield return r;

                        // Show turn indicator (allies and monsters, matching the original)
                        if (p.IsAlly)
                            r = _sink.OnTurnStarted(p.Ally);
                        else
                            r = _sink.OnMonsterTurnStarted(p.Enemy);
                        if (r != null) yield return r;

                        // Execute the participant's turn
                        if (p.IsAlly)
                        {
                            if (p.Ally.IsHero)
                                yield return ExecuteHeroTurn(battleContext);
                            else
                                yield return ExecuteMercenaryTurn(p.Ally, battleContext);
                        }
                        else
                        {
                            yield return ExecuteMonsterTurn(p.Enemy, battleContext);
                        }

                        r = _sink.TurnDelay();
                        if (r != null) yield return r;

                        if (!HasValidAlliesInPit() || !HasLivingMonsters())
                        {
                            Debug.Log("[BattleEngine] Battle ending mid-round");
                            break;
                        }
                    }

                    // ── End-of-round ticks: regen, buffs, DoTs ───────────────────
                    if (_hero.Combatant.CurrentHP > 0)
                    {
                        _hero.Combatant.TickRegeneration();
                        _hero.Combatant.TickBuffDurations();
                    }
                    for (int i = 0; i < _mercenaries.Count; i++)
                    {
                        var ally = _mercenaries[i];
                        if (ally.Combatant.CurrentHP > 0)
                        {
                            ally.Combatant.TickRegeneration();
                            ally.Combatant.TickBuffDurations();
                        }
                    }
                    yield return TickDoTsAndHandleDeaths(battleContext);

                    r = _sink.TurnDelay();
                    if (r != null) yield return r;
                }

                // Determine outcome
                if (!HasLivingMonsters())
                    Outcome = BattleOutcome.MonstersCleared;
                else
                    Outcome = BattleOutcome.AlliesWipedOrGone;

                Debug.Log($"[BattleEngine] Battle complete. Outcome: {Outcome}");
            }
            finally
            {
                // Clear all battle buffs so they never leak out of battle
                if (_hero?.Combatant != null) _hero.Combatant.ClearBattleState();
                if (_mercenaries != null)
                {
                    for (int i = 0; i < _mercenaries.Count; i++)
                        _mercenaries[i]?.Combatant?.ClearBattleState();
                }
            }
        }

        // ── Internal participant struct ───────────────────────────────────────────

        private struct Participant
        {
            public bool IsAlly;
            public IBattleAlly Ally;
            public IEnemy Enemy;
            public float TurnValue;
        }

        // ── Round helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns true when at least one ally is present and alive in the pit.
        /// Mirrors HasValidAlliesInPit in the original AttackMonsterAction.
        /// </summary>
        private bool HasValidAlliesInPit()
        {
            if (_hero.IsPresent && _hero.Combatant.CurrentHP > 0) return true;
            for (int i = 0; i < _mercenaries.Count; i++)
            {
                var ally = _mercenaries[i];
                if (ally.IsPresent && ally.Combatant.CurrentHP > 0) return true;
            }
            return false;
        }

        /// <summary>Returns true when at least one monster in <see cref="_monsters"/> is still alive.</summary>
        private bool HasLivingMonsters()
        {
            for (int i = 0; i < _monsters.Count; i++)
            {
                if (_monsters[i].CurrentHP > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Adds any <see cref="IBattleAlly"/> from <see cref="_mercenaries"/> that is not yet
        /// tracked in <see cref="_participants"/> (late arrivals added by the sink).
        /// </summary>
        private void SyncLateArrivingAllies()
        {
            for (int i = 0; i < _mercenaries.Count; i++)
            {
                var ally = _mercenaries[i];
                bool already = false;
                for (int j = 0; j < _participants.Count; j++)
                {
                    if (_participants[j].IsAlly && _participants[j].Ally == ally)
                    {
                        already = true;
                        break;
                    }
                }
                if (!already)
                {
                    _participants.Add(new Participant { IsAlly = true, Ally = ally });
                    Debug.Log($"[BattleEngine] {ally.Combatant.Name} joins the battle!");
                }
            }
        }

        /// <summary>
        /// Calculates turn values for all participants and pre-queues the hero's action
        /// when the queue is empty.
        /// </summary>
        private void CalculateAllTurnValues(BattleContext battleContext)
        {
            // Snapshot living enemy list + full merc roster for the decision engine
            BuildLivingEnemyList();
            BuildMercList();

            for (int i = 0; i < _participants.Count; i++)
            {
                var p = _participants[i];
                if (p.IsAlly)
                {
                    if (p.Ally.Combatant.CurrentHP > 0)
                    {
                        p.TurnValue = CalculateTurnValue(p.Ally.Combatant.GetTotalStats().Agility);
                        if (p.Ally.IsHero)
                            QueueHeroActionForRound();
                    }
                    else
                    {
                        p.TurnValue = -1f;
                    }
                }
                else // monster
                {
                    if (p.Enemy.CurrentHP > 0)
                        p.TurnValue = CalculateTurnValue(p.Enemy.Stats.Agility);
                    else
                        p.TurnValue = -1f;
                }
                _participants[i] = p;
            }
        }

        /// <summary>
        /// Queues the hero's action for this round when the queue is empty.
        /// Mirrors QueueHeroActionForRound from AttackMonsterAction.
        /// </summary>
        private void QueueHeroActionForRound()
        {
            if (_heroActionQueue.HasActions()) return;

            var decision = BattleTacticDecisionEngine.DecideHeroAction(_partyView, _tempLivingEnemies, _tempMercs);
            switch (decision.Kind)
            {
                case BattleAction.ActionKind.UseAttackSkill:
                    _heroActionQueue.EnqueueSkill(decision.Skill);
                    break;
                case BattleAction.ActionKind.UseHealingSkill:
                    _heroActionQueue.EnqueueSkill(decision.Skill, decision.Target, decision.TargetsHero);
                    break;
                case BattleAction.ActionKind.UseConsumable:
                    _heroActionQueue.EnqueueItem(decision.Consumable, decision.BagIndex, decision.Target, decision.TargetsHero);
                    break;
                case BattleAction.ActionKind.PhysicalAttack:
                default:
                    _heroActionQueue.EnqueueAttack(_partyView.Hero.WeaponShield1);
                    break;
            }
        }

        /// <summary>
        /// Re-evaluates the hero's queued offensive action at turn-start.
        /// If healing is now urgently needed (burst damage since round start),
        /// overrides the queued attack with a healing action.
        /// Mirrors ReEvaluateHeroQueuedAction from AttackMonsterAction.
        /// </summary>
        private QueuedAction ReEvaluateHeroQueuedAction(QueuedAction queuedAction)
        {
            bool isQueuedOffensiveAction = queuedAction != null &&
                (queuedAction.ActionType == QueuedActionType.Attack ||
                 (queuedAction.ActionType == QueuedActionType.UseSkill &&
                  queuedAction.Skill != null && queuedAction.Skill.HPRestoreAmount <= 0));

            if (!isQueuedOffensiveAction) return queuedAction;

            var reEvaluatedDecision = BattleTacticDecisionEngine.DecideHeroAction(_partyView, _tempLivingEnemies, _tempMercs);

            if (reEvaluatedDecision.Kind == BattleAction.ActionKind.UseHealingSkill ||
                reEvaluatedDecision.Kind == BattleAction.ActionKind.UseConsumable)
            {
                Debug.Log($"[BattleEngine] Hero re-evaluated: overriding queued offensive action with {reEvaluatedDecision.Kind}");
                switch (reEvaluatedDecision.Kind)
                {
                    case BattleAction.ActionKind.UseHealingSkill:
                    {
                        var newAction = new QueuedAction(reEvaluatedDecision.Skill);
                        newAction.Target = reEvaluatedDecision.Target;
                        newAction.TargetsHero = reEvaluatedDecision.TargetsHero;
                        return newAction;
                    }
                    case BattleAction.ActionKind.UseConsumable:
                    {
                        var newAction = new QueuedAction(reEvaluatedDecision.Consumable, reEvaluatedDecision.BagIndex);
                        newAction.Target = reEvaluatedDecision.Target;
                        newAction.TargetsHero = reEvaluatedDecision.TargetsHero;
                        return newAction;
                    }
                }
            }

            return queuedAction;
        }

        // ── Hero turn ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Executes the hero's turn: dequeues and re-evaluates their queued action,
        /// then dispatches to the appropriate handler.
        /// </summary>
        private IEnumerator ExecuteHeroTurn(BattleContext battleContext)
        {
            var hero = _partyView.Hero;
            if (!_hero.IsPresent) yield break;

            var queuedAction = _heroActionQueue.Dequeue();
            BuildLivingEnemyList();
            BuildMercList();
            queuedAction = ReEvaluateHeroQueuedAction(queuedAction);

            if (queuedAction == null)
            {
                Debug.Warn("[BattleEngine] Hero turn but no queued action (unexpected)");
                yield break;
            }

            Debug.Log($"[BattleEngine] Hero's turn — executing: {queuedAction.ActionType}");

            if (queuedAction.ActionType == QueuedActionType.UseItem)
            {
                var consumeTarget = queuedAction.Target ?? (object)hero;
                yield return ApplyItemAndDisplay(queuedAction.Consumable, queuedAction.BagIndex,
                    consumeTarget, queuedAction.TargetsHero, hero.Name);
            }
            else if (queuedAction.ActionType == QueuedActionType.UseSkill)
            {
                var skill = queuedAction.Skill;
                if (hero.CurrentMP >= hero.GetEffectiveMPCost(skill.MPCost))
                {
                    bool isHealingSkill = skill.HPRestoreAmount > 0 || skill.MPRestoreAmount > 0 ||
                        skill.GrantedBuffs.Count > 0 ||
                        skill.TargetType == SkillTargetType.Self ||
                        skill.TargetType == SkillTargetType.SingleAlly ||
                        skill.TargetType == SkillTargetType.AllAllies;

                    if (isHealingSkill)
                    {
                        // Hero uses SpendMP (applies MPCostReduction)
                        hero.SpendMP(skill.MPCost);
                        var healTarget = queuedAction.Target ?? (object)hero;
                        bool targetIsHero = queuedAction.TargetsHero || healTarget == (object)hero;
                        var targetAlly = FindAllyForTarget(healTarget, targetIsHero);
                        yield return ApplyHealingSkillEffectsAndDisplay(skill, hero, healTarget, targetAlly, hero.Name);
                        Debug.Log($"[BattleEngine] Hero used healing skill {skill.Name}");
                    }
                    else
                    {
                        yield return ExecuteHeroAttackSkill(skill, hero, battleContext);
                    }
                }
                else
                {
                    Debug.Log($"[BattleEngine] Not enough MP to use {skill.Name}");
                }
            }
            else if (queuedAction.ActionType == QueuedActionType.Attack)
            {
                yield return ExecuteHeroPhysicalAttack(queuedAction, hero, battleContext);
            }
        }

        /// <summary>Executes the hero's attack skill via the shared ExecuteCombatantAttackSkill path.</summary>
        private IEnumerator ExecuteHeroAttackSkill(ISkill skill, RolePlayingFramework.Heroes.Hero hero,
            BattleContext battleContext)
        {
            hero.SpendMP(skill.MPCost);
            Debug.Log($"[BattleEngine] Hero used {skill.Name}, consumed {skill.MPCost} MP");
            yield return ExecuteCombatantAttackSkill(skill, hero, "hero", battleContext,
                preferredTarget: null, heroKill: true);
        }

        /// <summary>
        /// Executes the hero's physical attack against a random living monster.
        /// Preserves the exact Nez.Random call sequence from the original.
        /// </summary>
        private IEnumerator ExecuteHeroPhysicalAttack(QueuedAction queuedAction,
            RolePlayingFramework.Heroes.Hero hero, BattleContext battleContext)
        {
            BuildLivingEnemyList();
            if (_tempLivingEnemies.Count == 0) yield break;

            var attackResolver = new EnhancedAttackResolver();
            int idx = Random.Range(0, _tempLivingEnemies.Count);
            var targetEnemy = _tempLivingEnemies[idx];
            var targetBattleStats = BattleStats.CalculateForMonster(targetEnemy);
            var heroBattleStats   = hero.GetBattleStats();

            Debug.Log($"[BattleEngine] Hero attacking {targetEnemy.Name}");
            var heroAttackResult = attackResolver.Resolve(heroBattleStats, targetBattleStats, DamageKind.Physical);

            // Phase 4: Quickdraw — crit roll BEFORE dealing damage
            bool isCrit = BattleReactionHelper.RollFirstAttackCrit(hero,
                battleContext.IsFirstOffensiveAction(hero),
                Random.NextFloat());
            battleContext.MarkActed(hero);

            if (queuedAction.WeaponItem == null)
                _sink.PlaySound(BattleSound.Punch);

            if (heroAttackResult.Hit)
            {
                int heroTargetHpBefore = targetEnemy.CurrentHP;
                int finalDamage = isCrit ? heroAttackResult.Damage * 2 : heroAttackResult.Damage;
                bool enemyDied = targetEnemy.TakeDamage(finalDamage);
                Debug.Log($"[BattleEngine] Hero deals {finalDamage} to {targetEnemy.Name}. HP: {targetEnemy.CurrentHP}/{targetEnemy.MaxHP}");

                string physAction = isCrit ? "physical.crit" : "physical";
                var evt = new BattleAttackEvent(hero.Name, "hero", physAction,
                    targetEnemy.Name, "monster", finalDamage, heroTargetHpBefore, targetEnemy.CurrentHP, enemyDied);
                _sink.OnAttackResolved(in evt);

                if (isCrit)
                {
                    var rc = _sink.ShowCritOnMonster(targetEnemy);
                    if (rc != null) yield return rc;
                }

                var rd = _sink.ShowDamageOnMonster(targetEnemy, finalDamage);
                if (rd != null) yield return rd;

                if (enemyDied)
                {
                    Debug.Log($"[BattleEngine] {targetEnemy.Name} defeated!");
                    AwardEnemyDeathRewards(targetEnemy, heroKill: true);
                    var rm = _sink.ShowMonsterDeath(targetEnemy);
                    if (rm != null) yield return rm;
                    _monsters.Remove(targetEnemy);
                }
            }
            else
            {
                Debug.Log($"[BattleEngine] Hero missed {targetEnemy.Name}!");
                var rm = _sink.ShowMissOnMonster(targetEnemy);
                if (rm != null) yield return rm;
            }
        }

        // ── Mercenary turn ────────────────────────────────────────────────────────

        /// <summary>Executes a mercenary's turn using the decision engine and shared action paths.</summary>
        private IEnumerator ExecuteMercenaryTurn(IBattleAlly mercAlly, BattleContext battleContext)
        {
            if (!mercAlly.IsPresent || mercAlly.Combatant.CurrentHP <= 0) yield break;

            var mercenary = (Mercenary)mercAlly.Combatant;
            BuildLivingEnemyList();
            if (_tempLivingEnemies.Count == 0) yield break;

            BuildMercList();
            var mercDecision = BattleTacticDecisionEngine.DecideMercenaryAction(
                mercenary, _partyView, _tempLivingEnemies, _tempMercs);

            switch (mercDecision.Kind)
            {
                case BattleAction.ActionKind.UseHealingSkill:
                {
                    var healSkill = mercDecision.Skill;
                    if (mercenary.CurrentMP >= mercenary.GetEffectiveMPCost(healSkill.MPCost))
                    {
                        // Merc uses UseMP (which calls SpendMP internally)
                        mercenary.UseMP(healSkill.MPCost);
                        _sink.OnMercenaryActionShown(mercAlly, new QueuedAction(healSkill));
                        var healTarget = mercDecision.Target ?? (object)_partyView.Hero;
                        bool targetIsHero = mercDecision.TargetsHero || healTarget == (object)_partyView.Hero;
                        var targetAlly = FindAllyForTarget(healTarget, targetIsHero);
                        yield return ApplyHealingSkillEffectsAndDisplay(healSkill, mercenary, healTarget,
                            targetAlly, mercenary.Name);
                        Debug.Log($"[BattleEngine] {mercenary.Name} used {healSkill.Name}");
                    }
                    break;
                }

                case BattleAction.ActionKind.UseConsumable:
                {
                    var mcTarget = mercDecision.Target ?? (object)_partyView.Hero;
                    yield return ApplyItemAndDisplay(mercDecision.Consumable, mercDecision.BagIndex,
                        mcTarget, mercDecision.TargetsHero, mercenary.Name);
                    break;
                }

                case BattleAction.ActionKind.UseAttackSkill:
                {
                    var atkSkill = mercDecision.Skill;
                    if (!mercenary.SpendMP(atkSkill.MPCost))
                    {
                        Debug.Log($"[BattleEngine] {mercenary.Name} not enough MP for {atkSkill.Name}");
                        yield break;
                    }

                    _sink.OnMercenaryActionShown(mercAlly, new QueuedAction(atkSkill));

                    // Face the preferred target if alive; otherwise face the first living monster.
                    BuildLivingEnemyList();
                    if (_tempLivingEnemies.Count == 0) yield break;
                    var faceEnemy = _tempLivingEnemies[0];
                    if (mercDecision.TargetEnemy != null)
                    {
                        for (int fi = 0; fi < _tempLivingEnemies.Count; fi++)
                        {
                            if (_tempLivingEnemies[fi] == mercDecision.TargetEnemy)
                            {
                                faceEnemy = mercDecision.TargetEnemy;
                                break;
                            }
                        }
                    }
                    _sink.FaceAllyToward(mercAlly, faceEnemy);

                    yield return ExecuteCombatantAttackSkill(atkSkill, mercenary, "merc", battleContext,
                        preferredTarget: mercDecision.TargetEnemy, heroKill: false);
                    break;
                }

                case BattleAction.ActionKind.PhysicalAttack:
                default:
                {
                    yield return ExecuteMercenaryPhysicalAttack(mercDecision, mercenary, mercAlly, battleContext);
                    break;
                }
            }
        }

        /// <summary>Executes a mercenary's physical attack against a target monster.</summary>
        private IEnumerator ExecuteMercenaryPhysicalAttack(BattleAction decision, Mercenary mercenary,
            IBattleAlly mercAlly, BattleContext battleContext)
        {
            var paTarget = decision.TargetEnemy;
            if (paTarget == null || paTarget.CurrentHP <= 0)
            {
                BuildLivingEnemyList();
                if (_tempLivingEnemies.Count == 0) yield break;
                paTarget = _tempLivingEnemies[Random.Range(0, _tempLivingEnemies.Count)];
            }

            var targetBattleStats = BattleStats.CalculateForMonster(paTarget);
            var mercBattleStats   = BattleStats.CalculateForMercenary(mercenary);

            Debug.Log($"[BattleEngine] {mercenary.Name} attacking {paTarget.Name}");
            _sink.OnMercenaryActionShown(mercAlly, new QueuedAction(mercenary.WeaponShield1));
            _sink.FaceAllyToward(mercAlly, paTarget);

            var attackResolver = new EnhancedAttackResolver();
            var mercAttackResult = attackResolver.Resolve(mercBattleStats, targetBattleStats, DamageKind.Physical);

            // Phase 4: Quickdraw — crit roll BEFORE dealing damage
            bool isCrit = BattleReactionHelper.RollFirstAttackCrit(mercenary,
                battleContext.IsFirstOffensiveAction(mercenary),
                Random.NextFloat());
            battleContext.MarkActed(mercenary);

            _sink.PlaySound(BattleSound.Punch);

            if (mercAttackResult.Hit)
            {
                int mercTargetHpBefore = paTarget.CurrentHP;
                int finalDamage = isCrit ? mercAttackResult.Damage * 2 : mercAttackResult.Damage;
                bool enemyDied = paTarget.TakeDamage(finalDamage);
                Debug.Log($"[BattleEngine] {mercenary.Name} deals {finalDamage} to {paTarget.Name}. HP: {paTarget.CurrentHP}/{paTarget.MaxHP}");

                string mercPhysAction = isCrit ? "physical.crit" : "physical";
                var evt = new BattleAttackEvent(mercenary.Name, "merc", mercPhysAction,
                    paTarget.Name, "monster", finalDamage, mercTargetHpBefore, paTarget.CurrentHP, enemyDied);
                _sink.OnAttackResolved(in evt);

                if (isCrit)
                {
                    var rc = _sink.ShowCritOnMonster(paTarget);
                    if (rc != null) yield return rc;
                }

                var rd = _sink.ShowDamageOnMonster(paTarget, finalDamage);
                if (rd != null) yield return rd;

                if (enemyDied)
                {
                    Debug.Log($"[BattleEngine] {paTarget.Name} defeated by {mercenary.Name}!");
                    // Merc counter-kills: heroKill = false
                    AwardEnemyDeathRewards(paTarget, heroKill: false);
                    var rm = _sink.ShowMonsterDeath(paTarget);
                    if (rm != null) yield return rm;
                    _monsters.Remove(paTarget);
                }
            }
            else
            {
                Debug.Log($"[BattleEngine] {mercenary.Name} missed {paTarget.Name}!");
                var rm = _sink.ShowMissOnMonster(paTarget);
                if (rm != null) yield return rm;
            }
        }

        // ── Shared combatant attack skill ─────────────────────────────────────────

        /// <summary>
        /// Shared coroutine for both hero and mercenary attack skills.
        /// Snapshots HP before Execute, diffs damage, handles crits, deaths, rewards.
        /// Preserves the exact Nez.Random call sequence (crit roll BEFORE Execute,
        /// MarkActed AFTER, second crit pass on crit exactly as original).
        /// </summary>
        private IEnumerator ExecuteCombatantAttackSkill(ISkill skill, ICombatant caster,
            string actorType, BattleContext battleContext,
            IEnemy preferredTarget, bool heroKill)
        {
            BuildLivingEnemyList();
            if (_tempLivingEnemies.Count == 0) yield break;

            // Honor the preferred target: move it to index 0
            SelectPrimaryTarget(_tempLivingEnemies, preferredTarget);

            // Snapshot HP before Execute
            _monsterHPBefore.Clear();
            for (int i = 0; i < _tempLivingEnemies.Count; i++)
            {
                var e = _tempLivingEnemies[i];
                _monsterHPBefore[e] = e.CurrentHP;
            }

            var primaryTarget = _tempLivingEnemies[0];
            _surroundingTargets.Clear();
            for (int i = 1; i < _tempLivingEnemies.Count; i++)
                _surroundingTargets.Add(_tempLivingEnemies[i]);

            // Phase 4: Quickdraw crit roll BEFORE Execute
            bool isCrit = BattleReactionHelper.RollFirstAttackCrit(caster,
                battleContext.IsFirstOffensiveAction(caster),
                Random.NextFloat());

            var attackResolver = new EnhancedAttackResolver();
            skill.Execute(caster, primaryTarget, _surroundingTargets, attackResolver, battleContext);

            // Phase 4: second crit damage pass (doubles damage from first Execute pass)
            if (isCrit)
            {
                for (int ci = 0; ci < _tempLivingEnemies.Count; ci++)
                {
                    var critE = _tempLivingEnemies[ci];
                    if (!_monsterHPBefore.TryGetValue(critE, out int critHpB)) continue;
                    int firstPassDmg = critHpB - critE.CurrentHP;
                    if (firstPassDmg > 0 && critE.CurrentHP > 0)
                        critE.TakeDamage(firstPassDmg);
                }
            }

            // Phase 4: MarkActed AFTER Execute
            battleContext.MarkActed(caster);

            // Display damage and handle deaths for all affected monsters
            bool critTextShown = false;
            for (int i = _tempLivingEnemies.Count - 1; i >= 0; i--)
            {
                var enemy = _tempLivingEnemies[i];
                if (!_monsterHPBefore.TryGetValue(enemy, out int hpBefore)) continue;

                int damage = hpBefore - enemy.CurrentHP;
                if (damage <= 0)
                {
                    // Show "Miss" on primary target only when skill executed but dealt 0 damage
                    if (enemy == primaryTarget && enemy.CurrentHP > 0)
                    {
                        var rm = _sink.ShowMissOnMonster(enemy);
                        if (rm != null) yield return rm;
                    }
                    continue;
                }

                Debug.Log($"[BattleEngine] {skill.Name} dealt {damage} to {enemy.Name}. HP: {enemy.CurrentHP}/{enemy.MaxHP}");

                // Analytics + console (SkillName drives the ConsoleSkillAttack line)
                string analyticsSkillId = isCrit ? (skill.Id + ".crit") : skill.Id;
                var evt = new BattleAttackEvent(caster.Name, actorType, analyticsSkillId,
                    enemy.Name, "monster", damage, hpBefore, enemy.CurrentHP, enemy.CurrentHP <= 0,
                    skill.Name);
                _sink.OnAttackResolved(in evt);

                if (isCrit && !critTextShown)
                {
                    var rc = _sink.ShowCritOnMonster(enemy);
                    if (rc != null) yield return rc;
                    critTextShown = true;
                }

                // Enable the damage digit WITHOUT waiting; a single shared wait follows the loop
                // (matches the original multi-target pacing).
                _sink.ShowSkillDamageOnMonster(enemy, damage);

                if (enemy.CurrentHP <= 0)
                {
                    Debug.Log($"[BattleEngine] {enemy.Name} defeated by {skill.Name}!");
                    AwardEnemyDeathRewards(enemy, heroKill);
                    _monsters.Remove(enemy);
                }
            }

            // One shared digit-bounce wait, then fade all dead monsters (reverse order),
            // exactly as the original skill path did.
            var rb = _sink.DigitBounceDelay();
            if (rb != null) yield return rb;

            for (int i = _tempLivingEnemies.Count - 1; i >= 0; i--)
            {
                var deadEnemy = _tempLivingEnemies[i];
                if (deadEnemy.CurrentHP > 0) continue;
                var rf = _sink.ShowMonsterDeath(deadEnemy);
                if (rf != null) yield return rf;
            }
        }

        // ── Monster turn ──────────────────────────────────────────────────────────

        /// <summary>
        /// Executes a monster's turn: selects a valid ally target (respecting Untargetable)
        /// and dispatches to the unified <see cref="ExecuteMonsterAttackAlly"/> path.
        /// Preserves the exact Untargetable anti-stall guard logic from the original.
        /// </summary>
        private IEnumerator ExecuteMonsterTurn(IEnemy enemy, BattleContext battleContext)
        {
            if (enemy.CurrentHP <= 0) yield break;

            // Build list of valid targets (present + alive)
            _tempLivingAllies.Clear();
            if (_hero.IsPresent && _hero.Combatant.CurrentHP > 0)
                _tempLivingAllies.Add(_hero);
            for (int i = 0; i < _mercenaries.Count; i++)
            {
                var ally = _mercenaries[i];
                if (ally.IsPresent && ally.Combatant.CurrentHP > 0)
                    _tempLivingAllies.Add(ally);
            }

            if (_tempLivingAllies.Count == 0)
            {
                Debug.Log($"[BattleEngine] {enemy.Name} has no valid targets — skipping turn");
                yield break;
            }

            // Phase 4: Vanish — filter out untargetable allies, but only when NOT every ally is untargetable
            // (anti-stall guard: if all are untargetable, the monster can still attack anyone)
            int untargetableCount = 0;
            for (int ui = 0; ui < _tempLivingAllies.Count; ui++)
            {
                if (_tempLivingAllies[ui].Combatant.GetBuffTotal(BuffType.Untargetable) > 0)
                    untargetableCount++;
            }
            if (untargetableCount > 0 && untargetableCount < _tempLivingAllies.Count)
            {
                for (int ui = _tempLivingAllies.Count - 1; ui >= 0; ui--)
                {
                    if (_tempLivingAllies[ui].Combatant.GetBuffTotal(BuffType.Untargetable) > 0)
                    {
                        Debug.Log($"[BattleEngine] {enemy.Name} cannot target {_tempLivingAllies[ui].Combatant.Name} (Untargetable)");
                        _tempLivingAllies.RemoveAt(ui);
                    }
                }
            }

            var targetAlly = _tempLivingAllies[Random.Range(0, _tempLivingAllies.Count)];

            // Sink handles monster facing + attack animation
            var r = _sink.OnMonsterWindup(enemy, targetAlly);
            if (r != null) yield return r;

            yield return ExecuteMonsterAttackAlly(enemy, targetAlly);
        }

        /// <summary>
        /// Unified monster→ally attack (replaces the near-duplicate ExecuteMonsterAttackHero
        /// and ExecuteMonsterAttackMercenary).  Differences (burst registration, death handling,
        /// analytics target type) are parameterised via <see cref="IBattleAlly.IsHero"/>.
        /// </summary>
        private IEnumerator ExecuteMonsterAttackAlly(IEnemy enemy, IBattleAlly targetAlly)
        {
            var target = targetAlly.Combatant;
            var enemyBattleStats = BattleStats.CalculateForMonster(enemy);
            var targetBattleStats = target.GetBattleStats();

            Debug.Log($"[BattleEngine] {enemy.Name} attacking {target.Name}");

            // Phase 3: deflect check — if the target deflects, no hit, no counter
            if (BattleReactionHelper.RollDeflect(target, Random.NextFloat()))
            {
                Debug.Log($"[BattleEngine] {target.Name} deflected {enemy.Name}'s attack!");
                var rd = _sink.ShowDeflectOnAlly(targetAlly);
                if (rd != null) yield return rd;
                yield break;
            }

            var attackResolver = new EnhancedAttackResolver();
            var enemyAttackResult = attackResolver.Resolve(enemyBattleStats, targetBattleStats, enemy.AttackKind);

            if (enemyAttackResult.Hit)
            {
                _sink.PlaySound(BattleSound.TakeDamage);

                int actualDamage = enemyAttackResult.Damage * DEBUG_DAMAGE_MULT;
                int targetHpBefore = target.CurrentHP;
                bool targetDied = target.TakeDamage(actualDamage);
                Debug.Log($"[BattleEngine] {enemy.Name} deals {actualDamage} to {target.Name}. HP: {target.CurrentHP}/{target.MaxHP}");

                string targetType = targetAlly.IsHero ? "hero" : "merc";
                var evt = new BattleAttackEvent(enemy.Name, "monster", "physical",
                    target.Name, targetType, actualDamage, targetHpBefore, target.CurrentHP, targetDied);
                _sink.OnAttackResolved(in evt);

                // Burst damage registration
                if (targetAlly.IsHero)
                    _partyView.RegisterHeroBurstDamage(actualDamage);
                else
                    _partyView.RegisterMercenaryBurstDamage((Mercenary)target, actualDamage);

                var rs = _sink.ShowDamageOnAlly(targetAlly, actualDamage);
                if (rs != null) yield return rs;

                if (targetDied)
                {
                    Debug.Log($"[BattleEngine] {target.Name} died in battle.");
                    _sink.OnAllyKilled(targetAlly, enemy);

                    // Dead mercenaries leave the roster (original: validMercenaries.Remove on death)
                    // so they stop receiving kill XP and are no longer decision candidates.
                    if (!targetAlly.IsHero)
                        _mercenaries.Remove(targetAlly);

                    var rsd = _sink.ShowAllyDeath(targetAlly, enemy);
                    if (rsd != null) yield return rsd;
                }
                else if (BattleReactionHelper.ShouldCounter(target))
                {
                    // Phase 3: counter-attack
                    Debug.Log($"[BattleEngine] {target.Name} counters {enemy.Name}!");
                    var counterStats  = target.GetBattleStats();
                    var attackResolver2 = new EnhancedAttackResolver();
                    var counterResult = attackResolver2.Resolve(counterStats, enemyBattleStats, DamageKind.Physical);
                    if (counterResult.Hit)
                    {
                        int counterHpBefore = enemy.CurrentHP;
                        bool counterKill = enemy.TakeDamage(counterResult.Damage);
                        Debug.Log($"[BattleEngine] Counter: {target.Name} deals {counterResult.Damage} to {enemy.Name}");

                        // Counter kills: heroKill depends on whether the counter came from the hero or a merc
                        var ctrEvt = new BattleAttackEvent(target.Name,
                            targetAlly.IsHero ? "hero" : "merc",
                            "counter",
                            enemy.Name, "monster",
                            counterResult.Damage, counterHpBefore, enemy.CurrentHP, counterKill);
                        _sink.OnAttackResolved(in ctrEvt);

                        var rcd = _sink.ShowDamageOnMonster(enemy, counterResult.Damage);
                        if (rcd != null) yield return rcd;

                        if (counterKill)
                        {
                            Debug.Log($"[BattleEngine] {enemy.Name} defeated by counter!");
                            // Merc counter-kills use heroKill=false (preserving original behaviour)
                            AwardEnemyDeathRewards(enemy, heroKill: targetAlly.IsHero);
                            var rcm = _sink.ShowMonsterDeath(enemy);
                            if (rcm != null) yield return rcm;
                            _monsters.Remove(enemy);
                        }
                    }
                    else
                    {
                        var rcm = _sink.ShowMissOnMonster(enemy);
                        if (rcm != null) yield return rcm;
                    }
                }
            }
            else
            {
                Debug.Log($"[BattleEngine] {enemy.Name} missed {target.Name}!");
                var rm = _sink.ShowMissOnAlly(targetAlly);
                if (rm != null) yield return rm;
            }
        }

        // ── Healing / consumable helpers ──────────────────────────────────────────

        /// <summary>
        /// Applies a healing skill's HP/MP restore and GrantedBuffs to the target,
        /// then fires display and analytics callbacks.
        /// </summary>
        private IEnumerator ApplyHealingSkillEffectsAndDisplay(ISkill skill, ICombatant caster,
            object healTarget, IBattleAlly targetAlly, string casterName)
        {
            if (skill.HPRestoreAmount > 0)
            {
                int healAmount = SkillHealCalculator.GetAmount(skill, caster);

                bool healed = false;
                if (healTarget is RolePlayingFramework.Heroes.Hero hpHero)
                    healed = hpHero.RestoreHP(healAmount);
                else if (healTarget is Mercenary hpMerc)
                    healed = hpMerc.RestoreHP(healAmount);

                if (healed)
                {
                    _sink.PlaySound(BattleSound.Restorative);
                    if (targetAlly != null)
                    {
                        var rh = _sink.ShowHealOnAlly(targetAlly, healAmount);
                        if (rh != null) yield return rh;
                    }

                    string targetName = healTarget is RolePlayingFramework.Heroes.Hero th
                        ? th.Name
                        : ((Mercenary)healTarget).Name;
                    int healHpAfter = healTarget is RolePlayingFramework.Heroes.Hero ha
                        ? ha.CurrentHP
                        : ((Mercenary)healTarget).CurrentHP;

                    // Source = skill.Id for analytics; SourceDisplayName = skill.Name for the console line
                    var evt = new BattleHealEvent(casterName, skill.Id, targetName, healAmount, healHpAfter,
                        skill.Name);
                    _sink.OnHealApplied(in evt);
                }
            }

            if (skill.MPRestoreAmount > 0)
            {
                if (healTarget is RolePlayingFramework.Heroes.Hero mpHero)
                    mpHero.RestoreMP(skill.MPRestoreAmount);
                else if (healTarget is Mercenary mpMerc)
                    mpMerc.RestoreMP(skill.MPRestoreAmount);
            }

            // Phase 3: apply GrantedBuffs
            var combatantTarget = healTarget as ICombatant;
            if (combatantTarget != null && skill.GrantedBuffs.Count > 0)
            {
                for (int b = 0; b < skill.GrantedBuffs.Count; b++)
                {
                    var grantedBuff = skill.GrantedBuffs[b];
                    int currentStacks = combatantTarget.GetBuffStacks(skill.Id, grantedBuff.Type);
                    if (currentStacks >= grantedBuff.MaxStacks)
                        continue;

                    combatantTarget.AddBattleBuff(new BattleBuff(grantedBuff.Type, grantedBuff.Magnitude,
                        grantedBuff.DurationTurns, skill.Id));
                    string buffLabel = GetBuffLabel(grantedBuff.Type, grantedBuff.Magnitude);
                    if (targetAlly != null)
                    {
                        var rb = _sink.ShowBuffOnAlly(targetAlly, buffLabel);
                        if (rb != null) yield return rb;
                    }
                }
            }
        }

        /// <summary>
        /// Consumes an item from the hero's bag, applies its effects to the target,
        /// and fires analytics callbacks.
        /// </summary>
        private IEnumerator ApplyItemAndDisplay(RolePlayingFramework.Equipment.Consumable consumable,
            int bagIndex, object target, bool targetsHero, string userName)
        {
            int hpBefore = 0;
            if (target is RolePlayingFramework.Heroes.Hero preHero) hpBefore = preHero.CurrentHP;
            else if (target is Mercenary preMerc)                    hpBefore = preMerc.CurrentHP;

            if (consumable.Consume(target))
            {
                _partyView.Bag.ConsumeFromStack(bagIndex);
                _sink.OnItemConsumed(); // sink fires OnInventoryChanged in live play

                int hpAfter = 0;
                if (target is RolePlayingFramework.Heroes.Hero postHero) hpAfter = postHero.CurrentHP;
                else if (target is Mercenary postMerc)                    hpAfter = postMerc.CurrentHP;

                int healAmount = hpAfter - hpBefore;
                if (healAmount > 0)
                {
                    // The original derived BOTH the user and target names from the heal target
                    // (analytics actor for item heals is the target, not the caster).
                    string itemUserName = targetsHero
                        ? (_partyView.Hero?.Name ?? "Hero")
                        : ((target is Mercenary usrMerc) ? usrMerc.Name : "Mercenary");
                    string targetName = targetsHero
                        ? (_partyView.Hero?.Name ?? "Hero")
                        : ((target is Mercenary tgtMerc) ? tgtMerc.Name : "Mercenary");

                    var evt = new BattleHealEvent(itemUserName, consumable.Name, targetName, healAmount, hpAfter);
                    _sink.OnConsumableHealApplied(consumable, in evt);

                    _sink.PlaySound(BattleSound.Restorative);
                    var targetAlly = FindAllyForTarget(target, targetsHero);
                    if (targetAlly != null)
                    {
                        var rh = _sink.ShowHealOnAlly(targetAlly, healAmount);
                        if (rh != null) yield return rh;
                    }
                }
            }
            else
            {
                Debug.Log($"[BattleEngine] Failed to use {consumable.Name}");
            }
        }

        // ── DoT ticking ───────────────────────────────────────────────────────────

        /// <summary>
        /// Ticks all active DoT entries in the battle context, fires damage display and
        /// analytics callbacks, and awards rewards for DoT kills.
        /// </summary>
        private IEnumerator TickDoTsAndHandleDeaths(BattleContext battleContext)
        {
            var tickResults = battleContext.TickDoTs();
            for (int i = 0; i < tickResults.Count; i++)
            {
                var result = tickResults[i];
                if (result.Damage <= 0) continue;
                if (!_monsters.Contains(result.Target)) continue;

                Debug.Log($"[BattleEngine] DoT {result.SourceSkillId}: {result.Damage} dmg to {result.Target.Name}. HP: {result.Target.CurrentHP}/{result.Target.MaxHP}");

                var dotEvt = new BattleAttackEvent(
                    result.ActorName, result.ActorType, result.SourceSkillId + ".dot",
                    result.Target.Name, "monster",
                    result.Damage, result.Target.CurrentHP + result.Damage, result.Target.CurrentHP,
                    result.TargetDied);
                _sink.OnAttackResolved(in dotEvt);

                var rd = _sink.ShowDamageOnMonster(result.Target, result.Damage);
                if (rd != null) yield return rd;

                if (result.TargetDied)
                {
                    Debug.Log($"[BattleEngine] {result.Target.Name} defeated by DoT ({result.SourceSkillId})!");
                    AwardEnemyDeathRewards(result.Target, heroKill: true);
                    var rm = _sink.ShowMonsterDeath(result.Target);
                    if (rm != null) yield return rm;
                    _monsters.Remove(result.Target);
                }
            }
        }

        // ── Reward awarding ───────────────────────────────────────────────────────

        /// <summary>
        /// Applies pure headless reward math (XP/JP/SP to hero; XP to mercs), then
        /// delegates gold / services / boss handling to the sink.
        /// </summary>
        private void AwardEnemyDeathRewards(IEnemy enemy, bool heroKill)
        {
            var hero = _partyView.Hero;
            hero.AddExperience(enemy.ExperienceYield);
            hero.EarnJP(enemy.JPYield);
            hero.EarnSynergyPointsWithAcceleration(enemy.SPYield);

            // Award XP to all living mercs
            for (int mi = 0; mi < _mercenaries.Count; mi++)
            {
                if (_mercenaries[mi].Combatant is Mercenary merc)
                    merc.AddExperience(enemy.ExperienceYield);
            }

            // Gold, InnExhausted, DefeatedMonsterService, AlliedMonsterManager, boss orb — sink's job
            _sink.OnEnemyDefeated(enemy, heroKill);
        }

        // ── Temp list builders ────────────────────────────────────────────────────

        /// <summary>Rebuilds <see cref="_tempLivingEnemies"/> from the current monster list.</summary>
        private void BuildLivingEnemyList()
        {
            _tempLivingEnemies.Clear();
            for (int i = 0; i < _monsters.Count; i++)
            {
                if (_monsters[i].CurrentHP > 0)
                    _tempLivingEnemies.Add(_monsters[i]);
            }
        }

        /// <summary>
        /// Rebuilds <see cref="_tempMercs"/> from the current mercenary roster.
        /// Deliberately unfiltered (the roster is already pruned on death): the original
        /// passed the raw validMercenaries list to the decision engine, which applies its
        /// own HP/targeting checks internally.
        /// </summary>
        private void BuildMercList()
        {
            _tempMercs.Clear();
            for (int i = 0; i < _mercenaries.Count; i++)
            {
                if (_mercenaries[i].Combatant is Mercenary merc)
                    _tempMercs.Add(merc);
            }
        }

        // ── Misc helpers ──────────────────────────────────────────────────────────

        /// <summary>Finds the IBattleAlly wrapper for the given heal target object.</summary>
        private IBattleAlly FindAllyForTarget(object target, bool targetsHero)
        {
            if (targetsHero || target is RolePlayingFramework.Heroes.Hero)
                return _hero;

            if (target is Mercenary targetMerc)
            {
                for (int i = 0; i < _mercenaries.Count; i++)
                {
                    if (_mercenaries[i].Combatant is Mercenary m && m == targetMerc)
                        return _mercenaries[i];
                }
            }

            return _hero;
        }

        /// <summary>Returns a short display label for a buff grant, e.g. "DEF+1".</summary>
        private static string GetBuffLabel(BuffType type, int magnitude)
        {
            if (type == BuffType.DefenseUp)  return "DEF+" + magnitude;
            if (type == BuffType.EvasionUp)  return "EVA+" + magnitude;
            if (type == BuffType.MPRegen)    return "MP+"  + magnitude;
            return type.ToString() + "+" + magnitude;
        }
    }
}
