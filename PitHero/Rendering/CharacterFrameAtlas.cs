using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Textures;

namespace PitHero.Rendering
{
    /// <summary>
    /// Shared runtime atlas for baked character poses (issue #459). Each page is one large render target cut
    /// into fixed cells the size of a <see cref="PitHero.ECS.Components.MultiSpriteAnimator"/> composite;
    /// a character bakes each pose it shows into a cell once and from then on draws that cell as one quad,
    /// so every character on screen draws from the same texture and batches with the others.
    ///
    /// Cells are handed out from a free list and returned when a character leaves the scene. Pages are
    /// created on demand and live for the whole process. They are plain render targets: FNA never loses
    /// render-target contents (RenderTarget2D.IsContentLost is always false), so no copy to a Texture2D is needed.
    /// </summary>
    public static class CharacterFrameAtlas
    {
        /// <summary>Width and height of one atlas page in pixels.</summary>
        public const int PageSize = GameConfig.CharacterFramePageSize;

        /// <summary>Width of one baked pose cell.</summary>
        public const int CellWidth = GameConfig.CharacterFrameCellWidth;

        /// <summary>Height of one baked pose cell.</summary>
        public const int CellHeight = GameConfig.CharacterFrameCellHeight;

        private const int Columns = PageSize / CellWidth;
        private const int Rows = PageSize / CellHeight;

        /// <summary>Cells on one page.</summary>
        public const int CellsPerPage = Columns * Rows;

        private static readonly List<RenderTarget2D> _pages = new List<RenderTarget2D>(4);
        private static readonly List<Sprite> _cellSprites = new List<Sprite>(CellsPerPage * 4);
        private static readonly Stack<int> _freeCells = new Stack<int>(CellsPerPage * 4);
        private static Vector2 _cellOrigin;

        private static readonly RasterizerState ScissorRasterizer = new RasterizerState
        {
            CullMode = CullMode.CullCounterClockwiseFace,
            ScissorTestEnable = true
        };

        /// <summary>Cells currently handed out (read by tests and the render stats line).</summary>
        public static int CellsInUse => _cellSprites.Count - _freeCells.Count;

        /// <summary>Pages created so far.</summary>
        public static int PageCount => _pages.Count;

        /// <summary>
        /// Claims a free cell for a pose whose entity position maps to <paramref name="origin"/> inside the
        /// cell. Returns -1 when no graphics device exists (headless tests) or the origin differs from the one
        /// the atlas was built for.
        /// </summary>
        public static int Allocate(Vector2 origin)
        {
            if (Core.GraphicsDevice == null)
                return -1;
            if (_cellSprites.Count == 0)
                _cellOrigin = origin;
            else if (_cellOrigin != origin)
                return -1;

            if (_freeCells.Count == 0 && !AddPage())
                return -1;
            return _freeCells.Pop();
        }

        /// <summary>Returns a cell to the free list.</summary>
        public static void Release(int cell)
        {
            if (cell >= 0 && cell < _cellSprites.Count)
                _freeCells.Push(cell);
        }

        /// <summary>The sprite that draws <paramref name="cell"/>, with its origin at the entity pivot.</summary>
        public static Sprite GetSprite(int cell) => _cellSprites[cell];

        /// <summary>
        /// Renders <paramref name="count"/> layers, from index <paramref name="start"/> of the arrays, into
        /// <paramref name="cell"/>. The batcher must be between
        /// Begin and End with the camera transform; it is ended, pointed at the page, and begun again with the
        /// same state the compositor uses, so the caller keeps drawing as before. Each layer is drawn at the
        /// cell's pivot plus its offset, flipped and tinted, and clipped to the cell.
        /// </summary>
        public static void Bake(Batcher batcher, Camera camera, int cell,
            Sprite[] sprites, Vector2[] offsets, bool[] flips, Color[] colors, int start, int count)
        {
            var device = Core.GraphicsDevice;
            int page = cell / CellsPerPage;
            int local = cell - page * CellsPerPage;
            var rect = new Rectangle((local % Columns) * CellWidth, (local / Columns) * CellHeight, CellWidth, CellHeight);

            var prevTargets = device.GetRenderTargets();
            batcher.End();

            device.SetRenderTarget(_pages[page]);
            RenderStats.CompositorTargetSwitches++;
            device.ScissorRectangle = rect;

            // Clear only this cell: an opaque transparent quad overwrites whatever an earlier pose left there
            batcher.Begin(BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, ScissorRasterizer,
                null, Matrix.Identity, false);
            batcher.Draw(Graphics.Instance.PixelTexture, new Vector2(rect.X, rect.Y), Color.Transparent, 0f,
                Vector2.Zero, new Vector2(CellWidth, CellHeight), SpriteEffects.None, 0f);
            batcher.End();

            batcher.Begin(BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, ScissorRasterizer,
                null, Matrix.CreateTranslation(rect.X + _cellOrigin.X, rect.Y + _cellOrigin.Y, 0f), false);
            for (int i = start; i < start + count; i++)
            {
                var sprite = sprites[i];
                if (sprite == null)
                    continue;
                batcher.Draw(sprite, offsets[i], colors[i], 0f, sprite.Origin, Vector2.One,
                    flips[i] ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }
            batcher.End();

            device.SetRenderTargets(prevTargets.Length > 0 ? prevTargets : null);
            RenderStats.CompositorTargetSwitches++;
            RenderStats.Bakes++;

            batcher.Begin(BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None,
                RasterizerState.CullCounterClockwise, null, camera.TransformMatrix, false);
        }

        private static bool AddPage()
        {
            var device = Core.GraphicsDevice;
            if (device == null || _pages.Count >= GameConfig.CharacterFrameMaxPages)
                return false;

            var target = new RenderTarget2D(device, PageSize, PageSize, false, SurfaceFormat.Color,
                DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            int pageIndex = _pages.Count;
            _pages.Add(target);

            // A fresh page starts undefined on some drivers; clear it once
            var prevTargets = device.GetRenderTargets();
            device.SetRenderTarget(target);
            device.Clear(Color.Transparent);
            device.SetRenderTargets(prevTargets.Length > 0 ? prevTargets : null);

            int first = pageIndex * CellsPerPage;
            for (int i = 0; i < CellsPerPage; i++)
            {
                var rect = new Rectangle((i % Columns) * CellWidth, (i / Columns) * CellHeight, CellWidth, CellHeight);
                var sprite = new Sprite(target, rect, _cellOrigin);
                _cellSprites.Add(sprite);
            }
            // Push in reverse so cells are handed out from the top-left
            for (int i = CellsPerPage - 1; i >= 0; i--)
                _freeCells.Push(first + i);

            Debug.Log($"[CharacterFrameAtlas] Added page {pageIndex} ({CellsPerPage} cells)");
            return true;
        }
    }
}
