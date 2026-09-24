using System;
using System.Buffers;
using System.Collections.Generic;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// Encodes one chunk tick by tick (the capture side of the frame stream, fed by the recorder of issue
    /// #427). Per tick: <see cref="BeginTick"/>, <see cref="AddEntity(ushort, ReadOnlySpan{byte})"/> for
    /// every drawn entity, <see cref="EndTick"/> with the HUD record. Tile and console events and the tile
    /// keyframe are added as they happen. The first tick of a chunk is written whole (base frame); every
    /// later tick is an entity-level delta against the previous tick: an entity whose op bytes differ is
    /// re-emitted whole, an entity gone since the previous tick gets a tombstone (opsLen = 0xFFFF), the
    /// HUD record is re-emitted behind a flag byte only when it changed.
    /// A full chunk must be finished right after the <see cref="EndTick"/> that filled it, so the next
    /// tick's events land in the next chunk. No allocation per tick after warm-up.
    /// </summary>
    public sealed class FrameChunkBuilder
    {
        /// <summary>opsLen value that marks an entity removed since the previous tick.</summary>
        public const ushort TombstoneLength = 0xFFFF;
        /// <summary>Largest op byte string one entity may carry.</summary>
        public const int MaxEntityOpsLength = 0xFFFE;
        private const int MaxKeyframeLayers = 8;
        private const int IdSpace = ushort.MaxValue + 1;

        private readonly int _chunkTicks;
        private long _firstTick = -1;
        private int _tickCount;
        private bool _inTick;

        // Previous and current tick snapshots (swapped at EndTick)
        private FrameEntity[] _prev = new FrameEntity[512];
        private FrameEntity[] _cur = new FrameEntity[512];
        private int _prevCount, _curCount;
        private byte[] _prevOps = new byte[64 * 1024];
        private byte[] _curOps = new byte[64 * 1024];
        private int _curOpsLength;
        // id -> index into _cur / _prev, valid only while the matching stamp equals the tick that wrote it
        // (stamped maps: rotation is a swap, no clearing or refilling passes over the entity lists)
        private int[] _curSlot = new int[IdSpace];
        private int[] _prevSlot = new int[IdSpace];
        private int[] _curSlotStamp = new int[IdSpace];
        private int[] _prevSlotStamp = new int[IdSpace];
        private int _stamp = 2;
        private HudRecord _prevHud;

        // Encoded sections: base frame then deltas, back to back
        private byte[] _sections = new byte[256 * 1024];
        private int _sectionsLength;
        private int[] _sectionLengths = new int[128];

        private readonly List<TileEvent> _tileEvents = new List<TileEvent>(256);
        private readonly List<ConsoleEvent> _consoleEvents = new List<ConsoleEvent>(64);
        private readonly List<ConsoleSegmentRecord> _segments = new List<ConsoleSegmentRecord>(256);
        private readonly TileLayerSnapshot[] _keyframe = new TileLayerSnapshot[MaxKeyframeLayers];
        private int _keyframeCount;

        private byte[] _scratch = new byte[16 * 1024];

        public FrameChunkBuilder(int chunkTicks)
        {
            if (chunkTicks <= 0 || chunkTicks > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(chunkTicks));
            _chunkTicks = chunkTicks;
        }

        public int ChunkTicks => _chunkTicks;
        /// <summary>First tick of the chunk being built, or -1 until known.</summary>
        public long FirstTick => _firstTick;
        /// <summary>Ticks ended so far in this chunk.</summary>
        public int TickCount => _tickCount;
        public bool InTick => _inTick;
        public bool IsEmpty => _tickCount == 0 && !_inTick;
        /// <summary>True when the chunk holds <see cref="ChunkTicks"/> ticks and must be finished.</summary>
        public bool IsFull => _tickCount >= _chunkTicks;
        /// <summary>The tick the next <see cref="BeginTick"/> must carry, or -1 until <see cref="Begin"/>.</summary>
        public long NextTick => _firstTick < 0 ? -1 : _firstTick + _tickCount;
        public bool HasTileKeyframe => _keyframeCount > 0;
        public int TileEventCount => _tileEvents.Count;
        public int ConsoleEventCount => _consoleEvents.Count;

        /// <summary>Declares the first tick of the next chunk (needed before events that precede its first frame).</summary>
        public void Begin(long firstTick)
        {
            if (_tickCount != 0 || _inTick)
                throw new InvalidOperationException("Begin called on a chunk in progress");
            if (firstTick < 0)
                throw new ArgumentOutOfRangeException(nameof(firstTick));
            _firstTick = firstTick;
        }

        /// <summary>Starts capturing a tick; ticks must be consecutive within a chunk.</summary>
        public void BeginTick(long tick)
        {
            if (_inTick)
                throw new InvalidOperationException("BeginTick called twice without EndTick");
            if (IsFull)
                throw new InvalidOperationException("Chunk is full; Finish it first");
            if (_firstTick < 0)
                _firstTick = tick;
            else if (tick != _firstTick + _tickCount)
                throw new ArgumentException("Ticks must be consecutive within a chunk", nameof(tick));
            _inTick = true;
            _curCount = 0;
            _curOpsLength = 0;
            AdvanceStamp(1);
        }

        private void AdvanceStamp(int by)
        {
            if (_stamp > int.MaxValue - 8)
            {
                Array.Clear(_curSlotStamp, 0, _curSlotStamp.Length);
                Array.Clear(_prevSlotStamp, 0, _prevSlotStamp.Length);
                _stamp = 2;
            }
            _stamp += by;
        }

        public void AddEntity(ushort id, byte[] ops, int offset, int length)
            => AddEntity(id, new ReadOnlySpan<byte>(ops, offset, length));

        /// <summary>Adds one entity's op bytes for the current tick (copied; each id at most once per tick).</summary>
        public void AddEntity(ushort id, ReadOnlySpan<byte> ops)
        {
            if (!_inTick)
                throw new InvalidOperationException("AddEntity outside BeginTick/EndTick");
            if (ops.Length > MaxEntityOpsLength)
                throw new ArgumentException("Entity op string too long", nameof(ops));
            int need = _curOpsLength + ops.Length;
            if (need > _curOps.Length)
            {
                int size = _curOps.Length * 2;
                while (size < need)
                    size *= 2;
                var grown = new byte[size];
                Buffer.BlockCopy(_curOps, 0, grown, 0, _curOpsLength);
                _curOps = grown;
            }
            ops.CopyTo(new Span<byte>(_curOps, _curOpsLength, ops.Length));
            Record(id, ops.Length);
        }

        /// <summary>
        /// A writer positioned at the end of the current tick's op arena, so a capture emits straight
        /// into the builder without a scratch copy. Pair with <see cref="EndEntity"/>.
        /// </summary>
        public FrameWriter BeginEntity()
        {
            if (!_inTick)
                throw new InvalidOperationException("BeginEntity outside BeginTick/EndTick");
            return new FrameWriter(_curOps, _curOpsLength);
        }

        /// <summary>Commits what was written since <see cref="BeginEntity"/> as the entity's ops (nothing written = not drawn).</summary>
        public void EndEntity(ushort id, ref FrameWriter w)
        {
            _curOps = w.Buffer; // the writer may have grown the arena
            int length = w.Length - _curOpsLength;
            if (length <= 0)
                return;
            if (length > MaxEntityOpsLength)
                throw new ArgumentException("Entity op string too long", nameof(w));
            Record(id, length);
        }

        private void Record(ushort id, int length)
        {
            if (_curSlotStamp[id] == _stamp)
                throw new ArgumentException("Entity id added twice in one tick", nameof(id));
            _curSlotStamp[id] = _stamp;
            _curSlot[id] = _curCount;
            if (_curCount == _cur.Length)
                Array.Resize(ref _cur, _cur.Length * 2);
            _cur[_curCount++] = new FrameEntity(id, _curOpsLength, length);
            _curOpsLength += length;
        }

        /// <summary>Closes the tick: writes the base frame or the delta against the previous tick.</summary>
        public void EndTick(in HudRecord hud)
        {
            if (!_inTick)
                throw new InvalidOperationException("EndTick without BeginTick");

            int start = _sectionsLength;
            var w = new FrameWriter(_sections, start);
            if (_tickCount == 0)
            {
                w.WriteU32((uint)_curCount);
                for (int i = 0; i < _curCount; i++)
                {
                    var e = _cur[i];
                    w.WriteU16(e.Id);
                    w.WriteU16((ushort)e.Length);
                    w.WriteBytes(_curOps, e.Offset, e.Length);
                }
                hud.Write(ref w);
            }
            else
            {
                int countAt = w.ReserveU32();
                uint count = 0;
                int prevStamp = _stamp - 1;
                for (int i = 0; i < _curCount; i++)
                {
                    var e = _cur[i];
                    if (_prevSlotStamp[e.Id] == prevStamp)
                    {
                        var p = _prev[_prevSlot[e.Id]];
                        if (p.Length == e.Length
                            && new ReadOnlySpan<byte>(_prevOps, p.Offset, p.Length).SequenceEqual(new ReadOnlySpan<byte>(_curOps, e.Offset, e.Length)))
                            continue;
                    }
                    w.WriteU16(e.Id);
                    w.WriteU16((ushort)e.Length);
                    w.WriteBytes(_curOps, e.Offset, e.Length);
                    count++;
                }
                for (int j = 0; j < _prevCount; j++)
                {
                    ushort id = _prev[j].Id;
                    if (_curSlotStamp[id] == _stamp)
                        continue;
                    w.WriteU16(id);
                    w.WriteU16(TombstoneLength);
                    count++;
                }
                w.PatchU32(countAt, count);
                if (hud.Equals(_prevHud))
                {
                    w.WriteU8(0);
                }
                else
                {
                    w.WriteU8(1);
                    hud.Write(ref w);
                }
            }
            _sections = w.Buffer;
            if (_sectionLengths.Length <= _tickCount)
                Array.Resize(ref _sectionLengths, _sectionLengths.Length * 2);
            _sectionLengths[_tickCount] = w.Length - start;
            _sectionsLength = w.Length;

            // Rotate: the current tick becomes the previous one (stamped maps make this a swap)
            var te = _prev; _prev = _cur; _cur = te;
            var to = _prevOps; _prevOps = _curOps; _curOps = to;
            var ts = _prevSlot; _prevSlot = _curSlot; _curSlot = ts;
            var tst = _prevSlotStamp; _prevSlotStamp = _curSlotStamp; _curSlotStamp = tst;
            _prevCount = _curCount;
            _curCount = 0;
            _curOpsLength = 0;
            _prevHud = hud;
            _tickCount++;
            _inTick = false;
        }

        /// <summary>Records a tile mutation; the tick must fall inside this chunk.</summary>
        public void AddTileEvent(long tick, byte layer, ushort x, ushort y, int gid)
        {
            CheckEventTick(tick);
            _tileEvents.Add(new TileEvent(tick, layer, x, y, gid));
        }

        /// <summary>Records a console line (up to 255 segments); the tick must fall inside this chunk.</summary>
        public void AddConsoleEvent(long tick, ReadOnlySpan<ConsoleSegmentRecord> segments)
        {
            CheckEventTick(tick);
            if (segments.Length > byte.MaxValue)
                throw new ArgumentException("Too many console segments", nameof(segments));
            int start = _segments.Count;
            for (int i = 0; i < segments.Length; i++)
                _segments.Add(segments[i]);
            _consoleEvents.Add(new ConsoleEvent(tick, start, (byte)segments.Length));
        }

        private void CheckEventTick(long tick)
        {
            if (_firstTick < 0)
                _firstTick = tick;
            if (tick < _firstTick || tick >= _firstTick + _chunkTicks)
                throw new ArgumentOutOfRangeException(nameof(tick), "Event tick outside the chunk being built");
        }

        /// <summary>
        /// Stores a full gid snapshot of one tile layer as of the chunk's first tick (copied). Setting a
        /// layer index twice replaces it.
        /// </summary>
        public void SetTileKeyframeLayer(byte layerIndex, int width, int height, uint[] gids)
        {
            if (gids == null) throw new ArgumentNullException(nameof(gids));
            if (width < 0 || height < 0 || width > ushort.MaxValue || height > ushort.MaxValue || gids.Length < width * height)
                throw new ArgumentException("Bad tile layer dimensions", nameof(gids));
            int slot = -1;
            for (int i = 0; i < _keyframeCount; i++)
            {
                if (_keyframe[i].LayerIndex == layerIndex)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0)
            {
                if (_keyframeCount == MaxKeyframeLayers)
                    throw new InvalidOperationException("Too many tile keyframe layers");
                slot = _keyframeCount++;
            }
            ref var layer = ref _keyframe[slot];
            int count = width * height;
            layer.LayerIndex = layerIndex;
            layer.Width = (ushort)width;
            layer.Height = (ushort)height;
            if (layer.Gids == null || layer.Gids.Length < count)
                layer.Gids = new uint[count];
            Array.Copy(gids, layer.Gids, count);
        }

        /// <summary>
        /// Finishes the chunk with the registry's pending table delta and resets for the next chunk
        /// (which starts right after this one). Returns null when no tick was captured.
        /// </summary>
        public RawFrameChunk Finish(SpriteKeyRegistry registry)
        {
            if (_inTick)
                throw new InvalidOperationException("Finish called inside a tick");
            if (_tickCount == 0)
                return null;
            var tw = new FrameWriter(_scratch);
            if (registry != null)
            {
                registry.WriteTableDelta(ref tw);
            }
            else
            {
                tw.WriteU16(0);
                tw.WriteU16(0);
                tw.WriteU16(0);
            }
            _scratch = tw.Buffer;
            return Finish(_scratch, 0, tw.Length);
        }

        /// <summary>Finishes the chunk with an already serialized table delta (used when re-encoding a chunk).</summary>
        public RawFrameChunk Finish(byte[] tableDelta, int offset, int length)
        {
            if (_inTick)
                throw new InvalidOperationException("Finish called inside a tick");
            if (_tickCount == 0)
                return null;

            int headerLength = 4 * (_tickCount + 2);
            int eventsLength = 4 + _tileEvents.Count * 11 + 4;
            for (int i = 0; i < _consoleEvents.Count; i++)
                eventsLength += 3 + _consoleEvents[i].SegmentCount * 8;
            int keyframeLength = 0;
            if (_keyframeCount > 0)
            {
                keyframeLength = 1;
                for (int i = 0; i < _keyframeCount; i++)
                    keyframeLength += 5 + 4 * _keyframe[i].GidCount;
            }
            int rawLength = headerLength + _sectionsLength + eventsLength + keyframeLength;
            int total = FrameChunk.PrefixSize + length + rawLength;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(total);
            var w = new FrameWriter(buffer);
            FrameChunk.WritePrefix(ref w, _firstTick, _tickCount, length, rawLength, 0);
            w.WriteBytes(tableDelta, offset, length);
            for (int k = 0; k < _tickCount; k++)
                w.WriteU32((uint)_sectionLengths[k]);
            w.WriteU32((uint)eventsLength);
            w.WriteU32((uint)keyframeLength);
            w.WriteBytes(_sections, 0, _sectionsLength);

            w.WriteU32((uint)_tileEvents.Count);
            for (int i = 0; i < _tileEvents.Count; i++)
            {
                var t = _tileEvents[i];
                w.WriteU16((ushort)(t.Tick - _firstTick));
                w.WriteU8(t.Layer);
                w.WriteU16(t.X);
                w.WriteU16(t.Y);
                w.WriteI32(t.Gid);
            }
            w.WriteU32((uint)_consoleEvents.Count);
            for (int i = 0; i < _consoleEvents.Count; i++)
            {
                var c = _consoleEvents[i];
                w.WriteU16((ushort)(c.Tick - _firstTick));
                w.WriteU8(c.SegmentCount);
                for (int s = 0; s < c.SegmentCount; s++)
                {
                    var seg = _segments[c.SegmentStart + s];
                    w.WriteU16(seg.StringId);
                    w.WriteU32(seg.Color);
                    w.WriteU16(seg.ItemStringId);
                }
            }
            if (_keyframeCount > 0)
            {
                w.WriteU8((byte)_keyframeCount);
                for (int i = 0; i < _keyframeCount; i++)
                {
                    var layer = _keyframe[i];
                    w.WriteU8(layer.LayerIndex);
                    w.WriteU16(layer.Width);
                    w.WriteU16(layer.Height);
                    int count = layer.GidCount;
                    for (int g = 0; g < count; g++)
                        w.WriteU32(layer.Gids[g]);
                }
            }
            if (w.Length != total || !ReferenceEquals(w.Buffer, buffer))
            {
                ArrayPool<byte>.Shared.Return(buffer);
                throw new InvalidOperationException("Frame chunk size accounting mismatch");
            }

            var raw = new RawFrameChunk(_firstTick, _tickCount, buffer, length, rawLength);
            long next = _firstTick + _tickCount;
            Reset();
            _firstTick = next;
            return raw;
        }

        /// <summary>Forgets everything, including the first tick (call <see cref="Begin"/> afterwards).</summary>
        public void Reset()
        {
            AdvanceStamp(2); // no stale prev/cur entry can match the next tick's stamps
            _prevCount = 0;
            _curCount = 0;
            _curOpsLength = 0;
            _sectionsLength = 0;
            _tickCount = 0;
            _inTick = false;
            _firstTick = -1;
            _prevHud = default;
            _tileEvents.Clear();
            _consoleEvents.Clear();
            _segments.Clear();
            _keyframeCount = 0;
        }

        /// <summary>
        /// Rebuilds the builder's state from a decoded chunk, keeping only ticks and events up to
        /// <paramref name="lastTick"/>, so recording can continue from the middle of that chunk (resume
        /// after a scene rebuild, or a truncation for Time Travel).
        /// </summary>
        public void LoadFrom(DecodedChunk decoded, DecodedFrame scratch, long lastTick)
        {
            if (decoded == null || !decoded.IsLoaded) throw new ArgumentException("Chunk not loaded", nameof(decoded));
            if (scratch == null) throw new ArgumentNullException(nameof(scratch));
            if (decoded.TickCount > _chunkTicks)
                throw new ArgumentException("Chunk longer than this builder's chunk size", nameof(decoded));
            Reset();
            _firstTick = decoded.FirstTick;
            long span = lastTick - decoded.FirstTick;
            int keep = span >= decoded.TickCount - 1 ? decoded.TickCount : (int)Math.Max(0, span + 1);
            for (int k = 0; k < keep; k++)
            {
                decoded.DecodeFrame(k, scratch);
                BeginTick(decoded.FirstTick + k);
                for (int s = 0; s < scratch.SlotCount; s++)
                {
                    if (!scratch.TryGetSlot(s, out var e))
                        continue;
                    AddEntity(e.Id, scratch.OpsBuffer, e.Offset, e.Length);
                }
                EndTick(scratch.Hud);
            }
            for (int i = 0; i < decoded.TileEvents.Count; i++)
            {
                var t = decoded.TileEvents[i];
                if (t.Tick <= lastTick)
                    _tileEvents.Add(t);
            }
            for (int i = 0; i < decoded.ConsoleEvents.Count; i++)
            {
                var c = decoded.ConsoleEvents[i];
                if (c.Tick > lastTick)
                    continue;
                int start = _segments.Count;
                for (int s = 0; s < c.SegmentCount; s++)
                    _segments.Add(decoded.ConsoleSegments[c.SegmentStart + s]);
                _consoleEvents.Add(new ConsoleEvent(c.Tick, start, c.SegmentCount));
            }
            for (int i = 0; i < decoded.TileKeyframeLayerCount; i++)
            {
                var layer = decoded.GetTileKeyframeLayer(i);
                SetTileKeyframeLayer(layer.LayerIndex, layer.Width, layer.Height, layer.Gids);
            }
        }
    }
}
