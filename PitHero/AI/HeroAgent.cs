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
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();

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
                    int totalFogTiles = 0;
                    
                    // Use the same explorable area bounds as WanderAction
                    int explorationMinX, explorationMinY, explorationMaxX, explorationMaxY;
                    
                    if (pitWidthManager != null)
                    {
                        explorationMinX = GameConfig.PitRectX + 1; // x=2
                        explorationMinY = GameConfig.PitRectY + 1; // y=3  
                        explorationMaxX = pitWidthManager.CurrentPitRightEdge - 2; // Last explorable column
                        explorationMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // y=9
                    }
                    else
                    {
                        explorationMinX = GameConfig.PitRectX + 1; // 2
                        explorationMinY = GameConfig.PitRectY + 1; // 3
                        explorationMaxX = GameConfig.PitRectX + GameConfig.PitRectWidth - 2; // 11
                        explorationMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // 9
                    }
                    
                    for (var x = explorationMinX; x <= explorationMaxX && !anyFog; x++)
                    {
                        for (var y = explorationMinY; y <= explorationMaxY; y++)
                        {
                            if (x >= 0 && y >= 0 && x < fogLayer.Width && y < fogLayer.Height)
                            {
                                var fogTile = fogLayer.GetTile(x, y);
                                if (fogTile != null)
                                {
                                    totalFogTiles++;
                                    anyFog = true;
                                    break;
                                }
                            }
                        }
                    }

                    Debug.Log($"[HeroAgent] Exploration check in area ({explorationMinX},{explorationMinY}) to ({explorationMaxX},{explorationMaxY}): {totalFogTiles} fog tiles remaining, anyFog={anyFog}");

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
