using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Sprites;
using Nez.Tiled;
using Nez.Textures;
using Nez.UI;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.UI
{
    /// <summary>
    /// Manages world-space entities for till mode: a cursor that highlights the tile under the
    /// mouse and overlay sprites for tiles already marked ReadyToTill.
    /// </summary>
    public class TillModeOverlay
    {
        private readonly Scene _scene;
        private readonly TmxMap _map;
        private Stage _stage;

        private Entity _cursorEntity;
        private PrototypeSpriteRenderer _cursorRenderer;

        private readonly Dictionary<Point, Entity> _overlayEntities = new Dictionary<Point, Entity>();

        // Last tile processed per drag gesture; reset when button is released
        private static readonly Point NoTile = new Point(int.MinValue, int.MinValue);
        private Point _lastMarkDragTile   = NoTile;
        private Point _lastUnmarkDragTile = NoTile;

        // Auto-scroll state to restore on exit
        private bool _savedAutoScroll;

        private static readonly Color CursorTillableColor   = new Color(101, 67, 33, 128);
        private static readonly Color CursorUntillableColor = new Color(255, 0, 0, 128);

        private const int TillZerothGid = 122;   // GIDs 122-137 are the 16 transition variants
        private const int TillMinTileX  = 120;
        private const int TillMinTileY  = 1;

        // Cached tileset for GIDs 122-137 (same sheet for all variants)
        private TmxTileset _tillTileset;

        public TillModeOverlay(Scene scene, TmxMap map)
        {
            _scene = scene;
            _map   = map;
        }

        public void SetStage(Stage stage) => _stage = stage;

        /// <summary>Activates till mode: creates the cursor entity and overlay entities for existing ReadyToTill tiles.</summary>
        public void OnEnterTillMode()
        {
            _savedAutoScroll = UIWindowManager.AutoScrollToHeroEnabled;
            UIWindowManager.SetAutoScrollToHero(false);

            _lastMarkDragTile   = NoTile;
            _lastUnmarkDragTile = NoTile;

            CreateCursor();
            RestoreOverlays();
        }

        /// <summary>Deactivates till mode: destroys the cursor and all overlay entities. Tile state persists.</summary>
        public void OnExitTillMode()
        {
            UIWindowManager.SetAutoScrollToHero(_savedAutoScroll);

            DestroyCursor();
            DestroyAllOverlays();
        }

        /// <summary>Per-frame update: moves the cursor, updates its color, and handles left/right click.</summary>
        public void Update()
        {
            var worldPos = _scene.Camera.MouseToWorldPoint();
            int tileX = (int)(worldPos.X / GameConfig.TileSize);
            int tileY = (int)(worldPos.Y / GameConfig.TileSize);

            bool tillable = tileX >= TillMinTileX && tileY >= TillMinTileY;

            if (_cursorEntity != null)
            {
                float cx = tileX * GameConfig.TileSize + GameConfig.TileSize / 2f;
                float cy = tileY * GameConfig.TileSize + GameConfig.TileSize / 2f;
                _cursorEntity.SetPosition(cx, cy);

                if (_cursorRenderer != null)
                    _cursorRenderer.Color = tillable ? CursorTillableColor : CursorUntillableColor;
            }

            // Don't place tiles while the mouse is over any UI element (buttons, dialogs, etc.)
            if (_stage != null && _stage.Hit(_stage.ScreenToStageCoordinates(_stage.GetMousePosition())) != null)
            {
                _lastMarkDragTile   = NoTile;
                _lastUnmarkDragTile = NoTile;
                return;
            }

            var tileService = Core.Services.GetService<TileStateService>();
            if (tileService == null)
                return;

            var tile = new Point(tileX, tileY);

            bool shiftHeld = Input.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
                          || Input.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);

            if (Input.LeftMouseButtonDown && !shiftHeld)
            {
                _lastUnmarkDragTile = NoTile;
                if (tile != _lastMarkDragTile)
                {
                    _lastMarkDragTile = tile;
                    if (tillable && !tileService.HasFlag(tile, TileStateFlag.ReadyToTill))
                    {
                        tileService.SetFlag(tile, TileStateFlag.ReadyToTill);
                        CreateOverlayEntity(tile);
                        RecalculateNeighborhood(tile);
                    }
                }
            }
            else if (Input.LeftMouseButtonDown && shiftHeld)
            {
                _lastMarkDragTile = NoTile;
                if (tile != _lastUnmarkDragTile)
                {
                    _lastUnmarkDragTile = tile;
                    if (tileService.HasFlag(tile, TileStateFlag.ReadyToTill))
                    {
                        tileService.ClearFlag(tile, TileStateFlag.ReadyToTill);
                        DestroyOverlayEntity(tile);
                        RecalculateNeighborhood(tile);
                    }
                }
            }
            else
            {
                _lastMarkDragTile   = NoTile;
                _lastUnmarkDragTile = NoTile;
            }
        }

        private void CreateCursor()
        {
            if (_cursorEntity != null)
                return;

            _cursorEntity = _scene.CreateEntity("till-cursor");
            _cursorRenderer = _cursorEntity.AddComponent(new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
            _cursorRenderer.Color = CursorTillableColor;
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

        private void RestoreOverlays()
        {
            var tileService = Core.Services.GetService<TileStateService>();
            if (tileService == null)
                return;

            foreach (var tile in tileService.GetTilesWithFlag(TileStateFlag.ReadyToTill))
            {
                if (!_overlayEntities.ContainsKey(tile))
                    CreateOverlayEntity(tile);
            }
        }

        private void DestroyAllOverlays()
        {
            var keys = new List<Point>(_overlayEntities.Keys);
            for (int i = 0; i < keys.Count; i++)
                DestroyOverlayEntity(keys[i]);
        }

        private void CreateOverlayEntity(Point tile)
        {
            if (_overlayEntities.ContainsKey(tile))
                return;

            float wx = tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f;
            float wy = tile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f;

            var entity = _scene.CreateEntity("till-overlay-" + tile.X + "-" + tile.Y);
            entity.SetPosition(wx, wy);

            var renderer = entity.AddComponent(new SpriteRenderer(GetBitmaskSprite(tile.X, tile.Y)));
            renderer.Color = Color.Yellow;
            renderer.SetRenderLayer(GameConfig.RenderLayerSingleTileObject);

            _overlayEntities[tile] = entity;
        }

        private void DestroyOverlayEntity(Point tile)
        {
            if (!_overlayEntities.TryGetValue(tile, out var entity))
                return;

            entity.Destroy();
            _overlayEntities.Remove(tile);
        }

        // Recompute transition sprites for the 8 tiles surrounding center (skips tiles with no entity).
        private void RecalculateNeighborhood(Point center)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    UpdateOverlaySprite(new Point(center.X + dx, center.Y + dy));
                }
        }

        private void UpdateOverlaySprite(Point tile)
        {
            if (!_overlayEntities.TryGetValue(tile, out var entity))
                return;
            var renderer = entity.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.Sprite = GetBitmaskSprite(tile.X, tile.Y);
        }

        private Sprite GetBitmaskSprite(int tileX, int tileY)
        {
            int gid = TileBitmask.GetTileIndex(tileX, tileY, TillZerothGid, IsReadyToTill);
            var tileset = _tillTileset ??= _map.GetTilesetForTileGid(TillZerothGid);
            var rectF = tileset.TileRegions[gid];
            var rect  = new Rectangle((int)rectF.X, (int)rectF.Y, (int)rectF.Width, (int)rectF.Height);
            return new Sprite(tileset.Image.Texture, rect);
        }

        private bool IsReadyToTill(int x, int y)
        {
            var svc = Core.Services.GetService<TileStateService>();
            return svc != null && svc.HasFlag(new Point(x, y), TileStateFlag.ReadyToTill);
        }
    }
}
