using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
using System.Collections;

namespace PitHero.AI
{
    /// <summary>
    /// GOAP action for mercenaries to walk to the hero statue (tile 112,6) when being promoted to hero
    /// </summary>
    public class WalkToHeroStatueAction : MercenaryActionBase
    {
        private const int StatueTileX = 112;
        private const int StatueTileY = 6;
        private ICoroutine _walkCoroutine;

        public WalkToHeroStatueAction() : base(GoapConstants.WalkToHeroStatueAction, 1)
        {
            SetPrecondition(GoapConstants.IsAlive, true);
            SetPrecondition(GoapConstants.IsBeingPromotedToHero, true);
            SetPostcondition(GoapConstants.HasArrivedAtHeroStatue, true);
        }

        public override bool Execute(MercenaryComponent mercenary)
        {
            if (mercenary == null)
                return true;

            // Check if already arrived
            if (mercenary.HasArrivedAtStatue)
                return true;

            // Start walking if not already started
            if (_walkCoroutine == null)
            {
                _walkCoroutine = Core.StartCoroutine(WalkToStatue(mercenary));
            }

            // Action continues until mercenary arrives at statue
            return mercenary.HasArrivedAtStatue;
        }

        private IEnumerator WalkToStatue(MercenaryComponent mercenary)
        {
            var tileMover = mercenary.Entity.GetComponent<TileByTileMover>();
            var pathfinding = mercenary.Entity.GetComponent<PathfindingActorComponent>();
            var facingComponent = mercenary.Entity.GetComponent<ActorFacingComponent>();

            if (tileMover == null || pathfinding == null)
            {
                Debug.Warn("[WalkToHeroStatueAction] Missing required components");
                mercenary.HasArrivedAtStatue = true;
                yield break;
            }

            Debug.Log($"[WalkToHeroStatueAction] {mercenary.LinkedMercenary.Name} walking to hero statue at ({StatueTileX},{StatueTileY})");

            // Calculate current tile position
            var currentPos = mercenary.Entity.Transform.Position;
            var currentTile = new Point(
                (int)(currentPos.X / GameConfig.TileSize),
                (int)(currentPos.Y / GameConfig.TileSize)
            );

            var statueTile = new Point(StatueTileX, StatueTileY);

            // Calculate path to statue
            var path = pathfinding.CalculatePath(currentTile, statueTile);

            if (path == null || path.Count == 0)
            {
                Debug.Warn($"[WalkToHeroStatueAction] Could not find path to hero statue for {mercenary.LinkedMercenary.Name}");
                mercenary.HasArrivedAtStatue = true;
                yield break;
            }

            Debug.Log($"[WalkToHeroStatueAction] {mercenary.LinkedMercenary.Name} found path with {path.Count} steps");

            // Follow the path
            for (int i = 0; i < path.Count; i++)
            {
                var targetTile = path[i];
                var currentTilePos = new Point(
                    (int)(mercenary.Entity.Transform.Position.X / GameConfig.TileSize),
                    (int)(mercenary.Entity.Transform.Position.Y / GameConfig.TileSize)
                );

                // Determine direction to move
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

                    // Wait for movement to complete
                    while (tileMover.IsMoving)
                    {
                        yield return null;
                    }
                }

                // Small delay between moves
                yield return Coroutine.WaitForSeconds(0.05f);
            }

            // Arrived at statue - face the statue (statue is at tile 112,3 so hero faces up)
            if (facingComponent != null)
            {
                facingComponent.SetFacing(Direction.Up);
            }

            Debug.Log($"[WalkToHeroStatueAction] {mercenary.LinkedMercenary.Name} arrived at hero statue");

            // Mark as arrived
            mercenary.HasArrivedAtStatue = true;
            _walkCoroutine = null;
        }
    }
}

