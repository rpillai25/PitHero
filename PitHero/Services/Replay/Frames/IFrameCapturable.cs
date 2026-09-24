namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// A custom RenderableComponent that records itself into the replay frame stream (design doc
    /// features/feature_replay_frame_recording_424.md §3.1). Called once per simulation tick while
    /// the component is enabled; emit zero or more draw ops for what <c>Render</c> would draw right
    /// now. Must be read-only over the scene: no allocation, no mutation, no Nez.Random, no Input.
    /// Every RenderableComponent in PitHero is either stock (SpriteRenderer, SpriteAnimator and their
    /// subclasses, the composites), <see cref="IFrameCapturable"/>, or <see cref="ILiveOnlyRenderable"/>
    /// (FrameCaptureCoverageTests fails when a new one is none of the three).
    /// </summary>
    public interface IFrameCapturable
    {
        /// <summary>Emits this component's draw ops for the current tick.</summary>
        void CaptureFrame(ref FrameWriter w, FrameCaptureContext ctx);
    }

    /// <summary>
    /// Marks a RenderableComponent that is never recorded and keeps drawing live while a recorded
    /// frame is shown (clouds scroll on wall time, the tree band is constant, the HUD is fed from the
    /// recorded HUD record, the action queue is hidden in v1).
    /// </summary>
    public interface ILiveOnlyRenderable
    {
    }
}
