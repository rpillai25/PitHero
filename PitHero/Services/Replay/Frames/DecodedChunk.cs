using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// One chunk inflated and indexed: the base frame, the per-tick deltas (each against the previous
    /// tick), the tile and console events of the chunk and the optional tile keyframe. Reused across
    /// loads (pooled buffers); <see cref="DecodeFrame"/> steps a <see cref="DecodedFrame"/> forward by
    /// one delta when it already holds the previous tick of this chunk, otherwise rebuilds it from the base.
    /// Payload layout (after inflate): baseLen u32, deltaLen u32 × (tickCount − 1), eventsLen u32,
    /// keyframeLen u32, then the sections in that order.
    /// </summary>
    public sealed class DecodedChunk
    {
        private const int MaxKeyframeLayers = 8;

        private byte[] _raw = new byte[256 * 1024];
        private int _rawLength;
        private int _baseOffset, _baseLength;
        private int[] _deltaOffsets = new int[128];
        private int[] _deltaLengths = new int[128];
        private readonly TileLayerSnapshot[] _keyframe = new TileLayerSnapshot[MaxKeyframeLayers];

        /// <summary>First tick of the chunk, or -1 before the first load.</summary>
        public long FirstTick { get; private set; } = -1;
        public int TickCount { get; private set; }
        public long LastTick => FirstTick + TickCount - 1;
        /// <summary>The compressed chunk this was decoded from.</summary>
        public FrameChunk Chunk { get; private set; }
        /// <summary>Incremented on every load so frames decoded from an earlier load are not stepped forward.</summary>
        public int Generation { get; private set; }
        public bool IsLoaded => FirstTick >= 0;

        /// <summary>Tile mutations inside the chunk, in tick order.</summary>
        public readonly List<TileEvent> TileEvents = new List<TileEvent>(256);
        /// <summary>Console lines inside the chunk, in tick order; segments in <see cref="ConsoleSegments"/>.</summary>
        public readonly List<ConsoleEvent> ConsoleEvents = new List<ConsoleEvent>(64);
        public readonly List<ConsoleSegmentRecord> ConsoleSegments = new List<ConsoleSegmentRecord>(256);

        public bool HasTileKeyframe => TileKeyframeLayerCount > 0;
        public int TileKeyframeLayerCount { get; private set; }

        /// <summary>Encoded bytes of the base frame (k = 0) or of delta k, for size diagnostics.</summary>
        public int GetSectionLength(int k)
        {
            if (!IsLoaded || k < 0 || k >= TickCount)
                throw new ArgumentOutOfRangeException(nameof(k));
            return k == 0 ? _baseLength : _deltaLengths[k];
        }

        /// <summary>A layer of the tile keyframe taken at <see cref="FirstTick"/>.</summary>
        public TileLayerSnapshot GetTileKeyframeLayer(int i)
        {
            if (i < 0 || i >= TileKeyframeLayerCount)
                throw new ArgumentOutOfRangeException(nameof(i));
            return _keyframe[i];
        }

        /// <summary>Inflates and indexes a chunk, replacing whatever was loaded before.</summary>
        internal void Load(FrameChunk chunk)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            FirstTick = -1;
            Generation++;
            Inflate(chunk);

            var r = new FrameReader(_raw, 0, _rawLength);
            int tickCount = chunk.TickCount;
            if (_deltaOffsets.Length < tickCount)
            {
                _deltaOffsets = new int[Math.Max(tickCount, _deltaOffsets.Length * 2)];
                _deltaLengths = new int[_deltaOffsets.Length];
            }
            int baseLen = (int)r.ReadU32();
            for (int k = 1; k < tickCount; k++)
                _deltaLengths[k] = (int)r.ReadU32();
            int eventsLen = (int)r.ReadU32();
            int keyframeLen = (int)r.ReadU32();

            int pos = r.Position;
            _baseOffset = pos;
            _baseLength = baseLen;
            pos += baseLen;
            for (int k = 1; k < tickCount; k++)
            {
                _deltaOffsets[k] = pos;
                pos += _deltaLengths[k];
            }
            int eventsOffset = pos;
            pos += eventsLen;
            int keyframeOffset = pos;
            pos += keyframeLen;
            if (pos != _rawLength)
                throw new InvalidDataException("Frame chunk section lengths do not match the payload");

            ParseEvents(eventsOffset, eventsLen, chunk.FirstTick);
            ParseKeyframe(keyframeOffset, keyframeLen);

            Chunk = chunk;
            TickCount = tickCount;
            FirstTick = chunk.FirstTick;
        }

        private void Inflate(FrameChunk chunk)
        {
            int rawLen = chunk.PayloadRawLength;
            if (_raw.Length < rawLen)
            {
                int size = _raw.Length * 2;
                while (size < rawLen)
                    size *= 2;
                _raw = new byte[size];
            }
            int total = 0;
            using (var ms = new MemoryStream(chunk.Bytes, chunk.PayloadOffset, chunk.PayloadLength, writable: false))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            {
                while (total < rawLen)
                {
                    int n = ds.Read(_raw, total, rawLen - total);
                    if (n <= 0)
                        break;
                    total += n;
                }
            }
            if (total != rawLen)
                throw new InvalidDataException("Frame chunk payload inflated to an unexpected size");
            _rawLength = rawLen;
        }

        private void ParseEvents(int offset, int length, long firstTick)
        {
            TileEvents.Clear();
            ConsoleEvents.Clear();
            ConsoleSegments.Clear();
            var r = new FrameReader(_raw, offset, length);
            int tileCount = (int)r.ReadU32();
            for (int i = 0; i < tileCount; i++)
            {
                long tick = firstTick + r.ReadU16();
                byte layer = r.ReadU8();
                ushort x = r.ReadU16();
                ushort y = r.ReadU16();
                int gid = r.ReadI32();
                TileEvents.Add(new TileEvent(tick, layer, x, y, gid));
            }
            int consoleCount = (int)r.ReadU32();
            for (int i = 0; i < consoleCount; i++)
            {
                long tick = firstTick + r.ReadU16();
                byte segCount = r.ReadU8();
                int start = ConsoleSegments.Count;
                for (int s = 0; s < segCount; s++)
                {
                    ushort stringId = r.ReadU16();
                    uint color = r.ReadU32();
                    ushort itemId = r.ReadU16();
                    ConsoleSegments.Add(new ConsoleSegmentRecord(stringId, color, itemId));
                }
                ConsoleEvents.Add(new ConsoleEvent(tick, start, segCount));
            }
            if (!r.AtEnd)
                throw new InvalidDataException("Frame chunk events section has trailing bytes");
        }

        private void ParseKeyframe(int offset, int length)
        {
            TileKeyframeLayerCount = 0;
            if (length == 0)
                return;
            var r = new FrameReader(_raw, offset, length);
            int layerCount = r.ReadU8();
            if (layerCount > MaxKeyframeLayers)
                throw new InvalidDataException("Frame chunk tile keyframe has too many layers");
            for (int i = 0; i < layerCount; i++)
            {
                ref var layer = ref _keyframe[i];
                layer.LayerIndex = r.ReadU8();
                layer.Width = r.ReadU16();
                layer.Height = r.ReadU16();
                int count = layer.GidCount;
                if (layer.Gids == null || layer.Gids.Length < count)
                    layer.Gids = new uint[count];
                for (int g = 0; g < count; g++)
                    layer.Gids[g] = r.ReadU32();
            }
            if (!r.AtEnd)
                throw new InvalidDataException("Frame chunk tile keyframe has trailing bytes");
            TileKeyframeLayerCount = layerCount;
        }

        /// <summary>
        /// Decodes the tick at <paramref name="tickOffset"/> into <paramref name="frame"/>: one delta when
        /// the frame already holds the previous tick of this load, otherwise base + deltas.
        /// </summary>
        public void DecodeFrame(int tickOffset, DecodedFrame frame)
        {
            if (!IsLoaded) throw new InvalidOperationException("No chunk loaded");
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (tickOffset < 0 || tickOffset >= TickCount)
                throw new ArgumentOutOfRangeException(nameof(tickOffset));

            long target = FirstTick + tickOffset;
            bool sameSource = frame.Source == this && frame.SourceGeneration == Generation;
            if (sameSource && frame.Tick == target)
                return;

            int start;
            if (sameSource && frame.Tick >= FirstTick && frame.Tick < target)
            {
                start = (int)(frame.Tick - FirstTick) + 1;
            }
            else
            {
                ApplyBase(frame);
                start = 1;
            }
            for (int k = start; k <= tickOffset; k++)
                ApplyDelta(k, frame);

            frame.Source = this;
            frame.SourceGeneration = Generation;
            frame.Tick = target;
            frame.CompactIfNeeded();
        }

        private void ApplyBase(DecodedFrame frame)
        {
            frame.Clear();
            var r = new FrameReader(_raw, _baseOffset, _baseLength);
            int count = (int)r.ReadU32();
            for (int i = 0; i < count; i++)
            {
                ushort id = r.ReadU16();
                int len = r.ReadU16();
                if (len == FrameChunkBuilder.TombstoneLength)
                    throw new InvalidDataException("Tombstone in a base frame");
                int at = r.Position;
                r.Skip(len);
                frame.Upsert(id, _raw, at, len);
            }
            frame.Hud = HudRecord.Read(ref r);
        }

        private void ApplyDelta(int k, DecodedFrame frame)
        {
            var r = new FrameReader(_raw, _deltaOffsets[k], _deltaLengths[k]);
            int count = (int)r.ReadU32();
            for (int i = 0; i < count; i++)
            {
                ushort id = r.ReadU16();
                int len = r.ReadU16();
                if (len == FrameChunkBuilder.TombstoneLength)
                {
                    frame.Remove(id);
                    continue;
                }
                int at = r.Position;
                r.Skip(len);
                frame.Upsert(id, _raw, at, len);
            }
            if (r.ReadU8() != 0)
                frame.Hud = HudRecord.Read(ref r);
        }
    }
}
