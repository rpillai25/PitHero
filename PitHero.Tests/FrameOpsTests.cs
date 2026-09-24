using System;
using System.IO;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>Pins the draw-op byte layouts, the writer/reader primitives, bounds checks and no-alloc after warm-up.</summary>
    [TestClass]
    public class FrameOpsTests
    {
        [TestMethod]
        public void Sprite_RoundTrip_AllFields()
        {
            var w = new FrameWriter(new byte[64]);
            w.WriteSprite(1234, -17.4f, 250.6f, 0.375f, -5, 0x80FF4020, (byte)(FrameOpFlags.FlipX | FrameOpFlags.ScreenSpace));
            Assert.AreEqual(1 + FrameOpCode.SpritePayload, w.Length);

            var r = new FrameReader(w.Buffer, 0, w.Length);
            Assert.AreEqual(FrameOpCode.Sprite, r.PeekOp());
            Assert.AreEqual(FrameOpCode.Sprite, r.ReadOpCode());
            r.ReadSprite(out var op);
            Assert.AreEqual((ushort)1234, op.SpriteId);
            Assert.AreEqual((short)-17, op.X);
            Assert.AreEqual((short)251, op.Y);
            Assert.AreEqual(0.375f, op.LayerDepth);
            Assert.AreEqual((short)-5, op.RenderLayer);
            Assert.AreEqual(0x80FF4020u, op.Color);
            Assert.AreEqual(FrameOpFlags.FlipX | FrameOpFlags.ScreenSpace, op.Flags);
            Assert.IsTrue(r.AtEnd);
        }

        [TestMethod]
        public void Composite_Text_Rect_NinePatch_RoundTrip_AndSkipOp()
        {
            var w = new FrameWriter(new byte[8]); // grows
            w.WriteCompositeHeader(100.2f, 50.5f, 0.25f, 30, 3);
            for (int l = 0; l < 3; l++)
                w.WriteCompositeLayer((ushort)(10 + l), l, -l, 0xFFFFFFFFu - (uint)l, l == 1 ? FrameOpFlags.FlipX : FrameOpFlags.None);
            w.WriteText(77, 1.4f, 2.6f, 0x11223344, 1.5f, 2, FrameOpFlags.Centered);
            w.WriteRect(-3f, 4f, 32f, 6f, 0xAABBCCDD, FrameOpFlags.Outline);
            w.WriteNinePatch(9, 10f, 20f, 120f, 40f, 0x01020304);
            int total = 1 + FrameOpCode.CompositeHeaderPayload + 3 * FrameOpCode.CompositeLayerBytes
                        + 1 + FrameOpCode.TextPayload + 1 + FrameOpCode.RectPayload + 1 + FrameOpCode.NinePatchPayload;
            Assert.AreEqual(total, w.Length);

            var r = new FrameReader(w.Buffer, 0, w.Length);
            Assert.AreEqual(FrameOpCode.Composite, r.ReadOpCode());
            r.ReadCompositeHeader(out var c);
            Assert.AreEqual((short)100, c.X);
            Assert.AreEqual((short)50, c.Y); // half to even, like SpriteCompositorBase's Math.Round
            Assert.AreEqual(0.25f, c.LayerDepth);
            Assert.AreEqual((short)30, c.RenderLayer);
            Assert.AreEqual((byte)3, c.LayerCount);
            for (int l = 0; l < 3; l++)
            {
                r.ReadCompositeLayer(out var layer);
                Assert.AreEqual((ushort)(10 + l), layer.SpriteId);
                Assert.AreEqual((short)l, layer.DX);
                Assert.AreEqual((short)-l, layer.DY);
                Assert.AreEqual(0xFFFFFFFFu - (uint)l, layer.Color);
                Assert.AreEqual(l == 1 ? FrameOpFlags.FlipX : FrameOpFlags.None, layer.Flags);
            }
            Assert.AreEqual(FrameOpCode.Text, r.ReadOpCode());
            r.ReadText(out var t);
            Assert.AreEqual((ushort)77, t.StringId);
            Assert.AreEqual((short)1, t.X);
            Assert.AreEqual((short)3, t.Y);
            Assert.AreEqual(0x11223344u, t.Color);
            Assert.AreEqual(1.5f, t.Scale);
            Assert.AreEqual((byte)2, t.FontId);
            Assert.AreEqual(FrameOpFlags.Centered, t.Flags);
            Assert.AreEqual(FrameOpCode.Rect, r.ReadOpCode());
            r.ReadRect(out var rect);
            Assert.AreEqual((short)-3, rect.X);
            Assert.AreEqual((short)32, rect.Width);
            Assert.AreEqual((short)6, rect.Height);
            Assert.AreEqual(0xAABBCCDDu, rect.Color);
            Assert.AreEqual(FrameOpFlags.Outline, rect.Flags);
            Assert.AreEqual(FrameOpCode.NinePatch, r.ReadOpCode());
            r.ReadNinePatch(out var np);
            Assert.AreEqual((ushort)9, np.PatchId);
            Assert.AreEqual((short)120, np.Width);
            Assert.AreEqual(0x01020304u, np.Color);
            Assert.IsTrue(r.AtEnd);

            // SkipOp walks the same bytes op by op
            var s = new FrameReader(w.Buffer, 0, w.Length);
            int ops = 0;
            while (!s.AtEnd) { s.SkipOp(); ops++; }
            Assert.AreEqual(4, ops);
        }

        [TestMethod]
        public void ToPixel_RoundsHalfToEvenLikeTheCompositors_AndClamps()
        {
            // SpriteCompositorBase snaps entity positions with Math.Round (half to even); the stream must agree
            Assert.AreEqual((short)50, FrameWriter.ToPixel(50.5f));
            Assert.AreEqual((short)52, FrameWriter.ToPixel(51.5f));
            Assert.AreEqual((short)-50, FrameWriter.ToPixel(-50.5f));
            Assert.AreEqual((short)3, FrameWriter.ToPixel(2.6f));
            Assert.AreEqual((short)2, FrameWriter.ToPixel(2.4999f));
            Assert.AreEqual(short.MaxValue, FrameWriter.ToPixel(1e9f));
            Assert.AreEqual(short.MinValue, FrameWriter.ToPixel(-1e9f));
        }

        [TestMethod]
        public void Primitives_And_Strings_RoundTrip()
        {
            var w = new FrameWriter(new byte[4]);
            w.WriteU8(0xAB);
            w.WriteU16(0xBEEF);
            w.WriteI16(-2);
            w.WriteU32(0xDEADBEEF);
            w.WriteI32(-123456);
            w.WriteI64(long.MinValue + 5);
            w.WriteF32(-0.0625f);
            w.WriteString("Atlas/héros ☃");
            w.WriteString(string.Empty);
            int at = w.ReserveU32();
            w.WriteBytes(new byte[] { 1, 2, 3 });
            w.PatchU32(at, 3);

            var r = new FrameReader(w.Buffer, 0, w.Length);
            Assert.AreEqual((byte)0xAB, r.ReadU8());
            Assert.AreEqual((ushort)0xBEEF, r.ReadU16());
            Assert.AreEqual((short)-2, r.ReadI16());
            Assert.AreEqual(0xDEADBEEFu, r.ReadU32());
            Assert.AreEqual(-123456, r.ReadI32());
            Assert.AreEqual(long.MinValue + 5, r.ReadI64());
            Assert.AreEqual(-0.0625f, r.ReadF32());
            Assert.AreEqual("Atlas/héros ☃", r.ReadString());
            Assert.AreEqual(string.Empty, r.ReadString());
            Assert.AreEqual(3u, r.ReadU32());
            var three = r.Slice(3);
            Assert.AreEqual(3, three.Length);
            Assert.AreEqual((byte)2, three[1]);
            Assert.AreEqual(0, r.Remaining);
        }

        [TestMethod]
        public void Reader_Overrun_Throws_InvalidData()
        {
            var buf = new byte[3];
            var r = new FrameReader(buf);
            r.ReadU16();
            Assert.ThrowsException<InvalidDataException>(() =>
            {
                var rr = new FrameReader(buf, 2, 1);
                rr.ReadU32();
            });
            Assert.ThrowsException<InvalidDataException>(() =>
            {
                var rr = new FrameReader(buf, 0, 3);
                rr.ReadOpCode();
                rr.ReadSprite(out _);
            });
            Assert.ThrowsException<InvalidDataException>(() =>
            {
                var rr = new FrameReader(new byte[] { 99 });
                rr.SkipOp();
            });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new FrameReader(buf, 2, 4));
        }

        [TestMethod]
        public void Writer_GrowsByDoubling_ThenAllocatesNothingAfterWarmUp()
        {
            var start = new byte[16];
            var w = new FrameWriter(start);
            for (int i = 0; i < 10; i++)
                w.WriteSprite(1, i, i, 0f, 0, 0, 0);
            Assert.IsFalse(ReferenceEquals(start, w.Buffer), "buffer must have grown");
            Assert.AreEqual(256, w.Buffer.Length, "grows by doubling from 16: 32, 64, 128, 256");
            var warm = w.Buffer;

            // Warm: same ops again into the same buffer, no growth and no allocation
            w.Reset();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10; i++)
                w.WriteSprite(1, i, i, 0f, 0, 0, 0);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.IsTrue(ReferenceEquals(warm, w.Buffer));
            Assert.AreEqual(0, after - before, "no allocation on the warm path");
        }

        [TestMethod]
        public void HudRecord_RoundTrip_AndSizeConstant()
        {
            var hud = new HudRecord
            {
                Hero = new HudMember { Present = true, Hp = 1, MaxHp = 2, Mp = 3, MaxMp = 4, Level = 5 },
                Merc1 = new HudMember { Present = false },
                Merc2 = new HudMember { Present = true, Hp = -1, MaxHp = 9999, Mp = 999, MaxMp = 999, Level = 99 },
                Gold = 123456789012L,
                PitLevel = 42,
                PitTier = 4,
                InGameSeconds = 3661.5f,
                Paused = true,
            };
            var w = new FrameWriter(new byte[128]);
            hud.Write(ref w);
            Assert.AreEqual(HudRecord.Size, w.Length);
            var r = new FrameReader(w.Buffer, 0, w.Length);
            var back = HudRecord.Read(ref r);
            Assert.AreEqual(hud, back);
            Assert.IsTrue(r.AtEnd);

            var changed = hud;
            changed.Merc1.Present = true;
            Assert.AreNotEqual(hud, changed);
        }
    }
}
