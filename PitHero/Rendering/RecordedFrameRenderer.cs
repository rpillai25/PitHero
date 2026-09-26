using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts;
using Nez.Textures;
using PitHero.ECS.Components;
using PitHero.Services.Replay.Frames;

namespace PitHero.Rendering
{
    /// <summary>
    /// The world pass of the replay frame viewer (design §3.2): draws a recorded tick instead of the
    /// live renderables. In one batch, ordered exactly as Nez's <c>RenderableComparer</c> orders live
    /// renderables (render layer descending, layer depth descending, material), it merges the shadow
    /// tile layers (Base 100, Detail 90, FogOfWar 70) and the live Top layer (2), every live-only world
    /// renderable (tree bands, clouds) and the frame's Sprite / Composite ops, then the overlay ops
    /// (text, rects) after every sprite of layer 0 and above, which is where the live overlays sit.
    /// Graded sprites and the terrain layers switch to the day-night material as the live pass does.
    /// Runs right after the scene's DefaultRenderer, which the viewer's filter reduces to the live UI
    /// canvas and HUD panels. Those are screen-space components that the DefaultRenderer also walks
    /// with the world camera; live they land under the terrain (their layers sort first), so this pass
    /// must come after it to cover them the same way.
    /// </summary>
    public sealed class RecordedFrameRenderer : Renderer
    {
        /// <summary>After the DefaultRenderer (0), whose world-camera ghost of the screen-space HUD panels this pass paints over.</summary>
        public const int Order = 1;

        private enum ItemKind : byte { Sprite, Composite, Overlay, Tile, Live }

        private struct Item
        {
            public int Layer;
            public float Depth;
            public byte Material;   // 0 none, 1 graded
            public int Index;       // stable tiebreak (frame order)
            public ItemKind Kind;
            public int Offset;      // op offset in the frame's ops buffer, or the tile layer index
            public int End;         // end of the owning entity's ops
            public RenderableComponent Live;
        }

        private sealed class ItemComparer : IComparer<Item>
        {
            public int Compare(Item a, Item b)
            {
                int r = b.Layer.CompareTo(a.Layer);
                if (r != 0) return r;
                r = b.Depth.CompareTo(a.Depth);
                if (r != 0) return r;
                r = b.Material.CompareTo(a.Material); // a material sorts before none, as RenderableComparer does
                if (r != 0) return r;
                return a.Index.CompareTo(b.Index);
            }
        }

        private const int TileBase = 0, TileDetail = 1, TileFog = 2, TileTop = -1;
        private const float OverlayDepth = -1f; // after every sprite on its layer (depth sorts descending)
        private const float OutlineThickness = 2f;

        private static readonly ItemComparer Comparer = new ItemComparer();

        private readonly ReplayFrameViewer _viewer;
        private Item[] _items = new Item[1024];
        private int _itemCount;
        private readonly StringBuilder _text = new StringBuilder(256);

        public RecordedFrameRenderer(ReplayFrameViewer viewer) : base(Order, null)
        {
            _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
            ShouldDebugRender = false;
        }

        public override void Render(Scene scene)
        {
            if (!_viewer.DrawsRecordedFrame)
                return; // passthrough: the live renderers draw the scene
            var cam = Camera ?? scene.Camera;
            var frame = _viewer.CurrentFrame;
            var tiles = _viewer.Tiles;
            var graded = _viewer.GradedMaterial;
            BeginRender(cam);

            _itemCount = 0;
            if (tiles != null && tiles.IsBound)
            {
                AddTile(GameConfig.RenderLayerBase, TileBase, graded != null);
                AddTile(GameConfig.RenderLayerDetail, TileDetail, graded != null);
                AddTile(GameConfig.RenderLayerFogOfWar, TileFog, false);
                AddTile(GameConfig.RenderLayerTop, TileTop, graded != null);
            }
            AddLiveWorldRenderables(scene, cam, graded);
            if (frame != null)
                AddFrameOps(frame, cam);

            Array.Sort(_items, 0, _itemCount, Comparer);

            for (int i = 0; i < _itemCount; i++)
            {
                ref var item = ref _items[i];
                switch (item.Kind)
                {
                    case ItemKind.Live:
                        RenderAfterStateCheck(item.Live, cam);
                        break;
                    case ItemKind.Tile:
                        SetMaterial(item.Material != 0 ? graded : null, cam);
                        tiles.Draw(item.Offset, Graphics.Instance.Batcher, cam, Vector2.Zero);
                        break;
                    case ItemKind.Sprite:
                        SetMaterial(item.Material != 0 ? graded : null, cam);
                        DrawSprite(frame, item.Offset, item.End);
                        break;
                    case ItemKind.Composite:
                        SetMaterial(item.Material != 0 ? graded : null, cam);
                        DrawComposite(frame, item.Offset, item.End);
                        break;
                    case ItemKind.Overlay:
                        SetMaterial(null, cam);
                        DrawOverlay(frame, item.Offset, item.End, cam);
                        break;
                }
            }

            EndRender();
        }

