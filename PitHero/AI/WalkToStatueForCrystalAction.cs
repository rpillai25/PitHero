using Microsoft.Xna.Framework;
using Nez;
using PitHero;
using PitHero.ECS.Components;
using PitHero.Services;
using System.Collections;

namespace PitHero.AI
{
    /// <summary>
    /// GOAP action for a hero who has respawned without a crystal to walk to the hero statue (tile 112,6)
    /// and trigger the crystal promotion ceremony via HeroPromotionService
    /// </summary>
    public class WalkToStatueForCrystalAction : HeroActionBase
    {
        private const int StatueTileX = 112;
        private const int StatueTileY = 6;
        private ICoroutine _walkCoroutine;

        public WalkToStatueForCrystalAction() : base(GoapConstants.WalkToStatueForCrystalAction, 1)
        {
            SetPrecondition(GoapConstants.HeroInitialized, true);
            SetPrecondition(GoapConstants.NeedsCrystal, true);
            SetPostcondition(GoapConstants.HasArrivedAtStatueForCrystal, true);
        }

        /// <summary>
        /// Execute the walk-to-statue action for a hero that needs a crystal
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            if (hero == null)
                return true;

            // Stop if hero has already arrived
            if (hero.HasArrivedAtStatueForCrystal)
                return true;

            // Start walking coroutine if not already started
            if (_walkCoroutine == null)
            {
                _walkCoroutine = Core.StartCoroutine(WalkToStatue(hero));
            }

            return hero.HasArrivedAtStatueForCrystal;
        }

        private IEnumerator WalkToStatue(HeroComponent hero)
        {
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            var pathfinding = hero.Entity.GetComponent<PathfindingActorComponent>();
            var facingComponent = hero.Entity.GetComponent<ActorFacingComponent>();

            if (tileMover == null || pathfinding == null)
            {
                Debug.Warn("[WalkToStatueForCrystalAction] Missing required components on hero entity");
                hero.HasArrivedAtStatueForCrystal = true;
                yield break;
            }

            Debug.Log($"[WalkToStatueForCrystalAction] Hero walking to statue at ({StatueTileX},{StatueTileY}) to receive crystal");

            var evtSvc = Core.Services.GetService<GameEventService>();
            var txtSvc = Core.Services.GetService<TextService>();
            if (evtSvc != null && txtSvc != null && hero.LinkedHero != null)
                evtSvc.Emit(ConsoleSegment.Build(txtSvc.DisplayText(TextType.UI, UITextKey.ConsoleHeroRespawn),
                    (hero.LinkedHero.Name, GameConfig.ConsoleColorHeroName)));

            var currentPos = hero.Entity.Transform.Position;
            var currentTile = new Point(
                (int)(currentPos.X / GameConfig.TileSize),
                (int)(currentPos.Y / GameConfig.TileSize)
            );

            var statueTile = new Point(StatueTileX, StatueTileY);

            var path = pathfinding.CalculatePath(currentTile, statueTile);

            if (path == null || path.Count == 0)
            {
                Debug.Warn("[WalkToStatueForCrystalAction] Could not find path to hero statue — marking arrived anyway");
                hero.HasArrivedAtStatueForCrystal = true;
                yield break;
            }

            Debug.Log($"[WalkToStatueForCrystalAction] Found path with {path.Count} steps to statue");

            for (int i = 0; i < path.Count; i++)
            {
                var targetTile = path[i];
                var currentTilePos = new Point(
                    (int)(hero.Entity.Transform.Position.X / GameConfig.TileSize),
                    (int)(hero.Entity.Transform.Position.Y / GameConfig.TileSize)
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

            // Face up toward the statue
            if (facingComponent != null)
            {
                facingComponent.SetFacing(Direction.Up);
            }

            Debug.Log("[WalkToStatueForCrystalAction] Hero arrived at statue — awaiting crystal promotion ceremony");

            hero.HasArrivedAtStatueForCrystal = true;
            _walkCoroutine = null;
        }
    }
}
