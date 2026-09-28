using System;
using System.Diagnostics;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// A synthetic stream built from the #425 census proportions (design doc §6.1–6.2: ~366 drawn
    /// renderables, ~24 changing per tick, walking 8-layer composites, animators, texts, fog tile events,
    /// console lines, a tile keyframe per chunk) must stay under the §6.2 budget of 40 MB per hour when
    /// encoded with the default chunk size and compressed. The stream is stationary, so ten simulated
    /// minutes scaled to the hour is the same measurement at a sixth of the suite time (the unoptimized
    /// Debug JIT the suite runs under makes the full hour take a minute). Guards the encoding against
    /// regressions deflate would not hide: a wider op, a lost tombstone, a keyframe written twice.
    /// </summary>
    [TestClass]
    public class FrameSizeBudgetTests
    {
        private const long BudgetBytesPerHour = 40L * 1024 * 1024;
        private const int TicksPerHour = 60 * 60 * 60;
        private const int SimulatedMinutes = 10;
        private const int MapWidth = 60, MapHeight = 48, MapLayers = 3;

        [TestMethod]
        public void SyntheticHour_CensusMix_StaysUnderBudget()
        {
            int chunkTicks = GameConfig.ReplayFrameChunkTicks;
            int ticks = SimulatedMinutes * 60 * 60;
            int chunkCount = ticks / chunkTicks;
            double hourScale = (double)TicksPerHour / (chunkCount * chunkTicks);
            var world = FrameTestWorld.CensusMix(42);
            var registry = new SpriteKeyRegistry();
            for (int i = 0; i < 540; i++)
                registry.InternSprite(new SpriteKey("Atlases/Actors.png", (i % 32) * 32, (i / 32) * 32, 32, 32));
            var builder = new FrameChunkBuilder(chunkTicks);
            var rng = new Random(7);
            var gids = new uint[MapLayers][];
            for (int l = 0; l < MapLayers; l++)
            {
                gids[l] = new uint[MapWidth * MapHeight];
                for (int i = 0; i < gids[l].Length; i++)
                    gids[l][i] = l == 2 ? 13u : (uint)rng.Next(1, 40);
            }
            var segments = new ConsoleSegmentRecord[6];

            long compressed = 0, raw = 0, tableBytes = 0;
            int tileEvents = 0, consoleLines = 0;
            long deflateTicks = 0, finishTicks = 0;
            var sw = Stopwatch.StartNew();
            for (int c = 0; c < chunkCount; c++)
            {
                builder.Begin((long)c * chunkTicks);
                for (int l = 0; l < MapLayers; l++)
                    builder.SetTileKeyframeLayer((byte)l, MapWidth, MapHeight, gids[l]);
                for (int t = 0; t < chunkTicks; t++)
                {
                    long tick = (long)c * chunkTicks + t;
                    // Census: 273 tile mutations per minute, 75% fog clears; 6 console lines per minute
                    if (rng.NextDouble() < 273.0 / 3600.0)
                    {
                        int layer = rng.NextDouble() < 0.75 ? 2 : 0;
                        int cell = rng.Next(gids[layer].Length);
                        uint gid = layer == 2 ? 0u : (uint)rng.Next(1, 40);
                        gids[layer][cell] = gid;
                        builder.AddTileEvent(tick, (byte)layer, (ushort)(cell % MapWidth), (ushort)(cell / MapWidth), (int)gid);
                        tileEvents++;
                    }
                    if (rng.NextDouble() < 6.0 / 3600.0)
                    {
                        for (int s = 0; s < segments.Length; s++)
                            segments[s] = new ConsoleSegmentRecord(registry.InternString("console line " + consoleLines + " part " + s), 0xFFFFFFFF, 0);
                        builder.AddConsoleEvent(tick, segments);
                        consoleLines++;
                    }
                    world.Step(builder);
                }
                long t0 = Stopwatch.GetTimestamp();
                var rawChunk = builder.Finish(registry);
                long t1 = Stopwatch.GetTimestamp();
                raw += rawChunk.PayloadRawLength;
                var chunk = FrameChunkCodec.Compress(rawChunk);
                deflateTicks += Stopwatch.GetTimestamp() - t1;
                finishTicks += t1 - t0;
                compressed += chunk.Length;
                tableBytes += chunk.TableDeltaLength;
            }
            sw.Stop();

            long compressedPerHour = (long)(compressed * hourScale);
            double mbPerHour = compressedPerHour / (1024.0 * 1024.0);
            double msPerTick = 1000.0 / Stopwatch.Frequency;
            Console.WriteLine("Synthetic " + SimulatedMinutes + " min: " + chunkCount + " chunks, raw " + (raw / (1024.0 * 1024.0)).ToString("0.0") + " MB, compressed "
                + (compressed / (1024.0 * 1024.0)).ToString("0.0") + " MB (" + ((double)raw / compressed).ToString("0.0") + "x) = "
                + mbPerHour.ToString("0.0") + " MB/h; tables " + tableBytes + " B, " + tileEvents + " tile events, " + consoleLines
                + " console lines, " + world.LiveCount + " live entities; " + sw.ElapsedMilliseconds + " ms total ("
                + (sw.Elapsed.TotalMilliseconds * 1000.0 / ticks).ToString("0.0") + " us/tick), finish " + (finishTicks * msPerTick).ToString("0")
                + " ms, deflate " + (deflateTicks * msPerTick).ToString("0") + " ms (" + (deflateTicks * msPerTick / chunkCount).ToString("0.0") + " ms/chunk)");
            Assert.IsTrue(compressedPerHour <= BudgetBytesPerHour, "synthetic stream compresses to " + mbPerHour.ToString("0.0") + " MB per hour, over the 40 MB budget");
            Assert.IsTrue(raw > compressed * 3, "deflate ratio collapsed: " + ((double)raw / compressed).ToString("0.0") + "x");
        }
    }
}
