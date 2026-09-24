using System;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// A finished, compressed chunk of the frame stream: <see cref="GameConfig.ReplayFrameChunkTicks"/>
    /// consecutive ticks (fewer for the last chunk of a session or after a truncation). Immutable once
    /// built, so it can be handed to the sidecar writer thread and kept in the store at the same time.
    /// Byte layout:
    ///   prefix (uncompressed, <see cref="PrefixSize"/> bytes): firstTick i64, tickCount u16, formatVersion u16,
    ///          tableDeltaLen u32, payloadRawLen u32, payloadLen u32
    ///   table delta (uncompressed): the sprite/string/nine-patch keys first seen in this chunk (SpriteKeyRegistry)
    ///   payload (deflate): section lengths, base frame, previous-tick deltas, events, optional tile keyframe
    /// The prefix and table delta stay uncompressed so a sidecar can be indexed and its tables rebuilt by
    /// reading a few dozen bytes per chunk, without inflating anything.
    /// </summary>
    public sealed class FrameChunk
    {
        public const int PrefixSize = 8 + 2 + 2 + 4 + 4 + 4;

        public long FirstTick { get; }
        public int TickCount { get; }
        public int FormatVersion { get; }
        /// <summary>The complete chunk bytes (prefix + table delta + deflated payload). Never mutate.</summary>
        public byte[] Bytes { get; }
        public int TableDeltaLength { get; }
        public int PayloadRawLength { get; }
        public int PayloadLength { get; }

        public long LastTick => FirstTick + TickCount - 1;
        public int TableDeltaOffset => PrefixSize;
        public int PayloadOffset => PrefixSize + TableDeltaLength;
        /// <summary>Compressed size on disk and in the store's memory budget.</summary>
        public int Length => Bytes.Length;

        internal FrameChunk(long firstTick, int tickCount, int formatVersion, byte[] bytes, int tableDeltaLength, int payloadRawLength, int payloadLength)
        {
            FirstTick = firstTick;
            TickCount = tickCount;
            FormatVersion = formatVersion;
            Bytes = bytes;
            TableDeltaLength = tableDeltaLength;
            PayloadRawLength = payloadRawLength;
            PayloadLength = payloadLength;
        }

        /// <summary>Writes the prefix at the start of a writer; <paramref name="payloadLength"/> may be patched later.</summary>
        internal static void WritePrefix(ref FrameWriter w, long firstTick, int tickCount, int tableDeltaLength, int payloadRawLength, int payloadLength)
        {
            w.WriteI64(firstTick);
            w.WriteU16((ushort)tickCount);
            w.WriteU16((ushort)GameConfig.ReplayFrameFormatVersion);
            w.WriteU32((uint)tableDeltaLength);
            w.WriteU32((uint)payloadRawLength);
            w.WriteU32((uint)payloadLength);
        }

        /// <summary>
        /// Parses a chunk's prefix from <paramref name="available"/> bytes at <paramref name="offset"/>.
        /// Returns false when the prefix is incomplete or nonsensical; the caller decides whether the
        /// whole chunk (prefix + <see cref="TotalLength"/>) fits.
        /// </summary>
        public static bool TryReadPrefix(byte[] buffer, int offset, int available, out FrameChunkPrefix prefix)
        {
            prefix = default;
            if (buffer == null || available < PrefixSize || offset < 0 || offset + PrefixSize > buffer.Length)
                return false;
            var r = new FrameReader(buffer, offset, PrefixSize);
            prefix.FirstTick = r.ReadI64();
            prefix.TickCount = r.ReadU16();
            prefix.FormatVersion = r.ReadU16();
            uint tableLen = r.ReadU32();
            uint rawLen = r.ReadU32();
            uint payloadLen = r.ReadU32();
            if (prefix.FirstTick < 0 || prefix.TickCount == 0 || tableLen > int.MaxValue / 4 || rawLen > int.MaxValue / 4 || payloadLen > int.MaxValue / 4)
                return false;
            prefix.TableDeltaLength = (int)tableLen;
            prefix.PayloadRawLength = (int)rawLen;
            prefix.PayloadLength = (int)payloadLen;
            return true;
        }

        /// <summary>
        /// Wraps <paramref name="length"/> bytes at <paramref name="offset"/> as a chunk (copying them when
        /// they are not the whole array). Returns false when the prefix or the section lengths do not fit.
        /// </summary>
        public static bool TryParse(byte[] buffer, int offset, int length, out FrameChunk chunk)
        {
            chunk = null;
            if (!TryReadPrefix(buffer, offset, length, out var p))
                return false;
            if (p.TotalLength != length)
                return false;
            byte[] bytes;
            if (offset == 0 && length == buffer.Length)
            {
                bytes = buffer;
            }
            else
            {
                bytes = new byte[length];
                Buffer.BlockCopy(buffer, offset, bytes, 0, length);
            }
            chunk = new FrameChunk(p.FirstTick, p.TickCount, p.FormatVersion, bytes, p.TableDeltaLength, p.PayloadRawLength, p.PayloadLength);
            return true;
        }
    }

    /// <summary>The uncompressed prefix of a chunk, as read from a sidecar without loading the chunk.</summary>
    public struct FrameChunkPrefix
    {
        public long FirstTick;
        public int TickCount;
        public int FormatVersion;
        public int TableDeltaLength;
        public int PayloadRawLength;
        public int PayloadLength;

        public long LastTick => FirstTick + TickCount - 1;
        /// <summary>Whole chunk length: prefix + table delta + compressed payload.</summary>
        public int TotalLength => FrameChunk.PrefixSize + TableDeltaLength + PayloadLength;
    }
}
