namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// Layout of a <c>.frames</c> sidecar (design §3.1 "On disk"):
    ///   header  (<see cref="HeaderSize"/>): magic u32, frameFormatVersion i32, masterSeed i32, recordedAtUtcTicks i64, simulationVersion i32, chunkTicks i32
    ///   chunks  : [chunkLen u32][chunk bytes] ... appended as they finish (chunk i starts at tick i * chunkTicks)
    ///   footer  : footerMagic u32, totalTicks i64, count i32, {offset i64, firstTick i64, tickCount u16} × count
    ///   trailer (<see cref="TrailerSize"/>): footerOffset i64, endMagic u32
    /// The identity lives in the header so a footer-less file (crash) can still be matched to its .bin;
    /// the footer repeats nothing but adds the final tick count and the chunk index. Without a footer
    /// the reader rebuilds the index by scanning and drops a partial trailing chunk.
    /// </summary>
    public static class FrameSidecarFile
    {
        public const uint HeaderMagic = 0x52464850; // "PHFR"
        public const uint FooterMagic = 0x58464850; // "PHFX"
        public const uint EndMagic = 0x45464850;    // "PHFE"
        public const int HeaderSize = 4 + 4 + 4 + 8 + 4 + 4;
        public const int FooterFixedSize = 4 + 8 + 4;
        public const int IndexEntrySize = 8 + 8 + 2;
        public const int TrailerSize = 8 + 4;
        public const int ChunkLengthPrefixSize = 4;

        /// <summary>One chunk's place in the file.</summary>
        public struct IndexEntry
        {
            /// <summary>Offset of the chunk's u32 length prefix.</summary>
            public long Offset;
            public long FirstTick;
            public int TickCount;
            /// <summary>Chunk bytes after the length prefix.</summary>
            public int Length;

            public long LastTick => FirstTick + TickCount - 1;
        }

        /// <summary>Why a sidecar could not be opened, or <see cref="Ok"/>.</summary>
        public enum OpenResult
        {
            Ok,
            Missing,
            Corrupt,
            FormatMismatch,
            IdentityMismatch,
        }
    }
}
