using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.Combat;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using RolePlayingFramework.Enemies;
using System.Collections;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to attack an adjacent monster.
    /// Hero faces the monster, performs attack animation, then delegates the full
    /// multi-participant battle loop to <see cref="BattleEngine"/> via
    /// <see cref="LiveBattleAdapter"/>.
    /// </summary>
    public class AttackMonsterAction : HeroActionBase
    {
        private ICoroutine _battleCoroutine;

        public AttackMonsterAction() : base(GoapConstants.AttackMonster, 3)
        {
            // Preconditions: Hero must be adjacent to a monster
            SetPrecondition(GoapConstants.AdjacentToMonster, true);

            // Postconditions: Monster is defeated, recalculate adjacency
            SetPostcondition(GoapConstants.AdjacentToMonster, false);
            SetPostcondition(GoapConstants.BossDefeated, true);
        }

        public override bool Execute(HeroComponent hero)
        {
            if (_battleCoroutine != null)
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

            // Separate valid (has EnemyComponent) monster entities from IEnemy instances;
            // destroy invalid entities in place
            var validMonsterEntities = new List<Entity>(adjacentMonsters.Count);
            var monsters             = new List<IEnemy>(adjacentMonsters.Count);
            for (int i = 0; i < adjacentMonsters.Count; i++)
            {
                var ec = adjacentMonsters[i].GetComponent<EnemyComponent>();
                if (ec?.Enemy != null)
                {
                    validMonsterEntities.Add(adjacentMonsters[i]);
                    monsters.Add(ec.Enemy);
                }
                else
                {
                    Debug.Warn("[AttackMonster] Monster entity has no EnemyComponent, skipping");
                    adjacentMonsters[i].Destroy();
                }
            }

            if (monsters.Count == 0)
            {
                Debug.Log("[AttackMonster] No valid monsters to fight");
                hero.AdjacentToMonster = hero.CheckAdjacentToMonster();
                return true;
            }

            // Build ally wrappers for the engine
            var heroAlly   = new LiveHeroAlly(hero);
            var mercAllies = new List<IBattleAlly>();
            var mercEntities = FindMercenariesInPit();
            for (int i = 0; i < mercEntities.Count; i++)
            {
                var mc = mercEntities[i].GetComponent<MercenaryComponent>();
                if (mc?.LinkedMercenary != null)
                    mercAllies.Add(new LiveMercenaryAlly(mercEntities[i], mc));
            }

            // Wire adapter + engine and start coroutine
            var adapter = new LiveBattleAdapter(hero, validMonsterEntities);
            var engine  = new BattleEngine(adapter, adapter);

            _battleCoroutine = Core.StartCoroutine(
                RunBattleAndCleanup(engine, heroAlly, mercAllies, monsters, hero, adapter));

            Debug.Log("[AttackMonster] Multi-participant battle started successfully");
            return !HeroStateMachine.IsBattleInProgress;
        }

        public override bool Execute(IGoapContext context)
        {
            context.LogDebug("[AttackMonster] Starting monster attack with interface-based context");

            var heroTile = context.HeroController.CurrentTilePosition;
            context.LogDebug($"[AttackMonster] Hero at tile ({heroTile.X},{heroTile.Y})");

            // When called from the virtual layer with a BattleRunner wired up,
            // run the real headless battle.  This path is exercised by
            // VirtualHeroStateMachine.RunAdjacentBattlesIfAny() and by tests.
            if (context is PitHero.VirtualGame.VirtualGoapContext virtualCtx
                && virtualCtx.BattleRunner != null)
            {
                var metrics = virtualCtx.BattleRunner.RunAdjacentBattle();
                if (metrics != null)
                {
                    context.LogDebug($"[AttackMonster] Virtual battle complete: " +
                        $"{metrics.MonstersDefeated} monster(s) defeated in {metrics.Rounds} round(s)");
                }
                else
                {
                    context.LogDebug("[AttackMonster] No adjacent monsters found; skipping battle");
                }
                return true;
            }

            // Fallback no-op for contexts without a runner (exploration-only tests)
            context.LogDebug("[AttackMonster] Attack completed in virtual context (no BattleRunner)");
            return true;
        }

        // ── Battle coroutine wrapper ──────────────────────────────────────────────────

        /// <summary>
        /// Wraps the <see cref="BattleEngine.Run"/> coroutine and ensures cleanup
        /// (UI teardown, adjacency recalc, coroutine reference clear) always runs.
        /// </summary>
        private IEnumerator RunBattleAndCleanup(
            BattleEngine engine, IBattleAlly heroAlly,
            List<IBattleAlly> mercAllies, List<IEnemy> monsters,
            HeroComponent hero, LiveBattleAdapter adapter)
        {
            try
            {
                yield return engine.Run(heroAlly, mercAllies, monsters, hero.BattleActionQueue);

                if (hero.Entity != null)
                    hero.AdjacentToMonster = hero.CheckAdjacentToMonster();

                Debug.Log("[AttackMonster] Multi-participant battle sequence completed");
            }
            finally
            {
                adapter.CleanupBattleUI(mercAllies);
                _battleCoroutine = null;
            }
        }

        // ── Spatial / adjacency helpers ───────────────────────────────────────────────

        /// <summary>Find all adjacent monsters to the hero for multi-participant battle.</summary>
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
                    adjacentMonsters.Add(monster);
            }

            return adjacentMonsters;
        }

        /// <summary>Find all hired mercenaries who are currently in the pit.</summary>
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
                    mercenariesInPit.Add(merc);
            }

            return mercenariesInPit;
        }

        // ── Animation helpers ─────────────────────────────────────────────────────────

        /// <summary>Make hero face the target position.</summary>
        private void FaceTarget(HeroComponent hero, Vector2 targetPosition)
        {
            FaceTarget(hero.Entity, targetPosition);
        }

        /// <summary>Make any entity face the target position.</summary>
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

        /// <summary>Perform attack animation by moving hero slightly backward then forward.</summary>
        private void PerformAttackAnimation(HeroComponent hero)
        {
            // Simple animation simulation - in a real implementation, this would be handled by an animation system
            Debug.Log("[AttackMonster] Performing attack animation (simulation)");

            // For now, just log the animation. In a full implementation, this would:
            // 1. Move hero a few pixels backward
            // 2. Smoothly animate forward
            // 3. Use proper timing with Time.DeltaTime
        }

        // ── Tile helpers ──────────────────────────────────────────────────────────────

        /// <summary>Check if two tile positions are adjacent (8-directional adjacency).</summary>
        private bool IsAdjacent(Point tile1, Point tile2)
        {
            int deltaX = System.Math.Abs(tile1.X - tile2.X);
            int deltaY = System.Math.Abs(tile1.Y - tile2.Y);
            return deltaX <= 1 && deltaY <= 1 && (deltaX + deltaY > 0);
        }

        /// <summary>Get current tile position from hero component.</summary>
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

        /// <summary>Get the tile coordinates from a world position.</summary>
        private Point GetTileCoordinates(Vector2 worldPosition)
        {
            return new Point((int)(worldPosition.X / GameConfig.TileSize), (int)(worldPosition.Y / GameConfig.TileSize));
        }
    }
}
