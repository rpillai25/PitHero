using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Services;
using System.Collections;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that walks the hero to a designated tavern seat when the player stops adventuring.
    /// Once the hero arrives, hired mercenaries are teleported to their designated seats.
    /// </summary>
    public class WalkToTavernForStopAction : HeroActionBase
    {
        private ICoroutine _walkCoroutine;
        private bool _completed;
        private const float MercenaryProximityTiles = 3f;

        public WalkToTavernForStopAction() : base(GoapConstants.WalkToTavernForStopAction, 99)
        {
            SetPrecondition(GoapConstants.OutsidePit, true);
            SetPrecondition(GoapConstants.StoppedAdventure, true);

            SetPostcondition(GoapConstants.SeatedInTavern, true);
        }

        public override bool ShouldNotOverride()
        {
            return _walkCoroutine != null;
        }

        /// <summary>
        /// Execute the walk-to-tavern action
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            if (hero == null)
                return true;

            if (_completed)
            {
                _completed = false;
                _walkCoroutine = null;
                return true;
            }

            if (_walkCoroutine == null)
            {
                _walkCoroutine = Core.StartCoroutine(WalkToTavernCoroutine(hero));
            }

            return false;
        }

        /// <summary>
        /// Coroutine that walks the hero to the tavern seat and teleports mercenaries
        /// </summary>
        private IEnumerator WalkToTavernCoroutine(HeroComponent hero)
        {
            var heroEntity = hero.Entity;
            var tileMover = heroEntity.GetComponent<TileByTileMover>();
            var pathfinding = heroEntity.GetComponent<PathfindingActorComponent>();
            var facingComponent = heroEntity.GetComponent<ActorFacingComponent>();

            if (tileMover == null || pathfinding == null)
            {
                Debug.Warn("[WalkToTavernForStop] Missing required components on hero entity");
                hero.SeatedInTavern = true;
                _completed = true;
                yield break;
            }

            var heroSeatTile = new Point(GameConfig.TavernHeroSeatTileX, GameConfig.TavernHeroSeatTileY);

            Debug.Log($"[WalkToTavernForStop] Hero walking to tavern seat at ({heroSeatTile.X},{heroSeatTile.Y})");

            // Calculate path from current position to tavern seat
            var currentTile = tileMover.GetCurrentTileCoordinates();
            var path = pathfinding.CalculatePath(currentTile, heroSeatTile);

            if (path != null && path.Count > 0)
            {
                // Walk along the path
                for (int i = 0; i < path.Count; i++)
                {
                    var targetTile = path[i];
                    var currentTilePos = new Point(
                        (int)(heroEntity.Transform.Position.X / GameConfig.TileSize),
                        (int)(heroEntity.Transform.Position.Y / GameConfig.TileSize)
                    );

                    var dx = targetTile.X - currentTilePos.X;
                    var dy = targetTile.Y - currentTilePos.Y;

                    Direction? direction = null;
                    if (dx > 0) direction = Direction.Right;
                    else if (dx < 0) direction = Direction.Left;
                    else if (dy > 0) direction = Direction.Down;
                    else if (dy < 0) direction = Direction.Up;

                    if (direction.HasValue)
                    {
                        tileMover.StartMoving(direction.Value);

                        while (tileMover.IsMoving)
                        {
                            yield return null;
                        }
                    }

                    yield return Coroutine.WaitForSeconds(0.05f);
                }
            }
            else
            {
                // No path found - teleport to tavern seat
                Debug.Warn("[WalkToTavernForStop] No path to tavern seat, teleporting");
                heroEntity.Transform.Position = TileToWorldPosition(heroSeatTile);
                tileMover.SnapToTileGrid();
            }

            // Hero faces down at the tavern seat
            if (facingComponent != null)
            {
                facingComponent.SetFacing(Direction.Down);
            }

            Debug.Log("[WalkToTavernForStop] Hero arrived at tavern seat");

            // Wait for mercenaries to get close, then teleport them to their seats
            yield return SeatMercenaries(hero);

            hero.SeatedInTavern = true;
            _completed = true;

            Debug.Log("[WalkToTavernForStop] All party members seated in tavern");
        }

        /// <summary>
        /// Wait for hired mercenaries to get close to the hero, then teleport them to their designated seats
        /// </summary>
        private IEnumerator SeatMercenaries(HeroComponent hero)
        {
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager == null)
                yield break;

            var hiredMercenaries = mercenaryManager.GetHiredMercenaries();
            if (hiredMercenaries == null || hiredMercenaries.Count == 0)
                yield break;

            var heroTile = new Point(GameConfig.TavernHeroSeatTileX, GameConfig.TavernHeroSeatTileY);

            // Wait until each mercenary is within proximity distance of the hero
            bool allClose = false;
            float maxWaitTime = 30f;
            float elapsed = 0f;

            while (!allClose && elapsed < maxWaitTime)
            {
                allClose = true;
                hiredMercenaries = mercenaryManager.GetHiredMercenaries();

                for (int i = 0; i < hiredMercenaries.Count; i++)
                {
                    var mercEntity = hiredMercenaries[i];
                    if (mercEntity == null) continue;

                    var mercPos = mercEntity.Transform.Position;
                    var mercTile = new Point(
                        (int)(mercPos.X / GameConfig.TileSize),
                        (int)(mercPos.Y / GameConfig.TileSize)
                    );

                    float dist = Vector2.Distance(
                        new Vector2(mercTile.X, mercTile.Y),
                        new Vector2(heroTile.X, heroTile.Y)
                    );

                    if (dist > MercenaryProximityTiles)
                    {
                        allClose = false;
                        break;
                    }
                }

                if (!allClose)
                {
                    elapsed += Time.DeltaTime;
                    yield return null;
                }
            }

            if (!allClose)
            {
                Debug.Warn("[WalkToTavernForStop] Timed out waiting for mercenaries to approach, teleporting them");
            }

            // Teleport each mercenary to their designated seat
            hiredMercenaries = mercenaryManager.GetHiredMercenaries();
            for (int i = 0; i < hiredMercenaries.Count && i < 2; i++)
            {
                var mercEntity = hiredMercenaries[i];
                if (mercEntity == null) continue;

                Point seatTile;
                Direction facing;

                if (i == 0)
                {
                    seatTile = new Point(GameConfig.TavernMercenary1SeatTileX, GameConfig.TavernMercenary1SeatTileY);
                    facing = Direction.Right;
                }
                else
                {
                    seatTile = new Point(GameConfig.TavernMercenary2SeatTileX, GameConfig.TavernMercenary2SeatTileY);
                    facing = Direction.Left;
                }

                mercEntity.Transform.Position = TileToWorldPosition(seatTile);

                var mercTileMover = mercEntity.GetComponent<TileByTileMover>();
                if (mercTileMover != null)
                {
                    mercTileMover.SnapToTileGrid();
                }

                var mercFacing = mercEntity.GetComponent<ActorFacingComponent>();
                if (mercFacing != null)
                {
                    mercFacing.SetFacing(facing);
                }

                Debug.Log($"[WalkToTavernForStop] Mercenary {i + 1} seated at ({seatTile.X},{seatTile.Y}) facing {facing}");
            }
        }
    }
}
