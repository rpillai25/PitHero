using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using PitHero.ECS.Components;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// The capture context (string / nine-patch interning, screen-space layers, flag mapping, color
    /// multiply) and the adapters that can run without Core.Instance: components whose ops come from
    /// plain fields on an entity (outline boxes, text with no font), plus the skip rules.
    /// </summary>
    [TestClass]
    public class FrameCaptureAdapterTests
    {
        private static FrameCaptureContext NewContext() => new FrameCaptureContext(new SpriteKeyRegistry());

        [TestMethod]
        public void Context_InternsStringsAndPatches_AndKnowsScreenSpaceLayers()
        {
            var ctx = NewContext();
            Assert.AreEqual((ushort)1, ctx.StringId("Slime"));
            Assert.AreEqual((ushort)1, ctx.StringId("Slime"));
            Assert.AreEqual((ushort)2, ctx.StringId("Miss"));
            Assert.AreEqual((ushort)0, ctx.StringId(null));
            Assert.AreEqual((ushort)0, ctx.StringId(string.Empty));
            Assert.AreEqual((ushort)1, ctx.NinePatchId("NinePatchSpeechBubble"));
            Assert.AreEqual((ushort)0, ctx.SpriteId(null));
            Assert.AreEqual(2, ctx.Registry.StringCount);

            Assert.IsTrue(FrameCaptureContext.IsScreenSpaceLayer(GameConfig.RenderLayerSpeechBubble));
            Assert.IsTrue(FrameCaptureContext.IsScreenSpaceLayer(GameConfig.RenderLayerGraphicalHUD));
            Assert.IsTrue(FrameCaptureContext.IsScreenSpaceLayer(GameConfig.RenderLayerUI));
            Assert.IsTrue(FrameCaptureContext.IsScreenSpaceLayer(GameConfig.RenderLayerActionQueue));
            Assert.IsTrue(FrameCaptureContext.IsScreenSpaceLayer(GameConfig.TransparentPauseOverlay));
            Assert.IsFalse(FrameCaptureContext.IsScreenSpaceLayer(GameConfig.RenderLayerActors));
            Assert.IsFalse(FrameCaptureContext.IsScreenSpaceLayer(GameConfig.RenderLayerBase));

            Assert.AreEqual(FrameOpFlags.None, FrameCaptureContext.SpriteFlags(SpriteEffects.None, GameConfig.RenderLayerActors));
            Assert.AreEqual(FrameOpFlags.FlipX, FrameCaptureContext.SpriteFlags(SpriteEffects.FlipHorizontally, GameConfig.RenderLayerActors));
            Assert.AreEqual(FrameOpFlags.FlipX | FrameOpFlags.FlipY | FrameOpFlags.ScreenSpace,
                FrameCaptureContext.SpriteFlags(SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically, GameConfig.RenderLayerSpeechBubble));
        }

        [TestMethod]
        public void MultiplyColor_IsComponentWise_WithWhiteAsIdentity()
        {
            uint red = Color.Red.PackedValue;
            uint half = new Color(128, 128, 128, 128).PackedValue;
            Assert.AreEqual(red, FrameCaptureAdapters.MultiplyColor(red, Color.White.PackedValue));
            Assert.AreEqual(red, FrameCaptureAdapters.MultiplyColor(Color.White.PackedValue, red));
            uint product = FrameCaptureAdapters.MultiplyColor(red, half);
            var c = new Color { PackedValue = product };
            Assert.AreEqual(128, c.R);
            Assert.AreEqual(0, c.G);
            Assert.AreEqual(0, c.B);
            Assert.AreEqual(128, c.A);
            Assert.AreEqual(0u, FrameCaptureAdapters.MultiplyColor(red, 0u));
        }

        [TestMethod]
        public void BuildingOutline_AndSelectBox_BecomeOutlineRects()
        {
            var entity = new Entity("box");
            entity.SetPosition(100.4f, 50.6f);
            var outline = entity.AddComponent(new BuildingOutlineRenderComponent());
            outline.SetSize(64f, 48f);
            outline.SetColor(Color.Orange);
            var select = entity.AddComponent(new SelectBoxRenderComponent());
            select.SetColor(Color.Yellow);
            var ctx = NewContext();

            var w = new FrameWriter(new byte[64]);
            FrameCaptureAdapters.Capture(outline, ref w, ctx);
            Assert.AreEqual(1 + FrameOpCode.RectPayload, w.Length);
            var r = new FrameReader(w.Buffer, 0, w.Length);
            Assert.AreEqual(FrameOpCode.Rect, r.ReadOpCode());
            r.ReadRect(out var rect);
            Assert.AreEqual((short)100, rect.X);
            Assert.AreEqual((short)51, rect.Y);
            Assert.AreEqual((short)64, rect.Width);
            Assert.AreEqual((short)48, rect.Height);
            Assert.AreEqual(Color.Orange.PackedValue, rect.Color);
            Assert.AreEqual(FrameOpFlags.Outline, rect.Flags);

            w.Reset();
            FrameCaptureAdapters.Capture(select, ref w, ctx);
            r = new FrameReader(w.Buffer, 0, w.Length);
            Assert.AreEqual(FrameOpCode.Rect, r.ReadOpCode());
            r.ReadRect(out rect);
            Assert.AreEqual((short)84, rect.X, "centered 32 px box: x - 16");
            Assert.AreEqual((short)35, rect.Y);
            Assert.AreEqual((short)32, rect.Width);
            Assert.AreEqual(Color.Yellow.PackedValue, rect.Color);
            Assert.AreEqual(FrameOpFlags.Outline, rect.Flags);
        }

        [TestMethod]
        public void TextRenderComponent_WithoutFont_EmitsNothing_AndSkippedKindsEmitNothing()
        {
            var entity = new Entity("label");
            var text = entity.AddComponent(new TextRenderComponent());
            text.SetText("Ada");
            var ctx = NewContext();
            var w = new FrameWriter(new byte[64]);
            FrameCaptureAdapters.Capture(text, ref w, ctx);
            Assert.AreEqual(0, w.Length, "no font, nothing drawn, nothing captured");

            // Live-only renderables and the pause dim never produce ops
            var hud = entity.AddComponent(new BuildingOutlineRenderComponent());
            hud.SetRenderLayer(GameConfig.TransparentPauseOverlay);
            FrameCaptureAdapters.Capture(hud, ref w, ctx);
            Assert.AreEqual(0, w.Length);

            // Classification is decided once per renderable
            Assert.AreEqual(FrameCaptureAdapters.Kind.Capturable, FrameCaptureAdapters.Classify(text));
            Assert.AreEqual(FrameCaptureAdapters.Kind.Capturable, FrameCaptureAdapters.Classify(hud));
            Assert.AreEqual(FrameCaptureAdapters.Kind.Sprite, FrameCaptureAdapters.Classify(new YSortSpriteRenderer()));
            Assert.AreEqual(FrameCaptureAdapters.Kind.CompositeLayer, FrameCaptureAdapters.Classify(new HeroHeadAnimationComponent(Color.White)));
        }
    }
}
