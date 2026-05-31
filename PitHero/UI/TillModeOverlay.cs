using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Sprites;
using Nez.Tiled;
using Nez.Textures;
using PitHero.Farming;
using PitHero.Services;

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

        private const int TillGid = 170;
        private const int TillMinTileX = 120;
        private const int TillMinTileY = 1;

        public TillModeOverlay(Scene scene, TmxMap map)
        {
            _scene = scene;
            _map   = map;
        }

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

            var tileset = _map.GetTilesetForTileGid(TillGid);
            var rectF   = tileset.TileRegions[TillGid];
            var rect    = new Rectangle((int)rectF.X, (int)rectF.Y, (int)rectF.Width, (int)rectF.Height);
            var sprite  = new Sprite(tileset.Image.Texture, rect);

            float wx = tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f;
            float wy = tile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f;

            var entity = _scene.CreateEntity("till-overlay-" + tile.X + "-" + tile.Y);
            entity.SetPosition(wx, wy);

            var renderer = entity.AddComponent(new SpriteRenderer(sprite));
            renderer.Color = new Color(255, 255, 255, 128);
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
    }
}
