using System;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// The mutable tile layers (Base, Detail, FogOfWar) as they were at any recorded tick, rebuilt from
    /// the nearest tile keyframe at or before the tick plus the tile events up to it (design §3.1).
    /// Headless: gid arrays only; <c>ShadowTileLayers</c> copies them into Nez layers for drawing.
    /// Sequential ticks inside one chunk apply only the events in between; anything else rebuilds from
    /// the keyframe, which is a few thousand array writes.
    /// </summary>
    public sealed class ShadowTileGrid
    {
        private const int LayerCount = 3;

        private readonly uint[][] _gids = new uint[LayerCount][];
        private readonly int[] _widths = new int[LayerCount];
        private readonly int[] _heights = new int[LayerCount];
        private long _appliedTick = -1;
        private int _appliedChunk = -1;
        private int _appliedGeneration = -1;

        /// <summary>Tick the grids currently show, or -1 before the first sync.</summary>
        public long AppliedTick => _appliedTick;
        /// <summary>Incremented whenever any gid changed, so a consumer can re-copy only then.</summary>
        public int Version { get; private set; }

        public int Width(int layer) => _widths[layer];
        public int Height(int layer) => _heights[layer];
        /// <summary>Row-major gids of a layer (null until a keyframe covering it was applied).</summary>
        public uint[] Gids(int layer) => _gids[layer];

        /// <summary>
        /// Brings the grids to <paramref name="tick"/> from the store. False when no keyframe at or
        /// before the tick could be loaded (the grids are then left as they were).
        /// </summary>
        public bool SyncTo(FrameStore store, long tick)
        {
            if (store == null || tick < 0 || tick > store.EndTick)
                return false;
            int chunkIndex = store.ChunkIndexOf(tick);
            if (!store.TryGetDecodedChunk(chunkIndex, out var chunk))
                return false;

            bool sameSource = chunkIndex == _appliedChunk && chunk.Generation == _appliedGeneration;
            if (sameSource && tick == _appliedTick)
                return true;
            if (sameSource && tick > _appliedTick)
            {
                ApplyEvents(chunk, _appliedTick, tick);
                _appliedTick = tick;
                return true;
            }

            // Rebuild: the nearest keyframe at or before the tick, then every event from there on
            int keyframeChunk = chunkIndex;
            var kf = chunk;
            while (!kf.HasTileKeyframe)
            {
                keyframeChunk--;
                if (keyframeChunk < 0 || !store.TryGetDecodedChunk(keyframeChunk, out kf))
                    return false;
            }
            ApplyKeyframe(kf);
            long from = kf.FirstTick - 1;
            for (int c = keyframeChunk; c <= chunkIndex; c++)
            {
                if (!store.TryGetDecodedChunk(c, out var events))
                    return false;
                ApplyEvents(events, from, tick);
                from = events.LastTick;
                if (c == chunkIndex)
                {
                    _appliedGeneration = events.Generation;
                }
            }
            _appliedChunk = chunkIndex;
            _appliedTick = tick;
            return true;
        }

        private void ApplyKeyframe(DecodedChunk chunk)
        {
            for (int i = 0; i < chunk.TileKeyframeLayerCount; i++)
            {
                var layer = chunk.GetTileKeyframeLayer(i);
                int li = layer.LayerIndex;
                if (li < 0 || li >= LayerCount)
                    continue;
                int count = layer.GidCount;
                if (_gids[li] == null || _gids[li].Length != count)
                    _gids[li] = new uint[count];
                Array.Copy(layer.Gids, _gids[li], count);
                _widths[li] = layer.Width;
                _heights[li] = layer.Height;
            }
            Version++;
        }

        /// <summary>Applies the chunk's tile events with fromExclusive &lt; tick ≤ toInclusive.</summary>
        private void ApplyEvents(DecodedChunk chunk, long fromExclusive, long toInclusive)
        {
            var events = chunk.TileEvents;
            bool changed = false;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Tick <= fromExclusive)
                    continue;
                if (e.Tick > toInclusive)
                    break;
                int li = e.Layer;
                if (li < 0 || li >= LayerCount || _gids[li] == null)
                    continue;
                int idx = e.X + e.Y * _widths[li];
                if (idx < 0 || idx >= _gids[li].Length)
                    continue;
                uint gid = (uint)e.Gid;
                if (_gids[li][idx] == gid)
                    continue;
                _gids[li][idx] = gid;
                changed = true;
            }
            if (changed)
                Version++;
        }

        /// <summary>Forgets the applied state so the next sync rebuilds from a keyframe.</summary>
        public void Invalidate()
        {
            _appliedTick = -1;
            _appliedChunk = -1;
            _appliedGeneration = -1;
        }
    }
}