        // ───────────────────────────── item collection ─────────────────────────────

        private ref Item NewItem()
        {
            if (_itemCount == _items.Length)
                Array.Resize(ref _items, _items.Length * 2);
            ref var item = ref _items[_itemCount];
            item.Index = _itemCount;
            item.Live = null;
            _itemCount++;
            return ref item;
        }

        private void AddTile(int renderLayer, int tileIndex, bool graded)
        {
            ref var item = ref NewItem();
            item.Layer = renderLayer;
            item.Depth = 0f;
            item.Material = graded ? (byte)1 : (byte)0;
            item.Kind = ItemKind.Tile;
            item.Offset = tileIndex;
            item.End = 0;
        }

        private void AddLiveWorldRenderables(Scene scene, Camera cam, Material graded)
        {
            var list = scene.RenderableComponents;
            for (int i = 0; i < list.Count; i++)
            {
                var renderable = list[i];
                if (!(renderable is ILiveOnlyRenderable) || renderable is ActionQueueVisualizationComponent)
                    continue;
                var rc = renderable as RenderableComponent;
                if (rc == null || FrameCaptureContext.IsScreenSpaceLayer(rc.RenderLayer))
                    continue;
                if (!rc.Enabled || !rc.IsVisibleFromCamera(cam))
                    continue;
                ref var item = ref NewItem();
                item.Layer = rc.RenderLayer;
                item.Depth = rc.LayerDepth;
                item.Material = rc.Material != null ? (byte)1 : (byte)0;
                item.Kind = ItemKind.Live;
                item.Live = rc;
                item.Offset = 0;
                item.End = 0;
            }
        }

        private void AddFrameOps(DecodedFrame frame, Camera cam)
        {
            var bounds = cam.Bounds;
            float margin = GameConfig.ReplayFrameViewCullMarginPixels;
            float left = bounds.X - margin, right = bounds.Right + margin;
            float top = bounds.Y - margin, bottom = bounds.Bottom + margin;
            var ops = frame.OpsBuffer;

            for (int s = 0; s < frame.SlotCount; s++)
            {
                if (!frame.TryGetSlot(s, out var entity))
                    continue;
                int end = entity.Offset + entity.Length;
                var r = new FrameReader(ops, entity.Offset, entity.Length);
                while (!r.AtEnd)
                {
                    int at = r.Position;
                    byte code = r.ReadOpCode();
                    switch (code)
                    {
                        case FrameOpCode.Sprite:
                        {
                            r.ReadSprite(out var op);
                            if ((op.Flags & FrameOpFlags.ScreenSpace) != 0)
                                break;
                            if (op.X < left || op.X > right || op.Y < top || op.Y > bottom)
                                break;
                            ref var item = ref NewItem();
                            item.Layer = op.RenderLayer;
                            item.Depth = op.LayerDepth;
                            item.Material = (op.Flags & FrameOpFlags.Graded) != 0 ? (byte)1 : (byte)0;
                            item.Kind = ItemKind.Sprite;
                            item.Offset = at;
                            item.End = end;
                            break;
                        }
                        case FrameOpCode.Composite:
                        {
                            r.ReadCompositeHeader(out var op);
                            byte flags = 0;
                            for (int l = 0; l < op.LayerCount; l++)
                            {
                                r.ReadCompositeLayer(out var layer);
                                flags |= layer.Flags;
                            }
                            if ((flags & FrameOpFlags.ScreenSpace) != 0)
                                break;
                            if (op.X < left || op.X > right || op.Y < top || op.Y > bottom)
                                break;
                            ref var item = ref NewItem();
                            item.Layer = op.RenderLayer;
                            item.Depth = op.LayerDepth;
                            item.Material = (flags & FrameOpFlags.Graded) != 0 ? (byte)1 : (byte)0;
                            item.Kind = ItemKind.Composite;
                            item.Offset = at;
                            item.End = end;
                            break;
                        }
                        case FrameOpCode.Text:
                        {
                            r.ReadText(out var op);
                            if (RecordedFrameScreenRenderer.IsScreenPassText(in op))
                                break;
                            AddOverlay(at, end);
                            break;
                        }
                        case FrameOpCode.Rect:
                        {
                            r.ReadRect(out var op);
                            if ((op.Flags & FrameOpFlags.ScreenSpace) != 0)
                                break;
                            AddOverlay(at, end);
                            break;
                        }
                        case FrameOpCode.NinePatch:
                            r.ReadNinePatch(out _); // always the screen pass (speech bubbles)
                            break;
                        default:
                            return; // corrupt entity: stop reading it
                    }
                }
            }
        }

