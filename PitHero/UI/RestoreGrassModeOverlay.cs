using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.UI
{
    /// <summary>
    /// Manages world-space cursor and drag interaction for restore-grass mode.
    /// Left-drag over a valid tilled tile clears its water and restores it to grass.
    /// Validity: farm bounds, Tilled flag set, no crop plan, no active crop, no building.
    /// </summary>
    public class RestoreGrassModeOverlay
    {
        private readonly Scene _scene;
        private Stage _stage;

        private readonly TileBrush _brush;
        private readonly System.Func<Point, bool> _isRestorablePredicate;

        // Services resolved once per Update and read by the per-tile predicate
        private TileStateService _frameTileService;
        private BuildingService _frameBuildingService;
        private CropPlantingService _frameCropPlanService;
        private CropGrowthService _frameCropGrowthService;

        private static readonly Point NoTile = new Point(int.MinValue, int.MinValue);
        private Point _lastActionTile = NoTile;

        private static readonly Color CursorValidColor   = new Color(0, 200, 0, 128);
        private static readonly Color CursorInvalidColor = new Color(255, 0, 0, 128);

        /// <summary>Initializes the overlay with the parent scene.</summary>
        public RestoreGrassModeOverlay(Scene scene)
        {
            _scene = scene;
            _brush = new TileBrush(scene, "restore-grass-cursor");
            _isRestorablePredicate = IsRestorable;
        }

        /// <summary>Supplies the UI stage for hit-testing against UI elements.</summary>
        public void SetStage(Stage stage) => _stage = stage;

        /// <summary>Activates restore-grass mode: creates the cursor entity.</summary>
        public void OnEnterRestoreGrassMode()
        {
            _lastActionTile = NoTile;
            CreateCursor();
        }

        /// <summary>Deactivates restore-grass mode: destroys the cursor entity.</summary>
        public void OnExitRestoreGrassMode()
        {
            DestroyCursor();
        }

        /// <summary>Per-frame update: steps the brush, moves the cursors, and handles left-drag restore.</summary>
        public void Update()
        {
            _brush.HandleSizeInput(_stage);

            var worldPos = _scene.Camera.MouseToWorldPoint();
            int tileX = (int)(worldPos.X / GameConfig.TileSize);
            int tileY = (int)(worldPos.Y / GameConfig.TileSize);

            _frameTileService       = Core.Services.GetService<TileStateService>();
            _frameBuildingService   = Core.Services.GetService<BuildingService>();
            _frameCropPlanService   = Core.Services.GetService<CropPlantingService>();
            _frameCropGrowthService = Core.Services.GetService<CropGrowthService>();

            var tile = new Point(tileX, tileY);
            _brush.UpdateCursors(tile, _isRestorablePredicate, CursorValidColor, CursorInvalidColor);

            // Suppress tile actions while mouse is over UI or outside the window. UI clicks are
            // handled by SettingsUI's release-time exit check (same mechanism as till mode), so
            // clicking the Restore Grass button toggles the mode off without re-entering.
            if ((_stage != null && _stage.Hit(_stage.GetMousePosition()) != null)
                || !MouseUtils.IsMouseInsideWindow())
            {
                _lastActionTile = NoTile;
                return;
            }

            if (Input.LeftMouseButtonDown)
            {
                if (tile != _lastActionTile)
                {
                    _lastActionTile = tile;
                    // One command per brush tile; the handler re-validates each one
                    int brushTiles = _brush.TileCount;
                    for (int i = 0; i < brushTiles; i++)
                    {
                        var brushTile = _brush.GetTile(tile, i);
                        if (!IsRestorable(brushTile))
                            continue;
                        // Lands on a deterministic tick via the command queue (replay system)
                        Services.Replay.PlayerCommandService.Dispatch(new Services.Replay.PlayerCommand(
                            Services.Replay.PlayerCommandType.RestoreGrassTile, brushTile.X, brushTile.Y));
                    }
                }
            }
            else
            {
                _lastActionTile = NoTile;
            }
        }

        // Restorable: inside the farm bounds, tilled, and free of crop plans, crops and buildings.
        private bool IsRestorable(Point tile)
        {
            if (tile.X < GameConfig.FarmMinTillTileX || tile.Y < GameConfig.FarmMinTillTileY)
                return false;
            if (_frameTileService == null || !_frameTileService.HasFlag(tile, TileStateFlag.Tilled))
                return false;
            if (_frameTileService.HasFlag(tile, TileStateFlag.CropGrowing | TileStateFlag.CropGrown))
                return false;
            if (_frameCropPlanService != null && _frameCropPlanService.HasPlan(tile))
                return false;
            if (_frameCropGrowthService != null && _frameCropGrowthService.HasCrop(tile))
                return false;
            return _frameBuildingService == null || !_frameBuildingService.IsTileOccupied(tile.X, tile.Y);
        }

        private void CreateCursor() => _brush.CreateCursors();

        private void DestroyCursor() => _brush.DestroyCursors();
    }
}
