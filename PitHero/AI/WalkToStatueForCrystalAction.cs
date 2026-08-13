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
            // Respawned heroes always spawn outside; requiring it lets the planner chain a
            // jump-out first when a manual job change is requested inside the pit
            SetPrecondition(GoapConstants.OutsidePit, true);
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

            // Start walking coroutine if not already started. The NeedsCrystal guard stops a
            // spurious restart on the frame after the walk aborts a manual job change (the
            // state machine replans off that flag flip one tick later).
            if (_walkCoroutine == null && hero.NeedsCrystal)
            {
                _walkCoroutine = Core.StartCoroutine(WalkToStatue(hero));
            }

            return hero.HasArrivedAtStatueForCrystal;
        }

        /// <summary>Give up on pathing after this long. Death path falls back to a ceremony in
        /// place (worse than misplaced lightning is a softlocked crystal-less hero); the manual
        /// path aborts the job change instead — the hero keeps its job and the player can retry.</summary>
        private const float MaxPathRetrySeconds = 60f;
        private const float PathRetryDelay = 0.5f;

        private IEnumerator WalkToStatue(HeroComponent hero)
        {
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            var pathfinding = hero.Entity.GetComponent<PathfindingActorComponent>();
            var facingComponent = hero.Entity.GetComponent<ActorFacingComponent>();

            if (tileMover == null || pathfinding == null)
            {
                Debug.Warn("[WalkToStatueForCrystalAction] Missing required components on hero entity");
                hero.HasArrivedAtStatueForCrystal = true;
                _walkCoroutine = null;
                yield break;
            }

            Debug.Log($"[WalkToStatueForCrystalAction] Hero walking to statue at ({StatueTileX},{StatueTileY}) to receive crystal");

            // Respawn chatter belongs to the death path only; a manual job change is not a respawn
            if (!hero.PendingManualJobChange)
            {
                SpeechBubbleDialogue.SayRespawn(hero.Entity);

                if (hero.LinkedHero != null)
                    Core.Services.GetService<GameEventService>()?.EmitLocalized(UITextKey.ConsoleHeroRespawn,
                        (hero.LinkedHero.Name, GameConfig.ConsoleColorHeroName));
            }

            var statueTile = new Point(StatueTileX, StatueTileY);
            float retryElapsed = 0f;
            bool arrived = false;

            // Self-healing walk: repath on any blocked step instead of silently skipping the
            // rest of the path. A single failed StartMoving (a mercenary in the corridor, tavern
            // furniture, a stale tile after seating) previously no-opped every remaining step
            // and then faked arrival — putting the ceremony wherever the hero stood.
            while (!arrived && retryElapsed < MaxPathRetrySeconds)
            {
                if (hero.Entity == null || hero.Entity.IsDestroyed)
                {
                    _walkCoroutine = null;
                    yield break;
                }

                // Let any in-flight tile move finish, then path from a clean grid position
                while (tileMover.IsMoving)
                    yield return null;
                tileMover.SnapToTileGrid();

                var currentTile = new Point(
                    (int)(hero.Entity.Transform.Position.X / GameConfig.TileSize),
                    (int)(hero.Entity.Transform.Position.Y / GameConfig.TileSize)
                );

                if (currentTile.X == statueTile.X && currentTile.Y == statueTile.Y)
                {
                    arrived = true;
                    break;
                }

                var path = pathfinding.CalculatePath(currentTile, statueTile);
                if (path == null || path.Count == 0)
                {
                    Debug.Warn($"[WalkToStatueForCrystalAction] No path from ({currentTile.X},{currentTile.Y}) to statue — retrying in {PathRetryDelay}s");
                    yield return Coroutine.WaitForSeconds(PathRetryDelay);
                    retryElapsed += PathRetryDelay;
                    continue;
                }

                bool blocked = false;
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
                        if (!tileMover.StartMoving(direction.Value))
                        {
                            blocked = true;
                            break;
                        }

                        while (tileMover.IsMoving)
                        {
                            yield return null;
                        }
                    }

                    yield return Coroutine.WaitForSeconds(0.05f);
                }

                if (blocked)
                {
                    Debug.Log("[WalkToStatueForCrystalAction] Step blocked — waiting and repathing to statue");
                    yield return Coroutine.WaitForSeconds(PathRetryDelay);
                    retryElapsed += PathRetryDelay;
                }
                // Loop re-checks actual arrival at the top; a fully walked path falls through
                // to the position check instead of assuming success.
            }

            if (arrived)
            {
                // Face up toward the statue
                if (facingComponent != null)
                {
                    facingComponent.SetFacing(Direction.Up);
                }

                Debug.Log("[WalkToStatueForCrystalAction] Hero arrived at statue — awaiting crystal promotion ceremony");
                hero.HasArrivedAtStatueForCrystal = true;
            }
            else if (hero.PendingManualJobChange)
            {
                // Never run a manual ceremony away from the statue — abort the request instead
                Debug.Warn("[WalkToStatueForCrystalAction] Could not reach statue — aborting manual job change, hero keeps current job");
                hero.PendingManualJobChange = false;
                hero.NeedsCrystal = false;
            }
            else
            {
                // Death path: a crystal-less hero must not softlock; ceremony in place as last resort
                Debug.Warn("[WalkToStatueForCrystalAction] Could not reach statue — marking arrived anyway to avoid softlock");
                hero.HasArrivedAtStatueForCrystal = true;
            }

            _walkCoroutine = null;
        }
    }
}
