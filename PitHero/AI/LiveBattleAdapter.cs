using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.Combat;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Mercenaries;
using System.Collections;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Live Nez implementation of both <see cref="IBattlePartyView"/> and
    /// <see cref="IBattleEventSink"/> for use in the running game.
    ///
    /// Wraps a <see cref="HeroComponent"/> (party settings / burst-damage tracking),
    /// maps <see cref="IEnemy"/> and <see cref="IBattleAlly"/> references to their
    /// Nez entities for display, and routes analytics / service calls through the
    /// appropriate game services.
    /// </summary>
    public sealed class LiveBattleAdapter : IBattlePartyView, IBattleEventSink
    {
        private readonly HeroComponent _heroComponent;
        private readonly List<Entity> _monsterEntities;

        // IEnemy → Entity mapping, built at construction and not mutated after
        private readonly Dictionary<IEnemy, Entity> _enemyToEntity = new Dictionary<IEnemy, Entity>(8);

        // Mercenary → (Entity, MercenaryComponent) for burst / critical checks
        private readonly Dictionary<Mercenary, (Entity e, MercenaryComponent c)> _mercMap
            = new Dictionary<Mercenary, (Entity, MercenaryComponent)>(4);

        // Battle UI entities created in OnBattleStarted and destroyed in finally-cleanup
        private Entity _turnIndicatorEntity;
        private BattleTurnIndicatorComponent _turnIndicator;

        // ── Construction ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Initialises the adapter.
        /// The caller must pass the raw monster-entity list that corresponds to
        /// the <see cref="IEnemy"/> list being given to the engine so the adapter can
        /// build the IEnemy → Entity map before the battle coroutine starts.
        /// </summary>
        public LiveBattleAdapter(HeroComponent heroComponent, List<Entity> monsterEntities)
        {
            _heroComponent    = heroComponent;
            _monsterEntities  = monsterEntities;

            // Build the enemy→entity map
            for (int i = 0; i < monsterEntities.Count; i++)
            {
                var ec = monsterEntities[i]?.GetComponent<EnemyComponent>();
                if (ec?.Enemy != null)
                    _enemyToEntity[ec.Enemy] = monsterEntities[i];
            }

            // Build the mercenary map from any mercs already in the pit
            var scene = Core.Scene;
            if (scene != null)
            {
                var mercEntities = scene.FindEntitiesWithTag(GameConfig.TAG_MERCENARY);
                for (int i = 0; i < mercEntities.Count; i++)
                {
                    var mc = mercEntities[i].GetComponent<MercenaryComponent>();
                    if (mc?.LinkedMercenary != null && mc.IsHired)
                        _mercMap[mc.LinkedMercenary] = (mercEntities[i], mc);
                }
            }
        }

        // ── IBattlePartyView ─────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public Hero Hero => _heroComponent.LinkedHero;

        /// <inheritdoc/>
        public BattleTactic CurrentBattleTactic => _heroComponent.CurrentBattleTactic;

        /// <inheritdoc/>
        public ItemBag Bag => _heroComponent.Bag;

        /// <inheritdoc/>
        public HeroHealPriority[] GetHealPrioritiesInOrder() => _heroComponent.GetHealPrioritiesInOrder();

        /// <inheritdoc/>
        public bool HealingItemExhausted
        {
            get => _heroComponent.HealingItemExhausted;
            set => _heroComponent.HealingItemExhausted = value;
        }

        /// <inheritdoc/>
        public bool HealingSkillExhausted
        {
            get => _heroComponent.HealingSkillExhausted;
            set => _heroComponent.HealingSkillExhausted = value;
        }

        /// <inheritdoc/>
        public bool UseConsumablesOnMercenaries => _heroComponent.UseConsumablesOnMercenaries;

        /// <inheritdoc/>
        public bool MercenariesCanUseConsumables => _heroComponent.MercenariesCanUseConsumables;

        /// <inheritdoc/>
        public bool IsHeroHPCritical() => _heroComponent.IsHeroHPCritical();

        /// <inheritdoc/>
        public bool IsMercenaryHPCritical(Mercenary merc)
        {
            if (merc == null) return false;
            if (_mercMap.TryGetValue(merc, out var pair))
                return _heroComponent.IsMercenaryHPCritical(pair.e, pair.c);
            return false;
        }

        /// <inheritdoc/>
        public void RegisterHeroBurstDamage(int damage)
            => _heroComponent.RegisterHeroBurstDamage(damage);

        /// <inheritdoc/>
        public void RegisterMercenaryBurstDamage(Mercenary merc, int damage)
        {
            if (merc == null) return;
            if (_mercMap.TryGetValue(merc, out var pair))
                _heroComponent.RegisterMercenaryBurstDamage(pair.e, pair.c, damage);
        }

        // ── IBattleEventSink — pacing ────────────────────────────────────────────────

        /// <inheritdoc/>
        public IEnumerator WaitWhilePaused()
        {
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused != true) return null;
            return WaitUntilUnpaused(pauseService);
        }

        private static IEnumerator WaitUntilUnpaused(PauseService pauseService)
        {
            while (pauseService.IsPaused)
                yield return null;
        }

        /// <inheritdoc/>
        public IEnumerator TurnDelay()
            => WaitForSecondsRespectingPause(GameConfig.BattleTurnWait);

        /// <inheritdoc/>
        public IEnumerator DigitBounceDelay()
            => WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);

        private static IEnumerator WaitForSecondsRespectingPause(float seconds)
        {
            var pauseService = Core.Services.GetService<PauseService>();
            while (pauseService?.IsPaused == true)
                yield return null;
            yield return Coroutine.WaitForSeconds(seconds);
        }

        // ── IBattleEventSink — lifecycle ─────────────────────────────────────────────

        /// <inheritdoc/>
        public IEnumerator OnBattleStarted()
        {
            // Add HP bars to all monster entities
            for (int i = 0; i < _monsterEntities.Count; i++)
            {
                var me = _monsterEntities[i];
                if (me != null && !me.IsDestroyed)
                    me.AddComponent(new MonsterHPBarComponent());
            }

            // Battle UI components for mercs already in the battle
            var scene = Core.Scene;
            if (scene != null)
            {
                var mercEntities = scene.FindEntitiesWithTag(GameConfig.TAG_MERCENARY);
                for (int i = 0; i < mercEntities.Count; i++)
                {
                    var mc = mercEntities[i].GetComponent<MercenaryComponent>();
                    if (mc != null && mc.IsHired && mc.InsidePit)
                        AddMercenaryBattleUIComponents(mercEntities[i]);
                }
            }

            // Create turn indicator entity
            _turnIndicatorEntity = Core.Scene?.CreateEntity("battle-turn-indicator");
            if (_turnIndicatorEntity != null)
                _turnIndicator = _turnIndicatorEntity.AddComponent(new BattleTurnIndicatorComponent());

            HeroStateMachine.IsBattleInProgress = true;

            Debug.Log($"[LiveBattleAdapter] Battle started. HeroStateMachine.IsBattleInProgress=true");
            return null;
        }

        /// <inheritdoc/>
        public IEnumerator OnRoundStarted() => null;

        /// <inheritdoc/>
        public void RecruitLateArrivingAllies(List<IBattleAlly> currentAllies)
        {
            var scene = Core.Scene;
            if (scene == null) return;

            var mercEntities = scene.FindEntitiesWithTag(GameConfig.TAG_MERCENARY);
            for (int i = 0; i < mercEntities.Count; i++)
            {
                var mercEntity = mercEntities[i];
                if (mercEntity == null || mercEntity.IsDestroyed) continue;

                var mc = mercEntity.GetComponent<MercenaryComponent>();
                if (mc?.LinkedMercenary == null || !mc.IsHired || !mc.InsidePit) continue;
                if (mc.LinkedMercenary.CurrentHP <= 0) continue;

                // Check if already tracked
                bool already = false;
                for (int j = 0; j < currentAllies.Count; j++)
                {
                    if (currentAllies[j] is LiveMercenaryAlly lma && lma.Entity == mercEntity)
                    {
                        already = true;
                        break;
                    }
                }
                if (already) continue;

                // New late-arriving mercenary
                var newAlly = new LiveMercenaryAlly(mercEntity, mc);
                currentAllies.Add(newAlly);

                // Register in mercMap if not present
                if (!_mercMap.ContainsKey(mc.LinkedMercenary))
                    _mercMap[mc.LinkedMercenary] = (mercEntity, mc);

                AddMercenaryBattleUIComponents(mercEntity);
                mc.LinkedMercenary.ClearBattleState();
                Debug.Log($"[LiveBattleAdapter] {mc.LinkedMercenary.Name} joins the battle (late arrival)!");
            }
        }

        /// <inheritdoc/>
        public IEnumerator OnTurnStarted(IBattleAlly ally)
        {
            if (_turnIndicator == null) return null;
            var entity = GetEntityForAlly(ally);
            if (entity != null)
                _turnIndicator.Show(entity, false);
            return null;
        }

        /// <inheritdoc/>
        public IEnumerator OnMonsterTurnStarted(IEnemy enemy)
        {
            if (_turnIndicator == null) return null;
            var entity = GetEntityForEnemy(enemy);
            if (entity != null)
                _turnIndicator.Show(entity, true);
            return null;
        }

        /// <inheritdoc/>
        public IEnumerator OnMonsterWindup(IEnemy enemy, IBattleAlly target)
        {
            var monsterEntity = GetEntityForEnemy(enemy);
            if (monsterEntity == null) return null;

            var targetEntity = GetEntityForAlly(target);
            if (targetEntity != null)
                FaceEntity(monsterEntity, targetEntity.Transform.Position);

            var monsterAnim = monsterEntity.GetComponent<EnemyAnimationComponent>();
            if (monsterAnim == null) return null;
            return monsterAnim.PlayAttackAnimation();
        }

        // ── IBattleEventSink — visual feedback ───────────────────────────────────────

        /// <inheritdoc/>
        public IEnumerator ShowDamageOnMonster(IEnemy enemy, int damage)
        {
            var entity = GetEntityForEnemy(enemy);
            return entity != null ? ShowDamageDigitOnEntity(entity, damage, BouncyDigitComponent.EnemyDigitColor) : null;
        }

        /// <inheritdoc/>
        public void ShowSkillDamageOnMonster(IEnemy enemy, int damage)
        {
            var entity = GetEntityForEnemy(enemy);
            var bouncyDigit = entity?.GetComponent<BouncyDigitComponent>();
            if (bouncyDigit != null)
            {
                bouncyDigit.Init(damage, BouncyDigitComponent.EnemyDigitColor, false);
                bouncyDigit.SetEnabled(true);
            }
        }

        /// <inheritdoc/>
        public IEnumerator ShowDamageOnAlly(IBattleAlly ally, int damage)
        {
            var entity = GetEntityForAlly(ally);
            return entity != null ? ShowDamageDigitOnEntity(entity, damage, BouncyDigitComponent.HeroDigitColor) : null;
        }

        /// <inheritdoc/>
        public IEnumerator ShowMissOnMonster(IEnemy enemy)
        {
            var entity = GetEntityForEnemy(enemy);
            return entity != null ? ShowTextOnEntity(entity, "Miss", BouncyTextComponent.EnemyMissColor) : null;
        }

        /// <inheritdoc/>
        public IEnumerator ShowMissOnAlly(IBattleAlly ally)
        {
            var entity = GetEntityForAlly(ally);
            return entity != null ? ShowTextOnEntity(entity, "Miss", BouncyTextComponent.HeroMissColor) : null;
        }

        /// <inheritdoc/>
        public IEnumerator ShowDeflectOnAlly(IBattleAlly ally)
        {
            var entity = GetEntityForAlly(ally);
            return entity != null ? ShowTextOnEntity(entity, "Deflect", BouncyTextComponent.HeroMissColor) : null;
        }

        /// <inheritdoc/>
        public IEnumerator ShowCritOnMonster(IEnemy enemy)
        {
            var entity = GetEntityForEnemy(enemy);
            return entity != null ? ShowTextOnEntity(entity, "Crit", Color.Yellow) : null;
        }

        /// <inheritdoc/>
        public IEnumerator ShowHealOnAlly(IBattleAlly ally, int amount)
        {
            var entity = GetEntityForAlly(ally);
            if (entity == null) return null;
            return ShowDamageDigitOnEntity(entity, amount, Color.Green);
        }

        /// <inheritdoc/>
        public IEnumerator ShowBuffOnAlly(IBattleAlly ally, string label)
        {
            var entity = GetEntityForAlly(ally);
            return entity != null ? ShowTextOnEntity(entity, label, Color.Cyan) : null;
        }

        /// <inheritdoc/>
        public IEnumerator ShowMonsterDeath(IEnemy enemy)
        {
            var entity = GetEntityForEnemy(enemy);
            if (entity == null) return null;
            return FadeOutAndDestroyMonster(entity);
        }

        /// <inheritdoc/>
        public IEnumerator ShowAllyDeath(IBattleAlly ally, IEnemy killer)
        {
            if (ally.IsHero)
            {
                // HeroDeathComponent handles the death animation
                var entity = GetEntityForAlly(ally);
                if (entity != null)
                {
                    var deathComp = entity.GetComponent<HeroDeathComponent>()
                        ?? entity.AddComponent(new HeroDeathComponent());
                    deathComp.StartDeathAnimation(killer.Name);
                }
                return null;
            }
            else
            {
                // Merc: fade out and destroy entity
                var entity = GetEntityForAlly(ally);
                if (entity == null) return null;
                return FadeOutAndDestroyMercenary(entity);
            }
        }

        // ── IBattleEventSink — analytics / side effects ──────────────────────────────

        /// <inheritdoc/>
        public void OnAttackResolved(in BattleAttackEvent evt)
        {
            PitHero.Services.Analytics.AnalyticsService.LogAttack(
                evt.ActorName, evt.ActorType, evt.Action,
                evt.TargetName, evt.TargetType,
                evt.Damage, evt.HpBefore, evt.HpAfter, evt.Killed, evt.Missed);

            // DoT ticks logged analytics-only in the original — no console line
            if (evt.Action != null && evt.Action.EndsWith(".dot"))
                return;

            var evtSvc = Core.Services.GetService<GameEventService>();
            if (evtSvc == null) return;

            if (evt.Missed)
            {
                if (evt.SkillName != null)
                {
                    evtSvc.EmitLocalized(UITextKey.ConsoleSkillAttackMiss,
                        (evt.ActorName, GameConfig.ConsoleColorHeroName),
                        (evt.SkillName, Color.White),
                        (evtSvc.MonsterName(evt.TargetName), GameConfig.ConsoleColorEnemyName));
                }
                else if (evt.ActorType == "monster")
                {
                    evtSvc.EmitLocalized(UITextKey.ConsoleAttackMiss,
                        (evtSvc.MonsterName(evt.ActorName), GameConfig.ConsoleColorEnemyName),
                        (evt.TargetName, GameConfig.ConsoleColorHeroName));
                }
                else
                {
                    evtSvc.EmitLocalized(UITextKey.ConsoleAttackMiss,
                        (evt.ActorName, GameConfig.ConsoleColorHeroName),
                        (evtSvc.MonsterName(evt.TargetName), GameConfig.ConsoleColorEnemyName));
                }
                return;
            }

            // Crit callout precedes the normal hit line (analytics carries it via the ".crit" action suffix)
            if (evt.Action != null && evt.Action.EndsWith(".crit"))
                evtSvc.EmitLocalized(UITextKey.ConsoleCritical);

            if (evt.SkillName != null)
            {
                // Skill attacks use the four-argument ConsoleSkillAttack line with the skill's display name
                evtSvc.EmitLocalized(UITextKey.ConsoleSkillAttack,
                    (evt.ActorName, GameConfig.ConsoleColorHeroName),
                    (evt.SkillName, Color.White),
                    (evtSvc.MonsterName(evt.TargetName), GameConfig.ConsoleColorEnemyName),
                    (evt.Damage.ToString(), Color.White));
            }
            else if (evt.ActorType == "monster")
            {
                evtSvc.EmitLocalized(UITextKey.ConsoleMonsterAttack,
                    (evtSvc.MonsterName(evt.ActorName), GameConfig.ConsoleColorEnemyName),
                    (evt.TargetName, GameConfig.ConsoleColorHeroName),
                    (evt.Damage.ToString(), Color.White));
            }
            else
            {
                evtSvc.EmitLocalized(UITextKey.ConsoleAttack,
                    (evt.ActorName, GameConfig.ConsoleColorHeroName),
                    (evtSvc.MonsterName(evt.TargetName), GameConfig.ConsoleColorEnemyName),
                    (evt.Damage.ToString(), Color.White));
            }
        }

        /// <inheritdoc/>
        public void OnHealApplied(in BattleHealEvent evt)
        {
            // Console line first (matches original order: console, then analytics),
            // showing the skill's display name rather than its analytics id.
            var evtSvc = Core.Services.GetService<GameEventService>();
            if (evtSvc != null)
            {
                evtSvc.EmitLocalized(UITextKey.ConsoleHealSkill,
                    (evt.ActorName, GameConfig.ConsoleColorHeroName),
                    (evt.SourceDisplayName ?? evt.Source, Color.White),
                    (evt.TargetName, GameConfig.ConsoleColorHeroName),
                    (evt.Amount.ToString(), Color.White));
            }

            PitHero.Services.Analytics.AnalyticsService.LogHeal(
                evt.ActorName, evt.Source, evt.TargetName, evt.Amount, evt.HpAfter);
        }

        /// <inheritdoc/>
        public void OnBuffApplied(in BattleBuffEvent evt)
        {
            // Console line first (matching OnHealApplied's console-then-analytics order),
            // showing the skill's display name and the same effect label as the floating text.
            var evtSvc = Core.Services.GetService<GameEventService>();
            if (evtSvc != null)
            {
                evtSvc.EmitLocalized(UITextKey.ConsoleBuffSkill,
                    (evt.CasterName, GameConfig.ConsoleColorHeroName),
                    (evt.SourceDisplayName ?? evt.Source, Color.White),
                    (evt.TargetName, GameConfig.ConsoleColorHeroName),
                    (evt.EffectLabel ?? evt.BuffTypeName, Color.White));
            }

            PitHero.Services.Analytics.AnalyticsService.LogBuff(
                evt.CasterName, evt.Source, evt.TargetName, evt.BuffTypeName, evt.Magnitude, evt.DurationTurns);
        }

        /// <inheritdoc/>
        public void OnConsumableHealApplied(RolePlayingFramework.Equipment.Consumable consumable, in BattleHealEvent evt)
        {
            // ConsoleBattleHealConsumable with the item name in its rarity colour (original :919-925)
            var evtSvc = Core.Services.GetService<GameEventService>();
            if (evtSvc != null)
            {
                evtSvc.EmitLocalized(UITextKey.ConsoleBattleHealConsumable,
                    (evt.ActorName, GameConfig.ConsoleColorHeroName),
                    (consumable.Name, RarityUtils.GetRarityColor(consumable.Rarity)),
                    (evt.TargetName, GameConfig.ConsoleColorHeroName),
                    (evt.Amount.ToString(), Color.White));
            }

            PitHero.Services.Analytics.AnalyticsService.LogHeal(
                evt.ActorName, evt.Source, evt.TargetName, evt.Amount, evt.HpAfter);
        }

        /// <inheritdoc/>
        public void OnItemConsumed()
        {
            PitHero.UI.InventorySelectionManager.OnInventoryChanged?.Invoke();
        }

        /// <inheritdoc/>
        public void OnMercenaryActionShown(IBattleAlly merc, QueuedAction action)
        {
            if (merc is LiveMercenaryAlly lma)
                lma.Component.ActionQueueVisualization?.ShowAction(action);
        }

        /// <inheritdoc/>
        public void FaceAllyToward(IBattleAlly ally, IEnemy target)
        {
            var allyEntity = GetEntityForAlly(ally);
            var targetEntity = GetEntityForEnemy(target);
            if (allyEntity != null && targetEntity != null)
                FaceEntity(allyEntity, targetEntity.Transform.Position);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Side effects run in the original's order: analytics, gold/InnExhausted,
        /// DefeatedMonsterService, AlliedMonsterManager recruit (its console line prints
        /// before the death line), then boss handling, then the ConsoleMonsterDied line.
        /// </remarks>
        public void OnEnemyDefeated(IEnemy enemy, bool heroKill)
        {
            PitHero.Services.Analytics.AnalyticsService.LogMonsterDefeated(enemy);

            // Gold + InnExhausted reset
            var gameState = Core.Services.GetService<PitHero.Services.GameStateService>();
            if (gameState != null)
            {
                gameState.AddFunds(enemy.GoldYield, "battle");
                // Only hero kills reset InnExhausted (preserves original: rewardHeroComponent!=null guard)
                if (heroKill && enemy.GoldYield > 0)
                    _heroComponent.InnExhausted = false;
            }

            // DefeatedMonsterService + AlliedMonsterManager (recruit console line emits inside TryRecruit)
            Core.Services.GetService<PitHero.Services.DefeatedMonsterService>()?.MarkDefeated(enemy.EnemyId);

            var alliedMonsterMgr = Core.Services.GetService<PitHero.Services.AlliedMonsterManager>();
            if (alliedMonsterMgr != null)
            {
                var recruited = alliedMonsterMgr.TryRecruit(enemy);
                if (recruited != null)
                    Debug.Log($"[LiveBattleAdapter] {enemy.Name} recruited as '{recruited.Name}'");
            }

            // Boss flag + WizardOrb tint
            if (enemy.IsBoss)
            {
                _heroComponent.BossDefeated = true;
                var scene = Core.Scene;
                if (scene != null)
                {
                    var orbEntities = scene.FindEntitiesWithTag(GameConfig.TAG_WIZARD_ORB);
                    if (orbEntities.Count > 0)
                    {
                        var orbRenderer = orbEntities[0].GetComponent<Nez.RenderableComponent>();
                        if (orbRenderer != null)
                            orbRenderer.Color = Color.White;
                    }
                }
                Debug.Log($"[LiveBattleAdapter] Boss {enemy.Name} defeated - BossDefeated=true");
            }

            // Console event for monster death (after recruit, matching original line order)
            var evtSvc = Core.Services.GetService<GameEventService>();
            if (evtSvc != null)
                evtSvc.EmitLocalized(
                    enemy.IsBoss ? EventPriority.High : EventPriority.Normal,
                    UITextKey.ConsoleMonsterDied,
                    (evtSvc.MonsterName(enemy.Name), GameConfig.ConsoleColorEnemyName));
        }

        /// <inheritdoc/>
        public void OnAllyKilled(IBattleAlly ally, IEnemy killer)
        {
            if (ally.IsHero)
            {
                PitHero.Services.Analytics.AnalyticsService.LogCharacterKilled(
                    (Hero)ally.Combatant, killer);
                Debug.Log($"[LiveBattleAdapter] Hero killed by {killer.Name}.");
            }
            else
            {
                var merc = (Mercenary)ally.Combatant;
                PitHero.Services.Analytics.AnalyticsService.LogCharacterKilled(merc, killer);
                Debug.Log($"[LiveBattleAdapter] {merc.Name} killed by {killer.Name}.");

                if (_mercMap.TryGetValue(merc, out var pair))
                    HandleMercenaryDeath(pair.e, killer.Name);
            }
        }

        // ── IBattleEventSink — audio ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public void PlaySound(BattleSound sound)
        {
            var sfx = Core.GetGlobalManager<SoundEffectManager>();
            if (sfx == null) return;
            switch (sound)
            {
                case BattleSound.Punch:
                    sfx.PlaySound(SoundEffectType.Punch);
                    break;
                case BattleSound.TakeDamage:
                    sfx.PlaySound(SoundEffectType.TakeDamage);
                    break;
                case BattleSound.Restorative:
                    sfx.PlaySound(SoundEffectType.Restorative);
                    break;
                case BattleSound.EnemyDefeat:
                    sfx.PlaySound(SoundEffectType.EnemyDefeat);
                    break;
            }
        }

        // ── Post-battle cleanup ──────────────────────────────────────────────────────

        /// <summary>
        /// Cleans up all battle UI added during <see cref="OnBattleStarted"/>:
        /// destroys the turn indicator, removes monster HP bars, and removes merc
        /// bounce-digit / bounce-text components.
        /// Called by <see cref="AttackMonsterAction"/> in the battle's finally block.
        /// </summary>
        public void CleanupBattleUI(List<IBattleAlly> mercAllies)
        {
            HeroStateMachine.IsBattleInProgress = false;

            if (_turnIndicatorEntity != null)
                _turnIndicatorEntity.Destroy();

            // Remove HP bars from monster entities
            for (int i = 0; i < _monsterEntities.Count; i++)
            {
                var me = _monsterEntities[i];
                if (me == null || me.IsDestroyed) continue;
                var hpBar = me.GetComponent<MonsterHPBarComponent>();
                if (hpBar != null) me.RemoveComponent(hpBar);
            }

            // Remove bounce components from merc entities
            for (int i = 0; i < mercAllies.Count; i++)
            {
                if (mercAllies[i] is LiveMercenaryAlly lma)
                {
                    var me = lma.Entity;
                    if (me == null || me.IsDestroyed) continue;
                    var digit = me.GetComponent<BouncyDigitComponent>();
                    if (digit != null) me.RemoveComponent(digit);
                    var text = me.GetComponent<BouncyTextComponent>();
                    if (text != null) me.RemoveComponent(text);
                }
            }
        }

        // ── Private display helpers ──────────────────────────────────────────────────

        private Entity GetEntityForEnemy(IEnemy enemy)
        {
            if (enemy == null) return null;
            _enemyToEntity.TryGetValue(enemy, out var e);
            return e;
        }

        private static Entity GetEntityForAlly(IBattleAlly ally)
        {
            if (ally is LiveHeroAlly lha) return lha.Entity;
            if (ally is LiveMercenaryAlly lma) return lma.Entity;
            return null;
        }

        private static IEnumerator ShowDamageDigitOnEntity(Entity entity, int damage, Color digitColor)
        {
            var bouncyDigit = entity?.GetComponent<BouncyDigitComponent>();
            if (bouncyDigit != null)
            {
                bouncyDigit.Init(damage, digitColor, false);
                bouncyDigit.SetEnabled(true);
                yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
            }
        }

        private static IEnumerator ShowTextOnEntity(Entity entity, string text, Color textColor)
        {
            var bouncyText = entity?.GetComponent<BouncyTextComponent>();
            if (bouncyText != null)
            {
                bouncyText.Init(text, textColor);
                bouncyText.SetEnabled(true);
                yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
            }
        }

        private static void FaceEntity(Entity entity, Vector2 targetPosition)
        {
            var delta = targetPosition - entity.Transform.Position;
            Direction faceDir;
            if (System.Math.Abs(delta.X) >= System.Math.Abs(delta.Y))
                faceDir = delta.X < 0 ? Direction.Left : Direction.Right;
            else
                faceDir = delta.Y < 0 ? Direction.Up : Direction.Down;
            entity.GetComponent<ActorFacingComponent>()?.SetFacing(faceDir);
        }

        private static void AddMercenaryBattleUIComponents(Entity mercEntity)
        {
            if (!mercEntity.HasComponent<BouncyDigitComponent>())
            {
                var digit = mercEntity.AddComponent<BouncyDigitComponent>();
                digit.SetRenderLayer(GameConfig.RenderLayerLowest);
                digit.SetEnabled(false);
            }
            if (!mercEntity.HasComponent<BouncyTextComponent>())
            {
                var text = mercEntity.AddComponent<BouncyTextComponent>();
                text.SetRenderLayer(GameConfig.RenderLayerLowest);
                text.SetEnabled(false);
            }
        }

        private IEnumerator FadeOutAndDestroyMonster(Entity monsterEntity)
        {
            if (monsterEntity == null || monsterEntity.IsDestroyed) yield break;

            var enemyAnim = monsterEntity.GetComponent<EnemyAnimationComponent>();
            SpriteRenderer spriteRenderer = monsterEntity.GetComponent<SpriteRenderer>();
#if DEBUG
            PrototypeSpriteRenderer protoRenderer = null;
            if (spriteRenderer == null)
                protoRenderer = monsterEntity.GetComponent<PrototypeSpriteRenderer>();
#endif

            Color origColorAnim   = enemyAnim    != null ? enemyAnim.Color   : Color.White;
            Color origColorSprite = spriteRenderer != null ? spriteRenderer.Color : Color.White;
#if DEBUG
            Color origColorProto  = Color.White;
            if (protoRenderer != null) origColorProto = protoRenderer.Color;
#endif

            PlaySound(BattleSound.EnemyDefeat);

            const float fadeDuration = 0.5f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService?.IsPaused == true) { yield return null; continue; }
                elapsed += Time.DeltaTime;
                float progress = elapsed < 0f ? 0f : (elapsed > fadeDuration ? 1f : elapsed / fadeDuration);
                byte alpha = (byte)(255 * (1f - progress));
                if (enemyAnim    != null) enemyAnim.Color    = new Color(origColorAnim.R,   origColorAnim.G,   origColorAnim.B,   alpha);
                if (spriteRenderer != null) spriteRenderer.Color = new Color(origColorSprite.R, origColorSprite.G, origColorSprite.B, alpha);
#if DEBUG
                if (protoRenderer != null) protoRenderer.Color = new Color(origColorProto.R, origColorProto.G, origColorProto.B, alpha);
#endif
                yield return null;
            }
            monsterEntity.Destroy();
        }

        private static IEnumerator FadeOutAndDestroyMercenary(Entity mercenaryEntity)
        {
            if (mercenaryEntity == null || mercenaryEntity.IsDestroyed) yield break;

            var bodyAnim   = mercenaryEntity.GetComponent<HeroBodyAnimationComponent>();
            var hand1Anim  = mercenaryEntity.GetComponent<HeroHand1AnimationComponent>();
            var hand2Anim  = mercenaryEntity.GetComponent<HeroHand2AnimationComponent>();
            var pantsAnim  = mercenaryEntity.GetComponent<HeroPantsAnimationComponent>();
            var shirtAnim  = mercenaryEntity.GetComponent<HeroShirtAnimationComponent>();
            var hairAnim   = mercenaryEntity.GetComponent<HeroHairAnimationComponent>();

            Color origBody  = bodyAnim  != null ? bodyAnim.Color  : Color.White;
            Color origHand1 = hand1Anim != null ? hand1Anim.Color : Color.White;
            Color origHand2 = hand2Anim != null ? hand2Anim.Color : Color.White;
            Color origPants = pantsAnim != null ? pantsAnim.Color : Color.White;
            Color origShirt = shirtAnim != null ? shirtAnim.Color : Color.White;
            Color origHair  = hairAnim  != null ? hairAnim.Color  : Color.White;

            const float fadeDuration = 0.5f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService?.IsPaused == true) { yield return null; continue; }
                elapsed += Time.DeltaTime;
                float progress = elapsed < 0f ? 0f : (elapsed > fadeDuration ? 1f : elapsed / fadeDuration);
                byte alpha = (byte)(255 * (1f - progress));
                if (bodyAnim  != null) bodyAnim.Color  = new Color(origBody.R,  origBody.G,  origBody.B,  alpha);
                if (hand1Anim != null) hand1Anim.Color = new Color(origHand1.R, origHand1.G, origHand1.B, alpha);
                if (hand2Anim != null) hand2Anim.Color = new Color(origHand2.R, origHand2.G, origHand2.B, alpha);
                if (pantsAnim != null) pantsAnim.Color = new Color(origPants.R, origPants.G, origPants.B, alpha);
                if (shirtAnim != null) shirtAnim.Color = new Color(origShirt.R, origShirt.G, origShirt.B, alpha);
                if (hairAnim  != null) hairAnim.Color  = new Color(origHair.R,  origHair.G,  origHair.B,  alpha);
                yield return null;
            }

            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            mercenaryManager?.UntrackMercenary(mercenaryEntity);
            mercenaryEntity.Destroy();
        }

        private void HandleMercenaryDeath(Entity mercenaryEntity, string killerName)
        {
            var mercComponent = mercenaryEntity.GetComponent<MercenaryComponent>();
            if (mercComponent == null) return;

            var evtSvc = Core.Services.GetService<GameEventService>();
            if (evtSvc != null)
                evtSvc.EmitLocalized(UITextKey.ConsoleMercenaryDied,
                    (mercComponent.LinkedMercenary.Name, GameConfig.ConsoleColorHeroName),
                    (evtSvc.MonsterName(killerName), GameConfig.ConsoleColorEnemyName));

            // Transfer gear to SecondChanceMerchantVault
            var vault = Core.Services.GetService<SecondChanceMerchantVault>();
            if (vault != null)
            {
                var merc = mercComponent.LinkedMercenary;
                var gearToTransfer = new List<RolePlayingFramework.Equipment.IItem>(6);
                if (merc.WeaponShield1  != null) gearToTransfer.Add(merc.WeaponShield1);
                if (merc.Armor          != null) gearToTransfer.Add(merc.Armor);
                if (merc.Hat            != null) gearToTransfer.Add(merc.Hat);
                if (merc.WeaponShield2  != null) gearToTransfer.Add(merc.WeaponShield2);
                if (merc.Accessory1     != null) gearToTransfer.Add(merc.Accessory1);
                if (merc.Accessory2     != null) gearToTransfer.Add(merc.Accessory2);
                vault.AddItems(gearToTransfer);
            }

            // Reassign followers
            var mercManager = Core.Services.GetService<MercenaryManager>();
            if (mercManager != null)
            {
                var inheritedTarget = mercComponent.FollowTarget;
                if (inheritedTarget == null || inheritedTarget.IsDestroyed)
                    inheritedTarget = _heroComponent.Entity;

                var hiredMercs = mercManager.GetHiredMercenaries();
                for (int i = 0; i < hiredMercs.Count; i++)
                {
                    var otherEntity = hiredMercs[i];
                    if (otherEntity == mercenaryEntity || otherEntity.IsDestroyed) continue;
                    var otherComp = otherEntity.GetComponent<MercenaryComponent>();
                    if (otherComp == null || otherComp.FollowTarget != mercenaryEntity) continue;
                    otherComp.FollowTarget = inheritedTarget == otherEntity ? _heroComponent.Entity : inheritedTarget;
                }
            }
        }
    }
}