        private void AddOverlay(int at, int end)
        {
            ref var item = ref NewItem();
            item.Layer = GameConfig.RenderLayerLowest;
            item.Depth = OverlayDepth;
            item.Material = 0;
            item.Kind = ItemKind.Overlay;
            item.Offset = at;
            item.End = end;
        }

        // ───────────────────────────── drawing ─────────────────────────────

        /// <summary>Switches the batch material the way <see cref="Renderer.RenderAfterStateCheck"/> does for live renderables.</summary>
        private void SetMaterial(Material material, Camera cam)
        {
            if (material != null && material != _currentMaterial)
            {
                _currentMaterial = material;
                if (_currentMaterial.Effect != null)
                    _currentMaterial.OnPreRender(cam);
                Graphics.Instance.Batcher.End();
                Graphics.Instance.Batcher.Begin(_currentMaterial, cam.TransformMatrix);
            }
            else if (material == null && _currentMaterial != Material)
            {
                _currentMaterial = Material;
                Graphics.Instance.Batcher.End();
                Graphics.Instance.Batcher.Begin(_currentMaterial, cam.TransformMatrix);
            }
        }

        private void DrawSprite(DecodedFrame frame, int at, int end)
        {
            var r = new FrameReader(frame.OpsBuffer, at, end - at);
            r.ReadOpCode();
            r.ReadSprite(out var op);
            var sprite = _viewer.Sprites.Get(op.SpriteId);
            if (sprite == null)
                return;
            Graphics.Instance.Batcher.Draw(sprite, new Vector2(op.X, op.Y), Packed(op.Color), 0f, sprite.Origin, Vector2.One,
                Effects(op.Flags), op.LayerDepth);
        }

        private void DrawComposite(DecodedFrame frame, int at, int end)
        {
            var r = new FrameReader(frame.OpsBuffer, at, end - at);
            r.ReadOpCode();
            r.ReadCompositeHeader(out var op);
            var batcher = Graphics.Instance.Batcher;
            for (int l = 0; l < op.LayerCount; l++)
            {
                r.ReadCompositeLayer(out var layer);
                var sprite = _viewer.Sprites.Get(layer.SpriteId);
                if (sprite == null)
                    continue;
                batcher.Draw(sprite, new Vector2(op.X + layer.DX, op.Y + layer.DY), Packed(layer.Color), 0f, sprite.Origin, Vector2.One,
                    Effects(layer.Flags), op.LayerDepth);
            }
        }

        private void DrawOverlay(DecodedFrame frame, int at, int end, Camera cam)
        {
            var r = new FrameReader(frame.OpsBuffer, at, end - at);
            byte code = r.ReadOpCode();
            float inverseZoom = 1f / cam.RawZoom;
            var batcher = Graphics.Instance.Batcher;
            if (code == FrameOpCode.Text)
            {
                r.ReadText(out var op);
                DrawText(batcher, in op, inverseZoom);
            }
            else if (code == FrameOpCode.Rect)
            {
                r.ReadRect(out var op);
                float k = (op.Flags & FrameOpFlags.ConstantScreenSize) != 0 ? inverseZoom : 1f;
                float x = op.X + op.DX * k, y = op.Y + op.DY * k, w = op.Width * k, h = op.Height * k;
                if ((op.Flags & FrameOpFlags.Outline) != 0)
                    batcher.DrawHollowRect(x, y, w, h, Packed(op.Color), OutlineThickness * k);
                else
                    batcher.DrawRect(x, y, w, h, Packed(op.Color));
            }
        }

