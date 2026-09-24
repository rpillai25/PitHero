namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// Somewhere a <see cref="FrameStore"/> can (re)load compressed chunks from when they are not in
    /// memory: a sidecar file, or another store whose chunks are being handed over. Chunk index i covers
    /// ticks [i * chunkTicks, (i + 1) * chunkTicks).
    /// </summary>
    public interface IFrameChunkSource
    {
        /// <summary>Number of chunk indices the source can serve (0..ChunkCount-1); gaps are allowed.</summary>
        int ChunkCount { get; }
        /// <summary>Last tick the source has a frame for, or -1 when empty.</summary>
        long EndTick { get; }
        /// <summary>Loads one chunk; false when the source has no chunk at that index.</summary>
        bool TryLoadChunk(int chunkIndex, out FrameChunk chunk);
    }
}
