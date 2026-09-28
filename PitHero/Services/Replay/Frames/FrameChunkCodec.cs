using System;
using System.IO;
using System.IO.Compression;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// Compress a raw chunk into a <see cref="FrameChunk"/>, decode a chunk into a
    /// <see cref="DecodedChunk"/>, and re-encode a chunk cut at a tick. Compression is thread-safe and
    /// meant for the sidecar worker.
    /// <para>
    /// The payload is Brotli (frame format v4, issue #432). Deflate's 32 KB window saw only a fifth of
    /// a 120-tick chunk (~156 KB raw on an 8-hour farm), so the walk cycles that repeat across the
    /// chunk went unmatched; measured on that file, Brotli quality 5 with a 4 MB window kept 13.3% of
    /// the raw bytes against deflate's 18.5% (about 37 MB/h instead of 51) in less time per chunk
    /// (1.8 ms against 1–6). Quality 9 gained nothing more; 11 gained 1.3 points for 100 ms a chunk.
    /// </para>
    /// </summary>
    public static class FrameChunkCodec
    {
        private const int PayloadLengthPrefixOffset = 8 + 2 + 2 + 4 + 4;

        /// <summary>Compresses the payload and returns the immutable chunk; the raw chunk's buffer is released.</summary>
        public static FrameChunk Compress(RawFrameChunk raw)
        {
            if (raw == null) throw new ArgumentNullException(nameof(raw));
            if (raw.Buffer == null) throw new ObjectDisposedException(nameof(RawFrameChunk));

            var source = new ReadOnlySpan<byte>(raw.Buffer, raw.PayloadOffset, raw.PayloadRawLength);
            var compressed = new byte[BrotliEncoder.GetMaxCompressedLength(raw.PayloadRawLength)];
            if (!BrotliEncoder.TryCompress(source, compressed, out int payloadLength, GameConfig.ReplayFrameBrotliQuality, GameConfig.ReplayFrameBrotliWindowBits))
                throw new InvalidDataException("Frame chunk payload did not compress");

            int total = raw.PayloadOffset + payloadLength;
            var bytes = new byte[total];
            Buffer.BlockCopy(raw.Buffer, 0, bytes, 0, raw.PayloadOffset);
            bytes[PayloadLengthPrefixOffset] = (byte)payloadLength;
            bytes[PayloadLengthPrefixOffset + 1] = (byte)(payloadLength >> 8);
            bytes[PayloadLengthPrefixOffset + 2] = (byte)(payloadLength >> 16);
            bytes[PayloadLengthPrefixOffset + 3] = (byte)(payloadLength >> 24);
            Buffer.BlockCopy(compressed, 0, bytes, raw.PayloadOffset, payloadLength);

            var chunk = new FrameChunk(raw.FirstTick, raw.TickCount, GameConfig.ReplayFrameFormatVersion, bytes,
                raw.TableDeltaLength, raw.PayloadRawLength, payloadLength);
            raw.Release();
            return chunk;
        }

        /// <summary>
        /// Decompresses a chunk's payload into <paramref name="into"/> (sized by the caller to
        /// <see cref="FrameChunk.PayloadRawLength"/>); throws when the bytes do not decode to exactly that size.
        /// </summary>
        public static void Decompress(FrameChunk chunk, byte[] into)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            var source = new ReadOnlySpan<byte>(chunk.Bytes, chunk.PayloadOffset, chunk.PayloadLength);
            var destination = new Span<byte>(into, 0, chunk.PayloadRawLength);
            if (!BrotliDecoder.TryDecompress(source, destination, out int written) || written != chunk.PayloadRawLength)
                throw new InvalidDataException("Frame chunk payload decompressed to an unexpected size");
        }

        /// <summary>Decompresses and indexes a chunk into a reusable decoded chunk.</summary>
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