        /// <summary>Draws a Text op in the current batch: world anchor, offsets and scale in screen pixels when ConstantScreenSize.</summary>
        internal void DrawText(Batcher batcher, in TextOp op, float inverseZoom)
        {
            var font = _viewer.Sprites.Font(op.FontId);
            string s = _viewer.Registry.GetString(op.StringId);
            if (font == null || string.IsNullOrEmpty(s))
                return;
            float k = (op.Flags & FrameOpFlags.ConstantScreenSize) != 0 ? inverseZoom : 1f;
            float scale = op.Scale * k;
            var pos = new Vector2(op.X + op.DX * k, op.Y + op.DY * k);
            bool whole = op.CharStart == 0 && op.CharCount == FrameOpCode.AllChars;
            if (!whole)
            {
                int start = op.CharStart < s.Length ? op.CharStart : s.Length;
                int count = op.CharCount == FrameOpCode.AllChars ? s.Length - start : op.CharCount;
                if (start + count > s.Length) count = s.Length - start;
                if (count <= 0)
                    return;
                _text.Clear();
                _text.Append(s, start, count);
            }
            if ((op.Flags & (FrameOpFlags.Centered | FrameOpFlags.CenteredY)) != 0)
            {
                var size = whole ? font.MeasureString(s) : font.MeasureString(_text);
                if ((op.Flags & FrameOpFlags.Centered) != 0) pos.X -= size.X * scale * 0.5f;
                if ((op.Flags & FrameOpFlags.CenteredY) != 0) pos.Y -= size.Y * scale * 0.5f;
            }
            var color = Packed(op.Color);
            if (whole)
                font.DrawInto(batcher, s, pos, color, 0f, Vector2.Zero, new Vector2(scale, scale), SpriteEffects.None, 0f);
            else
                font.DrawInto(batcher, _text, pos, color, 0f, Vector2.Zero, new Vector2(scale, scale), SpriteEffects.None, 0f);
        }

        internal static Color Packed(uint packed)
        {
            var c = default(Color);
            c.PackedValue = packed;
            return c;
        }

        internal static SpriteEffects Effects(byte flags)
        {
            var e = SpriteEffects.None;
            if ((flags & FrameOpFlags.FlipX) != 0) e |= SpriteEffects.FlipHorizontally;
            if ((flags & FrameOpFlags.FlipY) != 0) e |= SpriteEffects.FlipVertically;
            return e;
        }
    }

    /// <summary>
    /// The screen-space pass of the replay frame viewer: after post-processing and before the scene's
    /// <see cref="ScreenSpaceRenderer"/> (so the live UI stays on top), it draws the frame's
    /// screen-space sprites and the speech bubbles (nine-patch body, tail and typewriter text) exactly
    /// where <see cref="SpeechBubbleComponent"/> draws them: the anchor is the speaker's world point
    /// mapped through the world camera, the extents are screen pixels.
    /// </summary>
    public sealed class RecordedFrameScreenRenderer : Renderer
    {
        /// <summary>Before the scene's ScreenSpaceRenderer (100).</summary>
        public const int Order = 99;

        private readonly ReplayFrameViewer _viewer;
        private readonly RecordedFrameRenderer _world;
        private readonly Rectangle[] _patchRects = new Rectangle[9];
        private int _patchRectsW = -1, _patchRectsH = -1;

        public RecordedFrameScreenRenderer(ReplayFrameViewer viewer, RecordedFrameRenderer world) : base(Order, null)
        {
            _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            WantsToRenderAfterPostProcessors = true;
            ShouldDebugRender = false;
        }

        /// <summary>True for the Text ops the screen pass owns: speech-bubble text (the bubble fonts).</summary>
        internal static bool IsScreenPassText(in TextOp op)
        {
            return (op.Flags & FrameOpFlags.ScreenSpace) != 0
                || op.FontId == FrameFontId.SpeechBubble || op.FontId == FrameFontId.SpeechBubble2x;
        }

