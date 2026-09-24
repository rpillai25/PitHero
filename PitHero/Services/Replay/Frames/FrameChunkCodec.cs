using System;
using System.IO;
using System.IO.Compression;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// Compress (deflate) a raw chunk into a <see cref="FrameChunk"/>, decode a chunk into a
    /// <see cref="DecodedChunk"/>, and re-encode a chunk cut at a tick. Compression is thread-safe and
    /// meant for the sidecar worker (1–6 ms per chunk measured in #425).
    /// </summary>
    public static class FrameChunkCodec
    {
        private const int PayloadLengthPrefixOffset = 8 + 2 + 2 + 4 + 4;

        /// <summary>Deflates the payload and returns the immutable chunk; the raw chunk's buffer is released.</summary>
        public static FrameChunk Compress(RawFrameChunk raw) => Compress(raw, CompressionLevel.Optimal);

        public static FrameChunk Compress(RawFrameChunk raw, CompressionLevel level)
        {
            if (raw == null) throw new ArgumentNullException(nameof(raw));
            if (raw.Buffer == null) throw new ObjectDisposedException(nameof(RawFrameChunk));

            var ms = new MemoryStream(Math.Max(1024, raw.PayloadRawLength / 4));
            using (var ds = new DeflateStream(ms, level, leaveOpen: true))
                ds.Write(raw.Buffer, raw.PayloadOffset, raw.PayloadRawLength);
            int payloadLength = (int)ms.Length;

            int total = raw.PayloadOffset + payloadLength;
            var bytes = new byte[total];
            Buffer.BlockCopy(raw.Buffer, 0, bytes, 0, raw.PayloadOffset);
            bytes[PayloadLengthPrefixOffset] = (byte)payloadLength;
            bytes[PayloadLengthPrefixOffset + 1] = (byte)(payloadLength >> 8);
            bytes[PayloadLengthPrefixOffset + 2] = (byte)(payloadLength >> 16);
            bytes[PayloadLengthPrefixOffset + 3] = (byte)(payloadLength >> 24);
            Buffer.BlockCopy(ms.GetBuffer(), 0, bytes, raw.PayloadOffset, payloadLength);

            var chunk = new FrameChunk(raw.FirstTick, raw.TickCount, GameConfig.ReplayFrameFormatVersion, bytes,
                raw.TableDeltaLength, raw.PayloadRawLength, payloadLength);
            raw.Release();
            return chunk;
        }

        /// <summary>Inflates and indexes a chunk into a reusable decoded chunk.</summary>
        public static void Decode(FrameChunk chunk, DecodedChunk into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Load(chunk);
        }

        /// <summary>Inflates and indexes a chunk into a new decoded chunk (tests and one-off reads).</summary>
        public static DecodedChunk Decode(FrameChunk chunk)
        {
            var d = new DecodedChunk();
            d.Load(chunk);
            return d;
        }

        /// <summary>
        /// Re-encodes a chunk keeping only ticks (and events) up to <paramref name="lastTick"/>, with the
        /// original table delta. Returns the chunk itself when nothing is cut, null when the cut precedes it.
        /// </summary>
        public static FrameChunk Truncate(FrameChunk chunk, long lastTick)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            if (lastTick >= chunk.LastTick)
                return chunk;
            if (lastTick < chunk.FirstTick)
                return null;
            var decoded = Decode(chunk);
            var builder = new FrameChunkBuilder(chunk.TickCount);
            builder.LoadFrom(decoded, new DecodedFrame(), lastTick);
            var raw = builder.Finish(chunk.Bytes, chunk.TableDeltaOffset, chunk.TableDeltaLength);
            return Compress(raw);
        }
    }
}
