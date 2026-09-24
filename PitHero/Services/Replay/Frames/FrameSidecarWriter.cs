using System;
using System.Collections.Generic;
using System.IO;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// Append-only writer of a <c>.frames</c> sidecar. <see cref="Append"/> may be called from a worker
    /// thread: the chunk bytes are immutable, the call takes a lock and flushes, so a crash loses at most
    /// the chunk being written. Chunks must be contiguous (chunk i at tick i * chunkTicks); a chunk with
    /// the same first tick as the last one replaces it (a partial last chunk finished again after a save).
    /// <see cref="Finish"/> writes the footer and closes; <see cref="Dispose"/> closes without one.
    /// </summary>
    public sealed class FrameSidecarWriter : IFrameChunkSource, IDisposable
    {
        private readonly object _gate = new object();
        private readonly List<FrameSidecarFile.IndexEntry> _index = new List<FrameSidecarFile.IndexEntry>(4096);
        private readonly byte[] _small = new byte[64];
        private FileStream _stream;
        private bool _finished;

        public string Path { get; }
        public FrameSidecarIdentity Identity { get; }
        public int ChunkTicks { get; }
        public bool IsFinished
        {
            get { lock (_gate) return _finished; }
        }
        public bool IsOpen
        {
            get { lock (_gate) return _stream != null; }
        }
        public int ChunkCount
        {
            get { lock (_gate) return _index.Count; }
        }
        /// <summary>Last tick written, or -1.</summary>
        public long EndTick
        {
            get { lock (_gate) return _index.Count == 0 ? -1 : _index[_index.Count - 1].LastTick; }
        }
        /// <summary>Bytes on disk so far.</summary>
        public long Length
        {
            get { lock (_gate) return _stream != null ? _stream.Length : 0; }
        }

        private FrameSidecarWriter(string path, in FrameSidecarIdentity identity, int chunkTicks, FileStream stream)
        {
            Path = path;
            Identity = identity;
            ChunkTicks = chunkTicks;
            _stream = stream;
        }

        /// <summary>Creates (or overwrites) a sidecar and writes its identity header.</summary>
        public static FrameSidecarWriter Create(string path, in FrameSidecarIdentity identity, int chunkTicks)
        {
            if (chunkTicks <= 0) throw new ArgumentOutOfRangeException(nameof(chunkTicks));
            var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            var header = new byte[FrameSidecarFile.HeaderSize];
            var w = new FrameWriter(header);
            w.WriteU32(FrameSidecarFile.HeaderMagic);
            w.WriteI32(identity.FrameFormatVersion);
            w.WriteI32(identity.MasterSeed);
            w.WriteI64(identity.RecordedAtUtcTicks);
            w.WriteI32(identity.SimulationVersion);
            w.WriteI32(chunkTicks);
            stream.Write(header, 0, header.Length);
            stream.Flush();
            return new FrameSidecarWriter(path, identity, chunkTicks, stream);
        }

        /// <summary>
        /// Reopens an existing sidecar for appending: its index is recovered (footer or scan), the footer
        /// and any partial tail are cut off, and new chunks continue after the last complete one.
        /// </summary>
        public static FrameSidecarFile.OpenResult Reopen(string path, out FrameSidecarWriter writer)
            => Reopen(path, null, out writer);

        /// <summary>
        /// Reopens for appending and, when <paramref name="registry"/> is given, rebuilds its tables from
        /// the chunks kept, so ids already in the file resolve and new entries continue the numbering.
        /// </summary>
        public static FrameSidecarFile.OpenResult Reopen(string path, SpriteKeyRegistry registry, out FrameSidecarWriter writer)
        {
            writer = null;
            var result = FrameSidecarReader.Open(path, out var reader);
            if (result != FrameSidecarFile.OpenResult.Ok)
                return result;
            long dataEnd;
            var identity = reader.Identity;
            int chunkTicks = reader.ChunkTicks;
            var entries = new List<FrameSidecarFile.IndexEntry>(reader.ChunkCount);
            using (reader)
            {
                for (int i = 0; i < reader.ChunkCount; i++)
                    entries.Add(reader.GetEntry(i));
                dataEnd = reader.DataEnd;
                if (registry != null)
                {
                    try
                    {
                        reader.RebuildRegistry(registry);
                    }
                    catch (IOException)
                    {
                        return FrameSidecarFile.OpenResult.Corrupt;
                    }
                    catch (InvalidDataException)
                    {
                        return FrameSidecarFile.OpenResult.Corrupt;
                    }
                }
            }
            identity.TotalTicks = -1;
            var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            stream.SetLength(dataEnd);
            stream.Seek(dataEnd, SeekOrigin.Begin);
            writer = new FrameSidecarWriter(path, identity, chunkTicks, stream);
            writer._index.AddRange(entries);
            return FrameSidecarFile.OpenResult.Ok;
        }

        /// <summary>Appends a finished chunk (any thread). Contiguity is enforced; see the class summary.</summary>
        public void Append(FrameChunk chunk)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            lock (_gate)
            {
                if (_stream == null || _finished)
                    throw new InvalidOperationException("Sidecar writer is closed");
                long expected = _index.Count == 0 ? 0 : _index[_index.Count - 1].FirstTick + ChunkTicks;
                long offset;
                if (_index.Count > 0 && chunk.FirstTick == _index[_index.Count - 1].FirstTick)
                {
                    // Replace the last chunk
                    offset = _index[_index.Count - 1].Offset;
                    _index.RemoveAt(_index.Count - 1);
                    _stream.SetLength(offset);
                }
                else if (chunk.FirstTick != expected)
                {
                    throw new ArgumentException("Chunk does not follow the previous one", nameof(chunk));
                }
                else
                {
                    offset = _stream.Length;
                }
                if (chunk.TickCount > ChunkTicks)
                    throw new ArgumentException("Chunk longer than the sidecar's chunk size", nameof(chunk));

                _stream.Seek(offset, SeekOrigin.Begin);
                var w = new FrameWriter(_small);
                w.WriteU32((uint)chunk.Bytes.Length);
                _stream.Write(_small, 0, w.Length);
                _stream.Write(chunk.Bytes, 0, chunk.Bytes.Length);
                _stream.Flush();
                _index.Add(new FrameSidecarFile.IndexEntry
                {
                    Offset = offset,
                    FirstTick = chunk.FirstTick,
                    TickCount = chunk.TickCount,
                    Length = chunk.Bytes.Length,
                });
            }
        }

        /// <summary>Drops every chunk from index <paramref name="keepCount"/> on (Time Travel); later appends continue from there.</summary>
        public void TruncateChunks(int keepCount)
        {
            lock (_gate)
            {
                if (_stream == null || _finished)
                    throw new InvalidOperationException("Sidecar writer is closed");
                if (keepCount < 0) keepCount = 0;
                if (keepCount >= _index.Count)
                    return;
                long end = _index[keepCount].Offset;
                _index.RemoveRange(keepCount, _index.Count - keepCount);
                _stream.SetLength(end);
                _stream.Seek(end, SeekOrigin.Begin);
                _stream.Flush();
            }
        }

        /// <summary>Last tick of the chunks written so far (see <see cref="IFrameChunkSource"/>).</summary>
        long IFrameChunkSource.EndTick => EndTick;

        /// <summary>Reads back a chunk written to this file (any thread; used by the store to reload an evicted chunk).</summary>
        public bool TryLoadChunk(int chunkIndex, out FrameChunk chunk)
        {
            chunk = null;
            byte[] bytes;
            lock (_gate)
            {
                if (_stream == null || chunkIndex < 0 || chunkIndex >= _index.Count)
                    return false;
                var e = _index[chunkIndex];
                bytes = new byte[e.Length];
                long resume = _stream.Position;
                _stream.Seek(e.Offset + FrameSidecarFile.ChunkLengthPrefixSize, SeekOrigin.Begin);
                _stream.ReadExactly(bytes, 0, bytes.Length);
                _stream.Seek(resume, SeekOrigin.Begin);
            }
            return FrameChunk.TryParse(bytes, 0, bytes.Length, out chunk);
        }

        /// <summary>Writes the footer (chunk index + total ticks) and closes the file.</summary>
        public void Finish(long totalTicks)
        {
            lock (_gate)
            {
                if (_stream == null || _finished)
                    throw new InvalidOperationException("Sidecar writer is closed");
                long footerOffset = _stream.Length;
                _stream.Seek(footerOffset, SeekOrigin.Begin);
                var footer = new byte[FrameSidecarFile.FooterFixedSize + _index.Count * FrameSidecarFile.IndexEntrySize + FrameSidecarFile.TrailerSize];
                var w = new FrameWriter(footer);
                w.WriteU32(FrameSidecarFile.FooterMagic);
                w.WriteI64(totalTicks);
                w.WriteI32(_index.Count);
                for (int i = 0; i < _index.Count; i++)
                {
                    var e = _index[i];
                    w.WriteI64(e.Offset);
                    w.WriteI64(e.FirstTick);
                    w.WriteU16((ushort)e.TickCount);
                }
                w.WriteI64(footerOffset);
                w.WriteU32(FrameSidecarFile.EndMagic);
                _stream.Write(footer, 0, w.Length);
                _stream.Flush();
                _finished = true;
                _stream.Dispose();
                _stream = null;
            }
        }

        /// <summary>Closes without a footer (the reader will rebuild the index by scanning).</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                if (_stream == null)
                    return;
                _stream.Flush();
                _stream.Dispose();
                _stream = null;
            }
        }
    }
}