        public override void Render(Scene scene)
        {
            if (!_viewer.DrawsRecordedFrame)
                return;
            var frame = _viewer.CurrentFrame;
            var screenCam = scene.GetRenderer<ScreenSpaceRenderer>()?.Camera;
            var worldCam = scene.Camera;
            if (frame == null || screenCam == null || worldCam == null)
                return;
            BeginRender(screenCam);
            var batcher = Graphics.Instance.Batcher;
            var ops = frame.OpsBuffer;

            for (int s = 0; s < frame.SlotCount; s++)
            {
                if (!frame.TryGetSlot(s, out var entity))
                    continue;
                var r = new FrameReader(ops, entity.Offset, entity.Length);
                while (!r.AtEnd)
                {
                    byte code = r.ReadOpCode();
                    switch (code)
                    {
                        case FrameOpCode.Sprite:
                        {
                            r.ReadSprite(out var op);
                            if ((op.Flags & FrameOpFlags.ScreenSpace) == 0)
                                break;
                            var sprite = _viewer.Sprites.Get(op.SpriteId);
                            if (sprite != null)
                                batcher.Draw(sprite, new Vector2(op.X, op.Y), RecordedFrameRenderer.Packed(op.Color), 0f, sprite.Origin, Vector2.One,
                                    RecordedFrameRenderer.Effects(op.Flags), op.LayerDepth);
                            break;
                        }
                        case FrameOpCode.Composite:
                        {
                            r.ReadCompositeHeader(out var op);
                            for (int l = 0; l < op.LayerCount; l++)
                                r.ReadCompositeLayer(out _);
                            break; // composites are never screen-space
                        }
                        case FrameOpCode.Text:
                        {
                            r.ReadText(out var op);
                            if (!IsScreenPassText(in op))
                                break;
                            var anchored = op;
                            if ((op.Flags & FrameOpFlags.ConstantScreenSize) != 0)
                            {
                                var screen = worldCam.WorldToScreenPoint(new Vector2(op.X, op.Y));
                                anchored.X = FrameWriter.ToPixel(screen.X);
                                anchored.Y = FrameWriter.ToPixel(screen.Y);
                                anchored.Flags = (byte)(op.Flags & ~FrameOpFlags.ConstantScreenSize);
                            }
                            _world.DrawText(batcher, in anchored, 1f);
                            break;
                        }
                        case FrameOpCode.Rect:
                        {
                            r.ReadRect(out var op);
                            if ((op.Flags & FrameOpFlags.ScreenSpace) == 0)
                                break;
                            if ((op.Flags & FrameOpFlags.Outline) != 0)
                                batcher.DrawHollowRect(op.X + op.DX, op.Y + op.DY, op.Width, op.Height, RecordedFrameRenderer.Packed(op.Color), 2f);
                            else
                                batcher.DrawRect(op.X + op.DX, op.Y + op.DY, op.Width, op.Height, RecordedFrameRenderer.Packed(op.Color));
                            break;
                        }
                        case FrameOpCode.NinePatch:
                        {
                            r.ReadNinePatch(out var op);
                            DrawSpeechBubble(batcher, worldCam, in op);
                            break;
                        }
                        default:
                            goto nextEntity;
                    }
                }
                nextEntity:;
            }

            EndRender();
        }

        /// <summary>The bubble body patch by patch (borders scale with the bubble) and its tail, as the live component draws them.</summary>
        private void DrawSpeechBubble(Batcher batcher, Camera worldCam, in NinePatchOp op)
        {
            var patch = _viewer.Sprites.GetNinePatch(op.PatchId);
            var tail = _viewer.Sprites.SpeechBubbleTail;
            if (patch == null || op.Width <= 0 || op.Height <= 0)
                return;
            Vector2 anchor = (op.Flags & FrameOpFlags.ConstantScreenSize) != 0
                ? worldCam.WorldToScreenPoint(new Vector2(op.X, op.Y))
                : new Vector2(op.X, op.Y);
            anchor.X = FrameWriter.ToPixel(anchor.X);
            anchor.Y = FrameWriter.ToPixel(anchor.Y);
            float s = (float)op.Width / GameConfig.SpeechBubbleWidth;
            if (s <= 0f) s = 1f;
            int designW = GameConfig.SpeechBubbleWidth;
            int designH = (int)MathF.Round(op.Height / s);
            if (designW != _patchRectsW || designH != _patchRectsH)
            {
                patch.GenerateNinePatchRects(new Rectangle(0, 0, designW, designH), _patchRects, patch.Left, patch.Right, patch.Top, patch.Bottom);
                _patchRectsW = designW;
                _patchRectsH = designH;
            }
            float bubbleX = anchor.X + op.DX;
            float bubbleTopY = anchor.Y + op.DY;
            var color = RecordedFrameRenderer.Packed(op.Color);
            for (int i = 0; i < 9; i++)
            {
                var dest = _patchRects[i];
                var src = patch.NinePatchRects[i];
                if (dest.Width == 0 || dest.Height == 0 || src.Width == 0 || src.Height == 0)
                    continue;
                batcher.Draw(patch.Texture2D, new Vector2(bubbleX + dest.X * s, bubbleTopY + dest.Y * s), src, color, 0f, Vector2.Zero,
                    new Vector2(dest.Width * s / src.Width, dest.Height * s / src.Height), SpriteEffects.None, 0f);
            }
            if (tail != null)
            {
                float tailX = anchor.X - tail.SourceRect.Width * s / 2f;
                float tailTopY = anchor.Y - tail.SourceRect.Height * s;
                batcher.Draw(tail.Texture2D, new Vector2(tailX, tailTopY), tail.SourceRect, color, 0f, Vector2.Zero, s, SpriteEffects.None, 0f);
            }
        }
    }
}
