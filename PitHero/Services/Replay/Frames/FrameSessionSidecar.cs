using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Nez;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// The running session's <c>.frames</c> file plus the worker that fills it (the AutoSaveService
    /// shape): the main thread hands over a finished raw chunk and never waits; a worker task deflates
    /// it, appends it to the file and parks the compressed chunk for the main thread, which collects it
    /// in <see cref="Poll"/> and puts it in the store. Chunks are processed strictly in order. A write
    /// failure (disk full) is reported once and stops spilling; capture continues in memory only.
    /// The file itself doubles as the store's reload source for evicted chunks.
    /// </summary>
    public sealed class FrameSessionSidecar : IFrameChunkSource, IDisposable
    {
        private struct Result
        {
            public FrameChunk Chunk;
            public bool Spilled;
        }

        private readonly ConcurrentQueue<Result> _done = new ConcurrentQueue<Result>();
        private FrameSidecarWriter _writer;
        private Task _tail = Task.CompletedTask;
        private volatile bool _failed;
        private Exception _error;
        private bool _errorLogged;
        private int _spilledCount;
        private int _pending;

        private FrameSessionSidecar(FrameSidecarWriter writer, bool reopened)
        {
            _writer = writer;
            _spilledCount = writer.ChunkCount;
            WasReopened = reopened;
        }

        public string Path => _writer?.Path;
        /// <summary>True when an existing session file was continued rather than created.</summary>
        public bool WasReopened { get; }
        /// <summary>Chunks on disk, in index order, as observed by the main thread.</summary>
        public int SpilledChunkCount => _spilledCount;
        /// <summary>Raw chunks handed to the worker and not yet collected.</summary>
        public int PendingCount => _pending;
        public bool IsFailed => _failed;
        public long BytesOnDisk => _writer?.Length ?? 0;
        public int ChunkCount => _writer?.ChunkCount ?? 0;
        public long EndTick => _writer?.EndTick ?? -1;

        /// <summary>
        /// Continues the session file at <paramref name="path"/> if it exists and belongs to this
        /// identity (rebuilding <paramref name="registry"/> from it), otherwise creates it. Returns null
        /// when the file cannot be opened or created (capture then stays in memory).
        /// </summary>
        public static FrameSessionSidecar OpenOrCreate(string path, in FrameSidecarIdentity identity, int chunkTicks, SpriteKeyRegistry registry)
        {
            try
            {
                if (File.Exists(path))
                {
                    var result = FrameSidecarWriter.Reopen(path, registry, out var reopened);
                    if (result == FrameSidecarFile.OpenResult.Ok
                        && reopened.Identity.MatchesRecording(identity.MasterSeed, identity.RecordedAtUtcTicks, identity.SimulationVersion)
                        && reopened.ChunkTicks == chunkTicks)
                        return new FrameSessionSidecar(reopened, reopened: true);
                    reopened?.Dispose();
                    registry?.Clear();
                    Debug.Warn("[FrameSidecar] Session file " + System.IO.Path.GetFileName(path) + " unusable (" + result + "); starting over");
                }
                return new FrameSessionSidecar(FrameSidecarWriter.Create(path, identity, chunkTicks), reopened: false);
            }
            catch (Exception ex)
            {
                Debug.Warn("[FrameSidecar] Cannot open session file " + path + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Hands a finished raw chunk to the worker (main thread; returns at once).</summary>
        public void Enqueue(RawFrameChunk raw)
        {
            if (raw == null) throw new ArgumentNullException(nameof(raw));
            _pending++;
            _tail = _tail.ContinueWith(_ => Process(raw), System.Threading.CancellationToken.None,
                TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        private void Process(RawFrameChunk raw)
        {
            FrameChunk chunk = null;
            bool spilled = false;
            try
            {
                chunk = FrameChunkCodec.Compress(raw);
                if (!_failed)
                {
                    _writer.Append(chunk);
                    spilled = true;
                }
            }
            catch (Exception ex)
            {
                // Recorded for the main thread; never log from the worker
                _error = ex;
                _failed = true;
            }
            _done.Enqueue(new Result { Chunk = chunk, Spilled = spilled });
        }

        /// <summary>
        /// Collects finished chunks on the main thread: each goes into <paramref name="store"/> and, when
        /// it reached the disk, advances the spilled count. Reports the first write failure once.
        /// </summary>
        public void Poll(FrameStore store)
        {
            while (_done.TryDequeue(out var r))
            {
                _pending--;
                if (r.Chunk != null && store != null)
                    store.Add(r.Chunk);
                if (r.Spilled)
                    _spilledCount++;
            }
            if (_failed && !_errorLogged)
            {
                _errorLogged = true;
                Debug.Warn("[FrameSidecar] Spilling stopped: " + (_error != null ? _error.Message : "write failed") + "; frames stay in memory only");
            }
        }

        /// <summary>Waits for every handed-over chunk to land, then collects them (main thread; Time Travel, scene teardown).</summary>
        public void Drain(FrameStore store)
        {
            try
            {
                _tail.Wait();
            }
            catch (AggregateException)
            {
                // Surfaced through Poll
            }
            Poll(store);
        }

        /// <summary>After the store was truncated: drops chunks from <paramref name="keepCount"/> on. Call <see cref="Drain"/> first.</summary>
        public void TruncateChunks(int keepCount)
        {
            if (_writer == null || _failed)
                return;
            try
            {
                _writer.TruncateChunks(keepCount);
                if (_spilledCount > keepCount)
                    _spilledCount = keepCount;
            }
            catch (Exception ex)
            {
                _error = ex;
                _failed = true;
            }
        }

        /// <summary>Waits for the worker, writes the footer and closes the file.</summary>
        public void Finish(FrameStore store, long totalTicks)
        {
            Drain(store);
            if (_writer == null)
                return;
            try
            {
                if (!_failed)
                    _writer.Finish(totalTicks);
                else
                    _writer.Dispose();
            }
            catch (Exception ex)
            {
                Debug.Warn("[FrameSidecar] Could not finish session file: " + ex.Message);
                _writer.Dispose();
            }
            _writer = null;
        }

        /// <summary>Reloads a chunk from the file (the store's source for evicted chunks).</summary>
        public bool TryLoadChunk(int chunkIndex, out FrameChunk chunk)
        {
            chunk = null;
            var writer = _writer;
            if (writer == null || chunkIndex >= _spilledCount)
                return false;
            try
            {
                return writer.TryLoadChunk(chunkIndex, out chunk);
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>Closes without a footer (the file remains readable by scanning).</summary>
        public void Dispose()
        {
            try
            {
                _tail.Wait();
            }
            catch (AggregateException)
            {
            }
            _writer?.Dispose();
            _writer = null;
        }
    }
}
