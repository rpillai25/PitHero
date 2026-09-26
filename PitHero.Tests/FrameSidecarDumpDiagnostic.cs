using System;
using System.IO;
using System.Text;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// Diagnostic, not a test: dumps the HUD-font Text ops (damage numbers, miss text) of a session
    /// sidecar so their recorded offsets can be compared with the live geometry. Runs only when
    /// PITHERO_FRAMES_DUMP names a .frames file; writes next to it as &lt;file&gt;.textops.txt.
    /// </summary>
    [TestClass]
    public class FrameSidecarDumpDiagnostic
    {
        [TestMethod]
        public void DumpHudTextOps()
        {
            string path = Environment.GetEnvironmentVariable("PITHERO_FRAMES_DUMP");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            var result = FrameSidecarReader.Open(path, out var reader);
            Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, result, "open");
            var registry = new SpriteKeyRegistry();
            reader.RebuildRegistry(registry);
            var store = new FrameStore(reader.ChunkTicks, long.MaxValue);
            store.Preload(reader);
            var sb = new StringBuilder();
            sb.AppendLine($"chunks {reader.ChunkCount} endTick {reader.EndTick} strings {registry.StringCount}");
            int printed = 0;
            for (long tick = 0; tick <= reader.EndTick && printed < 400; tick += 7)
            {
                if (!store.TryGetFrame(tick, out var frame))
                    continue;
                for (int s = 0; s < frame.SlotCount; s++)
                {
                    if (!frame.TryGetSlot(s, out var e))
                        continue;
                    var r = frame.ReadOps(e);
                    while (!r.AtEnd)
                    {
                        byte code = r.ReadOpCode();
                        switch (code)
                        {
                            case FrameOpCode.Sprite: r.ReadSprite(out _); break;
                            case FrameOpCode.Composite:
                                r.ReadCompositeHeader(out var c);
                                for (int l = 0; l < c.LayerCount; l++) r.ReadCompositeLayer(out _);
                                break;
                            case FrameOpCode.Rect: r.ReadRect(out _); break;
                            case FrameOpCode.NinePatch: r.ReadNinePatch(out _); break;
                            case FrameOpCode.Text:
                                r.ReadText(out var t);
                                if (t.FontId == FrameFontId.Hud || t.FontId == FrameFontId.Hud2x)
                                {
                                    sb.AppendLine($"tick {tick} id {e.Id} '{registry.GetString(t.StringId)}' x {t.X} y {t.Y} dx {t.DX} dy {t.DY} scale {t.Scale} font {t.FontId} flags {t.Flags}");
                                    printed++;
                                }
                                break;
                            default: goto next;
                        }
                    }
                    next:;
                }
            }
            File.WriteAllText(path + ".textops.txt", sb.ToString());
            reader.Dispose();
        }
    }
}
