using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.AI.Interfaces;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;
using System.Linq;

namespace PitHero.AI
{
    /// <summary>
    /// Represents a participant in a multi-participant battle
    /// </summary>
    public struct BattleParticipant
    {
        public bool IsHero;
        public HeroComponent HeroComponent;
        public Entity MonsterEntity;
        public float TurnValue;
        
        public BattleParticipant(HeroComponent hero)
        {
            IsHero = true;
            HeroComponent = hero;
            MonsterEntity = null;
            TurnValue = 0f;
        }
        
        public BattleParticipant(Entity monster)
        {
            IsHero = false;
            HeroComponent = null;
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
        public AttackMonsterAction() : base(GoapConstants.AttackMonster, 3)
        {
            // Preconditions: Hero must be adjacent to a monster
            SetPrecondition(GoapConstants.AdjacentToMonster, true);
            
            // Postconditions: Monster is defeated, recalculate adjacency
            SetPostcondition(GoapConstants.AdjacentToMonster, false);
        }

        public override bool Execute(HeroComponent hero)
        {
            Debug.Log("[AttackMonster] Starting monster attack");

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
            Core.StartCoroutine(ExecuteMultiParticipantBattleSequence(hero, adjacentMonsters));
            
            Debug.Log("[AttackMonster] Multi-participant battle started successfully");
            return true;
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
        /// Make hero face the target position
        /// </summary>
        private void FaceTarget(HeroComponent hero, Vector2 targetPosition)
        {
            // Calculate the direction vector
            var delta = targetPosition - hero.Entity.Transform.Position;

            // Determine the facing direction based on the direction vector
            Direction faceDir;
            if (System.Math.Abs(delta.X) >= System.Math.Abs(delta.Y))
                faceDir = delta.X < 0 ? Direction.Left : Direction.Right;
            else
                faceDir = delta.Y < 0 ? Direction.Up : Direction.Down;

            // Set the hero's facing direction
            var facing = hero.Entity.GetComponent<ActorFacingComponent>();
            facing?.SetFacing(faceDir);

            Debug.Log($"[AttackMonster] Hero facing direction set to {faceDir} using delta ({delta.X},{delta.Y})");
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
        /// Execute the battle sequence with timing and bouncy digits
        /// </summary>
        private System.Collections.IEnumerator ExecuteBattleSequence(HeroComponent heroComponent, Entity monsterEntity)
        {
            Debug.Log("[AttackMonster] Starting battle sequence");

            // Set battle in progress to prevent movement
            HeroStateMachine.IsBattleInProgress = true;

            try
            {
                // Get the enemy component
                var enemyComponent = monsterEntity.GetComponent<EnemyComponent>();
                if (enemyComponent?.Enemy == null)
                {
                    Debug.Warn("[AttackMonster] Monster entity has no EnemyComponent, destroying directly");
                    monsterEntity.Destroy();
                    yield break;
                }

                // Get the hero's linked RPG hero
                if (heroComponent.LinkedHero == null)
                {
                    Debug.Warn("[AttackMonster] Hero has no LinkedHero, cannot start battle");
                    yield break;
                }

                var hero = heroComponent.LinkedHero;
                var enemy = enemyComponent.Enemy;

                Debug.Log($"[AttackMonster] Battle: {hero.Name} (Lv.{hero.Level}, HP {hero.CurrentHP}/{hero.MaxHP}) vs {enemy.Name} (Lv.{enemy.Level}, HP {enemy.CurrentHP}/{enemy.MaxHP})");

                // Create attack resolver for battle calculations
                var attackResolver = new SimpleAttackResolver();

                // Battle loop - continue until one side dies
                while (hero.CurrentHP > 0 && enemy.CurrentHP > 0)
                {
                    // Hero attacks first
                    Debug.Log("[AttackMonster] Hero's turn");
                    var heroAttackResult = attackResolver.Resolve(hero.GetTotalStats(), enemy.Stats, DamageKind.Physical, hero.Level, enemy.Level);
                    if (heroAttackResult.Hit)
                    {
                        bool enemyDied = enemy.TakeDamage(heroAttackResult.Damage);
                        Debug.Log($"[AttackMonster] Hero deals {heroAttackResult.Damage} damage to {enemy.Name}. Enemy HP: {enemy.CurrentHP}/{enemy.MaxHP}");

                        // Display damage on enemy
                        var enemyBouncyDigit = monsterEntity.GetComponent<BouncyDigitComponent>();
                        if (enemyBouncyDigit != null)
                        {
                            enemyBouncyDigit.Init(heroAttackResult.Damage, BouncyDigitComponent.EnemyDigitColor, false);
                            enemyBouncyDigit.SetEnabled(true);
                        }

                        if (enemyDied)
                        {
                            Debug.Log($"[AttackMonster] {enemy.Name} defeated!");
                            yield return Coroutine.WaitForSeconds(1.0f); // Show final damage
                            hero.AddExperience(enemy.ExperienceYield);
                            monsterEntity.Destroy();
                            break;
                        }
                    }
                    else
                    {
                        Debug.Log($"[AttackMonster] Hero missed {enemy.Name}!");
                    }

                    // Wait 1 second after hero attack
                    yield return Coroutine.WaitForSeconds(1.0f);

                    // Enemy counter-attacks if still alive
                    if (enemy.CurrentHP > 0)
                    {
                        Debug.Log("[AttackMonster] Enemy's turn");
                        var enemyAttackResult = attackResolver.Resolve(enemy.Stats, hero.GetTotalStats(), enemy.AttackKind, enemy.Level, hero.Level);
                        if (enemyAttackResult.Hit)
                        {
                            // Apply defense gear as flat mitigation
                            var finalDamage = enemyAttackResult.Damage - hero.GetEquipmentDefenseBonus();
                            if (finalDamage < 1) finalDamage = 1;

                            bool heroDied = hero.TakeDamage(finalDamage);
                            Debug.Log($"[AttackMonster] {enemy.Name} deals {finalDamage} damage to {hero.Name}. Hero HP: {hero.CurrentHP}/{hero.MaxHP}");

                            // Display damage on hero
                            var heroBouncyDigit = heroComponent.Entity.GetComponent<BouncyDigitComponent>();
                            if (heroBouncyDigit != null)
                            {
                                heroBouncyDigit.Init(finalDamage, BouncyDigitComponent.HeroDigitColor, false);
                                heroBouncyDigit.SetEnabled(true);
                            }

                            if (heroDied)
                            {
                                Debug.Log($"[AttackMonster] {hero.Name} died! Refilling HP to full for now.");
                                yield return Coroutine.WaitForSeconds(1.0f); // Show damage
                                // Refill hero HP to full for now (as requested)
                                hero.Heal(hero.MaxHP);
                                break;
                            }
                        }
                        else
                        {
                            Debug.Log($"[AttackMonster] {enemy.Name} missed {hero.Name}!");
                        }

                        // Wait 1 second after enemy attack
                        yield return Coroutine.WaitForSeconds(1.0f);
                    }
                }

                // Recalculate monster adjacency after battle
                heroComponent.AdjacentToMonster = heroComponent.CheckAdjacentToMonster();
                
                Debug.Log("[AttackMonster] Battle sequence completed");
            }
            finally
            {
                // Always clear battle state
                HeroStateMachine.IsBattleInProgress = false;
            }
        }

        /// <summary>
        /// Execute the multi-participant battle sequence with all adjacent monsters
        /// </summary>
        private System.Collections.IEnumerator ExecuteMultiParticipantBattleSequence(HeroComponent heroComponent, List<Entity> monsterEntities)
        {
            Debug.Log("[AttackMonster] Starting multi-participant battle sequence");

            // Set battle in progress to prevent movement
            HeroStateMachine.IsBattleInProgress = true;

            try
            {
                // Get the hero's linked RPG hero
                if (heroComponent.LinkedHero == null)
                {
                    Debug.Warn("[AttackMonster] Hero has no LinkedHero, cannot start battle");
                    yield break;
                }

                var hero = heroComponent.LinkedHero;
                var attackResolver = new SimpleAttackResolver();

                // Create list of battle participants
                var participants = new List<BattleParticipant>();
                participants.Add(new BattleParticipant(heroComponent));

                // Add all monster participants and validate they have EnemyComponents
                var validMonsters = new List<Entity>();
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

                Debug.Log($"[AttackMonster] Multi-participant battle: {hero.Name} (Lv.{hero.Level}, HP {hero.CurrentHP}/{hero.MaxHP}) vs {validMonsters.Count} monsters");

                // Battle loop - continue until hero dies or all monsters are defeated
                while (hero.CurrentHP > 0 && validMonsters.Any(m => m.GetComponent<EnemyComponent>()?.Enemy.CurrentHP > 0))
                {
                    // Calculate turn values for all participants at start of each round
                    for (int i = 0; i < participants.Count; i++)
                    {
                        var participant = participants[i];
                        if (participant.IsHero)
                        {
                            participant.TurnValue = CalculateTurnValue(hero.GetTotalStats().Agility);
                        }
                        else
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

                        if (participant.IsHero)
                        {
                            // Hero's turn - attack a random living monster
                            var livingMonsters = validMonsters.Where(m => m.GetComponent<EnemyComponent>()?.Enemy.CurrentHP > 0).ToList();
                            if (livingMonsters.Count == 0) break; // All monsters dead

                            var targetMonster = livingMonsters[Nez.Random.Range(0, livingMonsters.Count)];
                            var targetEnemy = targetMonster.GetComponent<EnemyComponent>().Enemy;

                            Debug.Log($"[AttackMonster] Hero's turn - attacking {targetEnemy.Name}");
                            var heroAttackResult = attackResolver.Resolve(hero.GetTotalStats(), targetEnemy.Stats, DamageKind.Physical, hero.Level, targetEnemy.Level);
                            
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
                                    yield return Coroutine.WaitForSeconds(1f);
                                }

                                if (enemyDied)
                                {
                                    Debug.Log($"[AttackMonster] {targetEnemy.Name} defeated!");
                                    hero.AddExperience(targetEnemy.ExperienceYield);
                                    targetMonster.Destroy();
                                    validMonsters.Remove(targetMonster);
                                }
                            }
                            else
                            {
                                Debug.Log($"[AttackMonster] Hero missed {targetEnemy.Name}!");
                            }
                        }
                        else
                        {
                            // Monster's turn - attack hero if still alive
                            var enemyComponent = participant.MonsterEntity.GetComponent<EnemyComponent>();
                            if (enemyComponent?.Enemy == null || enemyComponent.Enemy.CurrentHP <= 0) continue;

                            var enemy = enemyComponent.Enemy;
                            Debug.Log($"[AttackMonster] {enemy.Name}'s turn - attacking hero");
                            
                            var enemyAttackResult = attackResolver.Resolve(enemy.Stats, hero.GetTotalStats(), enemy.AttackKind, enemy.Level, hero.Level);
                            if (enemyAttackResult.Hit)
                            {
                                // Apply defense gear as flat mitigation
                                var finalDamage = enemyAttackResult.Damage - hero.GetEquipmentDefenseBonus();
                                if (finalDamage < 1) finalDamage = 1;

                                bool heroDied = hero.TakeDamage(finalDamage);
                                Debug.Log($"[AttackMonster] {enemy.Name} deals {finalDamage} damage to {hero.Name}. Hero HP: {hero.CurrentHP}/{hero.MaxHP}");

                                // Display damage on hero
                                var heroBouncyDigit = heroComponent.Entity.GetComponent<BouncyDigitComponent>();
                                if (heroBouncyDigit != null)
                                {
                                    heroBouncyDigit.Init(finalDamage, BouncyDigitComponent.HeroDigitColor, false);
                                    heroBouncyDigit.SetEnabled(true);
                                    yield return Coroutine.WaitForSeconds(1f);
                                }

                                if (heroDied)
                                {
                                    Debug.Log($"[AttackMonster] {hero.Name} died! Refilling HP to full for now.");
                                    // Refill hero HP to full for now (as requested)
                                    hero.Heal(hero.MaxHP);
                                    break; // End battle
                                }
                            }
                            else
                            {
                                Debug.Log($"[AttackMonster] {enemy.Name} missed {hero.Name}!");
                            }
                        }

                        // Wait between each participant's turn
                        yield return Coroutine.WaitForSeconds(1.0f);

                        // Break if hero died or all monsters are dead
                        if (hero.CurrentHP <= 0 || validMonsters.All(m => m.GetComponent<EnemyComponent>()?.Enemy.CurrentHP <= 0))
                            break;
                    }

                    // Wait between rounds
                    yield return Coroutine.WaitForSeconds(0.5f);
                }

                // Recalculate monster adjacency after battle
                heroComponent.AdjacentToMonster = heroComponent.CheckAdjacentToMonster();
                
                Debug.Log("[AttackMonster] Multi-participant battle sequence completed");
            }
            finally
            {
                // Always clear battle state
                HeroStateMachine.IsBattleInProgress = false;
            }
        }
    }
}