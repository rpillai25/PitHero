using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;
using RolePlayingFramework.Combat;
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
                            participant.TurnValue = CalculateTurnValue(hero.GetTotalStats().Agility);

                            // Queue default attack for hero if queue is empty
                            if (!heroComponent.BattleActionQueue.HasActions())
                            {
                                // Use equipped weapon or null for unarmed attack
                                heroComponent.BattleActionQueue.EnqueueAttack(hero.WeaponShield1);
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

                        if (participant.Type == BattleParticipant.ParticipantType.Hero)
                        {
                            // Check if there's a queued action
                            var queuedAction = heroComponent.BattleActionQueue.Dequeue();

                            if (queuedAction != null)
                            {
                                // Execute the queued action
                                Debug.Log($"[AttackMonster] Hero's turn - executing queued action: {queuedAction.ActionType}");

                                if (queuedAction.ActionType == QueuedActionType.UseItem)
                                {
                                    // Use the queued consumable
                                    var consumable = queuedAction.Consumable;
                                    Debug.Log($"[AttackMonster] Using queued item: {consumable.Name}");

                                    if (consumable.Consume(hero))
                                    {
                                        // Decrement stack or remove item from bag
                                        heroComponent.Bag.ConsumeFromStack(queuedAction.BagIndex);
                                        Debug.Log($"[AttackMonster] Successfully used {consumable.Name}");

                                        // Notify UI that inventory has changed
                                        PitHero.UI.InventorySelectionManager.OnInventoryChanged?.Invoke();
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
                                        // Get living monsters as targets
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
                                                        Debug.Log($"[AttackMonster] Earned {enemy.ExperienceYield} XP, {enemy.JPYield} JP, {enemy.SPYield} SP");
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
                            // Mercenary's turn - attack a random monster
                            var mercComponent = participant.MercenaryEntity.GetComponent<MercenaryComponent>();
                            if (mercComponent?.LinkedMercenary == null || mercComponent.LinkedMercenary.CurrentHP <= 0) continue;

                            var mercenary = mercComponent.LinkedMercenary;
                            var mercBattleStats = BattleStats.CalculateForMercenary(mercenary);

                            var livingMonsters = GetLivingMonsters(validMonsters);
                            if (livingMonsters.Count == 0) break; // All monsters dead

                            var targetMonster = livingMonsters[Nez.Random.Range(0, livingMonsters.Count)];
                            var targetEnemy = targetMonster.GetComponent<EnemyComponent>().Enemy;
                            var targetBattleStats = BattleStats.CalculateForMonster(targetEnemy);

                            Debug.Log($"[AttackMonster] {mercenary.Name}'s turn - attacking {targetEnemy.Name}");

                            // Make mercenary face the target monster
                            FaceTarget(participant.MercenaryEntity, targetMonster.Transform.Position);

                            var mercAttackResult = attackResolver.Resolve(mercBattleStats, targetBattleStats, DamageKind.Physical);

                            // Play punch sound effect for unarmed attacks (mercenaries currently don't have weapons)
                            SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
                            soundEffectManager?.PlaySound(SoundEffectType.Punch);

                            if (mercAttackResult.Hit)
                            {
                                bool enemyDied = targetEnemy.TakeDamage(mercAttackResult.Damage);
                                Debug.Log($"[AttackMonster] {mercenary.Name} deals {mercAttackResult.Damage} damage to {targetEnemy.Name}. Enemy HP: {targetEnemy.CurrentHP}/{targetEnemy.MaxHP}");

                                // Display damage on enemy
                                var enemyBouncyDigit = targetMonster.GetComponent<BouncyDigitComponent>();
                                if (enemyBouncyDigit != null)
                                {
                                    enemyBouncyDigit.Init(mercAttackResult.Damage, BouncyDigitComponent.EnemyDigitColor, false);
                                    enemyBouncyDigit.SetEnabled(true);
                                    yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
                                }

                                if (enemyDied)
                                {
                                    Debug.Log($"[AttackMonster] {targetEnemy.Name} defeated by {mercenary.Name}! Starting fade out");
                                    
                                    // Add gold to global Funds (mercenaries don't gain XP/JP/SP but gold is still awarded)
                                    var gameState = Nez.Core.Services.GetService<PitHero.Services.GameStateService>();
                                    if (gameState != null)
                                    {
                                        gameState.Funds += targetEnemy.GoldYield;
                                        Debug.Log($"[AttackMonster] Earned {targetEnemy.GoldYield} Gold (Total: {gameState.Funds})");
                                    }
                                    
                                    validMonsters.Remove(targetMonster);
                                    yield return FadeOutAndDestroyMonster(targetMonster);
                                }
                            }
                            else
                            {
                                Debug.Log($"[AttackMonster] {mercenary.Name} missed {targetEnemy.Name}!");

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
                                    bool heroDied = hero.TakeDamage(enemyAttackResult.Damage * DEBUG_DAMAGE_MULT);
                                    Debug.Log($"[AttackMonster] {enemy.Name} deals {enemyAttackResult.Damage} damage to {hero.Name}. Hero HP: {hero.CurrentHP}/{hero.MaxHP}");

                                    // Display damage on hero
                                    var heroBouncyDigit = heroComponent.Entity.GetComponent<BouncyDigitComponent>();
                                    if (heroBouncyDigit != null)
                                    {
                                        heroBouncyDigit.Init(enemyAttackResult.Damage, BouncyDigitComponent.HeroDigitColor, false);
                                        heroBouncyDigit.SetEnabled(true);
                                        yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
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
                                    bool mercDied = targetMercenary.TakeDamage(enemyAttackResult.Damage * DEBUG_DAMAGE_MULT);
                                    Debug.Log($"[AttackMonster] {enemy.Name} deals {enemyAttackResult.Damage} damage to {targetMercenary.Name}. Mercenary HP: {targetMercenary.CurrentHP}/{targetMercenary.MaxHP}");

                                    // Display damage on mercenary
                                    var mercBouncyDigit = targetEntity.GetComponent<BouncyDigitComponent>();
                                    if (mercBouncyDigit != null)
                                    {
                                        mercBouncyDigit.Init(enemyAttackResult.Damage, BouncyDigitComponent.HeroDigitColor, false);
                                        mercBouncyDigit.SetEnabled(true);
                                        yield return WaitForSecondsRespectingPause(GameConfig.BattleDigitBounceWait);
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

        // Temp list to avoid allocations each turn when picking random living monster
        private static readonly List<Entity> _tempLivingMonsters = new List<Entity>(16);
    }
}