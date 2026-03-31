using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using System.Collections.Generic;
using System.Linq;

namespace PitHero.AI
{
    /// <summary>
    /// Represents a participant in a multi-participant battle
    /// </summary>
    public struct BattleParticipant
    {
        public enum ParticipantType { Hero, Mercenary, Monster }
        
        public ParticipantType Type;
        public HeroComponent HeroComponent;
        public Entity MercenaryEntity;
        public Entity MonsterEntity;
        public float TurnValue;

        public BattleParticipant(HeroComponent hero)
        {
            Type = ParticipantType.Hero;
            HeroComponent = hero;
            MercenaryEntity = null;
            MonsterEntity = null;
            TurnValue = 0f;
        }

        public BattleParticipant(Entity mercenary, bool isMercenary)
        {
            Type = ParticipantType.Mercenary;
            HeroComponent = null;
            MercenaryEntity = mercenary;
            MonsterEntity = null;
            TurnValue = 0f;
        }

        public BattleParticipant(Entity monster)
        {
            Type = ParticipantType.Monster;
            HeroComponent = null;
            MercenaryEntity = null;
            MonsterEntity = monster;
            TurnValue = 0f;
        }
    }

    /// <summary>
    /// Action that causes the hero to attack an adjacent monster
    /// Hero faces the monster, performs attack animation, and defeats the monster
    /// </summary>
    public class AttackMonsterAction : HeroActionBase
    {
        ICoroutine existingMultiParticipantBattleCoroutine;

        private const int DEBUG_DAMAGE_MULT = 1;  //ToDo: Remove for production

        public AttackMonsterAction() : base(GoapConstants.AttackMonster, 3)
        {
            // Preconditions: Hero must be adjacent to a monster
            SetPrecondition(GoapConstants.AdjacentToMonster, true);

            // Postconditions: Monster is defeated, recalculate adjacency
            SetPostcondition(GoapConstants.AdjacentToMonster, false);
        }

        public override bool Execute(HeroComponent hero)
        {
            if (existingMultiParticipantBattleCoroutine != null)
            {
                Debug.Log("[AttackMonster] Multi-participant battle already in progress");
                return !HeroStateMachine.IsBattleInProgress;
            }

            Debug.Log("[AttackMonster] Starting AttackMonster action!");

            // Find all adjacent monsters for multi-participant battle
            var adjacentMonsters = FindAllAdjacentMonsters(hero);
            if (adjacentMonsters.Count == 0)
            {
                Debug.Warn("[AttackMonster] Could not find any adjacent monsters");
                // Recalculate if there are still monsters adjacent to hero
                hero.AdjacentToMonster = hero.CheckAdjacentToMonster();
                return true; // Complete as failed
            }

            Debug.Log($"[AttackMonster] Starting multi-participant battle with {adjacentMonsters.Count} monsters");

            // Face the first monster for animation purposes
            FaceTarget(hero, adjacentMonsters[0].Transform.Position);

            // Perform attack animation (simulate by moving hero slightly)
            PerformAttackAnimation(hero);

            // Start multi-participant battle sequence using coroutine
            existingMultiParticipantBattleCoroutine = Core.StartCoroutine(ExecuteMultiParticipantBattleSequence(hero, adjacentMonsters));

            Debug.Log("[AttackMonster] Multi-participant battle started successfully");
            return !HeroStateMachine.IsBattleInProgress;
        }

        public override bool Execute(IGoapContext context)
        {
            context.LogDebug("[AttackMonster] Starting monster attack with interface-based context");

            // Get current tile position
            var heroTile = context.HeroController.CurrentTilePosition;
            context.LogDebug($"[AttackMonster] Hero at tile ({heroTile.X},{heroTile.Y})");

            // Note: Virtual implementation would handle monster removal from virtual world state
            context.LogDebug("[AttackMonster] Attack completed in virtual context");
            return true;
        }

        /// <summary>
        /// Yield until the game is no longer paused
        /// </summary>
        private System.Collections.IEnumerator WaitWhilePaused()
        {
            var pauseService = Core.Services.GetService<PauseService>();
            while (pauseService?.IsPaused == true)
            {
                yield return null; // Wait one frame
            }
        }

        /// <summary>
        /// Wait for specified seconds while respecting pause state
        /// </summary>
        private System.Collections.IEnumerator WaitForSecondsRespectingPause(float seconds)
        {
            // First wait while paused
            yield return WaitWhilePaused();

            // Then wait for the specified time
            yield return Coroutine.WaitForSeconds(seconds);
        }

        /// <summary>
        /// Find all adjacent monsters to the hero for multi-participant battle
        /// </summary>
        private List<Entity> FindAllAdjacentMonsters(HeroComponent hero)
        {
            var heroTile = GetCurrentTilePosition(hero);
            var scene = Core.Scene;
            var adjacentMonsters = new List<Entity>();

            if (scene == null) return adjacentMonsters;

            var monsterEntities = scene.FindEntitiesWithTag(GameConfig.TAG_MONSTER);

            foreach (var monster in monsterEntities)
            {
                var monsterTile = GetTileCoordinates(monster.Transform.Position);
                if (IsAdjacent(heroTile, monsterTile))
                {
                    adjacentMonsters.Add(monster);
                }
            }

            return adjacentMonsters;
        }

        /// <summary>
        /// Find all hired mercenaries who are currently in the pit
        /// </summary>
        private List<Entity> FindMercenariesInPit()
        {
            var scene = Core.Scene;
            var mercenariesInPit = new List<Entity>();

            if (scene == null) return mercenariesInPit;

            var mercenaryEntities = scene.FindEntitiesWithTag(GameConfig.TAG_MERCENARY);

            foreach (var merc in mercenaryEntities)
            {
                var mercComponent = merc.GetComponent<MercenaryComponent>();
                if (mercComponent != null && mercComponent.IsHired && mercComponent.InsidePit)
                {
                    mercenariesInPit.Add(merc);
                }
            }

            return mercenariesInPit;
        }

        /// <summary>
        /// Get all living monsters from the valid monsters list (cached components to avoid repeated lookups)
        /// </summary>
        private List<Entity> GetLivingMonsters(List<Entity> validMonsters)
        {
            _tempLivingMonsters.Clear();
            for (int i = 0; i < validMonsters.Count; i++)
            {
                var enemyComponent = validMonsters[i].GetComponent<EnemyComponent>();
                if (enemyComponent?.Enemy != null && enemyComponent.Enemy.CurrentHP > 0)
                {
                    _tempLivingMonsters.Add(validMonsters[i]);
                }
            }
            return _tempLivingMonsters;
        }

