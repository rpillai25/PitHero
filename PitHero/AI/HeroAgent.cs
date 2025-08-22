using Nez;
using Nez.AI.GOAP;
using Nez.Tiled;
using PitHero.ECS.Components;
using PitHero.Util;

namespace PitHero.AI
{
    public class HeroAgent : Agent
    {
        private readonly HeroComponent _hero;

        // Make planner accessible for debugging
        public ActionPlanner Planner => _planner;

        public HeroAgent(HeroComponent hero)
        {
            _hero = hero;

            // Add all available actions
            _planner.AddAction(new MoveToPitAction());
            _planner.AddAction(new JumpIntoPitAction());
            _planner.AddAction(new WanderAction()); // register WanderAction
        }

        public override WorldState GetWorldState()
        {
            var ws = WorldState.Create(_planner);

            ws.Set(GoapConstants.HeroInitialized, true);

            if (_hero.PitInitialized)
                ws.Set(GoapConstants.PitInitialized, true);

            var tileMover = _hero.Entity.GetComponent<TileByTileMover>();
            if (tileMover != null && tileMover.IsMoving && !_hero.AdjacentToPitBoundaryFromOutside)
            {
                ws.Set(GoapConstants.MovingToPit, true);
            }

            if (_hero.AdjacentToPitBoundaryFromOutside)
                ws.Set(GoapConstants.AdjacentToPitBoundaryFromOutside, true);
            if (_hero.AdjacentToPitBoundaryFromInside)
                ws.Set(GoapConstants.AdjacentToPitBoundaryFromInside, true);
            if (_hero.EnteredPit)
                ws.Set(GoapConstants.EnteredPit, true);

            // Mark exploration complete when FogOfWar is fully cleared inside the pit rect
            var tms = Core.Services.GetService<TiledMapService>();
            if (tms?.CurrentMap != null)
            {
                var fogLayer = tms.CurrentMap.GetLayer<TmxLayer>("FogOfWar");
                if (fogLayer != null)
                {
                    var anyFog = false;
                    for (var x = GameConfig.PitRectX; x < GameConfig.PitRectX + GameConfig.PitRectWidth && !anyFog; x++)
                    {
                        for (var y = GameConfig.PitRectY; y < GameConfig.PitRectY + GameConfig.PitRectHeight; y++)
                        {
                            if (x >= 0 && y >= 0 && x < fogLayer.Width && y < fogLayer.Height)
                            {
                                if (fogLayer.GetTile(x, y) != null)
                                {
                                    anyFog = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!anyFog)
                        ws.Set(GoapConstants.MapExplored, true);
                }
            }

            Debug.Log($"[GOAP] State: PitInitialized={_hero.PitInitialized}, " +
                      $"AdjOut={_hero.AdjacentToPitBoundaryFromOutside}, " +
                      $"AdjIn={_hero.AdjacentToPitBoundaryFromInside}, " +
                      $"EnteredPit={_hero.EnteredPit}");
            return ws;
        }

        public override WorldState GetGoalState()
        {
            var goal = WorldState.Create(_planner);
            goal.Set(GoapConstants.MapExplored, true); // goal is exploration, not just entering pit
            return goal;
        }

        public string DescribePlanner() => _planner.Describe();
        public string DescribeCurrentState() => GetWorldState().Describe(_planner);
    }
}
