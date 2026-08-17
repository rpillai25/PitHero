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

        private Entity _cursorEntity;
        private PrototypeSpriteRenderer _cursorRenderer;

        private static readonly Point NoTile = new Point(int.MinValue, int.MinValue);
        private Point _lastActionTile = NoTile;

        private static readonly Color CursorValidColor   = new Color(0, 200, 0, 128);
        private static readonly Color CursorInvalidColor = new Color(255, 0, 0, 128);

        /// <summary>Initializes the overlay with the parent scene.</summary>
        public RestoreGrassModeOverlay(Scene scene)
        {
            _scene = scene;
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

        /// <summary>Per-frame update: moves cursor, colors it, and handles left-drag restore.</summary>
        public void Update()
        {
            var worldPos = _scene.Camera.MouseToWorldPoint();
            int tileX = (int)(worldPos.X / GameConfig.TileSize);
            int tileY = (int)(worldPos.Y / GameConfig.TileSize);

            var tileService     = Core.Services.GetService<TileStateService>();
            var buildingService = Core.Services.GetService<BuildingService>();
            var cropPlanService = Core.Services.GetService<CropPlantingService>();
            var cropGrowthService = Core.Services.GetService<CropGrowthService>();

            bool inFarmBounds  = tileX >= GameConfig.FarmMinTillTileX && tileY >= GameConfig.FarmMinTillTileY;
            var  tile          = new Point(tileX, tileY);
            bool isTilled      = tileService != null && tileService.HasFlag(tile, TileStateFlag.Tilled);
            bool hasPlan       = cropPlanService != null && cropPlanService.HasPlan(tile);
            bool hasCrop       = cropGrowthService != null && cropGrowthService.HasCrop(tile);
            bool hasCropFlag   = tileService != null && tileService.HasFlag(tile, TileStateFlag.CropGrowing | TileStateFlag.CropGrown);
            bool occupied      = buildingService != null && buildingService.IsTileOccupied(tileX, tileY);

            bool valid = inFarmBounds && isTilled && !hasPlan && !hasCrop && !hasCropFlag && !occupied;

            if (_cursorEntity != null)
            {
                float cx = tileX * GameConfig.TileSize + GameConfig.TileSize / 2f;
                float cy = tileY * GameConfig.TileSize + GameConfig.TileSize / 2f;
                _cursorEntity.SetPosition(cx, cy);

                if (_cursorRenderer != null)
                    _cursorRenderer.Color = valid ? CursorValidColor : CursorInvalidColor;
            }

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
                if (tile != _lastActionTile && valid)
                {
                    _lastActionTile = tile;
                    var wetService    = Core.Services.GetService<WetTileService>();
                    var tilledService = Core.Services.GetService<TilledTileService>();
                    wetService?.ClearWet(tile);
                    tilledService?.RestoreGrassTile(tile);
                }
            }
            else
            {
                _lastActionTile = NoTile;
            }
        }

        private void CreateCursor()
        {
            if (_cursorEntity != null)
                return;

            _cursorEntity = _scene.CreateEntity("restore-grass-cursor");
            _cursorRenderer = _cursorEntity.AddComponent(new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
            _cursorRenderer.Color = CursorValidColor;
            _cursorRenderer.SetRenderLayer(GameConfig.RenderLayerTop);
        }

        private void DestroyCursor()
        {
            if (_cursorEntity == null)
                return;

            _cursorEntity.Destroy();
            _cursorEntity   = null;
            _cursorRenderer = null;
        }
    }
}
