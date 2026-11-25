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
using RolePlayingFramework.Skills;
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
            // Monster HP is shown by the HUD strip beside the gold display (MainGameScene reads
            // HeroStateMachine.BattleMonsterEntities, set below); nothing is drawn on the monsters.

            // Every monster turns toward the hero for the fight
            for (int i = 0; i < _monsterEntities.Count; i++)
                FaceMonsterTowardHero(_monsterEntities[i]);

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

            // The whole party squares up to the monster the hero engaged (the hero faced the first one)
            _heroFocus = _monsterEntities.Count > 0 ? _monsterEntities[0] : null;
            FacePartyTowardHeroFocus();

            // Create turn indicator entity
            _turnIndicatorEntity = Core.Scene?.CreateEntity("battle-turn-indicator");
            if (_turnIndicatorEntity != null)
                _turnIndicator = _turnIndicatorEntity.AddComponent(new BattleTurnIndicatorComponent());

            HeroStateMachine.IsBattleInProgress = true;
            HeroStateMachine.BattleMonsterEntities = _monsterEntities;
            HeroStateMachine.CurrentThreatTarget = null;

            Debug.Log($"[LiveBattleAdapter] Battle started. HeroStateMachine.IsBattleInProgress=true");
            return null;
        }

        /// <inheritdoc/>
        public IEnumerator OnRoundStarted()
        {
            // Untargetable ticks down at the end of the round: un-fade anyone it just ran out on
            RefreshSeeThrough();
            return null;
        }

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
                FaceEntityFrom(mercEntity, mercEntity.Transform.Position, FocusPosition(mercEntity));
                Debug.Log($"[LiveBattleAdapter] {mc.LinkedMercenary.Name} joins the battle (late arrival)!");
            }
        }

        /// <inheritdoc/>
        public IEnumerator OnTurnStarted(IBattleAlly ally)
        {
            // Anyone who turned for their own action squares back up to the hero's target
            FacePartyTowardHeroFocus();
            RefreshSeeThrough();
            if (_turnIndicator == null) return null;
            var entity = GetEntityForAlly(ally);
            if (entity != null)
                _turnIndicator.Show(entity, false);
            return null;
        }

        /// <inheritdoc/>
        public IEnumerator OnMonsterTurnStarted(IEnemy enemy)
        {
            FacePartyTowardHeroFocus();
            RefreshSeeThrough();
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
            var monsterAnim = monsterEntity.GetComponent<EnemyAnimationComponent>();

            // A physical attacker not already beside its target leaps in front of it first
            var targetEntity = GetEntityForAlly(target);
            if (enemy.AttackKind == DamageKind.Physical && monsterAnim != null && targetEntity != null
                && TryPlanMonsterLeap(monsterEntity, targetEntity, out var offset))
                return MonsterLeapAttackRoutine(monsterEntity, monsterAnim, targetEntity, offset);

            // A magic attacker stays put and casts a bolt of its element at the target
            if (enemy.AttackKind == DamageKind.Magical && monsterAnim != null && targetEntity != null)
                return MonsterMagicAttackRoutine(enemy, monsterEntity, monsterAnim, targetEntity);

            // Otherwise a battling monster keeps facing the hero, whoever it is attacking
            FaceMonsterTowardHero(monsterEntity);
            if (monsterAnim == null) return null;
            return monsterAnim.PlayAttackAnimation();
        }

        /// <inheritdoc/>
        public IEnumerator EndMonsterAttack(IEnemy enemy)
        {
            var monsterEntity = GetEntityForEnemy(enemy);
            if (_magicCaster != null && monsterEntity == _magicCaster)
            {
                // A caster turned to its target; it squares back up to the hero
                _magicCaster = null;
                if (!monsterEntity.IsDestroyed && enemy.CurrentHP > 0)
                    FaceMonsterTowardHero(monsterEntity);
                return null;
            }
            if (_leaptMonster == null || monsterEntity != _leaptMonster)
                return null;
            if (monsterEntity.IsDestroyed || enemy.CurrentHP <= 0)
            {
                // Killed by a counter where it stood: it fades out there
                _leaptMonster = null;
                return null;
            }
            return MonsterLeapHomeRoutine(monsterEntity);
        }

        /// <inheritdoc/>
        public void ShowMonsterHitOnAlly(IEnemy enemy, IBattleAlly target)
        {
            var targetEntity = GetEntityForAlly(target);
            if (targetEntity == null)
                return;
            if (enemy.AttackKind == DamageKind.Magical)
            {
                // The bolt bursts on the target in its element
                MonsterMagicLook(enemy.Element, out _, out var impact);
                AttackEffects.PlayAnimationOn(targetEntity, impact, GameConfig.MonsterHitEffectDuration, false);
                return;
            }
            // The spray points away from the attacker: the art sprays right, so mirror it when the monster is right
            bool fromRight = false;
            var monsterEntity = GetEntityForEnemy(enemy);
            if (monsterEntity != null)
            {
                float monsterX = monsterEntity.Transform.Position.X
                    + (monsterEntity == _leaptMonster ? _leaptOffset.X : 0f);
                fromRight = monsterX > targetEntity.Transform.Position.X;
            }
            AttackEffects.PlayAnimationOn(targetEntity, AttackEffects.MonsterHitAnimation,
                GameConfig.MonsterHitEffectDuration, fromRight);
        }

        // ── Monster magic attacks ────────────────────────────────────────────────────

        /// <summary>The magic-attacking monster turned toward its target this turn, or null.</summary>
        private Entity _magicCaster;

        /// <summary>
        /// The bolt a magic-attacking monster casts and the burst it makes on the target, by the monster's
        /// element: fire → fireball + fire blast, dark → shadow bolt + dark hit, light → arcane bolt + holy
        /// burst, anything else → the rod's arcane bolt + burst.
        /// </summary>
        private static void MonsterMagicLook(ElementType element, out string bolt, out string impact)
        {
            switch (element)
            {
                case ElementType.Fire:
                    bolt = AttackEffects.AnimationName(SkillEffect.Fireball);
                    impact = AttackEffects.AnimationName(SkillEffect.FireBlast);
                    break;
                case ElementType.Dark:
                    bolt = AttackEffects.AnimationName(SkillEffect.ShadowBolt);
                    impact = AttackEffects.AnimationName(SkillEffect.SneakAttack);
                    break;
                case ElementType.Light:
                    bolt = AttackEffects.RodBoltAnimation;
                    impact = AttackEffects.AnimationName(SkillEffect.HolyBurst);
                    break;
                default:
                    bolt = AttackEffects.RodBoltAnimation;
                    impact = AttackEffects.RodBurstAnimation;
                    break;
            }
        }

        /// <summary>
        /// A magic attacker turns to its target, plays its attack animation, then a bolt of its element flies
        /// to the target (blocking, timed like the Fire skill's fireball). The burst comes on a hit, from
        /// <see cref="ShowMonsterHitOnAlly"/>; <see cref="EndMonsterAttack"/> turns it back to the hero.
        /// </summary>
        private IEnumerator MonsterMagicAttackRoutine(IEnemy enemy, Entity monsterEntity, EnemyAnimationComponent anim,
            Entity targetEntity)
        {
            var from = monsterEntity.Transform.Position;
            var targetPos = targetEntity.Transform.Position;
            FaceMonsterTowardX(monsterEntity, anim, from.X, targetPos.X);
            _magicCaster = monsterEntity;
            yield return anim.PlayAttackAnimation();

            MonsterMagicLook(enemy.Element, out var bolt, out _);
            yield return AttackEffects.FlyAnimation(Core.Scene, bolt, from, targetPos,
                GameConfig.SkillFireballSpeed, GameConfig.SkillFireballMinFlight, GameConfig.SkillFireballMaxFlight);
        }

        // ── Monster leap-in (render-only) ────────────────────────────────────────────

        /// <summary>The monster standing in front of its target after a leap, or null.</summary>
        private Entity _leaptMonster;
        /// <summary>Its sprite offset from home while it stands there.</summary>
        private Vector2 _leaptOffset;
        /// <summary>Its resting sprite offset (the tile anchor of a tall monster), restored when it is home.</summary>
        private Vector2 _leaptRest;

        /// <summary>The eight tiles around a target, as unit tile steps.</summary>
        private static readonly Vector2[] NeighborSteps =
        {
            new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
            new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1),
        };

        /// <summary>
        /// Where a monster lands to attack <paramref name="targetEntity"/>: the free tile beside the target
        /// nearest the monster (not under another party member or monster; the nearest tile when none is free).
        /// False (no leap) when the monster already stands beside the target.
        /// </summary>
        private bool TryPlanMonsterLeap(Entity monsterEntity, Entity targetEntity, out Vector2 offset)
        {
            offset = Vector2.Zero;
            var from = monsterEntity.Transform.Position;
            var targetPos = targetEntity.Transform.Position;
            var d = from - targetPos;
            if (System.MathF.Abs(d.X) <= GameConfig.MonsterAttackAdjacentRange
                && System.MathF.Abs(d.Y) <= GameConfig.MonsterAttackAdjacentRange)
                return false;

            float tile = GameConfig.TileSize;
            Vector2 nearest = Vector2.Zero, nearestFree = Vector2.Zero;
            float nearestScore = float.MaxValue, nearestFreeScore = float.MaxValue;
            for (int i = 0; i < NeighborSteps.Length; i++)
            {
                var spot = targetPos + NeighborSteps[i] * tile;
                float score = Vector2.DistanceSquared(spot, from);
                if (score < nearestScore)
                {
                    nearestScore = score;
                    nearest = spot;
                }
                if (score < nearestFreeScore && !IsSpotTaken(spot, monsterEntity))
                {
                    nearestFreeScore = score;
                    nearestFree = spot;
                }
            }
            var landing = nearestFreeScore < float.MaxValue ? nearestFree : nearest;
            offset = landing - from;
            return offset.LengthSquared() >= 4f;
        }

        /// <summary>True when the hero, a mercenary or another living monster stands on <paramref name="spot"/>.</summary>
        private bool IsSpotTaken(Vector2 spot, Entity self)
        {
            float r = GameConfig.TileSize * 0.5f;
            float r2 = r * r;
            var heroEntity = _heroComponent?.Entity;
            if (heroEntity != null && Vector2.DistanceSquared(heroEntity.Transform.Position, spot) < r2)
                return true;
            foreach (var kv in _mercMap)
            {
                var e = kv.Value.e;
                if (e != null && !e.IsDestroyed && kv.Key.CurrentHP > 0
                    && Vector2.DistanceSquared(e.Transform.Position, spot) < r2)
                    return true;
            }
            for (int i = 0; i < _monsterEntities.Count; i++)
            {
                var m = _monsterEntities[i];
                if (m != self && IsLivingMonster(m) && Vector2.DistanceSquared(m.Transform.Position, spot) < r2)
                    return true;
            }
            return false;
        }

        /// <summary>Turns a monster left or right toward <paramref name="x"/> and applies the flip at once.</summary>
        private static void FaceMonsterTowardX(Entity monsterEntity, EnemyAnimationComponent anim, float fromX, float x)
        {
            float dx = x - fromX;
            if (dx == 0f)
                return;
            var dir = dx < 0f ? Direction.Left : Direction.Right;
            monsterEntity.GetComponent<ActorFacingComponent>()?.SetFacing(dir);
            // Apply the flip now: a flip negates LocalOffset.X, so it must land before a leap offset is written
            anim.UpdateAnimationForDirection(dir);
        }

        private IEnumerator MonsterLeapAttackRoutine(Entity monsterEntity, EnemyAnimationComponent anim,
            Entity targetEntity, Vector2 offset)
        {
            var from = monsterEntity.Transform.Position;
            var targetX = targetEntity.Transform.Position.X;
            FaceMonsterTowardX(monsterEntity, anim, from.X, targetX);
            _leaptMonster = monsterEntity;
            _leaptRest = anim.LocalOffset;
            _leaptOffset = offset;
            yield return MonsterLeapArc(monsterEntity, anim, _leaptRest, Vector2.Zero, offset);

            // Face the target from where it landed (it may have hopped past it), then strike
            if (!monsterEntity.IsDestroyed)
            {
                anim.LocalOffset = _leaptRest;
                FaceMonsterTowardX(monsterEntity, anim, from.X + offset.X, targetX);
                anim.LocalOffset = _leaptRest + offset;
            }
            yield return anim.PlayAttackAnimation();
        }

        private IEnumerator MonsterLeapHomeRoutine(Entity monsterEntity)
        {
            var anim = monsterEntity.GetComponent<EnemyAnimationComponent>();
            if (anim != null)
            {
                var from = monsterEntity.Transform.Position;
                anim.LocalOffset = _leaptRest;
                FaceMonsterTowardX(monsterEntity, anim, from.X + _leaptOffset.X, from.X);
                anim.LocalOffset = _leaptRest + _leaptOffset;
                yield return MonsterLeapArc(monsterEntity, anim, _leaptRest, _leaptOffset, Vector2.Zero);
            }
            _leaptMonster = null;
            _leaptOffset = Vector2.Zero;
            // Home again: back to facing the hero
            if (anim != null && !monsterEntity.IsDestroyed)
                anim.LocalOffset = _leaptRest;
            FaceMonsterTowardHero(monsterEntity);
        }

        /// <summary>
        /// Render-only parabolic leap of a monster's sprite between two offsets from home (the entity, its tile
        /// and its AI never move). Timed like the mercenary jump in sim time and held while paused.
        /// </summary>
        private static IEnumerator MonsterLeapArc(Entity entity, EnemyAnimationComponent anim, Vector2 rest,
            Vector2 fromOffset, Vector2 toOffset)
        {
            var delta = toOffset - fromOffset;
            float duration = Mathf.Clamp(delta.Length() / GameConfig.MercAttackJumpSpeed,
                GameConfig.MercAttackJumpMinDuration, GameConfig.MercAttackJumpMaxDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += UnpausedDelta();
                if (entity.IsDestroyed)
                    continue;
                float t = Mathf.Clamp01(elapsed / duration);
                var at = Vector2.Lerp(fromOffset, toOffset, t);
                at.Y -= GameConfig.MonsterAttackLeapHeight * 4f * t * (1f - t);
                anim.LocalOffset = rest + at;
            }
            if (!entity.IsDestroyed)
                anim.LocalOffset = rest + toOffset;
        }

        /// <summary>Puts a monster still standing in front of its target back home at once (battle torn down).</summary>
        private void ResetLeaptMonster()
        {
            if (_leaptMonster == null)
                return;
            if (!_leaptMonster.IsDestroyed)
            {
                var anim = _leaptMonster.GetComponent<EnemyAnimationComponent>();
                if (anim != null)
                    anim.LocalOffset = _leaptRest;
            }
            _leaptMonster = null;
            _leaptOffset = Vector2.Zero;
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

        // ── Physical attack visuals (issue #458) ─────────────────────────────────────

        /// <summary>The mercenary entity currently standing beside the hero after an attack jump, or null.</summary>
        private Entity _jumpedEntity;
        /// <summary>Its sprite offset from home while it stands there.</summary>
        private Vector2 _jumpedOffset;
        /// <summary>Which way it faced before jumping; restored once it lands back home.</summary>
        private Direction? _jumpedFacing;

        /// <inheritdoc/>
        public IEnumerator ShowPhysicalAttack(IBattleAlly attacker, IEnemy target, IItem weapon)
        {
            var attackerEntity = GetEntityForAlly(attacker);
            var targetEntity = GetEntityForEnemy(target);
            if (attackerEntity == null || targetEntity == null)
                return null;
            if (attacker.IsHero)
                SetHeroFocus(targetEntity);
            return PhysicalAttackRoutine(attacker, attackerEntity, targetEntity.Transform.Position, AttackVisuals.For(weapon));
        }

        /// <inheritdoc/>
        public IEnumerator EndPhysicalAttack(IBattleAlly attacker)
            => JumpHomeIfJumped(GetEntityForAlly(attacker));

        /// <inheritdoc/>
        public IEnumerator EndSkillEffect(ICombatant caster)
            => JumpHomeIfJumped(GetEntityForCombatant(caster));

        /// <summary>Jumps <paramref name="entity"/> home if it is the mercenary standing beside the hero.</summary>
        private IEnumerator JumpHomeIfJumped(Entity entity)
        {
            if (_jumpedEntity == null || entity != _jumpedEntity)
                return null;
            if (entity.IsDestroyed)
            {
                _jumpedEntity = null;
                return null;
            }
            return JumpHomeRoutine(entity);
        }

        /// <summary>
        /// Where a melee mercenary lands to attack: half a tile beside the hero, on its own side. False (no
        /// jump) for the hero, a missing hero entity, or a mercenary already standing there.
        /// </summary>
        private bool TryPlanFrontLineJump(Entity attackerEntity, bool isMercenary, out Vector2 landing, out Vector2 offset)
        {
            landing = attackerEntity.Transform.Position;
            offset = Vector2.Zero;
            if (!isMercenary || _heroComponent?.Entity == null)
                return false;
            var from = attackerEntity.Transform.Position;
            var heroPos = _heroComponent.Entity.Transform.Position;
            float side = from.X < heroPos.X ? -1f : 1f;
            landing = new Vector2(heroPos.X + side * GameConfig.MercAttackSideOffset, heroPos.Y);
            offset = landing - from;
            return offset.LengthSquared() >= 4f;
        }

        private IEnumerator PhysicalAttackRoutine(IBattleAlly attacker, Entity attackerEntity, Vector2 targetPos, AttackVisual visual)
        {
            var from = attackerEntity.Transform.Position;

            // A melee mercenary jumps to the front line first
            if (!AttackVisuals.IsRanged(visual)
                && TryPlanFrontLineJump(attackerEntity, attacker is LiveMercenaryAlly, out var landing, out var offset))
            {
                _jumpedEntity = attackerEntity;
                _jumpedOffset = offset;
                _jumpedFacing = attackerEntity.GetComponent<ActorFacingComponent>()?.Facing;
                yield return JumpArc(attackerEntity, Vector2.Zero, offset, returning: false);
                from = landing;
            }

            FaceEntityFrom(attackerEntity, from, targetPos);

            // Melee effort: a quick push toward the target and a snap back while the effect plays
            if (!AttackVisuals.IsRanged(visual))
                LungeToward(attackerEntity, from, targetPos);

            IEnumerator visualRoutine;
            if (visual == AttackVisual.Arrow)
                visualRoutine = AttackEffects.FlyArrow(Core.Scene, from, targetPos);
            else if (visual == AttackVisual.Rod)
                visualRoutine = AttackEffects.PlayRod(Core.Scene, from, targetPos);
            else
                visualRoutine = AttackEffects.PlayMelee(Core.Scene, visual, from, targetPos);
            if (visualRoutine != null)
                yield return visualRoutine;
        }

        private IEnumerator JumpHomeRoutine(Entity entity)
        {
            var offset = _jumpedOffset;
            yield return JumpArc(entity, offset, Vector2.Zero, returning: true);
            RestoreJumpedFacing(entity);
            _jumpedEntity = null;
            _jumpedOffset = Vector2.Zero;
        }

        /// <summary>
        /// Render-only parabolic jump of an ally's sprite from one offset to another (relative to home). The
        /// entity, its tile and its AI never move. Its length depends only on the distance and holds while the
        /// game is paused, so the battle's tick count is the same live and in replays.
        /// </summary>
        private static IEnumerator JumpArc(Entity entity, Vector2 fromOffset, Vector2 toOffset, bool returning)
        {
            var delta = toOffset - fromOffset;
            float duration = Mathf.Clamp(delta.Length() / GameConfig.MercAttackJumpSpeed,
                GameConfig.MercAttackJumpMinDuration, GameConfig.MercAttackJumpMaxDuration);

            AttackGestureComponent.Cancel(entity); // a jump owns the sprite offset from here
            var jump = entity.GetComponent<HeroJumpComponent>();
            bool posed = jump != null && jump.BeginAirbornePose(DirectionOf(delta), keepRestOffset: returning);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += UnpausedDelta();
                if (!posed || entity.IsDestroyed)
                    continue;
                float t = Mathf.Clamp01(elapsed / duration);
                var at = Vector2.Lerp(fromOffset, toOffset, t);
                jump.SetAirborneOffset(at.X, at.Y, GameConfig.MercAttackJumpHeight * 4f * t * (1f - t));
            }

            if (posed && !entity.IsDestroyed)
                jump.EndAirbornePose(toOffset.X, toOffset.Y);
        }

        /// <summary>Turns a mercenary back to the way it faced before its attack jump.</summary>
        private void RestoreJumpedFacing(Entity entity)
        {
            if (_jumpedFacing.HasValue && !entity.IsDestroyed)
                entity.GetComponent<ActorFacingComponent>()?.SetFacing(_jumpedFacing.Value);
            _jumpedFacing = null;
        }

        /// <summary>Puts a still-displaced mercenary back home at once (battle torn down mid-attack).</summary>
        private void ResetJumpedAlly()
        {
            if (_jumpedEntity == null)
                return;
            if (!_jumpedEntity.IsDestroyed)
            {
                _jumpedEntity.GetComponent<HeroJumpComponent>()?.EndAirbornePose(0f, 0f);
                RestoreJumpedFacing(_jumpedEntity);
            }
            _jumpedFacing = null;
            _jumpedEntity = null;
            _jumpedOffset = Vector2.Zero;
        }

        private static Direction DirectionOf(Vector2 delta)
        {
            if (System.Math.Abs(delta.X) >= System.Math.Abs(delta.Y))
                return delta.X < 0 ? Direction.Left : Direction.Right;
            return delta.Y < 0 ? Direction.Up : Direction.Down;
        }

        /// <summary>The fixed step's delta, or zero while the game is paused.</summary>
        private static float UnpausedDelta()
        {
            var pauseService = Core.Services.GetService<PauseService>();
            return pauseService?.IsPaused == true ? 0f : Time.DeltaTime;
        }

        /// <summary>Reused list of the hit enemies' positions, primary first (no allocation per skill).</summary>
        private readonly List<Vector2> _targetPositions = new List<Vector2>(8);

        /// <summary>Where a skill's main effect plays.</summary>
        private enum EffectPlace : byte
        {
            /// <summary>On the primary target.</summary>
            Target,
            /// <summary>In front of the caster toward the target (<see cref="GameConfig.AttackEffectReach"/>), like a weapon swing.</summary>
            Reach,
            /// <summary>At the center of every hit enemy.</summary>
            GroupCenter,
            /// <summary>On the caster (where its sprite stands, after any jump).</summary>
            Self,
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Adding a skill visual = an animation in SkillsStencils.atlas ("Skill" + name, cropped with its anchor
        /// as the origin), a <see cref="SkillEffect"/> member, and a case here (see docs/ParticleEffects.md).
        /// Melee skills make a mercenary jump beside the hero first; <see cref="EndSkillEffect"/> jumps it back.
        /// Area skills strike each hit enemy with a detached hit effect partway through the area effect.
        /// </remarks>
        public IEnumerator ShowSkillEffectOnMonsters(ICombatant caster, ISkill skill,
            IEnemy primaryTarget, List<IEnemy> surroundingTargets)
        {
            var casterEntity = GetEntityForCombatant(caster);
            var targetEntity = GetEntityForEnemy(primaryTarget);
            if (casterEntity == null || targetEntity == null)
                return null;
            if (caster is Hero)
                SetHeroFocus(targetEntity);
            var scene = Core.Scene;
            var from = casterEntity.Transform.Position;
            var targetPos = targetEntity.Transform.Position;
            CollectTargetPositions(targetPos, surroundingTargets);
            var center = GetCenterOfEnemyGroup(primaryTarget, surroundingTargets) ?? targetPos;

            switch (skill.Id)
            {
                // ── Mage ──
                case "mage.fire":
                case "synergy.dragon_bolt":
                    return BoltRoutine(scene, SkillEffect.Fireball, from, targetPos, SkillEffect.FireBlast);
                case "mage.firestorm":
                    return Magic(SkillEffect.MeteorShower, center, GameConfig.SkillMeteorShowerDuration,
                        SkillEffect.Burn, GameConfig.SkillBurnDuration);

                // ── Archer ──
                case "archer.power_shot":
                case "synergy.piercing_arrow":
                    return ArrowRoutine(scene, from, targetPos, SkillEffect.ShotImpact);
                case "synergy.poison_arrow":
                    return ArrowRoutine(scene, from, targetPos, SkillEffect.PoisonHit);
                case "synergy.lightshot":
                    return ArrowRoutine(scene, from, targetPos, SkillEffect.HolyBurst);
                case "synergy.ki_arrow":
                    return ArrowRoutine(scene, from, targetPos, SkillEffect.KiBurst);
                case "archer.volley":
                    FaceEntity(casterEntity, targetPos);
                    return AttackEffects.FlyVolley(scene, from, _targetPositions,
                        SkillEffect.ShotImpact, GameConfig.SkillTargetHitDuration);
                case "synergy.arrow_flurry":
                    FaceEntity(casterEntity, targetPos);
                    return FlurryRoutine(scene, from);
                case "synergy.elemental_volley":
                    FaceEntity(casterEntity, targetPos);
                    return AttackEffects.FlyVolley(scene, from, _targetPositions,
                        SkillEffect.FireBlast, GameConfig.SkillTargetHitDuration);

                // ── Knight ──
                case "knight.spin_slash":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.SpinSlash, EffectPlace.Self,
                        GameConfig.SkillAreaEffectDuration, false, SkillEffect.FlashStrike, spin: true);
                case "knight.heavy_strike":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.HeavyStrike, EffectPlace.Target,
                        GameConfig.SkillHitEffectDuration, false, gesture: MeleeGesture.LeapStrike);

                // ── Monk ──
                case "monk.roundhouse":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.Roundhouse, EffectPlace.GroupCenter,
                        GameConfig.SkillAreaEffectDuration, false, SkillEffect.ShotImpact, gesture: MeleeGesture.HalfTurn);
                case "monk.flaming_fist":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.FlamingFist, EffectPlace.Target,
                        GameConfig.SkillHitEffectDuration, false);

                // ── Thief ──
                case "thief.sneak_attack":
                case "synergy.sneak_punch":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.SneakAttack, EffectPlace.Target,
                        GameConfig.SkillHitEffectDuration, false);

                // ── Synergy melee ──
                case "synergy.holy_strike":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.HolyStrike, EffectPlace.Reach,
                        GameConfig.SkillHitEffectDuration, true);
                case "synergy.iaido_slash":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.Iaido, EffectPlace.Reach,
                        GameConfig.SkillHitEffectDuration, true, gesture: MeleeGesture.SlideDash);
                case "synergy.flash_strike":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.FlashStrike, EffectPlace.Reach,
                        GameConfig.SkillHitEffectDuration, true);
                case "synergy.shadow_slash":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.ShadowSlash, EffectPlace.Reach,
                        GameConfig.SkillHitEffectDuration, true);
                case "synergy.sacred_blade":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.SacredBlade, EffectPlace.Reach,
                        GameConfig.SkillHitEffectDuration, true);
                case "synergy.spellblade":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.Spellblade, EffectPlace.Reach,
                        GameConfig.SkillHitEffectDuration, true);
                case "synergy.dragon_claw":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.DragonClaw, EffectPlace.Target,
                        GameConfig.SkillHitEffectDuration, false);
                case "synergy.sacred_strike":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.HolyBurst, EffectPlace.Target,
                        GameConfig.SkillHitEffectDuration, false);
                case "synergy.dragon_kick":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.Roundhouse, EffectPlace.Target,
                        GameConfig.SkillAreaEffectDuration, false, spin: true);
                case "synergy.energy_burst":
                    return Melee(caster, casterEntity, targetPos, SkillEffect.KiBurst, EffectPlace.GroupCenter,
                        GameConfig.SkillAreaEffectDuration, false, SkillEffect.KiBurst);

                // ── Synergy magic ──
                case "synergy.meteor":
                    return Magic(SkillEffect.Meteor, center, GameConfig.SkillMeteorShowerDuration,
                        SkillEffect.Burn, GameConfig.SkillBurnDuration);
                case "synergy.elemental_storm":
                    return Magic(SkillEffect.ElementalStorm, center, GameConfig.SkillMeteorShowerDuration,
                        SkillEffect.Burn, GameConfig.SkillBurnDuration);
                case "synergy.shadow_bolt":
                    return BoltRoutine(scene, SkillEffect.ShadowBolt, from, targetPos, SkillEffect.SneakAttack);
                case "synergy.blitz":
                    return AttackEffects.PlaySkill(scene, SkillEffect.Blitz, targetPos, GameConfig.SkillAreaEffectDuration);
                case "synergy.life_leech":
                    return AttackEffects.PlaySkill(scene, SkillEffect.LifeLeech, targetPos, GameConfig.SkillAreaEffectDuration);

                default:
                    return null;
            }
        }

        /// <summary>Fills <see cref="_targetPositions"/> with the primary target's position, then each surrounding target's.</summary>
        private void CollectTargetPositions(Vector2 primaryPos, List<IEnemy> surroundingTargets)
        {
            _targetPositions.Clear();
            _targetPositions.Add(primaryPos);
            if (surroundingTargets == null)
                return;
            for (int i = 0; i < surroundingTargets.Count; i++)
            {
                var e = GetEntityForEnemy(surroundingTargets[i]);
                if (e != null) _targetPositions.Add(e.Transform.Position);
            }
        }

        /// <summary>A magic area skill: the area effect over the group, then a hit on each target partway through.</summary>
        private IEnumerator Magic(SkillEffect area, Vector2 center, float duration, SkillEffect hit, float hitDuration)
            => AttackEffects.PlayAreaWithHits(Core.Scene, area, center, duration, hit, _targetPositions, hitDuration);

        /// <summary>
        /// A melee skill: a mercenary jumps beside the hero first, faces the target, then the effect plays at
        /// <paramref name="place"/> (rotated toward the target when <paramref name="directional"/>). An area melee
        /// skill strikes each target with <paramref name="hit"/> partway through (pointing away from the caster).
        /// With <paramref name="spin"/> the caster turns through the four directions while the effect plays,
        /// then faces the target again.
        /// </summary>
        private IEnumerator Melee(ICombatant caster, Entity casterEntity, Vector2 targetPos, SkillEffect effect,
            EffectPlace place, float duration, bool directional, SkillEffect hit = SkillEffect.None, bool spin = false,
            MeleeGesture gesture = MeleeGesture.Lunge)
        {
            var from = casterEntity.Transform.Position;
            if (TryPlanFrontLineJump(casterEntity, caster is Mercenary, out var landing, out var offset))
            {
                _jumpedEntity = casterEntity;
                _jumpedOffset = offset;
                _jumpedFacing = casterEntity.GetComponent<ActorFacingComponent>()?.Facing;
                yield return JumpArc(casterEntity, Vector2.Zero, offset, returning: false);
                from = landing;
            }
            FaceEntityFrom(casterEntity, from, targetPos);

            var delta = targetPos - from;
            var dir = delta.LengthSquared() > 0.0001f ? Vector2.Normalize(delta) : Vector2.UnitX;
            Vector2 effectPos;
            switch (place)
            {
                case EffectPlace.Reach: effectPos = from + dir * GameConfig.AttackEffectReach; break;
                case EffectPlace.GroupCenter: effectPos = GroupCenterOfTargets(); break;
                case EffectPlace.Self: effectPos = from; break;
                default: effectPos = targetPos; break;
            }
            float rotation = directional ? System.MathF.Atan2(delta.Y, delta.X) : 0f;

            IEnumerator play = hit != SkillEffect.None
                ? AttackEffects.PlayAreaWithHits(Core.Scene, effect, effectPos, duration, hit, _targetPositions,
                    GameConfig.SkillTargetHitDuration, from)
                : AttackEffects.PlaySkill(Core.Scene, effect, effectPos, duration, rotation);
            if (play == null)
                yield break;

            if (!spin)
            {
                switch (gesture)
                {
                    case MeleeGesture.SlideDash:
                        yield return SlideDashThen(casterEntity, dir, play);
                        break;
                    case MeleeGesture.LeapStrike:
                        yield return LeapStrikeThen(casterEntity, dir, play);
                        break;
                    case MeleeGesture.HalfTurn:
                        LungeToward(casterEntity, from, targetPos);
                        yield return HalfTurnThen(casterEntity, from, targetPos, play);
                        break;
                    default:
                        LungeToward(casterEntity, from, targetPos);
                        yield return play;
                        break;
                }
                yield break;
            }

            // Spin in place: step the effect by hand and turn the caster through the four directions
            var facing = casterEntity.GetComponent<ActorFacingComponent>();
            float elapsed = 0f;
            int lastStep = -1;
            while (play.MoveNext())
            {
                int step = (int)(elapsed / GameConfig.SpinSlashFacingStep);
                if (step != lastStep && facing != null && !casterEntity.IsDestroyed)
                {
                    facing.SetFacing(SpinOrder[step % SpinOrder.Length]);
                    lastStep = step;
                }
                yield return play.Current;
                elapsed += UnpausedDelta();
            }
            if (!casterEntity.IsDestroyed)
                FaceEntityFrom(casterEntity, from, targetPos);
        }

        /// <summary>How a melee attacker's body moves with the attack (render-only).</summary>
        private enum MeleeGesture : byte
        {
            /// <summary>A quick push toward the target and a snap back, while the effect plays.</summary>
            Lunge,
            /// <summary>Iaido: slide back, hold, dash past where it stood; the slice plays from the dash.</summary>
            SlideDash,
            /// <summary>Heavy Strike: leap toward the target and slam down; the effect starts near impact.</summary>
            LeapStrike,
            /// <summary>Roundhouse: a half-circle turn into the kick (one side, the target, the other side).</summary>
            HalfTurn,
        }

        /// <summary>
        /// Steps <paramref name="play"/> by hand while <paramref name="entity"/> turns a half circle through the
        /// target: the side a quarter turn counter-clockwise of it, the target, the side a quarter turn clockwise
        /// (facing north: left, up, right), one every <see cref="GameConfig.RoundhouseFacingStep"/>. It then faces
        /// the target for the rest of the effect. Takes exactly as long as <paramref name="play"/>.
        /// </summary>
        private static IEnumerator HalfTurnThen(Entity entity, Vector2 from, Vector2 targetPos, IEnumerator play)
        {
            var facing = entity.GetComponent<ActorFacingComponent>();
            var toward = DirectionOf(targetPos - from);
            HalfTurnSweep[0] = QuarterTurnCounterClockwise(toward);
            HalfTurnSweep[1] = toward;
            HalfTurnSweep[2] = QuarterTurnClockwise(toward);
            float elapsed = 0f;
            int lastStep = -1;
            while (play.MoveNext())
            {
                int step = (int)(elapsed / GameConfig.RoundhouseFacingStep);
                if (step != lastStep && facing != null && !entity.IsDestroyed)
                {
                    facing.SetFacing(step < HalfTurnSweep.Length ? HalfTurnSweep[step] : toward);
                    lastStep = step;
                }
                yield return play.Current;
                elapsed += UnpausedDelta();
            }
            if (facing != null && !entity.IsDestroyed)
                facing.SetFacing(toward);
        }

        /// <summary>Reused facing sequence for <see cref="HalfTurnThen"/>.</summary>
        private static readonly Direction[] HalfTurnSweep = new Direction[3];

        /// <summary>The cardinal direction a quarter turn counter-clockwise on screen (Up → Left).</summary>
        private static Direction QuarterTurnCounterClockwise(Direction d)
        {
            switch (d)
            {
                case Direction.Up: return Direction.Left;
                case Direction.Left: return Direction.Down;
                case Direction.Down: return Direction.Right;
                default: return Direction.Up;
            }
        }

        /// <summary>The cardinal direction a quarter turn clockwise on screen (Up → Right).</summary>
        private static Direction QuarterTurnClockwise(Direction d)
        {
            switch (d)
            {
                case Direction.Up: return Direction.Right;
                case Direction.Right: return Direction.Down;
                case Direction.Down: return Direction.Left;
                default: return Direction.Up;
            }
        }

        /// <summary>Starts the melee lunge on <paramref name="entity"/> toward <paramref name="targetPos"/> (detached).</summary>
        private static void LungeToward(Entity entity, Vector2 from, Vector2 targetPos)
        {
            var d = targetPos - from;
            if (d.LengthSquared() < 0.0001f)
                return;
            AttackGestureComponent.Lunge(entity, Vector2.Normalize(d), GameConfig.AttackLungeDistance,
                GameConfig.AttackLungeDuration);
        }

        /// <summary>
        /// Iaido: the caster's sprite slides back along <paramref name="dir"/>, holds, then dashes past where it
        /// stood, still facing the target; the slice (<paramref name="play"/>) plays from the dash while the
        /// sprite eases home. Adds the slide, hold and dash to the skill's time.
        /// </summary>
        private static IEnumerator SlideDashThen(Entity entity, Vector2 dir, IEnumerator play)
        {
            var body = entity.GetComponent<MultiSpriteAnimator>();
            AttackGestureComponent.Cancel(entity);
            var rest = body != null ? body.LocalOffset : Vector2.Zero;
            var back = -dir * GameConfig.IaidoSlideDistance;
            var past = dir * GameConfig.IaidoDashDistance;
            AfterimageTrailComponent.Begin(entity);

            float elapsed = 0f;
            while (elapsed < GameConfig.IaidoSlideDuration)
            {
                yield return null;
                elapsed += UnpausedDelta();
                float t = Mathf.Clamp01(elapsed / GameConfig.IaidoSlideDuration);
                if (body != null && !entity.IsDestroyed)
                    body.LocalOffset = rest + back * (t * (2f - t));
            }
            elapsed = 0f;
            while (elapsed < GameConfig.IaidoHoldDuration)
            {
                yield return null;
                elapsed += UnpausedDelta();
            }
            elapsed = 0f;
            while (elapsed < GameConfig.IaidoDashDuration)
            {
                yield return null;
                elapsed += UnpausedDelta();
                float t = Mathf.Clamp01(elapsed / GameConfig.IaidoDashDuration);
                if (body != null && !entity.IsDestroyed)
                    body.LocalOffset = rest + Vector2.Lerp(back, past, t);
            }

            if (body != null && !entity.IsDestroyed)
            {
                body.LocalOffset = rest + past;
                AttackGestureComponent.Return(entity, past, GameConfig.AttackGestureReturnDuration);
            }
            AfterimageTrailComponent.End(entity);
            yield return play;
        }

        /// <summary>
        /// Heavy Strike: the caster leaps along <paramref name="dir"/> (jump pose and shadow), rising slowly and
        /// slamming down; the effect (<paramref name="play"/>) starts at <see cref="GameConfig.HeavyStrikeEffectAt"/>
        /// of the leap so it lands with the blow, then the sprite eases home while the effect finishes. A
        /// mercenary standing beside the hero leaps from there.
        /// </summary>
        private IEnumerator LeapStrikeThen(Entity entity, Vector2 dir, IEnumerator play)
        {
            AttackGestureComponent.Cancel(entity);
            var jump = entity.GetComponent<HeroJumpComponent>();
            bool fromJumpSpot = entity == _jumpedEntity;
            var baseOffset = fromJumpSpot ? _jumpedOffset : Vector2.Zero;
            var facing = entity.GetComponent<ActorFacingComponent>()?.Facing ?? DirectionOf(dir);
            bool posed = jump != null && jump.BeginAirbornePose(facing, keepRestOffset: fromJumpSpot);
            var forward = dir * GameConfig.HeavyStrikeLeapForward;
            AfterimageTrailComponent.Begin(entity);

            bool effectRunning = false, effectDone = false;
            float elapsed = 0f;
            while (elapsed < GameConfig.HeavyStrikeLeapDuration)
            {
                yield return null;
                elapsed += UnpausedDelta();
                float t = Mathf.Clamp01(elapsed / GameConfig.HeavyStrikeLeapDuration);
                if (posed && !entity.IsDestroyed)
                {
                    float peak = GameConfig.HeavyStrikeLeapPeak;
                    float u = t < peak ? (peak - t) / peak : (t - peak) / (1f - peak);
                    float height = GameConfig.HeavyStrikeLeapHeight * (1f - u * u);
                    var at = baseOffset + forward * t;
                    jump.SetAirborneOffset(at.X, at.Y, height);
                }
                if (!effectRunning && t >= GameConfig.HeavyStrikeEffectAt)
                    effectRunning = true;
                if (effectRunning && !effectDone)
                    effectDone = !play.MoveNext();
            }

            if (posed && !entity.IsDestroyed)
            {
                var landed = baseOffset + forward;
                jump.EndAirbornePose(landed.X, landed.Y);
                AttackGestureComponent.Return(entity, forward, GameConfig.AttackGestureReturnDuration);
            }
            AfterimageTrailComponent.End(entity);
            if (!effectRunning)
                effectDone = !play.MoveNext();
            while (!effectDone)
            {
                yield return play.Current;
                effectDone = !play.MoveNext();
            }
        }

        /// <summary>Average of <see cref="_targetPositions"/> (the primary target when there are no others).</summary>
        private Vector2 GroupCenterOfTargets()
        {
            if (_targetPositions.Count == 0)
                return Vector2.Zero;
            var sum = Vector2.Zero;
            for (int i = 0; i < _targetPositions.Count; i++)
                sum += _targetPositions[i];
            return sum / _targetPositions.Count;
        }

        /// <summary>Clockwise turn order for a spinning attack (screen view: down, left, up, right).</summary>
        private static readonly Direction[] SpinOrder = { Direction.Down, Direction.Left, Direction.Up, Direction.Right };

        /// <summary>A bolt (fireball, shadow bolt) flies to the target, then <paramref name="impact"/> bursts there.</summary>
        private static IEnumerator BoltRoutine(Scene scene, SkillEffect bolt, Vector2 from, Vector2 to, SkillEffect impact)
        {
            yield return AttackEffects.FlySkill(scene, bolt, from, to,
                GameConfig.SkillFireballSpeed, GameConfig.SkillFireballMinFlight, GameConfig.SkillFireballMaxFlight);
            yield return AttackEffects.PlaySkill(scene, impact, to, GameConfig.SkillFireBlastDuration);
        }

        /// <summary>An arrow flies to the target, then <paramref name="impact"/> plays there.</summary>
        private static IEnumerator ArrowRoutine(Scene scene, Vector2 from, Vector2 to, SkillEffect impact)
        {
            yield return AttackEffects.FlyArrow(scene, from, to);
            var d = to - from;
            yield return AttackEffects.PlaySkill(scene, impact, to, GameConfig.SkillShotImpactDuration,
                impact == SkillEffect.ShotImpact ? System.MathF.Atan2(d.Y, d.X) : 0f);
        }

        /// <summary>Arrow Flurry: two volleys back to back, an impact on each target as each arrow lands.</summary>
        private IEnumerator FlurryRoutine(Scene scene, Vector2 from)
        {
            yield return AttackEffects.FlyVolley(scene, from, _targetPositions, SkillEffect.ShotImpact, GameConfig.SkillTargetHitDuration);
            yield return AttackEffects.FlyVolley(scene, from, _targetPositions, SkillEffect.ShotImpact, GameConfig.SkillTargetHitDuration);
        }

        /// <summary>Averages the entity positions of the primary and surrounding targets;
        /// null when no target has a live entity.</summary>
        private Vector2? GetCenterOfEnemyGroup(IEnemy primaryTarget, List<IEnemy> surroundingTargets)
        {
            var sum = Vector2.Zero;
            int count = 0;

            var primaryEntity = GetEntityForEnemy(primaryTarget);
            if (primaryEntity != null) { sum += primaryEntity.Position; count++; }

            if (surroundingTargets != null)
            {
                for (int i = 0; i < surroundingTargets.Count; i++)
                {
                    var e = GetEntityForEnemy(surroundingTargets[i]);
                    if (e != null) { sum += e.Position; count++; }
                }
            }

            return count > 0 ? sum / count : (Vector2?)null;
        }

        private Entity GetEntityForCombatant(ICombatant combatant)
        {
            if (combatant is Hero)
                return _heroComponent.Entity;
            if (combatant is Mercenary merc && _mercMap.TryGetValue(merc, out var m))
                return m.e;
            return null;
        }

        /// <inheritdoc/>
        public IEnumerator ShowHealOnAlly(IBattleAlly ally, int amount)
        {
            var entity = GetEntityForAlly(ally);
            if (entity == null) return null;
            // The heal swirl already played at the cast (ShowSkillCast), healed or not
            return ShowDamageDigitOnEntity(entity, amount, BouncyDigitComponent.HealDigitColor);
        }

        /// <inheritdoc/>
        public void ShowSkillCast(ICombatant caster, ISkill skill, IBattleAlly target)
        {
            // Every healing or cleansing skill shows the heal swirl on its target at the cast, even when the
            // target is already at full HP (Purify still clears debuffs then)
            if (skill.HPRestoreAmount > 0 || skill.CleansesDebuffs)
                PlayCastOn(GetEntityForAlly(target), SkillEffect.Heal, GameConfig.SkillHealEffectDuration);

            switch (skill.Id)
            {
                // Defense buffs on the target ally (Aura Heal and Soul Ward also get the heal swirl above)
                case "priest.defup":
                case "synergy.aura_heal":
                case "synergy.soul_ward":
                    PlayCastOn(GetEntityForAlly(target) ?? GetEntityForCombatant(caster), SkillEffect.DefenseUp,
                        GameConfig.SkillBuffEffectDuration);
                    break;
                // Self-buffs on the caster
                case "thief.vanish":
                    PlayCastOn(GetEntityForCombatant(caster), SkillEffect.Vanish, GameConfig.SkillVanishEffectDuration);
                    MakeSeeThrough(caster, GetEntityForCombatant(caster));
                    break;
                case "synergy.smoke_bomb":
                    PlayCastOn(GetEntityForCombatant(caster), SkillEffect.Vanish, GameConfig.SkillVanishEffectDuration);
                    break;
                case "synergy.fade":
                    PlayCastOn(GetEntityForCombatant(caster), SkillEffect.Fade, GameConfig.SkillVanishEffectDuration);
                    break;
                case "synergy.ki_cloak":
                    PlayCastOn(GetEntityForCombatant(caster), SkillEffect.KiCloak, GameConfig.SkillBuffEffectDuration);
                    break;
            }
        }

        /// <summary>Characters drawn see-through after Vanish, with the composite tint to restore.</summary>
        private readonly List<(ICombatant Combatant, MultiSpriteAnimator Body, Color Tint)> _seeThrough =
            new List<(ICombatant, MultiSpriteAnimator, Color)>();

        /// <summary>
        /// Draws <paramref name="entity"/>'s body at <see cref="GameConfig.VanishAlpha"/> while
        /// <paramref name="combatant"/>'s Untargetable buff lasts (<see cref="RefreshSeeThrough"/> un-fades it).
        /// Render-only; the composite tint is recorded, so replays show it.
        /// </summary>
        private void MakeSeeThrough(ICombatant combatant, Entity entity)
        {
            var body = entity?.GetComponent<MultiSpriteAnimator>();
            if (combatant == null || body == null || entity.IsDestroyed)
                return;
            for (int i = 0; i < _seeThrough.Count; i++)
            {
                if (_seeThrough[i].Body == body)
                    return;
            }
            _seeThrough.Add((combatant, body, body.Color));
            body.Color = body.Color * GameConfig.VanishAlpha; // premultiplied: scales colour and alpha together
        }

        /// <summary>Un-fades every see-through character whose Untargetable buff has run out.</summary>
        private void RefreshSeeThrough()
        {
            for (int i = _seeThrough.Count - 1; i >= 0; i--)
            {
                if (_seeThrough[i].Combatant.GetBuffTotal(BuffType.Untargetable) > 0)
                    continue;
                RestoreSeeThroughAt(i);
            }
        }

        /// <summary>Restores every see-through character's tint (battle over).</summary>
        private void RestoreSeeThrough()
        {
            for (int i = _seeThrough.Count - 1; i >= 0; i--)
                RestoreSeeThroughAt(i);
        }

        private void RestoreSeeThroughAt(int i)
        {
            var body = _seeThrough[i].Body;
            if (body?.Entity != null && !body.Entity.IsDestroyed)
                body.Color = _seeThrough[i].Tint;
            _seeThrough.RemoveAt(i);
        }

        private static void PlayCastOn(Entity entity, SkillEffect effect, float duration)
        {
            if (entity != null)
                AttackEffects.PlaySkillOn(entity, effect, duration);
        }

        /// <inheritdoc/>
        public IEnumerator ShowItemHealOnAlly(IBattleAlly ally, int amount, RolePlayingFramework.Equipment.Consumable consumable)
        {
            var entity = GetEntityForAlly(ally);
            if (entity == null) return null;
            Core.GetGlobalManager<ParticleEffectManager>()?.SpawnPotionHealEffect(consumable, entity);
            return ShowDamageDigitOnEntity(entity, amount, BouncyDigitComponent.HealDigitColor);
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
            Services.Replay.ReplayBattleTrace.Add($"attack {evt.ActorName} {evt.Action} -> {evt.TargetName} dmg={evt.Damage} hp={evt.HpBefore}->{evt.HpAfter} killed={evt.Killed} missed={evt.Missed}");

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
            Services.Replay.ReplayBattleTrace.Add($"buff {evt.CasterName} {evt.Source} -> {evt.TargetName} {evt.BuffTypeName} x{evt.Magnitude} turns={evt.DurationTurns}");
        }

        /// <inheritdoc/>
        /// <remarks>Analytics only — per-action threat lines would flood the console.</remarks>
        public void OnThreatGenerated(in BattleThreatEvent evt)
        {
            PitHero.Services.Analytics.AnalyticsService.LogThreat(
                evt.ActorName, evt.ActorType, evt.Source, evt.Amount, evt.Total);
            Services.Replay.ReplayBattleTrace.Add($"threat {evt.ActorName} {evt.Source} +{evt.Amount:0.#} = {evt.Total:0.#}");
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Publishes the target for the HUD tint (polled by MainGameScene.UpdateHeroHUD) and
        /// prints one console line per change. Null clears the tint silently.
        /// </remarks>
        public void OnThreatTargetChanged(IBattleAlly target)
        {
            HeroStateMachine.CurrentThreatTarget = target?.Combatant;
            if (target?.Combatant == null) return;

            var evtSvc = Core.Services.GetService<GameEventService>();
            evtSvc?.EmitLocalized(UITextKey.ConsoleThreatTarget,
                (target.Combatant.Name, GameConfig.ConsoleColorHeroName));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Speech bubble ("Over here!"), floating "Provoke!" label, one console line, and the
        /// <c>provoke</c> analytics row. The threat gain itself already went through OnThreatGenerated.
        /// </remarks>
        public IEnumerator OnProvoke(IBattleAlly tank, in BattleProvokeEvent evt)
        {
            PitHero.Services.Analytics.AnalyticsService.LogProvoke(
                evt.TankName, evt.TankType, evt.ProtectedName, evt.Reaction, evt.MpSpent, evt.ThreatTotal);

            var evtSvc = Core.Services.GetService<GameEventService>();
            if (evtSvc != null)
            {
                if (evt.ProtectedName != null)
                    evtSvc.EmitLocalized(UITextKey.ConsoleBattleProvoke,
                        (evt.TankName, GameConfig.ConsoleColorHeroName),
                        (evt.ProtectedName, GameConfig.ConsoleColorHeroName));
                else
                    evtSvc.EmitLocalized(UITextKey.ConsoleBattleProvokeSolo,
                        (evt.TankName, GameConfig.ConsoleColorHeroName));
            }

            var entity = GetEntityForAlly(tank);
            if (entity == null) return null;
            SpeechBubbleDialogue.SayProvoke(entity);
            AttackEffects.PlaySkillOn(entity, SkillEffect.Provoke, GameConfig.SkillProvokeEffectDuration);
            return ShowTextOnEntity(entity, "Provoke!", GameConfig.ThreatTargetHudTint);
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
                    (consumable.DisplayName, RarityUtils.GetRarityColor(consumable.Rarity)),
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
                gameState.Stats.MonstersDefeated++;
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
                SpeechBubbleDialogue.SayBossDefeated(_heroComponent.Entity);
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

                // Biome main boss drops an epic chest at its tile (#382). This runs mid-battle
                // inside the engine coroutine, so it must consume ZERO Nez.Random — the epic
                // draw uses LootShuffleService's private System.Random.
                if (enemy.EnemyId == RolePlayingFramework.Enemies.EnemyId.MoltenTitan)
                    SpawnBossEpicChest(enemy);
            }

            // Console event for monster death (after recruit, matching original line order)
            var evtSvc = Core.Services.GetService<GameEventService>();
            if (evtSvc != null)
                evtSvc.EmitLocalized(
                    enemy.IsBoss ? EventPriority.High : EventPriority.Normal,
                    UITextKey.ConsoleMonsterDied,
                    (evtSvc.MonsterName(enemy.Name), GameConfig.ConsoleColorEnemyName));
        }

        /// <summary>
        /// Spawns the epic treasure chest for a biome main-boss kill (#382). The boss entity
        /// is still alive here (fade/destroy happens later in ShowMonsterDeath), so its tile
        /// anchors the chest. GOAP flags are reset so the post-battle replan targets the new
        /// chest. Must consume zero Nez.Random — this runs mid-battle (RNG contract).
        /// </summary>
        private void SpawnBossEpicChest(IEnemy enemy)
        {
            var scene = Core.Scene;
            if (scene == null)
                return;

            var lootService = Core.Services.GetService<PitHero.Services.LootShuffleService>();
            if (lootService == null)
                return;

            var bossEntity = GetEntityForEnemy(enemy);
            var anchor = bossEntity != null
                ? bossEntity.Transform.Position
                : _heroComponent.Entity.Transform.Position;
            var tile = new Point((int)(anchor.X / GameConfig.TileSize), (int)(anchor.Y / GameConfig.TileSize));

            var item = lootService.DrawEpicItem();

            // Tier-2+ runs upgrade the epic drop exactly like chest gear (TreasureComponent parity).
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            int pitTier = pitWidthManager?.CurrentPitTier ?? 1;
            if (pitTier >= 2 && item is RolePlayingFramework.Equipment.Gear gear)
            {
                int depthDelta = (pitTier - 1) * Config.BiomeProgressionConfig.MaxBiomeLevel;
                item = RolePlayingFramework.Equipment.Gear.CreateTierScaledCopy(gear, pitTier, depthDelta);
            }

            TreasureSpawner.SpawnTreasureChestAtTile(scene, tile, item);

            // Un-latch ExploredPit and refresh chest adjacency so the post-battle replan
            // (Idle_Enter/Idle_Tick) sees the new CLOSED chest and goes to open it.
            _heroComponent.ExploredPit = false;
            _heroComponent.AdjacentToChest = _heroComponent.CheckAdjacentToChest();

            Debug.Log($"[LiveBattleAdapter] Boss epic chest spawned at ({tile.X},{tile.Y}) containing {item.Name}");
        }

        /// <inheritdoc/>
        public void OnAllyKilled(IBattleAlly ally, IEnemy killer)
        {
            if (ally.IsHero)
            {
                PitHero.Services.Analytics.AnalyticsService.LogCharacterKilled(
                    (Hero)ally.Combatant, killer);
                var heroStats = PitHero.Services.LifetimeStats.Live;
                if (heroStats != null) heroStats.HeroDefeats++;
                Debug.Log($"[LiveBattleAdapter] Hero killed by {killer.Name}.");
            }
            else
            {
                var merc = (Mercenary)ally.Combatant;
                var mercStats = PitHero.Services.LifetimeStats.Live;
                if (mercStats != null) mercStats.MercenaryDeaths++;
                PitHero.Services.Analytics.AnalyticsService.LogCharacterKilled(merc, killer);
                Debug.Log($"[LiveBattleAdapter] {merc.Name} killed by {killer.Name}.");

                if (_mercMap.TryGetValue(merc, out var pair))
                    HandleMercenaryDeath(pair.e, killer.Name);
            }
        }

        // ── IBattleEventSink — audio ─────────────────────────────────────────────────

        /// <inheritdoc/>
        public void PlaySound(BattleSound sound, ICombatant source)
        {
            var sfx = Core.GetGlobalManager<SoundEffectManager>();
            if (sfx == null) return;

            SoundEffectType sfxType;
            switch (sound)
            {
                case BattleSound.Punch:
                    sfxType = SoundEffectType.Punch;
                    break;
                case BattleSound.TakeDamage:
                    sfxType = SoundEffectType.TakeDamage;
                    break;
                case BattleSound.Restorative:
                    sfxType = SoundEffectType.Restorative;
                    break;
                case BattleSound.EnemyDefeat:
                    sfxType = SoundEffectType.EnemyDefeat;
                    break;
                default:
                    return;
            }

            var sourceEntity = source != null ? GetEntityForCombatant(source) : null;
            if (sourceEntity != null)
                sfx.PlaySoundAt(sfxType, sourceEntity.Transform.Position);
            else
                sfx.PlaySound(sfxType);
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
            HeroStateMachine.BattleMonsterEntities = null;
            HeroStateMachine.CurrentThreatTarget = null;

            ResetJumpedAlly();
            ResetLeaptMonster();
            RestoreSeeThrough();
            _magicCaster = null;

            if (_turnIndicatorEntity != null)
                _turnIndicatorEntity.Destroy();

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
            => FaceEntityFrom(entity, entity.Transform.Position, targetPosition);

        /// <summary>Faces an entity toward a target as seen from <paramref name="from"/> (where its sprite stands).</summary>
        private static void FaceEntityFrom(Entity entity, Vector2 from, Vector2 targetPosition)
        {
            entity.GetComponent<ActorFacingComponent>()?.SetFacing(DirectionOf(targetPosition - from));
        }

        /// <summary>The monster the hero is fighting; the whole party faces it between actions.</summary>
        private Entity _heroFocus;

        /// <summary>Makes <paramref name="monsterEntity"/> the hero's focus (the hero just targeted it).</summary>
        private void SetHeroFocus(Entity monsterEntity)
        {
            _heroFocus = monsterEntity;
        }

        /// <summary>True while <paramref name="monsterEntity"/> is a living monster of this battle.</summary>
        private static bool IsLivingMonster(Entity monsterEntity)
        {
            if (monsterEntity == null || monsterEntity.IsDestroyed)
                return false;
            var enemy = monsterEntity.GetComponent<EnemyComponent>()?.Enemy;
            return enemy != null && enemy.CurrentHP > 0;
        }

        /// <summary>
        /// The hero's focus: the monster the hero last targeted, else (before the hero's first action or
        /// once that monster dies) the living monster nearest the hero. Null when none is left.
        /// </summary>
        private Entity CurrentHeroFocus()
        {
            if (IsLivingMonster(_heroFocus))
                return _heroFocus;
            _heroFocus = null;
            var heroEntity = _heroComponent?.Entity;
            if (heroEntity == null)
                return null;
            var heroPos = heroEntity.Transform.Position;
            float best = float.MaxValue;
            for (int i = 0; i < _monsterEntities.Count; i++)
            {
                var m = _monsterEntities[i];
                if (!IsLivingMonster(m))
                    continue;
                float d = Vector2.DistanceSquared(heroPos, m.Transform.Position);
                if (d < best)
                {
                    best = d;
                    _heroFocus = m;
                }
            }
            return _heroFocus;
        }

        /// <summary>Where <paramref name="allyEntity"/> should look: the hero's focus, or straight ahead when there is none.</summary>
        private Vector2 FocusPosition(Entity allyEntity)
        {
            var focus = CurrentHeroFocus();
            return focus != null ? focus.Transform.Position : allyEntity.Transform.Position;
        }

        /// <summary>
        /// Turns the hero and every mercenary in the battle toward the hero's focus. Called at battle start
        /// and at the start of every turn, so anyone who turned for their own action (a mercenary striking
        /// another monster, a healer facing an ally) squares back up. A mercenary standing beside the hero
        /// after a jump faces from where its sprite is.
        /// </summary>
        private void FacePartyTowardHeroFocus()
        {
            var focus = CurrentHeroFocus();
            if (focus == null)
                return;
            var focusPos = focus.Transform.Position;
            var heroEntity = _heroComponent?.Entity;
            if (heroEntity != null && !heroEntity.IsDestroyed)
                FaceEntity(heroEntity, focusPos);
            foreach (var kv in _mercMap)
            {
                var e = kv.Value.e;
                var mc = kv.Value.c;
                if (e == null || e.IsDestroyed || mc == null || !mc.IsHired || !mc.InsidePit)
                    continue;
                if (kv.Key.CurrentHP <= 0)
                    continue;
                var from = e == _jumpedEntity ? e.Transform.Position + _jumpedOffset : e.Transform.Position;
                FaceEntityFrom(e, from, focusPos);
            }
        }

        /// <summary>
        /// Turns a battling monster toward the hero by X alone: left when the hero is to its left, right
        /// when the hero is to its right. Directly above or below (same X) it keeps its current facing.
        /// </summary>
        private void FaceMonsterTowardHero(Entity monsterEntity)
        {
            var heroEntity = _heroComponent?.Entity;
            if (monsterEntity == null || monsterEntity.IsDestroyed || heroEntity == null)
                return;
            float deltaX = heroEntity.Transform.Position.X - monsterEntity.Transform.Position.X;
            if (deltaX == 0f)
                return;
            monsterEntity.GetComponent<ActorFacingComponent>()?.SetFacing(deltaX < 0f ? Direction.Left : Direction.Right);
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

            Core.GetGlobalManager<SoundEffectManager>()?.PlaySoundAt(SoundEffectType.EnemyDefeat, monsterEntity.Transform.Position);

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

            var layers = new HeroAnimationComponent[]
            {
                mercenaryEntity.GetComponent<HeroBodyAnimationComponent>(),
                mercenaryEntity.GetComponent<HeroArmorAnimationComponent>(),
                mercenaryEntity.GetComponent<HeroShirtAnimationComponent>(),
                mercenaryEntity.GetComponent<HeroHeadAnimationComponent>(),
                mercenaryEntity.GetComponent<HeroEyesBaseAnimationComponent>(),
                mercenaryEntity.GetComponent<HeroEyesAnimationComponent>(),
                mercenaryEntity.GetComponent<HeroHairAnimationComponent>()
            };
            var origColors = new Color[layers.Length];
            for (int i = 0; i < layers.Length; i++)
                origColors[i] = layers[i] != null ? layers[i].Color : Color.White;
            // Fade the composite as a whole so its baked pose is reused (issue #459)
            var composite = mercenaryEntity.GetComponent<MultiSpriteAnimator>();
            var origComposite = composite != null ? composite.Color : Color.White;

            const float fadeDuration = 0.5f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService?.IsPaused == true) { yield return null; continue; }
                elapsed += Time.DeltaTime;
                float progress = elapsed < 0f ? 0f : (elapsed > fadeDuration ? 1f : elapsed / fadeDuration);
                byte alpha = (byte)(255 * (1f - progress));
                if (composite != null)
                    composite.Color = new Color(origComposite.R, origComposite.G, origComposite.B, alpha);
                else for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] != null)
                        layers[i].Color = new Color(origColors[i].R, origColors[i].G, origColors[i].B, alpha);
                }
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