        /// <summary>
        /// Checks if there are any valid allies (hero or mercenaries) still alive AND in the pit.
        /// Battle should end when all allies have died or left the pit.
        /// </summary>
        private bool HasValidAlliesInPit(Hero hero, HeroComponent heroComponent, List<Entity> validMercenaries)
        {
            // Hero is valid if alive AND in the pit
            if (hero.CurrentHP > 0 && heroComponent.Entity != null && !heroComponent.Entity.IsDestroyed && heroComponent.InsidePit)
                return true;

            // Check mercenaries - must be alive AND in the pit
            for (int i = 0; i < validMercenaries.Count; i++)
            {
                var mercEntity = validMercenaries[i];
                if (mercEntity == null || mercEntity.IsDestroyed) continue;
                
                var mercComp = mercEntity.GetComponent<MercenaryComponent>();
                if (mercComp?.LinkedMercenary != null && mercComp.LinkedMercenary.CurrentHP > 0 && mercComp.InsidePit)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Find the entity associated with a healing/buff target (hero or mercenary)
        /// </summary>
        private Entity FindTargetEntity(object target, bool targetsHero, HeroComponent heroComponent, List<Entity> validMercenaries)
        {
            if (targetsHero || target is Hero)
                return heroComponent.Entity;

            if (target is Mercenary merc)
            {
                for (int i = 0; i < validMercenaries.Count; i++)
                {
                    var mc = validMercenaries[i].GetComponent<MercenaryComponent>();
                    if (mc?.LinkedMercenary == merc)
                        return validMercenaries[i];
                }
            }

            return heroComponent.Entity;
        }

        /// <summary>
        /// Make hero face the target position
        /// </summary>
        private void FaceTarget(HeroComponent hero, Vector2 targetPosition)
        {
            FaceTarget(hero.Entity, targetPosition);
        }

        /// <summary>
        /// Make any entity face the target position
        /// </summary>
        private void FaceTarget(Entity entity, Vector2 targetPosition)
        {
            // Calculate the direction vector
            var delta = targetPosition - entity.Transform.Position;

            // Determine the facing direction based on the direction vector
            Direction faceDir;
            if (System.Math.Abs(delta.X) >= System.Math.Abs(delta.Y))
                faceDir = delta.X < 0 ? Direction.Left : Direction.Right;
            else
                faceDir = delta.Y < 0 ? Direction.Up : Direction.Down;

            // Set the entity's facing direction
            var facing = entity.GetComponent<ActorFacingComponent>();
            facing?.SetFacing(faceDir);

            Debug.Log($"[AttackMonster] Entity facing direction set to {faceDir} using delta ({delta.X},{delta.Y})");
        }

        /// <summary>
        /// Perform attack animation by moving hero slightly backward then forward
        /// </summary>
        private void PerformAttackAnimation(HeroComponent hero)
        {
            // Simple animation simulation - in a real implementation, this would be handled by an animation system
            Debug.Log("[AttackMonster] Performing attack animation (simulation)");

            // For now, just log the animation. In a full implementation, this would:
            // 1. Move hero a few pixels backward
            // 2. Smoothly animate forward
            // 3. Use proper timing with Time.DeltaTime
        }

        /// <summary>
        /// Check if two tile positions are adjacent (8-directional adjacency)
        /// </summary>
        private bool IsAdjacent(Point tile1, Point tile2)
        {
            int deltaX = System.Math.Abs(tile1.X - tile2.X);
            int deltaY = System.Math.Abs(tile1.Y - tile2.Y);
            return deltaX <= 1 && deltaY <= 1 && (deltaX + deltaY > 0);
        }

        /// <summary>
        /// Get current tile position from hero component
        /// </summary>
        private Point GetCurrentTilePosition(HeroComponent hero)
        {
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                return tileMover.GetCurrentTileCoordinates();
            }

            // Fallback to manual calculation
            return GetTileCoordinates(hero.Entity.Transform.Position);
        }

        /// <summary>
        /// Get the tile coordinates from a world position
        /// </summary>
        private Point GetTileCoordinates(Vector2 worldPosition)
        {
            return new Point((int)(worldPosition.X / GameConfig.TileSize), (int)(worldPosition.Y / GameConfig.TileSize));
        }

        /// <summary>
        /// Calculate turn value using agility + randomness formula
        /// Turn = (RAND(0,255) * (AGILITY - AGILITY / 4)) / 256
        /// </summary>
        private float CalculateTurnValue(int agility)
        {
            int randomValue = Nez.Random.Range(0, 256); // 0-255 inclusive
            float turn = (randomValue * (agility - agility / 4f)) / 256f;
            return turn;
        }

        /// <summary>
        /// Execute the multi-participant battle sequence with all adjacent monsters
        /// </summary>
        private System.Collections.IEnumerator ExecuteMultiParticipantBattleSequence(HeroComponent heroComponent, List<Entity> monsterEntities)
        {
            Debug.Log("[AttackMonster] Starting multi-participant battle sequence");

            HeroStateMachine.IsBattleInProgress = true;

            List<Entity> validMonsters = new List<Entity>();
            List<Entity> validMercenaries = new List<Entity>();
            Entity turnIndicatorEntity = null;
            try
            {
                if (heroComponent.LinkedHero == null)
                {
                    Debug.Warn("[AttackMonster] Hero has no LinkedHero, cannot start battle");
                    yield break;
                }

                var hero = heroComponent.LinkedHero;
                var attackResolver = new EnhancedAttackResolver();
                var heroBattleStats = BattleStats.CalculateForHero(hero);

                // Build participant list
                var participants = new List<BattleParticipant>();
                participants.Add(new BattleParticipant(heroComponent));

                var mercenariesInPit = FindMercenariesInPit();
                for (int i = 0; i < mercenariesInPit.Count; i++)
                {
                    var mercEntity = mercenariesInPit[i];
                    var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
                    if (mercComponent?.LinkedMercenary != null)
                    {
                        participants.Add(new BattleParticipant(mercEntity, true));
                        validMercenaries.Add(mercEntity);
                        Debug.Log($"[AttackMonster] {mercComponent.LinkedMercenary.Name} (Lv.{mercComponent.LinkedMercenary.Level}) joins the battle!");
                    }
                }

                for (int i = 0; i < monsterEntities.Count; i++)
                {
                    var monsterEntity = monsterEntities[i];
                    var enemyComponent = monsterEntity.GetComponent<EnemyComponent>();
                    if (enemyComponent?.Enemy != null)
                    {
                        participants.Add(new BattleParticipant(monsterEntity));
                        validMonsters.Add(monsterEntity);
                    }
                    else
                    {
                        Debug.Warn($"[AttackMonster] Monster entity has no EnemyComponent, skipping");
                        monsterEntity.Destroy();
                    }
                }

                if (validMonsters.Count == 0)
                {
                    Debug.Log("[AttackMonster] No valid monsters to fight");
                    yield break;
                }

                // Set up battle UI
                for (int i = 0; i < validMonsters.Count; i++)
                    validMonsters[i].AddComponent(new MonsterHPBarComponent());

                for (int i = 0; i < validMercenaries.Count; i++)
                {
                    var mercEntity = validMercenaries[i];
                    if (!mercEntity.HasComponent<BouncyDigitComponent>())
                    {
                        var bouncyDigit = mercEntity.AddComponent<BouncyDigitComponent>();
                        bouncyDigit.SetRenderLayer(GameConfig.RenderLayerLowest);
                        bouncyDigit.SetEnabled(false);
                    }
                    if (!mercEntity.HasComponent<BouncyTextComponent>())
                    {
                        var bouncyText = mercEntity.AddComponent<BouncyTextComponent>();
                        bouncyText.SetRenderLayer(GameConfig.RenderLayerLowest);
                        bouncyText.SetEnabled(false);
                    }
                }

                turnIndicatorEntity = Core.Scene.CreateEntity("battle-turn-indicator");
                var turnIndicator = turnIndicatorEntity.AddComponent(new BattleTurnIndicatorComponent());

                Debug.Log($"[AttackMonster] Multi-participant battle: {hero.Name} (Lv.{hero.Level}, HP {hero.CurrentHP}/{hero.MaxHP}) + {validMercenaries.Count} mercenaries vs {validMonsters.Count} monsters");

                // Battle loop
                while (HasValidAlliesInPit(hero, heroComponent, validMercenaries)
                       && validMonsters.Any(m => m.GetComponent<EnemyComponent>()?.Enemy.CurrentHP > 0))
                {
                    yield return WaitWhilePaused();

                    CalculateAllTurnValues(participants, hero, heroComponent, validMonsters, validMercenaries);
                    participants.Sort((a, b) => b.TurnValue.CompareTo(a.TurnValue));

                    for (int ti = 0; ti < participants.Count; ti++)
                    {
                        var participant = participants[ti];
                        if (participant.TurnValue < 0) continue;

                        // Skip mid-round deaths without incurring a turn wait
                        if (participant.Type == BattleParticipant.ParticipantType.Mercenary)
                        {
                            var mc = participant.MercenaryEntity.GetComponent<MercenaryComponent>();
                            if (mc?.LinkedMercenary == null || mc.LinkedMercenary.CurrentHP <= 0) continue;
                        }
                        else if (participant.Type == BattleParticipant.ParticipantType.Monster)
                        {
                            var ec = participant.MonsterEntity.GetComponent<EnemyComponent>();
                            if (ec?.Enemy == null || ec.Enemy.CurrentHP <= 0) continue;
                        }

                        yield return WaitWhilePaused();

                        // Show turn indicator
                        if (participant.Type == BattleParticipant.ParticipantType.Hero)
                            turnIndicator.Show(heroComponent.Entity);
                        else if (participant.Type == BattleParticipant.ParticipantType.Mercenary)
                            turnIndicator.Show(participant.MercenaryEntity);
                        else
                            turnIndicator.Show(participant.MonsterEntity, true);

                        // Execute participant's turn
                        if (participant.Type == BattleParticipant.ParticipantType.Hero)
                            yield return ExecuteHeroTurn(heroComponent, hero, validMonsters, validMercenaries, heroBattleStats, attackResolver);
                        else if (participant.Type == BattleParticipant.ParticipantType.Mercenary)
                            yield return ExecuteMercenaryTurn(participant, heroComponent, hero, validMonsters, validMercenaries, attackResolver);
                        else
                            yield return ExecuteMonsterTurn(participant, heroComponent, hero, heroBattleStats, validMercenaries, attackResolver);

                        yield return WaitForSecondsRespectingPause(GameConfig.BattleTurnWait);

                        bool noAlliesInPit = !HasValidAlliesInPit(hero, heroComponent, validMercenaries);
                        bool allMonstersDead = validMonsters.All(m => m.GetComponent<EnemyComponent>()?.Enemy.CurrentHP <= 0);
                        if (noAlliesInPit || allMonstersDead)
                        {
                            Debug.Log($"[AttackMonster] Battle ending - NoAlliesInPit: {noAlliesInPit}, AllMonstersDead: {allMonstersDead}");
                            break;
                        }
                    }

                    yield return WaitForSecondsRespectingPause(GameConfig.BattleTurnWait);
                }

                if (heroComponent.Entity != null)
                    heroComponent.AdjacentToMonster = heroComponent.CheckAdjacentToMonster();

                Debug.Log("[AttackMonster] Multi-participant battle sequence completed");
            }
            finally
            {
                HeroStateMachine.IsBattleInProgress = false;
                existingMultiParticipantBattleCoroutine = null;

                if (turnIndicatorEntity != null)
                    turnIndicatorEntity.Destroy();

                if (validMonsters != null)
                {
                    for (int i = 0; i < validMonsters.Count; i++)
                    {
                        var hpBar = validMonsters[i].GetComponent<MonsterHPBarComponent>();
                        if (hpBar != null)
                            validMonsters[i].RemoveComponent(hpBar);
                    }
                }

                if (validMercenaries != null)
                {
                    for (int i = 0; i < validMercenaries.Count; i++)
                    {
                        var mercEntity = validMercenaries[i];
                        var bouncyDigit = mercEntity.GetComponent<BouncyDigitComponent>();
                        if (bouncyDigit != null) mercEntity.RemoveComponent(bouncyDigit);
                        var bouncyText = mercEntity.GetComponent<BouncyTextComponent>();
                        if (bouncyText != null) mercEntity.RemoveComponent(bouncyText);
                    }
                }
            }
        }

        // ─── Turn value calculation ───────────────────────────────────────────────

        /// <summary>
        /// Calculates turn values for all participants at the start of a round and
        /// queues the hero's action when no action is already queued.
        /// </summary>
        private void CalculateAllTurnValues(List<BattleParticipant> participants, Hero hero, HeroComponent heroComponent, List<Entity> validMonsters, List<Entity> validMercenaries)
        {
            for (int i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                if (participant.Type == BattleParticipant.ParticipantType.Hero)
                {
                    if (hero.CurrentHP > 0)
                    {
                        participant.TurnValue = CalculateTurnValue(hero.GetTotalStats().Agility);
                        QueueHeroActionForRound(heroComponent, hero, validMonsters, validMercenaries);
                    }
                    else
                    {
                        participant.TurnValue = -1;
                    }
                }
                else if (participant.Type == BattleParticipant.ParticipantType.Mercenary)
                {
                    var mercComponent = participant.MercenaryEntity.GetComponent<MercenaryComponent>();
                    if (mercComponent?.LinkedMercenary != null && mercComponent.LinkedMercenary.CurrentHP > 0)
                        participant.TurnValue = CalculateTurnValue(mercComponent.LinkedMercenary.GetTotalStats().Agility);
                    else
                        participant.TurnValue = -1;
                }
                else // Monster
                {
                    var enemyComponent = participant.MonsterEntity.GetComponent<EnemyComponent>();
                    if (enemyComponent?.Enemy != null && enemyComponent.Enemy.CurrentHP > 0)
                        participant.TurnValue = CalculateTurnValue(enemyComponent.Enemy.Stats.Agility);
                    else
                        participant.TurnValue = -1;
                }
                participants[i] = participant;
            }
        }

        /// <summary>
        /// Decides and queues the hero's action for this round when the queue is empty.
        /// </summary>
        private void QueueHeroActionForRound(HeroComponent heroComponent, Hero hero, List<Entity> validMonsters, List<Entity> validMercenaries)
        {
            if (heroComponent.BattleActionQueue.HasActions()) return;

            var currentLivingMonsters = GetLivingMonsters(validMonsters);
            var decision = BattleTacticDecisionEngine.DecideHeroAction(heroComponent, currentLivingMonsters, validMercenaries);

            switch (decision.Kind)
            {
                case BattleAction.ActionKind.UseAttackSkill:
                    heroComponent.BattleActionQueue.EnqueueSkill(decision.Skill);
                    break;
                case BattleAction.ActionKind.UseHealingSkill:
                    heroComponent.BattleActionQueue.EnqueueSkill(decision.Skill, decision.Target, decision.TargetsHero);
                    break;
                case BattleAction.ActionKind.UseConsumable:
                    heroComponent.BattleActionQueue.EnqueueItem(decision.Consumable, decision.BagIndex, decision.Target, decision.TargetsHero);
                    break;
                case BattleAction.ActionKind.PhysicalAttack:
                default:
                    heroComponent.BattleActionQueue.EnqueueAttack(hero.WeaponShield1);
                    break;
            }
        }

        /// <summary>
        /// Re-evaluates the hero's queued offensive action during their turn.
        /// If healing is now urgently needed (burst damage occurred since round start),
        /// overrides the queued attack with a healing action.
        /// </summary>
        private QueuedAction ReEvaluateHeroQueuedAction(QueuedAction queuedAction, HeroComponent heroComponent, List<Entity> validMonsters, List<Entity> validMercenaries)
        {
            bool isQueuedOffensiveAction = queuedAction != null &&
                (queuedAction.ActionType == QueuedActionType.Attack ||
                 (queuedAction.ActionType == QueuedActionType.UseSkill &&
                  queuedAction.Skill != null && queuedAction.Skill.HPRestoreAmount <= 0));

            if (!isQueuedOffensiveAction) return queuedAction;

            var currentLivingMonsters = GetLivingMonsters(validMonsters);
            var reEvaluatedDecision = BattleTacticDecisionEngine.DecideHeroAction(heroComponent, currentLivingMonsters, validMercenaries);

            if (reEvaluatedDecision.Kind == BattleAction.ActionKind.UseHealingSkill ||
                reEvaluatedDecision.Kind == BattleAction.ActionKind.UseConsumable)
            {
                Debug.Log($"[AttackMonster] Hero re-evaluated: overriding queued offensive action with {reEvaluatedDecision.Kind} (healing needed since action was queued)");
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

        // ─── Shared display helpers ───────────────────────────────────────────────

        /// <summary>
        /// Displays a damage number on an entity and waits for the bounce animation.
        /// </summary>
        private System.Collections.IEnumerator ShowDamageDigitOnEntity(Entity entity, int damage, Color digitColor)
        {
            var bouncyDigit = entity?.GetComponent<BouncyDigitComponent>();
            if (bouncyDigit != null)
            {
                bouncyDigit.Init(damage, digitColor, false);
                bouncyDigit.SetEnabled(true);
                yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
            }
        }

        /// <summary>
        /// Displays a "Miss" text on an entity and waits for the bounce animation.
        /// </summary>
        private System.Collections.IEnumerator ShowMissTextOnEntity(Entity entity, Color textColor)
        {
            var bouncyText = entity?.GetComponent<BouncyTextComponent>();
            if (bouncyText != null)
            {
                bouncyText.Init("Miss", textColor);
                bouncyText.SetEnabled(true);
                yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
            }
        }

        /// <summary>
        /// Displays a green heal number on an entity and waits for the bounce animation.
        /// </summary>
        private System.Collections.IEnumerator ShowHealDigitOnEntity(Entity entity, int amount)
        {
            var bouncyDigit = entity?.GetComponent<BouncyDigitComponent>();
            if (bouncyDigit != null)
            {
                bouncyDigit.Init(amount, Color.Green, false);
                bouncyDigit.SetEnabled(true);
                yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
            }
        }

        // ─── Shared action application helpers ───────────────────────────────────

        /// <summary>
        /// Applies a healing skill's HP/MP restore effects and displays the heal digit.
        /// The caller is responsible for spending MP before calling this method.
        /// </summary>
        private System.Collections.IEnumerator ApplyHealingSkillEffectsAndDisplay(ISkill skill, object healTarget, bool targetsHero, HeroComponent heroComponent, List<Entity> validMercenaries)
        {
            if (skill.HPRestoreAmount > 0)
            {
                bool healed = false;
                if (healTarget is Hero hpHero)
                    healed = hpHero.RestoreHP(skill.HPRestoreAmount);
                else if (healTarget is Mercenary hpMerc)
                    healed = hpMerc.RestoreHP(skill.HPRestoreAmount);

                if (healed)
                {
                    var targetEntity = FindTargetEntity(healTarget, targetsHero, heroComponent, validMercenaries);
                    SoundEffectManager sfx = Core.GetGlobalManager<SoundEffectManager>();
                    sfx?.PlaySound(SoundEffectType.Restorative);
                    yield return ShowHealDigitOnEntity(targetEntity, skill.HPRestoreAmount);
                }
            }

            if (skill.MPRestoreAmount > 0)
            {
                if (healTarget is Hero mpHero)
                    mpHero.RestoreMP(skill.MPRestoreAmount);
                else if (healTarget is Mercenary mpMerc)
                    mpMerc.RestoreMP(skill.MPRestoreAmount);
            }
        }

        /// <summary>
        /// Consumes an item from the hero's bag, applies its effects to the target,
        /// and displays a heal digit if HP was restored.
        /// </summary>
        private System.Collections.IEnumerator ApplyItemAndDisplay(Consumable consumable, int bagIndex, object target, bool targetsHero, HeroComponent heroComponent, List<Entity> validMercenaries)
        {
            int hpBefore = 0;
            if (target is Hero preHero) hpBefore = preHero.CurrentHP;
            else if (target is Mercenary preMerc) hpBefore = preMerc.CurrentHP;

            if (consumable.Consume(target))
            {
                heroComponent.Bag.ConsumeFromStack(bagIndex);
                Debug.Log($"[AttackMonster] Successfully used {consumable.Name}");
                PitHero.UI.InventorySelectionManager.OnInventoryChanged?.Invoke();

                int hpAfter = 0;
                if (target is Hero postHero) hpAfter = postHero.CurrentHP;
                else if (target is Mercenary postMerc) hpAfter = postMerc.CurrentHP;

                int healAmount = hpAfter - hpBefore;
                if (healAmount > 0)
                {
                    var targetEntity = FindTargetEntity(target, targetsHero, heroComponent, validMercenaries);
                    SoundEffectManager sfx = Core.GetGlobalManager<SoundEffectManager>();
                    sfx?.PlaySound(SoundEffectType.Restorative);
                    yield return ShowHealDigitOnEntity(targetEntity, healAmount);
                }
            }
            else
            {
                Debug.Log($"[AttackMonster] Failed to use {consumable.Name}");
            }
        }

        /// <summary>
        /// Fades out and destroys all monsters in the list whose HP has reached zero.
        /// </summary>
        private System.Collections.IEnumerator FadeOutDeadMonsters(List<Entity> monsters)
        {
            for (int i = monsters.Count - 1; i >= 0; i--)
            {
                var entity = monsters[i];
                var comp = entity.GetComponent<EnemyComponent>();
                if (comp?.Enemy != null && comp.Enemy.CurrentHP <= 0)
                    yield return FadeOutAndDestroyMonster(entity);
            }
        }

        // ─── Reward awarding ──────────────────────────────────────────────────────

        /// <summary>
        /// Awards XP, JP, SP, and gold to the party when an enemy is defeated,
        /// and attempts to recruit the defeated enemy as an allied monster.
        /// Pass heroComponent to also reset the InnExhausted flag on a hero kill.
        /// </summary>
        private void AwardEnemyDeathRewards(Hero hero, RolePlayingFramework.Enemies.IEnemy enemy, HeroComponent heroComponent, List<Entity> validMercenaries)
        {
            hero.AddExperience(enemy.ExperienceYield);
            hero.EarnJP(enemy.JPYield);
            hero.EarnSynergyPointsWithAcceleration(enemy.SPYield);
            AwardMercenaryExperience(validMercenaries, enemy.ExperienceYield);

            var gameState = Nez.Core.Services.GetService<PitHero.Services.GameStateService>();
            if (gameState != null)
            {
                gameState.Funds += enemy.GoldYield;
                if (heroComponent != null && enemy.GoldYield > 0)
                    heroComponent.InnExhausted = false;
                Debug.Log($"[AttackMonster] Earned {enemy.ExperienceYield} XP, {enemy.JPYield} JP, {enemy.SPYield} SP, {enemy.GoldYield} Gold");
            }
            else
            {
                Debug.Log($"[AttackMonster] Earned {enemy.ExperienceYield} XP, {enemy.JPYield} JP, {enemy.SPYield} SP, {enemy.GoldYield} Gold");
            }

            var alliedMonsterMgr = Core.Services.GetService<PitHero.Services.AlliedMonsterManager>();
            if (alliedMonsterMgr != null)
            {
                var recruited = alliedMonsterMgr.TryRecruit(enemy);
                if (recruited != null)
                    Debug.Log($"[AttackMonster] {enemy.Name} recruited as '{recruited.Name}'");
            }
        }

        // ─── Hero turn ────────────────────────────────────────────────────────────

        /// <summary>
        /// Executes the hero's turn: dequeues and re-evaluates their queued action,
        /// then dispatches to the appropriate action handler.
        /// </summary>
        private System.Collections.IEnumerator ExecuteHeroTurn(HeroComponent heroComponent, Hero hero, List<Entity> validMonsters, List<Entity> validMercenaries, BattleStats heroBattleStats, EnhancedAttackResolver attackResolver)
        {
            var queuedAction = heroComponent.BattleActionQueue.Dequeue();
            queuedAction = ReEvaluateHeroQueuedAction(queuedAction, heroComponent, validMonsters, validMercenaries);

            if (queuedAction == null)
            {
                Debug.Warn("[AttackMonster] Hero turn but no queued action (unexpected)");
                yield break;
            }

            Debug.Log($"[AttackMonster] Hero's turn - executing queued action: {queuedAction.ActionType}");

            if (queuedAction.ActionType == QueuedActionType.UseItem)
            {
                var consumeTarget = queuedAction.Target ?? hero;
                Debug.Log($"[AttackMonster] Using queued item: {queuedAction.Consumable.Name}");
                yield return ApplyItemAndDisplay(queuedAction.Consumable, queuedAction.BagIndex, consumeTarget, queuedAction.TargetsHero, heroComponent, validMercenaries);
            }
            else if (queuedAction.ActionType == QueuedActionType.UseSkill)
            {
                var skill = queuedAction.Skill;
                Debug.Log($"[AttackMonster] Using queued skill: {skill.Name}");

                if (hero.CurrentMP >= skill.MPCost)
                {
                    bool isHealingSkill = skill.HPRestoreAmount > 0 || skill.MPRestoreAmount > 0 ||
                        skill.TargetType == SkillTargetType.Self ||
                        skill.TargetType == SkillTargetType.SingleAlly ||
                        skill.TargetType == SkillTargetType.AllAllies;

                    if (isHealingSkill)
                    {
                        hero.SpendMP(skill.MPCost);
                        var healTarget = queuedAction.Target ?? hero;
                        yield return ApplyHealingSkillEffectsAndDisplay(skill, healTarget, queuedAction.TargetsHero, heroComponent, validMercenaries);
                        Debug.Log($"[AttackMonster] Used healing skill {skill.Name}");
                    }
                    else
                    {
                        yield return ExecuteHeroAttackSkill(skill, hero, heroComponent, validMonsters, validMercenaries, attackResolver);
                    }
                }
                else
                {
                    Debug.Log($"[AttackMonster] Not enough MP to use {skill.Name} (need {skill.MPCost}, have {hero.CurrentMP})");
                }
            }
            else if (queuedAction.ActionType == QueuedActionType.Attack)
            {
                yield return ExecuteHeroPhysicalAttack(queuedAction, hero, heroComponent, validMonsters, validMercenaries, heroBattleStats, attackResolver);
            }
        }

        /// <summary>
        /// Executes the hero's attack skill against living monsters.
        /// </summary>
        private System.Collections.IEnumerator ExecuteHeroAttackSkill(ISkill skill, Hero hero, HeroComponent heroComponent, List<Entity> validMonsters, List<Entity> validMercenaries, EnhancedAttackResolver attackResolver)
        {
            var livingMonsters = GetLivingMonsters(validMonsters);
            if (livingMonsters.Count == 0) yield break;

            // Snapshot HP before the skill executes so we can calculate per-monster damage
            var monsterHPBefore = new Dictionary<RolePlayingFramework.Enemies.IEnemy, int>();
            for (int i = 0; i < livingMonsters.Count; i++)
            {
                var enemyComp = livingMonsters[i].GetComponent<EnemyComponent>();
                if (enemyComp?.Enemy != null)
                    monsterHPBefore[enemyComp.Enemy] = enemyComp.Enemy.CurrentHP;
            }

            var primaryTarget = livingMonsters[0].GetComponent<EnemyComponent>()?.Enemy;
            var surroundingTargets = new List<RolePlayingFramework.Enemies.IEnemy>();
            for (int i = 1; i < livingMonsters.Count; i++)
            {
                var enemyComp = livingMonsters[i].GetComponent<EnemyComponent>();
                if (enemyComp?.Enemy != null)
                    surroundingTargets.Add(enemyComp.Enemy);
            }

            skill.Execute(hero, primaryTarget, surroundingTargets, attackResolver);
            hero.SpendMP(skill.MPCost);
            Debug.Log($"[AttackMonster] Successfully used {skill.Name}, consumed {skill.MPCost} MP");

            // Display damage and handle deaths for all affected monsters
            for (int i = livingMonsters.Count - 1; i >= 0; i--)
            {
                var monsterEntity = livingMonsters[i];
                var enemyComp = monsterEntity.GetComponent<EnemyComponent>();
                if (enemyComp?.Enemy == null) continue;

                var enemy = enemyComp.Enemy;
                if (!monsterHPBefore.TryGetValue(enemy, out int hpBefore)) continue;

                int damage = hpBefore - enemy.CurrentHP;
                if (damage <= 0) continue;

                Debug.Log($"[AttackMonster] {skill.Name} dealt {damage} damage to {enemy.Name}. Enemy HP: {enemy.CurrentHP}/{enemy.MaxHP}");

                var enemyBouncyDigit = monsterEntity.GetComponent<BouncyDigitComponent>();
                if (enemyBouncyDigit != null)
                {
                    enemyBouncyDigit.Init(damage, BouncyDigitComponent.EnemyDigitColor, false);
                    enemyBouncyDigit.SetEnabled(true);
                }

                if (enemy.CurrentHP <= 0)
                {
                    Debug.Log($"[AttackMonster] {enemy.Name} defeated by {skill.Name}!");
                    AwardEnemyDeathRewards(hero, enemy, heroComponent, validMercenaries);
                    validMonsters.Remove(monsterEntity);
                }
            }

            yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
            yield return FadeOutDeadMonsters(livingMonsters);
        }

        /// <summary>
        /// Executes the hero's physical attack against a random living monster.
        /// </summary>
        private System.Collections.IEnumerator ExecuteHeroPhysicalAttack(QueuedAction queuedAction, Hero hero, HeroComponent heroComponent, List<Entity> validMonsters, List<Entity> validMercenaries, BattleStats heroBattleStats, EnhancedAttackResolver attackResolver)
        {
            var livingMonsters = GetLivingMonsters(validMonsters);
            if (livingMonsters.Count == 0) yield break;

            var targetMonster = livingMonsters[Nez.Random.Range(0, livingMonsters.Count)];
            var targetEnemy = targetMonster.GetComponent<EnemyComponent>().Enemy;
            var targetBattleStats = BattleStats.CalculateForMonster(targetEnemy);

            Debug.Log($"[AttackMonster] Hero's turn - attacking {targetEnemy.Name}");
            var heroAttackResult = attackResolver.Resolve(heroBattleStats, targetBattleStats, DamageKind.Physical);

            if (queuedAction.WeaponItem == null)
            {
                SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
                soundEffectManager?.PlaySound(SoundEffectType.Punch);
            }

            if (heroAttackResult.Hit)
            {
                bool enemyDied = targetEnemy.TakeDamage(heroAttackResult.Damage);
                Debug.Log($"[AttackMonster] Hero deals {heroAttackResult.Damage} damage to {targetEnemy.Name}. Enemy HP: {targetEnemy.CurrentHP}/{targetEnemy.MaxHP}");

                yield return ShowDamageDigitOnEntity(targetMonster, heroAttackResult.Damage, BouncyDigitComponent.EnemyDigitColor);

                if (enemyDied)
                {
                    Debug.Log($"[AttackMonster] {targetEnemy.Name} defeated! Starting fade out");
                    AwardEnemyDeathRewards(hero, targetEnemy, heroComponent, validMercenaries);
                    validMonsters.Remove(targetMonster);
                    yield return FadeOutAndDestroyMonster(targetMonster);
                }
            }
            else
            {
                Debug.Log($"[AttackMonster] Hero missed {targetEnemy.Name}!");
                yield return ShowMissTextOnEntity(targetMonster, BouncyTextComponent.EnemyMissColor);
            }
        }

        // ─── Mercenary turn ───────────────────────────────────────────────────────

        /// <summary>
        /// Executes a mercenary's turn: decides their action and dispatches to the
        /// appropriate handler.
        /// </summary>
        private System.Collections.IEnumerator ExecuteMercenaryTurn(BattleParticipant participant, HeroComponent heroComponent, Hero hero, List<Entity> validMonsters, List<Entity> validMercenaries, EnhancedAttackResolver attackResolver)
        {
            var mercComponent = participant.MercenaryEntity.GetComponent<MercenaryComponent>();
            if (mercComponent?.LinkedMercenary == null || mercComponent.LinkedMercenary.CurrentHP <= 0) yield break;

            var mercenary = mercComponent.LinkedMercenary;
            var mercBattleStats = BattleStats.CalculateForMercenary(mercenary);

            var livingMonsters = GetLivingMonsters(validMonsters);
            if (livingMonsters.Count == 0) yield break;

            var mercDecision = BattleTacticDecisionEngine.DecideMercenaryAction(mercComponent, heroComponent, livingMonsters, validMercenaries);

            switch (mercDecision.Kind)
            {
                case BattleAction.ActionKind.UseHealingSkill:
                {
                    var healSkill = mercDecision.Skill;
                    if (mercenary.CurrentMP >= healSkill.MPCost)
                    {
                        mercenary.UseMP(healSkill.MPCost);
                        mercComponent.ActionQueueVisualization?.ShowAction(new QueuedAction(healSkill));
                        var healTarget = mercDecision.Target ?? hero;
                        yield return ApplyHealingSkillEffectsAndDisplay(healSkill, healTarget, mercDecision.TargetsHero, heroComponent, validMercenaries);
                        Debug.Log($"[AttackMonster] {mercenary.Name} used {healSkill.Name}");
                    }
                    break;
                }

                case BattleAction.ActionKind.UseConsumable:
                {
                    var mcTarget = mercDecision.Target ?? hero;
                    yield return ApplyItemAndDisplay(mercDecision.Consumable, mercDecision.BagIndex, mcTarget, mercDecision.TargetsHero, heroComponent, validMercenaries);
                    break;
                }

                case BattleAction.ActionKind.UseAttackSkill:
                {
                    yield return ExecuteMercenaryAttackSkill(mercDecision, mercenary, mercComponent, mercBattleStats, participant, validMonsters, validMercenaries, hero, heroComponent, attackResolver);
                    break;
                }

                case BattleAction.ActionKind.PhysicalAttack:
                default:
                {
                    yield return ExecuteMercenaryPhysicalAttack(mercDecision, mercenary, mercComponent, mercBattleStats, participant, validMonsters, validMercenaries, hero, heroComponent, attackResolver);
                    break;
                }
            }
        }

        /// <summary>
        /// Executes a mercenary's attack skill against living monsters.
        /// </summary>
        private System.Collections.IEnumerator ExecuteMercenaryAttackSkill(BattleAction decision, Mercenary mercenary, MercenaryComponent mercComponent, BattleStats mercBattleStats, BattleParticipant participant, List<Entity> validMonsters, List<Entity> validMercenaries, Hero hero, HeroComponent heroComponent, EnhancedAttackResolver attackResolver)
        {
            var atkSkill = decision.Skill;
            if (mercenary.CurrentMP < atkSkill.MPCost)
            {
                Debug.Log($"[AttackMonster] {mercenary.Name} not enough MP for {atkSkill.Name}");
                yield break;
            }

            mercenary.UseMP(atkSkill.MPCost);
            mercComponent.ActionQueueVisualization?.ShowAction(new QueuedAction(atkSkill));

            var skillDamageKind = atkSkill.Element != ElementType.Neutral ? DamageKind.Magical : DamageKind.Physical;
            var skillLiving = GetLivingMonsters(validMonsters);
            if (skillLiving.Count == 0) yield break;

            bool isAoE = atkSkill.TargetType == SkillTargetType.SurroundingEnemies;
            int startIndex = 0;
            int endIndex = isAoE ? skillLiving.Count : 1;

            if (!isAoE && decision.TargetEntity != null)
            {
                for (int si = 0; si < skillLiving.Count; si++)
                {
                    if (skillLiving[si] == decision.TargetEntity)
                    {
                        startIndex = si;
                        endIndex = si + 1;
                        break;
                    }
                }
            }

            for (int si = startIndex; si < endIndex && si < skillLiving.Count; si++)
            {
                var sMonsterEntity = skillLiving[si];
                var sEnemyComp = sMonsterEntity.GetComponent<EnemyComponent>();
                if (sEnemyComp?.Enemy == null || sEnemyComp.Enemy.CurrentHP <= 0) continue;

                var sEnemy = sEnemyComp.Enemy;
                var sTargetStats = BattleStats.CalculateForMonster(sEnemy);
                var sResult = attackResolver.Resolve(mercBattleStats, sTargetStats, skillDamageKind);

                FaceTarget(participant.MercenaryEntity, sMonsterEntity.Transform.Position);

                if (sResult.Hit)
                {
                    bool sEnemyDied = sEnemy.TakeDamage(sResult.Damage);
                    Debug.Log($"[AttackMonster] {mercenary.Name}'s {atkSkill.Name} dealt {sResult.Damage} to {sEnemy.Name}. HP: {sEnemy.CurrentHP}/{sEnemy.MaxHP}");

                    var sDigit = sMonsterEntity.GetComponent<BouncyDigitComponent>();
                    if (sDigit != null)
                    {
                        sDigit.Init(sResult.Damage, BouncyDigitComponent.EnemyDigitColor, false);
                        sDigit.SetEnabled(true);
                    }

                    if (sEnemyDied)
                    {
                        Debug.Log($"[AttackMonster] {sEnemy.Name} defeated by {mercenary.Name}'s {atkSkill.Name}!");
                        AwardEnemyDeathRewards(hero, sEnemy, null, validMercenaries);
                        validMonsters.Remove(sMonsterEntity);
                    }
                }
                else
                {
                    Debug.Log($"[AttackMonster] {mercenary.Name}'s {atkSkill.Name} missed {sEnemy.Name}!");
                    var sMissText = sMonsterEntity.GetComponent<BouncyTextComponent>();
                    if (sMissText != null)
                    {
                        sMissText.Init("Miss", BouncyTextComponent.EnemyMissColor);
                        sMissText.SetEnabled(true);
                    }
                }
            }

            yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
            yield return FadeOutDeadMonsters(skillLiving);
        }

        /// <summary>
        /// Executes a mercenary's physical attack against a target monster.
        /// </summary>
        private System.Collections.IEnumerator ExecuteMercenaryPhysicalAttack(BattleAction decision, Mercenary mercenary, MercenaryComponent mercComponent, BattleStats mercBattleStats, BattleParticipant participant, List<Entity> validMonsters, List<Entity> validMercenaries, Hero hero, HeroComponent heroComponent, EnhancedAttackResolver attackResolver)
        {
            var paTarget = decision.TargetEntity;
            if (paTarget == null || paTarget.GetComponent<EnemyComponent>()?.Enemy?.CurrentHP <= 0)
            {
                var paLiving = GetLivingMonsters(validMonsters);
                if (paLiving.Count == 0) yield break;
                paTarget = paLiving[Nez.Random.Range(0, paLiving.Count)];
            }

            var targetEnemy = paTarget.GetComponent<EnemyComponent>().Enemy;
            var targetBattleStats = BattleStats.CalculateForMonster(targetEnemy);

            Debug.Log($"[AttackMonster] {mercenary.Name}'s turn - attacking {targetEnemy.Name}");
            mercComponent.ActionQueueVisualization?.ShowAction(new QueuedAction(mercenary.WeaponShield1));
            FaceTarget(participant.MercenaryEntity, paTarget.Transform.Position);

            var mercAttackResult = attackResolver.Resolve(mercBattleStats, targetBattleStats, DamageKind.Physical);

            SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
            soundEffectManager?.PlaySound(SoundEffectType.Punch);

            if (mercAttackResult.Hit)
            {
                bool enemyDied = targetEnemy.TakeDamage(mercAttackResult.Damage);
                Debug.Log($"[AttackMonster] {mercenary.Name} deals {mercAttackResult.Damage} damage to {targetEnemy.Name}. Enemy HP: {targetEnemy.CurrentHP}/{targetEnemy.MaxHP}");

                yield return ShowDamageDigitOnEntity(paTarget, mercAttackResult.Damage, BouncyDigitComponent.EnemyDigitColor);

                if (enemyDied)
                {
                    Debug.Log($"[AttackMonster] {targetEnemy.Name} defeated by {mercenary.Name}! Starting fade out");
                    AwardEnemyDeathRewards(hero, targetEnemy, null, validMercenaries);
                    validMonsters.Remove(paTarget);
                    yield return FadeOutAndDestroyMonster(paTarget);
                }
            }
            else
            {
                Debug.Log($"[AttackMonster] {mercenary.Name} missed {targetEnemy.Name}!");
                yield return ShowMissTextOnEntity(paTarget, BouncyTextComponent.EnemyMissColor);
            }
        }

        // ─── Monster turn ─────────────────────────────────────────────────────────

        /// <summary>
        /// Executes a monster's turn: selects a random valid ally target and attacks.
        /// </summary>
        private System.Collections.IEnumerator ExecuteMonsterTurn(BattleParticipant participant, HeroComponent heroComponent, Hero hero, BattleStats heroBattleStats, List<Entity> validMercenaries, EnhancedAttackResolver attackResolver)
        {
            var enemyComponent = participant.MonsterEntity.GetComponent<EnemyComponent>();
            if (enemyComponent?.Enemy == null || enemyComponent.Enemy.CurrentHP <= 0) yield break;

            var enemy = enemyComponent.Enemy;
            var enemyBattleStats = BattleStats.CalculateForMonster(enemy);

            // Build the list of valid targets (alive and still in the pit)
            var possibleTargets = new List<(Entity entity, bool isHero)>();

            if (hero.CurrentHP > 0 && heroComponent.Entity != null && heroComponent.InsidePit)
                possibleTargets.Add((heroComponent.Entity, true));

            for (int i = 0; i < validMercenaries.Count; i++)
            {
                var mercEntity = validMercenaries[i];
                if (mercEntity == null || mercEntity.IsDestroyed) continue;
                var mercComp = mercEntity.GetComponent<MercenaryComponent>();
                if (mercComp?.LinkedMercenary != null && mercComp.LinkedMercenary.CurrentHP > 0 && mercComp.InsidePit)
                    possibleTargets.Add((mercEntity, false));
            }

            if (possibleTargets.Count == 0)
            {
                Debug.Log($"[AttackMonster] {enemy.Name} has no valid targets in the pit - skipping turn");
                yield break;
            }

            var targetChoice = possibleTargets[Nez.Random.Range(0, possibleTargets.Count)];
            var targetEntity = targetChoice.entity;

            if (targetEntity == null || targetEntity.IsDestroyed)
            {
                Debug.Log($"[AttackMonster] {enemy.Name}'s target entity was destroyed - skipping turn");
                yield break;
            }

            FaceTarget(participant.MonsterEntity, targetEntity.Transform.Position);

            if (targetChoice.isHero)
                yield return ExecuteMonsterAttackHero(enemy, enemyBattleStats, heroComponent, hero, heroBattleStats, attackResolver);
            else
                yield return ExecuteMonsterAttackMercenary(enemy, enemyBattleStats, targetEntity, heroComponent, validMercenaries, attackResolver);
        }

        /// <summary>
        /// Executes a monster's attack against the hero.
        /// </summary>
        private System.Collections.IEnumerator ExecuteMonsterAttackHero(RolePlayingFramework.Enemies.IEnemy enemy, BattleStats enemyBattleStats, HeroComponent heroComponent, Hero hero, BattleStats heroBattleStats, EnhancedAttackResolver attackResolver)
        {
            Debug.Log($"[AttackMonster] {enemy.Name}'s turn - attacking {hero.Name}");
            var enemyAttackResult = attackResolver.Resolve(enemyBattleStats, heroBattleStats, enemy.AttackKind);

            if (enemyAttackResult.Hit)
            {
                SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
                soundEffectManager?.PlaySound(SoundEffectType.TakeDamage);

                int actualDamage = enemyAttackResult.Damage * DEBUG_DAMAGE_MULT;
                bool heroDied = hero.TakeDamage(actualDamage);
                Debug.Log($"[AttackMonster] {enemy.Name} deals {enemyAttackResult.Damage} damage to {hero.Name}. Hero HP: {hero.CurrentHP}/{hero.MaxHP}");

                heroComponent.RegisterHeroBurstDamage(actualDamage);

                yield return ShowDamageDigitOnEntity(heroComponent.Entity, enemyAttackResult.Damage, BouncyDigitComponent.HeroDigitColor);

                if (heroDied)
                {
                    Debug.Log($"[AttackMonster] {hero.Name} died! Battle continues with mercenaries.");
                    var deathComponent = heroComponent.Entity.GetComponent<HeroDeathComponent>();
                    if (deathComponent == null)
                        deathComponent = heroComponent.Entity.AddComponent(new HeroDeathComponent());
                    deathComponent.StartDeathAnimation();
                }
            }
            else
            {
                Debug.Log($"[AttackMonster] {enemy.Name} missed {hero.Name}!");
                yield return ShowMissTextOnEntity(heroComponent.Entity, BouncyTextComponent.HeroMissColor);
            }
        }

        /// <summary>
        /// Executes a monster's attack against a mercenary.
        /// </summary>
        private System.Collections.IEnumerator ExecuteMonsterAttackMercenary(RolePlayingFramework.Enemies.IEnemy enemy, BattleStats enemyBattleStats, Entity targetEntity, HeroComponent heroComponent, List<Entity> validMercenaries, EnhancedAttackResolver attackResolver)
        {
            var targetMercComp = targetEntity.GetComponent<MercenaryComponent>();
            if (targetMercComp?.LinkedMercenary == null)
            {
                Debug.Log($"[AttackMonster] {enemy.Name}'s target mercenary no longer valid - skipping attack");
                yield break;
            }

            var targetMercenary = targetMercComp.LinkedMercenary;
            var targetMercBattleStats = BattleStats.CalculateForMercenary(targetMercenary);

            Debug.Log($"[AttackMonster] {enemy.Name}'s turn - attacking {targetMercenary.Name}");
            var enemyAttackResult = attackResolver.Resolve(enemyBattleStats, targetMercBattleStats, enemy.AttackKind);

            if (enemyAttackResult.Hit)
            {
                SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
                soundEffectManager?.PlaySound(SoundEffectType.TakeDamage);

                int actualDamage = enemyAttackResult.Damage * DEBUG_DAMAGE_MULT;
                bool mercDied = targetMercenary.TakeDamage(actualDamage);
                Debug.Log($"[AttackMonster] {enemy.Name} deals {enemyAttackResult.Damage} damage to {targetMercenary.Name}. Mercenary HP: {targetMercenary.CurrentHP}/{targetMercenary.MaxHP}");

                heroComponent.RegisterMercenaryBurstDamage(targetEntity, targetMercComp, actualDamage);

                yield return ShowDamageDigitOnEntity(targetEntity, enemyAttackResult.Damage, BouncyDigitComponent.HeroDigitColor);

                if (mercDied)
                {
                    Debug.Log($"[AttackMonster] {targetMercenary.Name} died! Starting fade out");
                    HandleMercenaryDeath(targetEntity, heroComponent, validMercenaries);
                    validMercenaries.Remove(targetEntity);
                    yield return FadeOutAndDestroyMercenary(targetEntity);
                }
            }
            else
            {
                Debug.Log($"[AttackMonster] {enemy.Name} missed {targetMercenary.Name}!");
                yield return ShowMissTextOnEntity(targetEntity, BouncyTextComponent.HeroMissColor);
            }
        }


        /// <summary>
        /// Handle mercenary death by removing them permanently and reassigning followers if needed
        /// </summary>
        private void HandleMercenaryDeath(Entity mercenaryEntity, HeroComponent heroComponent, List<Entity> validMercenaries)
        {
            var mercComponent = mercenaryEntity.GetComponent<MercenaryComponent>();
            if (mercComponent == null) return;

            Debug.Log($"[AttackMonster] Mercenary {mercComponent.LinkedMercenary.Name} died in battle");
            
            // Check if this mercenary was following the hero
            bool wasFollowingHero = mercComponent.FollowTarget?.GetComponent<HeroComponent>() != null;

            // If this mercenary was following the hero, reassign another mercenary to follow
            if (wasFollowingHero && validMercenaries.Count > 0)
            {
                // Find another living mercenary to follow the hero
                Entity nextFollower = null;

                foreach (var otherMerc in validMercenaries)
                {
                    if (otherMerc == mercenaryEntity) continue; // Skip the dying mercenary

                    var otherMercComp = otherMerc.GetComponent<MercenaryComponent>();
                    if (otherMercComp != null && otherMercComp.LinkedMercenary.CurrentHP > 0)
                    {
                        nextFollower = otherMerc;
                        break;
                    }
                }

                // Assign the next mercenary to follow the hero
                if (nextFollower != null)
                {
                    var nextMercComp = nextFollower.GetComponent<MercenaryComponent>();
                    nextMercComp.FollowTarget = heroComponent.Entity;
                    Debug.Log($"[AttackMonster] {nextMercComp.LinkedMercenary.Name} is now following the hero");
                }
                else
                {
                    Debug.Log("[AttackMonster] No other living mercenaries to reassign");
                }
            }
        }

        /// <summary>Fades out a defeated monster entity then destroys it</summary>
        private System.Collections.IEnumerator FadeOutAndDestroyMonster(Entity monsterEntity)
        {
            if (monsterEntity == null)
                yield break;
            // Try to get any renderers that support color/alpha adjustments
            // We specifically look for EnemyAnimationComponent (PausableSpriteAnimator) and SpriteRenderer/PrototypeSpriteRenderer
            var enemyAnim = monsterEntity.GetComponent<EnemyAnimationComponent>();
            SpriteRenderer spriteRenderer = monsterEntity.GetComponent<SpriteRenderer>();
#if DEBUG
            PrototypeSpriteRenderer protoRenderer = null;
            if (spriteRenderer == null)
                protoRenderer = monsterEntity.GetComponent<PrototypeSpriteRenderer>();
#endif

            Color origColorAnim = Color.White;
            Color origColorSprite = Color.White;
            Color origColorProto = Color.White;
            if (enemyAnim != null)
                origColorAnim = enemyAnim.Color;
            if (spriteRenderer != null)
                origColorSprite = spriteRenderer.Color;
#if DEBUG
            if (protoRenderer != null)
                origColorProto = protoRenderer.Color;
#endif

            SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
            soundEffectManager?.PlaySound(SoundEffectType.EnemyDefeat);

            const float fadeDuration = 0.5f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                // Respect pause
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService?.IsPaused == true)
                {
                    yield return null;
                    continue;
                }
                elapsed += Time.DeltaTime;
                float progress = elapsed / fadeDuration;
                if (progress < 0f) progress = 0f; else if (progress > 1f) progress = 1f;
                byte alpha = (byte)(255 * (1f - progress));
                if (enemyAnim != null)
                {
                    enemyAnim.Color = new Color(origColorAnim.R, origColorAnim.G, origColorAnim.B, alpha);
                }
                if (spriteRenderer != null)
                {
                    spriteRenderer.Color = new Color(origColorSprite.R, origColorSprite.G, origColorSprite.B, alpha);
                }
#if DEBUG
                if (protoRenderer != null)
                {
                    protoRenderer.Color = new Color(origColorProto.R, origColorProto.G, origColorProto.B, alpha);
                }
#endif
                yield return null;
            }
            monsterEntity.Destroy();
        }

        /// <summary>Fades out a defeated mercenary entity then destroys it</summary>
        private System.Collections.IEnumerator FadeOutAndDestroyMercenary(Entity mercenaryEntity)
        {
            if (mercenaryEntity == null)
                yield break;

            // Get all hero animation components
            var bodyAnim = mercenaryEntity.GetComponent<HeroBodyAnimationComponent>();
            var hand1Anim = mercenaryEntity.GetComponent<HeroHand1AnimationComponent>();
            var hand2Anim = mercenaryEntity.GetComponent<HeroHand2AnimationComponent>();
            var pantsAnim = mercenaryEntity.GetComponent<HeroPantsAnimationComponent>();
            var shirtAnim = mercenaryEntity.GetComponent<HeroShirtAnimationComponent>();
            var hairAnim = mercenaryEntity.GetComponent<HeroHairAnimationComponent>();

            // Store original colors
            Color origColorBody = bodyAnim?.Color ?? Color.White;
            Color origColorHand1 = hand1Anim?.Color ?? Color.White;
            Color origColorHand2 = hand2Anim?.Color ?? Color.White;
            Color origColorPants = pantsAnim?.Color ?? Color.White;
            Color origColorShirt = shirtAnim?.Color ?? Color.White;
            Color origColorHair = hairAnim?.Color ?? Color.White;

            const float fadeDuration = 0.5f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                // Respect pause
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService?.IsPaused == true)
                {
                    yield return null;
                    continue;
                }
                elapsed += Time.DeltaTime;
                float progress = elapsed / fadeDuration;
                if (progress < 0f) progress = 0f; else if (progress > 1f) progress = 1f;
                byte alpha = (byte)(255 * (1f - progress));
                
                if (bodyAnim != null)
                    bodyAnim.Color = new Color(origColorBody.R, origColorBody.G, origColorBody.B, alpha);
                if (hand1Anim != null)
                    hand1Anim.Color = new Color(origColorHand1.R, origColorHand1.G, origColorHand1.B, alpha);
                if (hand2Anim != null)
                    hand2Anim.Color = new Color(origColorHand2.R, origColorHand2.G, origColorHand2.B, alpha);
                if (pantsAnim != null)
                    pantsAnim.Color = new Color(origColorPants.R, origColorPants.G, origColorPants.B, alpha);
                if (shirtAnim != null)
                    shirtAnim.Color = new Color(origColorShirt.R, origColorShirt.G, origColorShirt.B, alpha);
                if (hairAnim != null)
                    hairAnim.Color = new Color(origColorHair.R, origColorHair.G, origColorHair.B, alpha);

                yield return null;
            }

            // Remove from mercenary manager tracking and destroy
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager != null)
            {
                // The mercenary manager has a private RemoveMercenary method, but we can just remove from the global list
                mercenaryManager.GetAllMercenaries().Remove(mercenaryEntity);
            }

            mercenaryEntity.Destroy();
        }


        /// <summary>Awards experience to all mercenaries in the battle.</summary>
        private static void AwardMercenaryExperience(List<Entity> mercenaries, int xpAmount)
        {
            for (int mi = 0; mi < mercenaries.Count; mi++)
            {
                var mComp = mercenaries[mi].GetComponent<MercenaryComponent>();
                if (mComp?.LinkedMercenary != null)
                    mComp.LinkedMercenary.AddExperience(xpAmount);
            }
        }

        // Temp list to avoid allocations each turn when picking random living monster
        private static readonly List<Entity> _tempLivingMonsters = new List<Entity>(16);
    }
}