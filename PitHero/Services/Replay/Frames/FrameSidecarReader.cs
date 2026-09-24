using System;
using System.Collections.Generic;
using System.IO;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// Reader of a <c>.frames</c> sidecar: validates the identity header, loads the chunk index from the
    /// footer or rebuilds it by scanning when the footer is missing (a partial trailing chunk is dropped),
    /// then serves chunks lazily to a <see cref="FrameStore"/>. Chunk reads take a lock, so a viewer and a
    /// worker may share one reader.
    /// </summary>
    public sealed class FrameSidecarReader : IFrameChunkSource, IDisposable
    {
        private readonly object _gate = new object();
        private readonly List<FrameSidecarFile.IndexEntry> _entries;
        private FileStream _stream;

        public string Path { get; }
        /// <summary>Header identity; <see cref="FrameSidecarIdentity.TotalTicks"/> comes from the footer, else from the last chunk.</summary>
        public FrameSidecarIdentity Identity { get; }
        public int ChunkTicks { get; }
        /// <summary>False when the index had to be rebuilt by scanning (unfinished or crashed session).</summary>
        public bool HasFooter { get; }
        /// <summary>End of the last complete chunk (where a footer, or new chunks, would go).</summary>
        public long DataEnd { get; }

        public int ChunkCount => _entries.Count;
        public long EndTick => _entries.Count == 0 ? -1 : _entries[_entries.Count - 1].LastTick;
        public long TotalTicks => Identity.TotalTicks;

        private FrameSidecarReader(string path, FileStream stream, in FrameSidecarIdentity identity, int chunkTicks, bool hasFooter,
            List<FrameSidecarFile.IndexEntry> entries, long dataEnd)
        {
            Path = path;
            _stream = stream;
            Identity = identity;
            ChunkTicks = chunkTicks;
            HasFooter = hasFooter;
            _entries = entries;
            DataEnd = dataEnd;
        }

        /// <summary>Opens a sidecar and checks it belongs to the recording with these header values.</summary>
        public static FrameSidecarFile.OpenResult Open(string path, int masterSeed, long recordedAtUtcTicks, int simulationVersion, out FrameSidecarReader reader)
        {
            var result = Open(path, out reader);
            if (result != FrameSidecarFile.OpenResult.Ok)
                return result;
            if (!reader.Identity.MatchesRecording(masterSeed, recordedAtUtcTicks, simulationVersion))
            {
                reader.Dispose();
                reader = null;
                return FrameSidecarFile.OpenResult.IdentityMismatch;
            }
            return FrameSidecarFile.OpenResult.Ok;
        }

        /// <summary>Opens a sidecar without an identity check (the caller compares <see cref="Identity"/> itself).</summary>
        public static FrameSidecarFile.OpenResult Open(string path, out FrameSidecarReader reader)
        {
            reader = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return FrameSidecarFile.OpenResult.Missing;

            FileStream stream = null;
            try
            {
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long fileLength = stream.Length;
                if (fileLength < FrameSidecarFile.HeaderSize)
                    return FrameSidecarFile.OpenResult.Corrupt;

                var header = new byte[FrameSidecarFile.HeaderSize];
                stream.ReadExactly(header, 0, header.Length);
                var r = new FrameReader(header);
                if (r.ReadU32() != FrameSidecarFile.HeaderMagic)
                    return FrameSidecarFile.OpenResult.Corrupt;
                int formatVersion = r.ReadI32();
                int masterSeed = r.ReadI32();
                long recordedAt = r.ReadI64();
                int simulationVersion = r.ReadI32();
                int chunkTicks = r.ReadI32();
                if (chunkTicks <= 0 || chunkTicks > ushort.MaxValue)
                    return FrameSidecarFile.OpenResult.Corrupt;
                if (formatVersion != GameConfig.ReplayFrameFormatVersion)
                    return FrameSidecarFile.OpenResult.FormatMismatch;

                var entries = new List<FrameSidecarFile.IndexEntry>(1024);
                bool hasFooter = TryReadFooter(stream, fileLength, chunkTicks, formatVersion, entries, out long totalTicks, out long dataEnd);
                if (!hasFooter)
                {
                    entries.Clear();
                    dataEnd = Scan(stream, fileLength, chunkTicks, formatVersion, entries);
                    totalTicks = entries.Count == 0 ? 0 : entries[entries.Count - 1].LastTick + 1;
                }
                var identity = new FrameSidecarIdentity(masterSeed, recordedAt, simulationVersion, formatVersion, totalTicks);
                reader = new FrameSidecarReader(path, stream, identity, chunkTicks, hasFooter, entries, dataEnd);
                stream = null;
                return FrameSidecarFile.OpenResult.Ok;
            }
            catch (IOException)
            {
                return FrameSidecarFile.OpenResult.Corrupt;
            }
            catch (InvalidDataException)
            {
                return FrameSidecarFile.OpenResult.Corrupt;
            }
            finally
            {
                stream?.Dispose();
            }
        }

        private static bool TryReadFooter(FileStream stream, long fileLength, int chunkTicks, int formatVersion,
            List<FrameSidecarFile.IndexEntry> entries, out long totalTicks, out long dataEnd)
        {
            totalTicks = 0;
            dataEnd = FrameSidecarFile.HeaderSize;
            if (fileLength < FrameSidecarFile.HeaderSize + FrameSidecarFile.FooterFixedSize + FrameSidecarFile.TrailerSize)
                return false;

            var trailer = new byte[FrameSidecarFile.TrailerSize];
            stream.Seek(fileLength - FrameSidecarFile.TrailerSize, SeekOrigin.Begin);
            stream.ReadExactly(trailer, 0, trailer.Length);
            var tr = new FrameReader(trailer);
            long footerOffset = tr.ReadI64();
            if (tr.ReadU32() != FrameSidecarFile.EndMagic)
                return false;
            if (footerOffset < FrameSidecarFile.HeaderSize || footerOffset > fileLength - FrameSidecarFile.TrailerSize - FrameSidecarFile.FooterFixedSize)
                return false;

            long footerLength = fileLength - FrameSidecarFile.TrailerSize - footerOffset;
            if (footerLength > int.MaxValue)
                return false;
            var footer = new byte[(int)footerLength];
            stream.Seek(footerOffset, SeekOrigin.Begin);
            stream.ReadExactly(footer, 0, footer.Length);
            var fr = new FrameReader(footer);
            if (fr.ReadU32() != FrameSidecarFile.FooterMagic)
                return false;
            totalTicks = fr.ReadI64();
            int count = fr.ReadI32();
            if (count < 0 || FrameSidecarFile.FooterFixedSize + (long)count * FrameSidecarFile.IndexEntrySize != footerLength)
                return false;

            long previousEnd = FrameSidecarFile.HeaderSize;
            for (int i = 0; i < count; i++)
            {
                long offset = fr.ReadI64();
                long firstTick = fr.ReadI64();
                int tickCount = fr.ReadU16();
                if (offset != previousEnd || firstTick != (long)i * chunkTicks || tickCount == 0 || tickCount > chunkTicks)
                    return false;
                long nextOffset = i + 1 < count ? PeekOffset(footer, FrameSidecarFile.FooterFixedSize + (i + 1) * FrameSidecarFile.IndexEntrySize) : footerOffset;
                long length = nextOffset - offset - FrameSidecarFile.ChunkLengthPrefixSize;
                if (length < FrameChunk.PrefixSize || length > int.MaxValue)
                    return false;
                entries.Add(new FrameSidecarFile.IndexEntry { Offset = offset, FirstTick = firstTick, TickCount = tickCount, Length = (int)length });
                previousEnd = nextOffset;
            }
            if (previousEnd != footerOffset)
                return false;
            dataEnd = footerOffset;
            return true;
        }

        private static long PeekOffset(byte[] footer, int at)
        {
            var r = new FrameReader(footer, at, 8);
            return r.ReadI64();
        }

        /// <summary>Walks the chunk records from the header; stops at the first partial or malformed one. Returns the data end.</summary>
        private static long Scan(FileStream stream, long fileLength, int chunkTicks, int formatVersion, List<FrameSidecarFile.IndexEntry> entries)
        {
            var head = new byte[FrameSidecarFile.ChunkLengthPrefixSize + FrameChunk.PrefixSize];
            long pos = FrameSidecarFile.HeaderSize;
            while (pos + head.Length <= fileLength)
            {
                stream.Seek(pos, SeekOrigin.Begin);
                stream.ReadExactly(head, 0, head.Length);
                var r = new FrameReader(head, 0, FrameSidecarFile.ChunkLengthPrefixSize);
                uint len = r.ReadU32();
                if (len < FrameChunk.PrefixSize || pos + FrameSidecarFile.ChunkLengthPrefixSize + len > fileLength)
                    break;
                if (!FrameChunk.TryReadPrefix(head, FrameSidecarFile.ChunkLengthPrefixSize, FrameChunk.PrefixSize, out var p))
                    break;
                if (p.TotalLength != len || p.FormatVersion != formatVersion || p.TickCount > chunkTicks || p.FirstTick != (long)entries.Count * chunkTicks)
                    break;
                entries.Add(new FrameSidecarFile.IndexEntry { Offset = pos, FirstTick = p.FirstTick, TickCount = p.TickCount, Length = (int)len });
                pos += FrameSidecarFile.ChunkLengthPrefixSize + len;
            }
            return pos;
        }

        /// <summary>The index entry of a chunk.</summary>
        public FrameSidecarFile.IndexEntry GetEntry(int chunkIndex) => _entries[chunkIndex];

        /// <summary>Reads one chunk from disk (any thread).</summary>
        public bool TryLoadChunk(int chunkIndex, out FrameChunk chunk)
        {
            chunk = null;
            if (chunkIndex < 0 || chunkIndex >= _entries.Count)
                return false;
            var e = _entries[chunkIndex];
            var bytes = new byte[e.Length];
            lock (_gate)
            {
                if (_stream == null)
                    return false;
                _stream.Seek(e.Offset + FrameSidecarFile.ChunkLengthPrefixSize, SeekOrigin.Begin);
                _stream.ReadExactly(bytes, 0, bytes.Length);
            }
            return FrameChunk.TryParse(bytes, 0, bytes.Length, out chunk) && chunk.FirstTick == e.FirstTick;
        }

        /// <summary>
        /// Rebuilds the sprite/string/nine-patch tables by applying every chunk's table delta in order,
        /// reading only each chunk's uncompressed head.
        /// </summary>
        public void RebuildRegistry(SpriteKeyRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var head = new byte[FrameChunk.PrefixSize];
            byte[] table = new byte[4096];
            lock (_gate)
            {
                if (_stream == null)
                    throw new ObjectDisposedException(nameof(FrameSidecarReader));
                for (int i = 0; i < _entries.Count; i++)
                {
                    var e = _entries[i];
                    _stream.Seek(e.Offset + FrameSidecarFile.ChunkLengthPrefixSize, SeekOrigin.Begin);
                    _stream.ReadExactly(head, 0, head.Length);
                    if (!FrameChunk.TryReadPrefix(head, 0, head.Length, out var p))
                        throw new InvalidDataException("Bad chunk prefix in sidecar");
                    if (table.Length < p.TableDeltaLength)
                        table = new byte[Math.Max(p.TableDeltaLength, table.Length * 2)];
                    _stream.ReadExactly(table, 0, p.TableDeltaLength);
                    var r = new FrameReader(table, 0, p.TableDeltaLength);
                    registry.ReadTableDelta(ref r);
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _stream?.Dispose();
                _stream = null;
            }
        }
    }
}
