using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;
using RolePlayingFramework.Combat;
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

            // Set battle in progress to prevent movement
            HeroStateMachine.IsBattleInProgress = true;

            List<Entity> validMonsters = new List<Entity>();
            List<Entity> validMercenaries = new List<Entity>();
            Entity turnIndicatorEntity = null;
            try
            {
                // Get the hero's linked RPG hero
                if (heroComponent.LinkedHero == null)
                {
                    Debug.Warn("[AttackMonster] Hero has no LinkedHero, cannot start battle");
                    yield break;
                }

                var hero = heroComponent.LinkedHero;
                var attackResolver = new EnhancedAttackResolver();

                // Calculate hero's battle stats once for the entire battle
                var heroBattleStats = BattleStats.CalculateForHero(hero);

                // Create list of battle participants
                var participants = new List<BattleParticipant>();
                participants.Add(new BattleParticipant(heroComponent));

                // Find all hired mercenaries who are in the pit
                var mercenariesInPit = FindMercenariesInPit();

                // Add mercenary participants
                foreach (var mercEntity in mercenariesInPit)
                {
                    var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
                    if (mercComponent?.LinkedMercenary != null)
                    {
                        participants.Add(new BattleParticipant(mercEntity, true));
                        validMercenaries.Add(mercEntity);
                        Debug.Log($"[AttackMonster] {mercComponent.LinkedMercenary.Name} (Lv.{mercComponent.LinkedMercenary.Level}) joins the battle!");
                    }
                }

                // Add all monster participants and validate they have EnemyComponents
                foreach (var monsterEntity in monsterEntities)
                {
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

                // Add HP bar components to monsters
                foreach (var monsterEntity in validMonsters)
                {
                    monsterEntity.AddComponent(new MonsterHPBarComponent());
                }

                // Add bouncy digit and text components to mercenaries for damage display
                foreach (var mercEntity in validMercenaries)
                {
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

                // Create battle turn indicator entity
                turnIndicatorEntity = Core.Scene.CreateEntity("battle-turn-indicator");
                var turnIndicator = turnIndicatorEntity.AddComponent(new BattleTurnIndicatorComponent());

                Debug.Log($"[AttackMonster] Multi-participant battle: {hero.Name} (Lv.{hero.Level}, HP {hero.CurrentHP}/{hero.MaxHP}) + {validMercenaries.Count} mercenaries vs {validMonsters.Count} monsters");

                // Battle loop - continue until hero AND all mercenaries are dead, or all monsters are defeated
                while ((hero.CurrentHP > 0 || validMercenaries.Any(m => m.GetComponent<MercenaryComponent>()?.LinkedMercenary?.CurrentHP > 0)) 
                       && validMonsters.Any(m => m.GetComponent<EnemyComponent>()?.Enemy.CurrentHP > 0))
                {
                    // Wait while paused before starting each round
                    yield return WaitWhilePaused();

                    // Calculate turn values for all participants at start of each round
                    for (int i = 0; i < participants.Count; i++)
                    {
                        var participant = participants[i];
                        if (participant.Type == BattleParticipant.ParticipantType.Hero)
                        {
                            // Check if hero is still alive before calculating turn
                            if (hero.CurrentHP > 0)
                            {
                                participant.TurnValue = CalculateTurnValue(hero.GetTotalStats().Agility);

                                // Use AI to decide action when queue is empty
                                if (!heroComponent.BattleActionQueue.HasActions())
                                {
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
                            }
                            else
                            {
                                participant.TurnValue = -1; // Mark as dead/invalid
                            }
                        }
                        else if (participant.Type == BattleParticipant.ParticipantType.Mercenary)
                        {
                            var mercComponent = participant.MercenaryEntity.GetComponent<MercenaryComponent>();
                            if (mercComponent?.LinkedMercenary != null && mercComponent.LinkedMercenary.CurrentHP > 0)
                            {
                                participant.TurnValue = CalculateTurnValue(mercComponent.LinkedMercenary.GetTotalStats().Agility);
                            }
                            else
                            {
                                participant.TurnValue = -1; // Mark as dead/invalid
                            }
                        }
                        else // Monster
                        {
                            var enemyComponent = participant.MonsterEntity.GetComponent<EnemyComponent>();
                            if (enemyComponent?.Enemy != null && enemyComponent.Enemy.CurrentHP > 0)
                            {
                                participant.TurnValue = CalculateTurnValue(enemyComponent.Enemy.Stats.Agility);
                            }
                            else
                            {
                                participant.TurnValue = -1; // Mark as dead/invalid
                            }
                        }
                        participants[i] = participant;
                    }

                    // Sort participants by turn value (highest first)
                    participants.Sort((a, b) => b.TurnValue.CompareTo(a.TurnValue));

                    // Execute turns in order
                    foreach (var participant in participants)
                    {
                        if (participant.TurnValue < 0) continue; // Skip dead/invalid participants

                        // Wait while paused before each participant's turn
                        yield return WaitWhilePaused();

                        // Show turn indicator above the active participant
                        if (participant.Type == BattleParticipant.ParticipantType.Hero)
                            turnIndicator.Show(heroComponent.Entity);
                        else if (participant.Type == BattleParticipant.ParticipantType.Mercenary)
                            turnIndicator.Show(participant.MercenaryEntity);
                        else
                            turnIndicator.Show(participant.MonsterEntity, true);

                        if (participant.Type == BattleParticipant.ParticipantType.Hero)
                        {
                            // Check if there's a queued action
                            var queuedAction = heroComponent.BattleActionQueue.Dequeue();

                            // Re-evaluate if healing is urgently needed since damage may have occurred since the action was queued
                            // This catches the scenario where a monster attacks AFTER the hero's action was decided at round start
                            // We only override non-healing actions (Attack or attack-type UseSkill)
                            bool isQueuedOffensiveAction = queuedAction != null && 
                                (queuedAction.ActionType == QueuedActionType.Attack ||
                                 (queuedAction.ActionType == QueuedActionType.UseSkill && 
                                  queuedAction.Skill != null && queuedAction.Skill.HPRestoreAmount <= 0));
                            
                            if (isQueuedOffensiveAction)
                            {
                                var currentLivingMonsters = GetLivingMonsters(validMonsters);
                                var reEvaluatedDecision = BattleTacticDecisionEngine.DecideHeroAction(heroComponent, currentLivingMonsters, validMercenaries);
                                if (reEvaluatedDecision.Kind == BattleAction.ActionKind.UseHealingSkill ||
                                    reEvaluatedDecision.Kind == BattleAction.ActionKind.UseConsumable)
                                {
                                    Debug.Log($"[AttackMonster] Hero re-evaluated: overriding queued offensive action with {reEvaluatedDecision.Kind} (healing needed since action was queued)");
                                    // Override the queued action with the re-evaluated healing action
                                    switch (reEvaluatedDecision.Kind)
                                    {
                                        case BattleAction.ActionKind.UseHealingSkill:
                                            queuedAction = new QueuedAction(reEvaluatedDecision.Skill);
                                            queuedAction.Target = reEvaluatedDecision.Target;
                                            queuedAction.TargetsHero = reEvaluatedDecision.TargetsHero;
                                            break;
                                        case BattleAction.ActionKind.UseConsumable:
                                            queuedAction = new QueuedAction(reEvaluatedDecision.Consumable, reEvaluatedDecision.BagIndex);
                                            queuedAction.Target = reEvaluatedDecision.Target;
                                            queuedAction.TargetsHero = reEvaluatedDecision.TargetsHero;
                                            break;
                                    }
                                }
                            }

                            if (queuedAction != null)
                            {
                                // Execute the queued action
                                Debug.Log($"[AttackMonster] Hero's turn - executing queued action: {queuedAction.ActionType}");

                                if (queuedAction.ActionType == QueuedActionType.UseItem)
                                {
                                    var consumable = queuedAction.Consumable;
                                    var consumeTarget = queuedAction.Target ?? hero;
                                    Debug.Log($"[AttackMonster] Using queued item: {consumable.Name}");

                                    // Track HP before for healing display
                                    int hpBeforeItem = 0;
                                    if (consumeTarget is Hero heroPreItem)
                                        hpBeforeItem = heroPreItem.CurrentHP;
                                    else if (consumeTarget is Mercenary mercPreItem)
                                        hpBeforeItem = mercPreItem.CurrentHP;

                                    if (consumable.Consume(consumeTarget))
                                    {
                                        heroComponent.Bag.ConsumeFromStack(queuedAction.BagIndex);
                                        Debug.Log($"[AttackMonster] Successfully used {consumable.Name}");
                                        PitHero.UI.InventorySelectionManager.OnInventoryChanged?.Invoke();

                                        // Show healing effect if HP was restored
                                        int hpAfterItem = 0;
                                        if (consumeTarget is Hero heroPostItem)
                                            hpAfterItem = heroPostItem.CurrentHP;
                                        else if (consumeTarget is Mercenary mercPostItem)
                                            hpAfterItem = mercPostItem.CurrentHP;

                                        int itemHealAmount = hpAfterItem - hpBeforeItem;
                                        if (itemHealAmount > 0)
                                        {
                                            var itemTargetEntity = FindTargetEntity(consumeTarget, queuedAction.TargetsHero, heroComponent, validMercenaries);
                                            SoundEffectManager itemSfx = Core.GetGlobalManager<SoundEffectManager>();
                                            itemSfx?.PlaySound(SoundEffectType.Restorative);

                                            var itemDigit = itemTargetEntity?.GetComponent<BouncyDigitComponent>();
                                            if (itemDigit != null)
                                            {
                                                itemDigit.Init(itemHealAmount, Color.Green, false);
                                                itemDigit.SetEnabled(true);
                                                yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Debug.Log($"[AttackMonster] Failed to use {consumable.Name}");
                                    }
                                }
                                else if (queuedAction.ActionType == QueuedActionType.UseSkill)
                                {
                                    // Use the queued skill
                                    var skill = queuedAction.Skill;
                                    Debug.Log($"[AttackMonster] Using queued skill: {skill.Name}");

                                    // Check if hero has enough MP
                                    if (hero.CurrentMP >= skill.MPCost)
                                    {
                                        // Check if this is a healing/buff skill (targets allies)
                                        if (skill.HPRestoreAmount > 0 || skill.MPRestoreAmount > 0 ||
                                            skill.TargetType == SkillTargetType.Self ||
                                            skill.TargetType == SkillTargetType.SingleAlly ||
                                            skill.TargetType == SkillTargetType.AllAllies)
                                        {
                                            hero.SpendMP(skill.MPCost);
                                            var healTarget = queuedAction.Target ?? hero;

                                            if (skill.HPRestoreAmount > 0)
                                            {
                                                bool healed = false;
                                                if (healTarget is Hero hpHero)
                                                    healed = hpHero.RestoreHP(skill.HPRestoreAmount);
                                                else if (healTarget is Mercenary hpMerc)
                                                    healed = hpMerc.RestoreHP(skill.HPRestoreAmount);

                                                if (healed)
                                                {
                                                    var hEntity = FindTargetEntity(healTarget, queuedAction.TargetsHero, heroComponent, validMercenaries);
                                                    SoundEffectManager hSfx = Core.GetGlobalManager<SoundEffectManager>();
                                                    hSfx?.PlaySound(SoundEffectType.Restorative);

                                                    var hDigit = hEntity?.GetComponent<BouncyDigitComponent>();
                                                    if (hDigit != null)
                                                    {
                                                        hDigit.Init(skill.HPRestoreAmount, Color.Green, false);
                                                        hDigit.SetEnabled(true);
                                                        yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                                    }
                                                }
                                            }

                                            if (skill.MPRestoreAmount > 0)
                                            {
                                                if (healTarget is Hero mpHero)
                                                    mpHero.RestoreMP(skill.MPRestoreAmount);
                                                else if (healTarget is Mercenary mpMerc)
                                                    mpMerc.RestoreMP(skill.MPRestoreAmount);
                                            }

                                            Debug.Log($"[AttackMonster] Used healing skill {skill.Name}");
                                        }
                                        else
                                        {
                                        // Attack skill - get living monsters as targets
                                        var livingMonsters = GetLivingMonsters(validMonsters);

                                        if (livingMonsters.Count == 0) break; // All monsters dead

                                        // Store HP before skill execution to calculate damage dealt
                                        var monsterHPBefore = new Dictionary<RolePlayingFramework.Enemies.IEnemy, int>();
                                        for (int i = 0; i < livingMonsters.Count; i++)
                                        {
                                            var enemyComp = livingMonsters[i].GetComponent<EnemyComponent>();
                                            if (enemyComp?.Enemy != null)
                                                monsterHPBefore[enemyComp.Enemy] = enemyComp.Enemy.CurrentHP;
                                        }

                                        // Cache components to avoid repeated lookups
                                        var primaryTarget = livingMonsters[0].GetComponent<EnemyComponent>()?.Enemy;
                                        var surroundingTargets = new List<RolePlayingFramework.Enemies.IEnemy>();
                                        for (int i = 1; i < livingMonsters.Count; i++)
                                        {
                                            var enemyComp = livingMonsters[i].GetComponent<EnemyComponent>();
                                            if (enemyComp?.Enemy != null)
                                                surroundingTargets.Add(enemyComp.Enemy);
                                        }

                                        // Execute the skill
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

                                            // Calculate damage dealt
                                            if (monsterHPBefore.TryGetValue(enemy, out int hpBefore))
                                            {
                                                int damage = hpBefore - enemy.CurrentHP;
                                                if (damage > 0)
                                                {
                                                    Debug.Log($"[AttackMonster] {skill.Name} dealt {damage} damage to {enemy.Name}. Enemy HP: {enemy.CurrentHP}/{enemy.MaxHP}");

                                                    // Display damage on enemy
                                                    var enemyBouncyDigit = monsterEntity.GetComponent<BouncyDigitComponent>();
                                                    if (enemyBouncyDigit != null)
                                                    {
                                                        enemyBouncyDigit.Init(damage, BouncyDigitComponent.EnemyDigitColor, false);
                                                        enemyBouncyDigit.SetEnabled(true);
                                                    }

                                                    // Check if enemy died
                                                    if (enemy.CurrentHP <= 0)
                                                    {
                                                        Debug.Log($"[AttackMonster] {enemy.Name} defeated by {skill.Name}! Starting fade out");
                                                        hero.AddExperience(enemy.ExperienceYield);
                                                        hero.EarnJP(enemy.JPYield);
                                                        hero.EarnSynergyPointsWithAcceleration(enemy.SPYield);

                                                        AwardMercenaryExperience(validMercenaries, enemy.ExperienceYield);

                                                        // Add gold to global Funds
                                                        var gameState = Nez.Core.Services.GetService<PitHero.Services.GameStateService>();
                                                        if (gameState != null)
                                                        {
                                                            gameState.Funds += enemy.GoldYield;

                                                            if (enemy.GoldYield > 0)
                                                            {
                                                                heroComponent.InnExhausted = false;
                                                            }
                                                        }

                                                        Debug.Log($"[AttackMonster] Earned {enemy.ExperienceYield} XP, {enemy.JPYield} JP, {enemy.SPYield} SP, {enemy.GoldYield} Gold");

                                                        // Try to recruit the defeated monster
                                                        var alliedMonsterMgr = Core.Services.GetService<PitHero.Services.AlliedMonsterManager>();
                                                        if (alliedMonsterMgr != null)
                                                        {
                                                            var recruited = alliedMonsterMgr.TryRecruit(enemy);
                                                            if (recruited != null)
                                                                Debug.Log($"[AttackMonster] {enemy.Name} recruited as '{recruited.Name}'");
                                                        }

                                                        validMonsters.Remove(monsterEntity);
                                                    }
                                                }
                                            }
                                        }

                                        // Wait for damage display
                                        yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);

                                        // Fade out and destroy all dead monsters
                                        var deadMonsters = livingMonsters.Where(m => m.GetComponent<EnemyComponent>()?.Enemy?.CurrentHP <= 0).ToList();
                                        foreach (var deadMonster in deadMonsters)
                                        {
                                            yield return FadeOutAndDestroyMonster(deadMonster);
                                        }
                                        } // end attack skill else
                                    }
                                    else
                                    {
                                        Debug.Log($"[AttackMonster] Not enough MP to use {skill.Name} (need {skill.MPCost}, have {hero.CurrentMP})");
                                    }
                                }
                                else if (queuedAction.ActionType == QueuedActionType.Attack)
                                {
                                    // Execute queued attack
                                    var livingMonsters = GetLivingMonsters(validMonsters);
                                    if (livingMonsters.Count == 0) break; // All monsters dead

                                    var targetMonster = livingMonsters[Nez.Random.Range(0, livingMonsters.Count)];
                                    var targetEnemy = targetMonster.GetComponent<EnemyComponent>().Enemy;
                                    var targetBattleStats = BattleStats.CalculateForMonster(targetEnemy);

                                    Debug.Log($"[AttackMonster] Hero's turn - attacking {targetEnemy.Name}");
                                    var heroAttackResult = attackResolver.Resolve(heroBattleStats, targetBattleStats, DamageKind.Physical);

                                    // Play punch sound effect for unarmed attacks
                                    if (queuedAction.WeaponItem == null)
                                    {
                                        SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
                                        soundEffectManager?.PlaySound(SoundEffectType.Punch);
                                    }

                                    if (heroAttackResult.Hit)
                                    {
                                        bool enemyDied = targetEnemy.TakeDamage(heroAttackResult.Damage);
                                        Debug.Log($"[AttackMonster] Hero deals {heroAttackResult.Damage} damage to {targetEnemy.Name}. Enemy HP: {targetEnemy.CurrentHP}/{targetEnemy.MaxHP}");

                                        // Display damage on enemy
                                        var enemyBouncyDigit = targetMonster.GetComponent<BouncyDigitComponent>();
                                        if (enemyBouncyDigit != null)
                                        {
                                            enemyBouncyDigit.Init(heroAttackResult.Damage, BouncyDigitComponent.EnemyDigitColor, false);
                                            enemyBouncyDigit.SetEnabled(true);
                                            yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                        }

                                        if (enemyDied)
                                        {
                                            Debug.Log($"[AttackMonster] {targetEnemy.Name} defeated! Starting fade out");
                                            hero.AddExperience(targetEnemy.ExperienceYield);
                                            hero.EarnJP(targetEnemy.JPYield);
                                            hero.EarnSynergyPointsWithAcceleration(targetEnemy.SPYield);

                                            AwardMercenaryExperience(validMercenaries, targetEnemy.ExperienceYield);
                                            
                                            // Add gold to global Funds
                                            var gameState = Nez.Core.Services.GetService<PitHero.Services.GameStateService>();
                                            if (gameState != null)
                                            {
                                                gameState.Funds += targetEnemy.GoldYield;
                                                
                                                // Reset InnExhausted flag when hero gains any gold
                                                if (targetEnemy.GoldYield > 0)
                                                {
                                                    heroComponent.InnExhausted = false;
                                                }
                                                
                                                Debug.Log($"[AttackMonster] Earned {targetEnemy.ExperienceYield} XP, {targetEnemy.JPYield} JP, {targetEnemy.SPYield} SP, {targetEnemy.GoldYield} Gold (Total: {gameState.Funds})");
                                            }
                                            else
                                            {
                                                Debug.Log($"[AttackMonster] Earned {targetEnemy.ExperienceYield} XP, {targetEnemy.JPYield} JP, {targetEnemy.SPYield} SP, {targetEnemy.GoldYield} Gold");
                                            }
                                            
                                            // Try to recruit the defeated monster
                                            var alliedMonsterMgr = Core.Services.GetService<PitHero.Services.AlliedMonsterManager>();
                                            if (alliedMonsterMgr != null)
                                            {
                                                var recruited = alliedMonsterMgr.TryRecruit(targetEnemy);
                                                if (recruited != null)
                                                    Debug.Log($"[AttackMonster] {targetEnemy.Name} recruited as '{recruited.Name}'");
                                            }
                                            
                                            validMonsters.Remove(targetMonster);
                                            // Start fade coroutine (wait for completion so removal timing stays consistent)
                                            yield return FadeOutAndDestroyMonster(targetMonster);
                                        }
                                    }
                                    else
                                    {
                                        Debug.Log($"[AttackMonster] Hero missed {targetEnemy.Name}!");

                                        // Display "Miss" on enemy
                                        var enemyBouncyText = targetMonster.GetComponent<BouncyTextComponent>();
                                        if (enemyBouncyText != null)
                                        {
                                            enemyBouncyText.Init("Miss", BouncyTextComponent.EnemyMissColor);
                                            enemyBouncyText.SetEnabled(true);
                                            yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // No queued action - this shouldn't happen anymore since we auto-queue attacks
                                Debug.Warn("[AttackMonster] Hero turn but no queued action (unexpected)");
                            }
                        }
                        else if (participant.Type == BattleParticipant.ParticipantType.Mercenary)
                        {
                            var mercComponent = participant.MercenaryEntity.GetComponent<MercenaryComponent>();
                            if (mercComponent?.LinkedMercenary == null || mercComponent.LinkedMercenary.CurrentHP <= 0) continue;

                            var mercenary = mercComponent.LinkedMercenary;
                            var mercBattleStats = BattleStats.CalculateForMercenary(mercenary);

                            var livingMonsters = GetLivingMonsters(validMonsters);
                            if (livingMonsters.Count == 0) break;

                            var mercDecision = BattleTacticDecisionEngine.DecideMercenaryAction(mercComponent, heroComponent, livingMonsters, validMercenaries);

                            switch (mercDecision.Kind)
                            {
                                case BattleAction.ActionKind.UseHealingSkill:
                                {
                                    var healSkill = mercDecision.Skill;
                                    if (mercenary.CurrentMP >= healSkill.MPCost)
                                    {
                                        mercenary.UseMP(healSkill.MPCost);

                                        // Show action icon on HUD
                                        mercComponent.ActionQueueVisualization?.ShowAction(new QueuedAction(healSkill));

                                        var healTarget = mercDecision.Target;
                                        if (healTarget == null) healTarget = hero;

                                        if (healSkill.HPRestoreAmount > 0)
                                        {
                                            bool healed = false;
                                            if (healTarget is Hero mhHero)
                                                healed = mhHero.RestoreHP(healSkill.HPRestoreAmount);
                                            else if (healTarget is Mercenary mhMerc)
                                                healed = mhMerc.RestoreHP(healSkill.HPRestoreAmount);

                                            if (healed)
                                            {
                                                var mhEntity = FindTargetEntity(healTarget, mercDecision.TargetsHero, heroComponent, validMercenaries);
                                                SoundEffectManager mhSfx = Core.GetGlobalManager<SoundEffectManager>();
                                                mhSfx?.PlaySound(SoundEffectType.Restorative);

                                                var mhDigit = mhEntity?.GetComponent<BouncyDigitComponent>();
                                                if (mhDigit != null)
                                                {
                                                    mhDigit.Init(healSkill.HPRestoreAmount, Color.Green, false);
                                                    mhDigit.SetEnabled(true);
                                                    yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                                }
                                            }
                                        }

                                        if (healSkill.MPRestoreAmount > 0)
                                        {
                                            if (healTarget is Hero mpRestoreHero)
                                                mpRestoreHero.RestoreMP(healSkill.MPRestoreAmount);
                                            else if (healTarget is Mercenary mpRestoreMerc)
                                                mpRestoreMerc.RestoreMP(healSkill.MPRestoreAmount);
                                        }

                                        Debug.Log($"[AttackMonster] {mercenary.Name} used {healSkill.Name}");
                                    }
                                    break;
                                }

                                case BattleAction.ActionKind.UseConsumable:
                                {
                                    var mcConsumable = mercDecision.Consumable;
                                    var mcTarget = mercDecision.Target ?? hero;

                                    int mcHpBefore = 0;
                                    if (mcTarget is Hero mcPreHero)
                                        mcHpBefore = mcPreHero.CurrentHP;
                                    else if (mcTarget is Mercenary mcPreMerc)
                                        mcHpBefore = mcPreMerc.CurrentHP;

                                    if (mcConsumable.Consume(mcTarget))
                                    {
                                        heroComponent.Bag.ConsumeFromStack(mercDecision.BagIndex);
                                        Debug.Log($"[AttackMonster] {mercenary.Name} used {mcConsumable.Name}");
                                        PitHero.UI.InventorySelectionManager.OnInventoryChanged?.Invoke();

                                        int mcHpAfter = 0;
                                        if (mcTarget is Hero mcPostHero)
                                            mcHpAfter = mcPostHero.CurrentHP;
                                        else if (mcTarget is Mercenary mcPostMerc)
                                            mcHpAfter = mcPostMerc.CurrentHP;

                                        int mcHealAmt = mcHpAfter - mcHpBefore;
                                        if (mcHealAmt > 0)
                                        {
                                            var mcHealEntity = FindTargetEntity(mcTarget, mercDecision.TargetsHero, heroComponent, validMercenaries);
                                            SoundEffectManager mcSfx = Core.GetGlobalManager<SoundEffectManager>();
                                            mcSfx?.PlaySound(SoundEffectType.Restorative);

                                            var mcDigit = mcHealEntity?.GetComponent<BouncyDigitComponent>();
                                            if (mcDigit != null)
                                            {
                                                mcDigit.Init(mcHealAmt, Color.Green, false);
                                                mcDigit.SetEnabled(true);
                                                yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                            }
                                        }
                                    }
                                    break;
                                }

                                case BattleAction.ActionKind.UseAttackSkill:
                                {
                                    var atkSkill = mercDecision.Skill;
                                    if (mercenary.CurrentMP >= atkSkill.MPCost)
                                    {
                                        mercenary.UseMP(atkSkill.MPCost);
                                        var skillDamageKind = atkSkill.Element != ElementType.Neutral ? DamageKind.Magical : DamageKind.Physical;

                                        // Show action icon on HUD
                                        mercComponent.ActionQueueVisualization?.ShowAction(new QueuedAction(atkSkill));

                                        // Build target list based on skill target type
                                        var skillLiving = GetLivingMonsters(validMonsters);
                                        if (skillLiving.Count == 0) break;

                                        bool isAoE = atkSkill.TargetType == SkillTargetType.SurroundingEnemies;
                                        int startIndex = 0;
                                        int endIndex = isAoE ? skillLiving.Count : 1;

                                        // For single target, pick the decision target or random
                                        if (!isAoE && mercDecision.TargetEntity != null)
                                        {
                                            int found = -1;
                                            for (int si = 0; si < skillLiving.Count; si++)
                                            {
                                                if (skillLiving[si] == mercDecision.TargetEntity)
                                                {
                                                    found = si;
                                                    break;
                                                }
                                            }
                                            if (found >= 0)
                                            {
                                                startIndex = found;
                                                endIndex = found + 1;
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

                                                    // Award XP to hero and all mercenaries
                                                    hero.AddExperience(sEnemy.ExperienceYield);
                                                    hero.EarnJP(sEnemy.JPYield);
                                                    hero.EarnSynergyPointsWithAcceleration(sEnemy.SPYield);
                                                    AwardMercenaryExperience(validMercenaries, sEnemy.ExperienceYield);

                                                    var sGameState = Nez.Core.Services.GetService<PitHero.Services.GameStateService>();
                                                    if (sGameState != null)
                                                    {
                                                        sGameState.Funds += sEnemy.GoldYield;
                                                        Debug.Log($"[AttackMonster] Earned {sEnemy.GoldYield} Gold (Total: {sGameState.Funds})");
                                                    }

                                                    var sAllyMgr = Core.Services.GetService<PitHero.Services.AlliedMonsterManager>();
                                                    if (sAllyMgr != null)
                                                    {
                                                        var sRecruited = sAllyMgr.TryRecruit(sEnemy);
                                                        if (sRecruited != null)
                                                            Debug.Log($"[AttackMonster] {sEnemy.Name} recruited as '{sRecruited.Name}'");
                                                    }

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

                                        // Fade out dead monsters
                                        for (int di = skillLiving.Count - 1; di >= 0; di--)
                                        {
                                            var dEntity = skillLiving[di];
                                            var dComp = dEntity.GetComponent<EnemyComponent>();
                                            if (dComp?.Enemy != null && dComp.Enemy.CurrentHP <= 0)
                                            {
                                                yield return FadeOutAndDestroyMonster(dEntity);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Debug.Log($"[AttackMonster] {mercenary.Name} not enough MP for {atkSkill.Name}");
                                    }
                                    break;
                                }

                                case BattleAction.ActionKind.PhysicalAttack:
                                default:
                                {
                                    // Physical attack - use target from decision or pick random
                                    var paTarget = mercDecision.TargetEntity;
                                    if (paTarget == null || paTarget.GetComponent<EnemyComponent>()?.Enemy?.CurrentHP <= 0)
                                    {
                                        var paLiving = GetLivingMonsters(validMonsters);
                                        if (paLiving.Count == 0) break;
                                        paTarget = paLiving[Nez.Random.Range(0, paLiving.Count)];
                                    }

                                    var targetEnemy = paTarget.GetComponent<EnemyComponent>().Enemy;
                                    var targetBattleStats = BattleStats.CalculateForMonster(targetEnemy);

                                    Debug.Log($"[AttackMonster] {mercenary.Name}'s turn - attacking {targetEnemy.Name}");

                                    // Show action icon on HUD (weapon or unarmed)
                                    mercComponent.ActionQueueVisualization?.ShowAction(new QueuedAction(mercenary.WeaponShield1));

                                    // Make mercenary face the target monster
                                    FaceTarget(participant.MercenaryEntity, paTarget.Transform.Position);

                                    var mercAttackResult = attackResolver.Resolve(mercBattleStats, targetBattleStats, DamageKind.Physical);

                                    // Play punch sound effect
                                    SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
                                    soundEffectManager?.PlaySound(SoundEffectType.Punch);

                                    if (mercAttackResult.Hit)
                                    {
                                        bool enemyDied = targetEnemy.TakeDamage(mercAttackResult.Damage);
                                        Debug.Log($"[AttackMonster] {mercenary.Name} deals {mercAttackResult.Damage} damage to {targetEnemy.Name}. Enemy HP: {targetEnemy.CurrentHP}/{targetEnemy.MaxHP}");

                                        var enemyBouncyDigit = paTarget.GetComponent<BouncyDigitComponent>();
                                        if (enemyBouncyDigit != null)
                                        {
                                            enemyBouncyDigit.Init(mercAttackResult.Damage, BouncyDigitComponent.EnemyDigitColor, false);
                                            enemyBouncyDigit.SetEnabled(true);
                                            yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                        }

                                        if (enemyDied)
                                        {
                                            Debug.Log($"[AttackMonster] {targetEnemy.Name} defeated by {mercenary.Name}! Starting fade out");

                                            // Award XP to hero and all mercenaries
                                            hero.AddExperience(targetEnemy.ExperienceYield);
                                            hero.EarnJP(targetEnemy.JPYield);
                                            hero.EarnSynergyPointsWithAcceleration(targetEnemy.SPYield);
                                            AwardMercenaryExperience(validMercenaries, targetEnemy.ExperienceYield);
                                            
                                            var gameState = Nez.Core.Services.GetService<PitHero.Services.GameStateService>();
                                            if (gameState != null)
                                            {
                                                gameState.Funds += targetEnemy.GoldYield;
                                                Debug.Log($"[AttackMonster] Earned {targetEnemy.GoldYield} Gold (Total: {gameState.Funds})");
                                            }

                                            var alliedMonsterMgr = Core.Services.GetService<PitHero.Services.AlliedMonsterManager>();
                                            if (alliedMonsterMgr != null)
                                            {
                                                var recruited = alliedMonsterMgr.TryRecruit(targetEnemy);
                                                if (recruited != null)
                                                    Debug.Log($"[AttackMonster] {targetEnemy.Name} recruited as '{recruited.Name}'");
                                            }
                                            
                                            validMonsters.Remove(paTarget);
                                            yield return FadeOutAndDestroyMonster(paTarget);
                                        }
                                    }
                                    else
                                    {
                                        Debug.Log($"[AttackMonster] {mercenary.Name} missed {targetEnemy.Name}!");

                                        var enemyBouncyText = paTarget.GetComponent<BouncyTextComponent>();
                                        if (enemyBouncyText != null)
                                        {
                                            enemyBouncyText.Init("Miss", BouncyTextComponent.EnemyMissColor);
                                            enemyBouncyText.SetEnabled(true);
                                            yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        else // Monster's turn
                        {
                            var enemyComponent = participant.MonsterEntity.GetComponent<EnemyComponent>();
                            if (enemyComponent?.Enemy == null || enemyComponent.Enemy.CurrentHP <= 0) continue;

                            var enemy = enemyComponent.Enemy;
                            var enemyBattleStats = BattleStats.CalculateForMonster(enemy);

                            // Pick a random target from hero and living mercenaries
                            var possibleTargets = new List<(Entity entity, bool isHero)>();
                            
                            if (hero.CurrentHP > 0)
                                possibleTargets.Add((heroComponent.Entity, true));

                            foreach (var mercEntity in validMercenaries)
                            {
                                var mercComp = mercEntity.GetComponent<MercenaryComponent>();
                                if (mercComp?.LinkedMercenary != null && mercComp.LinkedMercenary.CurrentHP > 0)
                                {
                                    possibleTargets.Add((mercEntity, false));
                                }
                            }

                            if (possibleTargets.Count == 0) continue; // No valid targets

                            var targetChoice = possibleTargets[Nez.Random.Range(0, possibleTargets.Count)];
                            var targetEntity = targetChoice.entity;
                            var targetIsHero = targetChoice.isHero;

                            // Make monster face the target when attacking
                            FaceTarget(participant.MonsterEntity, targetEntity.Transform.Position);

                            if (targetIsHero)
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

                                    // Register burst damage immediately so next heal decision sees it
                                    heroComponent.RegisterHeroBurstDamage(actualDamage);

                                    // Display damage on hero
                                    var heroBouncyDigit = heroComponent.Entity.GetComponent<BouncyDigitComponent>();
                                    if (heroBouncyDigit != null)
                                    {
                                        heroBouncyDigit.Init(enemyAttackResult.Damage, BouncyDigitComponent.HeroDigitColor, false);
                                        heroBouncyDigit.SetEnabled(true);
                                        yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                    }

                                    // If hero is alive and needs emergency heal, check if any mercenary healer can respond
                                    if (!heroDied && heroComponent.IsHeroHPCritical())
                                    {
                                        yield return TryEmergencyReactiveHeal(heroComponent, validMercenaries, hero, true);
                                    }

                                    if (heroDied)
                                    {
                                        Debug.Log($"[AttackMonster] {hero.Name} died! Battle continues with mercenaries.");

                                        // Start the hero death animation (but don't break - let mercenaries continue fighting)
                                        var deathComponent = heroComponent.Entity.GetComponent<HeroDeathComponent>();
                                        if (deathComponent == null)
                                        {
                                            deathComponent = heroComponent.Entity.AddComponent(new HeroDeathComponent());
                                        }
                                        deathComponent.StartDeathAnimation();

                                        // Don't break - mercenaries will continue the battle
                                    }
                                }
                                else
                                {
                                    Debug.Log($"[AttackMonster] {enemy.Name} missed {hero.Name}!");

                                    // Display "Miss" on hero
                                    var heroBouncyText = heroComponent.Entity.GetComponent<BouncyTextComponent>();
                                    if (heroBouncyText != null)
                                    {
                                        heroBouncyText.Init("Miss", BouncyTextComponent.HeroMissColor);
                                        heroBouncyText.SetEnabled(true);
                                        yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                    }
                                }
                            }
                            else // Target is mercenary
                            {
                                var targetMercComp = targetEntity.GetComponent<MercenaryComponent>();
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

                                    // Register burst damage immediately so next heal decision sees it
                                    heroComponent.RegisterMercenaryBurstDamage(targetEntity, targetMercComp, actualDamage);

                                    // Display damage on mercenary
                                    var mercBouncyDigit = targetEntity.GetComponent<BouncyDigitComponent>();
                                    if (mercBouncyDigit != null)
                                    {
                                        mercBouncyDigit.Init(enemyAttackResult.Damage, BouncyDigitComponent.HeroDigitColor, false);
                                        mercBouncyDigit.SetEnabled(true);
                                        yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                    }

                                    // If mercenary is alive and needs emergency heal, check if any healer can respond
                                    if (!mercDied && heroComponent.IsMercenaryHPCritical(targetEntity, targetMercComp))
                                    {
                                        yield return TryEmergencyReactiveHeal(heroComponent, validMercenaries, targetMercenary, false);
                                    }

                                    if (mercDied)
                                    {
                                        Debug.Log($"[AttackMonster] {targetMercenary.Name} died! Starting fade out");
                                        
                                        // Handle mercenary death and follower reassignment
                                        HandleMercenaryDeath(targetEntity, heroComponent, validMercenaries);
                                        validMercenaries.Remove(targetEntity);
                                        
                                        // Fade out and destroy the mercenary
                                        yield return FadeOutAndDestroyMercenary(targetEntity);
                                    }
                                }
                                else
                                {
                                    Debug.Log($"[AttackMonster] {enemy.Name} missed {targetMercenary.Name}!");

                                    // Display "Miss" on mercenary
                                    var mercBouncyText = targetEntity.GetComponent<BouncyTextComponent>();
                                    if (mercBouncyText != null)
                                    {
                                        mercBouncyText.Init("Miss", BouncyTextComponent.HeroMissColor);
                                        mercBouncyText.SetEnabled(true);
                                        yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                    }
                                }
                            }
                        }

                        // Wait between each participant's turn (respecting pause)
                        yield return WaitForSecondsRespectingPause(GameConfig.BattleTurnWait);

                        // Break if hero AND all mercenaries are dead, or all monsters are dead
                        bool allAlliesDead = hero.CurrentHP <= 0 && !validMercenaries.Any(m => m.GetComponent<MercenaryComponent>()?.LinkedMercenary?.CurrentHP > 0);
                        bool allMonstersDead = validMonsters.All(m => m.GetComponent<EnemyComponent>()?.Enemy.CurrentHP <= 0);
                        
                        if (allAlliesDead || allMonstersDead)
                        {
                            Debug.Log($"[AttackMonster] Battle ending - AllAlliesDead: {allAlliesDead}, AllMonstersDead: {allMonstersDead}");
                            break;
                        }
                    }

                    // Wait between rounds (respecting pause)
                    yield return WaitForSecondsRespectingPause(GameConfig.BattleTurnWait);
                }

                // Recalculate monster adjacency after battle (only if hero entity still exists)
                if (heroComponent.Entity != null)
                {
                    heroComponent.AdjacentToMonster = heroComponent.CheckAdjacentToMonster();
                }

                Debug.Log("[AttackMonster] Multi-participant battle sequence completed");
            }
            finally
            {
                // Always clear battle state
                HeroStateMachine.IsBattleInProgress = false;
                existingMultiParticipantBattleCoroutine = null;

                // Destroy battle turn indicator entity
                if (turnIndicatorEntity != null)
                {
                    turnIndicatorEntity.Destroy();
                }

                // Remove HP bar components from monsters
                if (validMonsters != null)
                {
                    foreach (var monsterEntity in validMonsters)
                    {
                        var hpBar = monsterEntity.GetComponent<MonsterHPBarComponent>();
                        if (hpBar != null)
                        {
                            monsterEntity.RemoveComponent(hpBar);
                        }
                    }
                }

                // Remove bouncy components from mercenaries
                if (validMercenaries != null)
                {
                    foreach (var mercEntity in validMercenaries)
                    {
                        var bouncyDigit = mercEntity.GetComponent<BouncyDigitComponent>();
                        if (bouncyDigit != null)
                        {
                            mercEntity.RemoveComponent(bouncyDigit);
                        }

                        var bouncyText = mercEntity.GetComponent<BouncyTextComponent>();
                        if (bouncyText != null)
                        {
                            mercEntity.RemoveComponent(bouncyText);
                        }
                    }
                }
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

        /// <summary>
        /// Attempts an emergency reactive heal from any healer mercenary when burst damage occurs.
        /// This allows healers to respond to urgent damage even if they've already acted this round.
        /// Returns an IEnumerator to yield during the heal animation.
        /// </summary>
        private System.Collections.IEnumerator TryEmergencyReactiveHeal(
            HeroComponent heroComponent,
            List<Entity> validMercenaries,
            object damagedTarget,
            bool targetIsHero)
        {
            // Check if target is in critical condition (either burst damage or HP threshold)
            bool needsEmergencyHeal = false;
            Entity targetEntity = null;
            int targetCurrentHP = 0;
            int targetMaxHP = 0;

            if (targetIsHero)
            {
                var hero = heroComponent.LinkedHero;
                if (hero != null && hero.CurrentHP > 0)
                {
                    needsEmergencyHeal = heroComponent.IsHeroHPCritical();
                    targetEntity = heroComponent.Entity;
                    targetCurrentHP = hero.CurrentHP;
                    targetMaxHP = hero.MaxHP;
                }
            }
            else if (damagedTarget is Mercenary damagedMerc)
            {
                // Find the mercenary entity
                for (int i = 0; i < validMercenaries.Count; i++)
                {
                    var mercComp = validMercenaries[i].GetComponent<MercenaryComponent>();
                    if (mercComp?.LinkedMercenary == damagedMerc && damagedMerc.CurrentHP > 0)
                    {
                        needsEmergencyHeal = heroComponent.IsMercenaryHPCritical(validMercenaries[i], mercComp);
                        targetEntity = validMercenaries[i];
                        targetCurrentHP = damagedMerc.CurrentHP;
                        targetMaxHP = damagedMerc.MaxHP;
                        break;
                    }
                }
            }

            if (!needsEmergencyHeal || targetEntity == null)
                yield break;

            Debug.Log($"[AttackMonster] Emergency heal check: target needs healing");

            // Check each mercenary for healing capability
            for (int i = 0; i < validMercenaries.Count; i++)
            {
                var healerEntity = validMercenaries[i];
                var healerMercComp = healerEntity.GetComponent<MercenaryComponent>();
                if (healerMercComp?.LinkedMercenary == null) continue;
                
                var healer = healerMercComp.LinkedMercenary;
                if (healer.CurrentHP <= 0) continue; // Dead healers can't heal
                if (healer == damagedTarget) continue; // Can't heal self in emergency (will be handled by their turn)

                // Check if this mercenary has a healing skill they can use
                var enumerator = healer.LearnedSkills.GetEnumerator();
                ISkill bestHealSkill = null;
                while (enumerator.MoveNext())
                {
                    var skill = enumerator.Current.Value;
                    if (skill.Kind == SkillKind.Active && 
                        skill.HPRestoreAmount > 0 && 
                        healer.CurrentMP >= skill.MPCost)
                    {
                        if (bestHealSkill == null || skill.HPRestoreAmount > bestHealSkill.HPRestoreAmount)
                        {
                            bestHealSkill = skill;
                        }
                    }
                }

                if (bestHealSkill != null)
                {
                    Debug.Log($"[AttackMonster] {healer.Name} performs emergency reactive heal using {bestHealSkill.Name}!");

                    // Use MP
                    healer.UseMP(bestHealSkill.MPCost);

                    // Show action icon on HUD
                    healerMercComp.ActionQueueVisualization?.ShowAction(new QueuedAction(bestHealSkill));

                    // Apply heal
                    bool healed = false;
                    if (targetIsHero)
                    {
                        healed = heroComponent.LinkedHero.RestoreHP(bestHealSkill.HPRestoreAmount);
                    }
                    else if (damagedTarget is Mercenary healedMerc)
                    {
                        healed = healedMerc.RestoreHP(bestHealSkill.HPRestoreAmount);
                    }

                    if (healed)
                    {
                        // Play heal sound
                        SoundEffectManager sfx = Core.GetGlobalManager<SoundEffectManager>();
                        sfx?.PlaySound(SoundEffectType.Restorative);

                        // Show healing digit
                        var digit = targetEntity.GetComponent<BouncyDigitComponent>();
                        if (digit != null)
                        {
                            digit.Init(bestHealSkill.HPRestoreAmount, Color.Green, false);
                            digit.SetEnabled(true);
                            yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                        }
                    }

                    // Only one healer responds per emergency
                    yield break;
                }
            }
        }

        // Temp list to avoid allocations each turn when picking random living monster
        private static readonly List<Entity> _tempLivingMonsters = new List<Entity>(16);
    }
}