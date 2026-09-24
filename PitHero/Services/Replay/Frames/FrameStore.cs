using System;
using System.Collections.Generic;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// In-memory ring of compressed chunks keyed by chunk index (tick / <see cref="ChunkTicks"/>), with
    /// O(1) random access to any recorded tick. Chunks stay compressed in RAM under
    /// <see cref="MemoryBudgetBytes"/>; beyond it, the least recently used chunk that can be reloaded is
    /// evicted: one that came from the attached <see cref="IFrameChunkSource"/>, or one the owner reports
    /// as already spilled to the sidecar through <see cref="IsSpilled"/>. Nothing is ever evicted without
    /// a source to reload it from (Braid forgets; we spill). Main-thread only.
    /// </summary>
    public sealed class FrameStore : IFrameChunkSource
    {
        private sealed class Slot
        {
            public FrameChunk Chunk;
            public bool FromSource;
            public long LastUse;
        }

        private readonly Dictionary<int, Slot> _slots = new Dictionary<int, Slot>(1024);
        private readonly List<int> _indices = new List<int>(1024);
        private readonly int _chunkTicks;
        private long _memoryBytes;
        private long _useCounter;
        private IFrameChunkSource _source;
        private int _sourceChunkLimit = int.MaxValue;
        private int _chunkCount;
        private long _endTick = -1;
        private int _pinnedIndex = -1;

        private readonly DecodedChunk[] _decoded = { new DecodedChunk(), new DecodedChunk() };
        private readonly FrameChunk[] _decodedOf = new FrameChunk[2];
        private readonly long[] _decodedUse = new long[2];
        private readonly DecodedFrame _frame = new DecodedFrame();

        public FrameStore(int chunkTicks, long memoryBudgetBytes)
        {
            if (chunkTicks <= 0) throw new ArgumentOutOfRangeException(nameof(chunkTicks));
            _chunkTicks = chunkTicks;
            MemoryBudgetBytes = memoryBudgetBytes;
        }

        /// <summary>Asked before evicting a chunk that was added with <see cref="Add"/>: true once it is on disk.</summary>
        public Func<int, bool> IsSpilled { get; set; }

        public int ChunkTicks => _chunkTicks;
        public long MemoryBudgetBytes { get; set; }
        /// <summary>Compressed bytes currently held in memory.</summary>
        public long MemoryBytes => _memoryBytes;
        public int MemoryChunkCount => _slots.Count;
        /// <summary>Chunk indices known (memory or source): 0..ChunkCount-1.</summary>
        public int ChunkCount => _chunkCount;
        /// <summary>Last tick with a frame, or -1 when empty.</summary>
        public long EndTick => _endTick;
        public bool IsEmpty => _endTick < 0;
        /// <summary>The attached lazy source, if any.</summary>
        public IFrameChunkSource Source => _source;

        public int ChunkIndexOf(long tick) => (int)(tick / _chunkTicks);
        public bool IsInMemory(int chunkIndex) => _slots.ContainsKey(chunkIndex);

        /// <summary>Adds or replaces a finished chunk; it must start on a chunk boundary.</summary>
        public void Add(FrameChunk chunk)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            if (chunk.FirstTick % _chunkTicks != 0)
                throw new ArgumentException("Chunk must start on a chunk boundary", nameof(chunk));
            if (chunk.TickCount > _chunkTicks)
                throw new ArgumentException("Chunk longer than the store's chunk size", nameof(chunk));

            int index = ChunkIndexOf(chunk.FirstTick);
            Put(index, chunk, fromSource: false);
            if (index == _pinnedIndex)
                _pinnedIndex = -1;
            if (index + 1 >= _chunkCount)
            {
                _chunkCount = index + 1;
                _endTick = chunk.LastTick;
            }
            EnforceBudget();
        }

        private void Put(int index, FrameChunk chunk, bool fromSource)
        {
            if (_slots.TryGetValue(index, out var slot))
            {
                _memoryBytes -= slot.Chunk.Length;
                slot.Chunk = chunk;
                slot.FromSource = fromSource;
            }
            else
            {
                slot = new Slot { Chunk = chunk, FromSource = fromSource };
                _slots[index] = slot;
                _indices.Add(index);
            }
            slot.LastUse = ++_useCounter;
            _memoryBytes += chunk.Length;
        }

        private void RemoveSlot(int index)
        {
            if (!_slots.TryGetValue(index, out var slot))
                return;
            _memoryBytes -= slot.Chunk.Length;
            _slots.Remove(index);
            _indices.Remove(index);
        }

        /// <summary>The compressed chunk at an index, from memory or the source.</summary>
        public bool TryGetChunk(int chunkIndex, out FrameChunk chunk)
        {
            if (_slots.TryGetValue(chunkIndex, out var slot))
            {
                slot.LastUse = ++_useCounter;
                chunk = slot.Chunk;
                return true;
            }
            if (_source != null && chunkIndex >= 0 && chunkIndex < _sourceChunkLimit && _source.TryLoadChunk(chunkIndex, out chunk))
            {
                Put(chunkIndex, chunk, fromSource: true);
                EnforceBudget();
                return true;
            }
            chunk = null;
            return false;
        }

        bool IFrameChunkSource.TryLoadChunk(int chunkIndex, out FrameChunk chunk) => TryGetChunk(chunkIndex, out chunk);

        /// <summary>The chunk at an index inflated; the instance is one of two reused slots.</summary>
        public bool TryGetDecodedChunk(int chunkIndex, out DecodedChunk decoded)
        {
            if (!TryGetChunk(chunkIndex, out var chunk))
            {
                decoded = null;
                return false;
            }
            for (int i = 0; i < _decoded.Length; i++)
            {
                if (ReferenceEquals(_decodedOf[i], chunk))
                {
                    _decodedUse[i] = ++_useCounter;
                    decoded = _decoded[i];
                    return true;
                }
            }
            int victim = _decodedUse[0] <= _decodedUse[1] ? 0 : 1;
            FrameChunkCodec.Decode(chunk, _decoded[victim]);
            _decodedOf[victim] = chunk;
            _decodedUse[victim] = ++_useCounter;
            decoded = _decoded[victim];
            return true;
        }

        /// <summary>
        /// The frame at a tick. The returned instance is reused by the next call, so draw from it at once.
        /// Sequential ticks inside one chunk cost one delta each.
        /// </summary>
        public bool TryGetFrame(long tick, out DecodedFrame frame)
        {
            frame = null;
            if (tick < 0 || tick > _endTick)
                return false;
            if (!TryGetDecodedChunk(ChunkIndexOf(tick), out var decoded))
                return false;
            int offset = (int)(tick - decoded.FirstTick);
            if (offset < 0 || offset >= decoded.TickCount)
                return false;
            decoded.DecodeFrame(offset, _frame);
            frame = _frame;
            return true;
        }

        /// <summary>
        /// Drops every frame after <paramref name="tick"/> (Time Travel): later chunks are removed, the
        /// chunk containing the tick is re-encoded and pinned in memory until a new chunk replaces it.
        /// </summary>
        public void TruncateAfter(long tick)
        {
            if (tick >= _endTick)
                return;
            if (tick < 0)
            {
                DropAllChunks();
                return;
            }
            int cut = ChunkIndexOf(tick);
            for (int i = _indices.Count - 1; i >= 0; i--)
            {
                if (_indices[i] > cut)
                    RemoveSlot(_indices[i]);
            }

            if (TryGetChunk(cut, out var cutChunk))
            {
                if (cutChunk.LastTick > tick)
                {
                    var truncated = FrameChunkCodec.Truncate(cutChunk, tick);
                    Put(cut, truncated, fromSource: false);
                    _pinnedIndex = cut;
                    _sourceChunkLimit = cut;
                    _endTick = truncated.LastTick;
                }
                else
                {
                    _sourceChunkLimit = cut + 1;
                    _endTick = cutChunk.LastTick;
                }
                _chunkCount = cut + 1;
            }
            else
            {
                _sourceChunkLimit = cut;
                _chunkCount = cut;
                _endTick = (long)cut * _chunkTicks - 1;
            }
            EnforceBudget();
        }

        private void DropAllChunks()
        {
            _slots.Clear();
            _indices.Clear();
            _memoryBytes = 0;
            _chunkCount = 0;
            _endTick = -1;
            _pinnedIndex = -1;
            _sourceChunkLimit = 0;
        }

        /// <summary>Attaches a lazy source (a sidecar reader); chunks not in memory are loaded from it on demand.</summary>
        public void Preload(IFrameChunkSource source)
        {
            _source = source;
            _sourceChunkLimit = int.MaxValue;
            if (source == null)
                return;
            if (source.ChunkCount > _chunkCount)
                _chunkCount = source.ChunkCount;
            if (source.EndTick > _endTick)
                _endTick = source.EndTick;
        }

        /// <summary>Takes over another store's chunks and source (a scene rebuild handing its stream to the new recorder).</summary>
        public void Preload(FrameStore other)
        {
            if (other == null || ReferenceEquals(other, this))
                return;
            if (other._chunkTicks != _chunkTicks)
                throw new ArgumentException("Chunk size mismatch", nameof(other));
            for (int i = 0; i < other._indices.Count; i++)
            {
                int index = other._indices[i];
                var slot = other._slots[index];
                Put(index, slot.Chunk, slot.FromSource);
            }
            _source = other._source;
            _sourceChunkLimit = other._sourceChunkLimit;
            _pinnedIndex = other._pinnedIndex;
            if (other._chunkCount > _chunkCount)
                _chunkCount = other._chunkCount;
            if (other._endTick > _endTick)
                _endTick = other._endTick;
            other.Clear();
            EnforceBudget();
        }

        /// <summary>Forgets every chunk, the source and the decoded cache.</summary>
        public void Clear()
        {
            DropAllChunks();
            _source = null;
            _sourceChunkLimit = int.MaxValue;
            for (int i = 0; i < _decodedOf.Length; i++)
            {
                _decodedOf[i] = null;
                _decodedUse[i] = 0;
            }
            _frame.Clear();
        }

        private bool IsEvictable(int index, Slot slot)
        {
            if (index == _pinnedIndex || _source == null || index >= _sourceChunkLimit)
                return false;
            if (slot.FromSource)
                return true;
            var spilled = IsSpilled;
            return spilled != null && spilled(index);
        }

        private void EnforceBudget()
        {
            while (_memoryBytes > MemoryBudgetBytes && _slots.Count > 0)
            {
                int victim = -1;
                long oldest = long.MaxValue;
                for (int i = 0; i < _indices.Count; i++)
                {
                    int index = _indices[i];
                    var slot = _slots[index];
                    if (slot.LastUse >= oldest || !IsEvictable(index, slot))
                        continue;
                    oldest = slot.LastUse;
                    victim = index;
                }
                if (victim < 0)
                    break;
                RemoveSlot(victim);
            }
        }
    }
}
