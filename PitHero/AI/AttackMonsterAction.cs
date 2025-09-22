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

            // Find the nearest adjacent monster
            var monsterEntity = FindNearestAdjacentMonster(hero);
            if (monsterEntity == null)
            {
                Debug.Warn("[AttackMonster] Could not find adjacent monster");
                // Recalculate if there are still monsters adjacent to hero
                hero.AdjacentToMonster = hero.CheckAdjacentToMonster();
                return true; // Complete as failed
            }

            // Face the monster
            FaceTarget(hero, monsterEntity.Transform.Position);

            // Perform attack animation (simulate by moving hero slightly)
            PerformAttackAnimation(hero);

            // Start battle sequence using coroutine
            Core.StartCoroutine(ExecuteBattleSequence(hero, monsterEntity));
            
            Debug.Log("[AttackMonster] Battle started successfully");
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
        /// Find the nearest adjacent monster to the hero
        /// </summary>
        private Entity FindNearestAdjacentMonster(HeroComponent hero)
        {
            var heroTile = GetCurrentTilePosition(hero);
            var scene = Core.Scene;
            if (scene == null) return null;

            var monsterEntities = scene.FindEntitiesWithTag(GameConfig.TAG_MONSTER);
            Entity nearestMonster = null;
            float nearestDistance = float.MaxValue;

            foreach (var monster in monsterEntities)
            {
                var monsterTile = GetTileCoordinates(monster.Transform.Position);
                if (IsAdjacent(heroTile, monsterTile))
                {
                    float distance = Vector2.Distance(hero.Entity.Transform.Position, monster.Transform.Position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestMonster = monster;
                    }
                }
            }

            return nearestMonster;
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
    }
}