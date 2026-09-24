using System;
using System.Buffers;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// A finished chunk before deflate: prefix (with payloadLen still 0) + table delta + raw payload in
    /// one rented buffer. Produced by <see cref="FrameChunkBuilder.Finish(SpriteKeyRegistry)"/> on the main
    /// thread and turned into a <see cref="FrameChunk"/> by <see cref="FrameChunkCodec.Compress"/> on any
    /// thread (deflate costs 1–6 ms per chunk, measured in #425, so it belongs on the sidecar worker).
    /// </summary>
    public sealed class RawFrameChunk
    {
        public long FirstTick { get; }
        public int TickCount { get; }
        /// <summary>Rented buffer: [prefix][table delta][raw payload]. Owned until <see cref="Release"/>.</summary>
        public byte[] Buffer { get; private set; }
        public int TableDeltaLength { get; }
        public int PayloadRawLength { get; }

        public int PayloadOffset => FrameChunk.PrefixSize + TableDeltaLength;
        public int Length => PayloadOffset + PayloadRawLength;

        internal RawFrameChunk(long firstTick, int tickCount, byte[] buffer, int tableDeltaLength, int payloadRawLength)
        {
            FirstTick = firstTick;
            TickCount = tickCount;
            Buffer = buffer;
            TableDeltaLength = tableDeltaLength;
            PayloadRawLength = payloadRawLength;
        }

        /// <summary>Returns the buffer to the pool; the chunk is unusable afterwards.</summary>
        public void Release()
        {
            var b = Buffer;
            Buffer = null;
            if (b != null)
                ArrayPool<byte>.Shared.Return(b);
        }
    }
}
