using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using PitHero.Services;
using System.Collections;

namespace PitHero.AI
{
    /// <summary>
    /// Action that seats the hero and mercenaries at the tavern when the player stops adventuring.
    /// GoTo state handles pathfinding to the tavern seat; this action handles arrival behavior only.
    /// </summary>
    public class WalkToTavernForStopAction : HeroActionBase
    {
        private ICoroutine _seatCoroutine;
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
            return _seatCoroutine != null;
        }

        /// <summary>
        /// Execute the tavern seating action — hero is already at tavern seat via GoTo state
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            if (hero == null)
                return true;

            if (_completed)
            {
                _completed = false;
                _seatCoroutine = null;
                return true;
            }

            if (_seatCoroutine == null)
            {
                _seatCoroutine = Core.StartCoroutine(SeatPartyCoroutine(hero));
            }

            return false;
        }

        /// <summary>
        /// Coroutine that faces the hero down and teleports mercenaries to their seats
        /// </summary>
        private IEnumerator SeatPartyCoroutine(HeroComponent hero)
        {
            var heroEntity = hero.Entity;
            var facingComponent = heroEntity.GetComponent<ActorFacingComponent>();

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

                SeatMercenaryAtPosition(mercEntity, seatTile, facing, i + 1);
            }
        }

        /// <summary>
        /// Teleport a mercenary to a seat position, snap to grid, set facing, and disable following
        /// </summary>
        public static void SeatMercenaryAtPosition(Entity mercEntity, Point seatTile, Direction facing, int mercNumber)
        {
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

            // Disable following so the mercenary stays at its seat
            var followComponent = mercEntity.GetComponent<MercenaryFollowComponent>();
            if (followComponent != null)
            {
                followComponent.Enabled = false;
                followComponent.ResetPathfinding();
            }

            Debug.Log($"[WalkToTavernForStop] Mercenary {mercNumber} seated at ({seatTile.X},{seatTile.Y}) facing {facing}");
        }

        /// <summary>
        /// Check if a mercenary entity is at its designated tavern seat
        /// </summary>
        public static bool IsMercenarySeated(Entity mercEntity, int mercIndex)
        {
            if (mercEntity == null) return true;

            var pos = mercEntity.Transform.Position;
            var currentTile = new Point(
                (int)(pos.X / GameConfig.TileSize),
                (int)(pos.Y / GameConfig.TileSize)
            );

            Point expectedSeat;
            if (mercIndex == 0)
                expectedSeat = new Point(GameConfig.TavernMercenary1SeatTileX, GameConfig.TavernMercenary1SeatTileY);
            else
                expectedSeat = new Point(GameConfig.TavernMercenary2SeatTileX, GameConfig.TavernMercenary2SeatTileY);

            return currentTile.X == expectedSeat.X && currentTile.Y == expectedSeat.Y;
        }

        /// <summary>
        /// Seat any hired mercenaries that are not at their designated seat and are close enough.
        /// Returns true if any mercenary was newly seated.
        /// </summary>
        public static bool SeatUnseatedMercenaries()
        {
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager == null) return false;

            var hiredMercenaries = mercenaryManager.GetHiredMercenaries();
            if (hiredMercenaries == null || hiredMercenaries.Count == 0) return false;

            var heroTile = new Point(GameConfig.TavernHeroSeatTileX, GameConfig.TavernHeroSeatTileY);
            bool seatedAny = false;

            for (int i = 0; i < hiredMercenaries.Count && i < 2; i++)
            {
                var mercEntity = hiredMercenaries[i];
                if (mercEntity == null) continue;

                // Skip if already at designated seat
                if (IsMercenarySeated(mercEntity, i)) continue;

                // Check if close enough to teleport
                var mercPos = mercEntity.Transform.Position;
                var mercTile = new Point(
                    (int)(mercPos.X / GameConfig.TileSize),
                    (int)(mercPos.Y / GameConfig.TileSize)
                );

                float dist = Vector2.Distance(
                    new Vector2(mercTile.X, mercTile.Y),
                    new Vector2(heroTile.X, heroTile.Y)
                );

                if (dist <= MercenaryProximityTiles)
                {
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

                    SeatMercenaryAtPosition(mercEntity, seatTile, facing, i + 1);
                    seatedAny = true;
                }
            }

            return seatedAny;
        }

        /// <summary>
        /// Re-enable MercenaryFollowComponent for all hired mercenaries (called when resuming adventuring)
        /// </summary>
        public static void ReenableMercenaryFollowing()
        {
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager == null) return;

            var hiredMercenaries = mercenaryManager.GetHiredMercenaries();
            if (hiredMercenaries == null) return;

            for (int i = 0; i < hiredMercenaries.Count; i++)
            {
                var mercEntity = hiredMercenaries[i];
                if (mercEntity == null) continue;

                var followComponent = mercEntity.GetComponent<MercenaryFollowComponent>();
                if (followComponent != null)
                {
                    followComponent.Enabled = true;
                    Debug.Log($"[WalkToTavernForStop] Re-enabled following for mercenary {i + 1}");
                }
            }
        }
    }
}
