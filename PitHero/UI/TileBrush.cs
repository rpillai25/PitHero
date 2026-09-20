using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// Presentation-only NxN footprint shared by the 1x1 farm placement modes (issue #422). Owns the
    /// brush size, the keyboard/wheel controls that step it, and a pool of per-tile cursor quads so a
    /// partly-invalid footprint stays readable. Nothing here is simulation state: each covered tile
    /// still dispatches its own re-validated PlayerCommand.
    /// </summary>
    public class TileBrush
    {
        private readonly Scene _scene;
        private readonly string _cursorName;

        // One cursor entity per cell of the largest brush; created on enter, enabled per frame.
        private readonly Entity[] _cursorEntities;
        private readonly PrototypeSpriteRenderer[] _cursorRenderers;

        private int _size = 1;

        /// <summary>Current footprint edge length in tiles (1 = a single tile); clamped on assignment.</summary>
        public int Size
        {
            get => _size;
            set
            {
                if (value < 1)
                    _size = 1;
                else if (value > GameConfig.TileBrushMaxSize)
                    _size = GameConfig.TileBrushMaxSize;
                else
                    _size = value;
            }
        }

        /// <summary>Number of tiles the current footprint covers.</summary>
        public int TileCount => _size * _size;

        /// <summary>Creates a brush whose cursor entities are named from the given prefix.</summary>
        public TileBrush(Scene scene, string cursorName)
        {
            _scene = scene;
            _cursorName = cursorName;
            int max = GameConfig.TileBrushMaxSize * GameConfig.TileBrushMaxSize;
            _cursorEntities = new Entity[max];
            _cursorRenderers = new PrototypeSpriteRenderer[max];
        }

        /// <summary>
        /// Top-left tile of the footprint for a cursor at <paramref name="cursorTile"/>. Odd sizes
        /// centre on the cursor; even sizes put the cursor at the top-left corner.
        /// </summary>
        public Point GetOrigin(Point cursorTile)
        {
            int offset = (_size - 1) / 2;
            return new Point(cursorTile.X - offset, cursorTile.Y - offset);
        }

        /// <summary>The footprint's tile at the given flat index (0 to TileCount-1), row-major.</summary>
        public Point GetTile(Point cursorTile, int index)
        {
            var origin = GetOrigin(cursorTile);
            return new Point(origin.X + index % _size, origin.Y + index / _size);
        }

        /// <summary>
        /// Steps the brush size on SHIFT+wheel and SHIFT+Up/Down. Skipped while a UI text field holds
        /// keyboard focus so typing never resizes the brush. The camera leaves a SHIFT+wheel alone
        /// while a brush mode is active (CameraControllerComponent.IsWheelClaimed).
        /// </summary>
        public void HandleSizeInput(Stage stage)
        {
            bool shiftHeld = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
            if (!shiftHeld)
                return;
            if (stage != null && stage.GetKeyboardFocus() != null)
                return;

            int step = 0;
            int wheel = Input.MouseWheelDelta;
            if (wheel > 0 || Input.IsKeyPressed(Keys.Up))
                step = 1;
            else if (wheel < 0 || Input.IsKeyPressed(Keys.Down))
                step = -1;
            if (step == 0)
                return;

            int previous = _size;
            Size = _size + step;
            if (_size == previous)
                return;

            Debug.Log($"[TileBrush] {_cursorName} brush size {_size}x{_size}");
        }

        /// <summary>Creates the cursor entity pool. Call when the owning mode is entered.</summary>
        public void CreateCursors()
        {
            for (int i = 0; i < _cursorEntities.Length; i++)
            {
                if (_cursorEntities[i] != null)
                    continue;
                var entity = _scene.CreateEntity(_cursorName + "-" + i);
                var renderer = entity.AddComponent(new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
                renderer.SetRenderLayer(GameConfig.RenderLayerTop);
                entity.SetEnabled(false);
                _cursorEntities[i] = entity;
                _cursorRenderers[i] = renderer;
            }
        }

        /// <summary>Destroys the cursor entity pool. Call when the owning mode is exited.</summary>
        public void DestroyCursors()
        {
            for (int i = 0; i < _cursorEntities.Length; i++)
            {
                if (_cursorEntities[i] == null)
                    continue;
                _cursorEntities[i].Destroy();
                _cursorEntities[i] = null;
                _cursorRenderers[i] = null;
            }
        }

        /// <summary>
        /// Positions and tints one cursor quad per footprint tile, hiding the unused pool entries.
        /// <paramref name="isValid"/> is asked per tile so a mixed footprint shows which cells take.
        /// </summary>
        public void UpdateCursors(Point cursorTile, System.Func<Point, bool> isValid, Color validColor, Color invalidColor)
        {
            int count = TileCount;
            for (int i = 0; i < _cursorEntities.Length; i++)
            {
                var entity = _cursorEntities[i];
                if (entity == null)
                    continue;
                if (i >= count)
                {
                    entity.SetEnabled(false);
                    continue;
                }

                var tile = GetTile(cursorTile, i);
                entity.SetEnabled(true);
                entity.SetPosition(
                    tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f,
                    tile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f);
                if (_cursorRenderers[i] != null)
                    _cursorRenderers[i].Color = isValid(tile) ? validColor : invalidColor;
            }
        }

        /// <summary>Hides every cursor quad without destroying the pool (pointer left the play area).</summary>
        public void HideCursors()
        {
            for (int i = 0; i < _cursorEntities.Length; i++)
                _cursorEntities[i]?.SetEnabled(false);
        }
    }
}
