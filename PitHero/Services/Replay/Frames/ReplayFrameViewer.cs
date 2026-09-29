using System;
using Nez;
using PitHero.ECS.Scenes;
using PitHero.Rendering;
using PitHero.UI;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// The replay frame viewer (design §3.2): shows any recorded tick of the current session over the
    /// live scene without simulating. Owns the playhead (<see cref="Cursor"/>), the decoded frame the
    /// renderers draw (<see cref="CurrentFrame"/>), the shadow tile layers, the sprite resolver, and
    /// the HUD / console feed. <see cref="AttachToScene"/> installs the scene's renderable filter (only
    /// the live UI canvas and the recorded-value-fed HUD keep drawing) and the two renderers;
    /// <see cref="DetachFromScene"/> removes them, which is all an exit costs. The live scene is never
    /// torn down and no component's Enabled flag is touched.
    ///
    /// Normally the store is read at the cursor every frame. <see cref="Freeze"/> pins a private copy of
    /// the current frame so it survives a scene rebuild and a stream truncation (Time Travel Here
    /// re-simulates behind it). The live scene never draws itself while a viewer is attached.
    ///
    /// The frames come either from the live session's recorder (<see cref="IsLiveStream"/>) or from a
    /// saved replay's <c>.frames</c> file through a file-backed store (issue #429), whose reader the
    /// viewer owns and closes in <see cref="Dispose"/>.
    /// </summary>
    public sealed class ReplayFrameViewer : IDisposable
    {
        private const int ConsoleAppendLimit = 50; // more new lines than the panel shows: rebuild instead of appending

        private readonly FrameRecorder _recorder; // null over a saved replay's file
        private readonly DecodedFrame _frozen = new DecodedFrame();
        // Mercenary portraits pinned at Freeze: the record has none and a rebuilding scene has no mercenaries to read
        private PitHero.UI.GraphicalHUD.PortraitSnapshot _frozenMerc1, _frozenMerc2;
        private readonly Func<IRenderable, bool> _filter;
        private Scene _scene;
        private RecordedFrameRenderer _world;
        private RecordedFrameScreenRenderer _screen;
        private int _shownConsoleCount = -1;
        private EventConsolePanel _console;

        public ReplayFrameCursor Cursor { get; }
        public FrameStore Store { get; }
        public SpriteKeyRegistry Registry { get; }
        public RecordedConsoleLog ConsoleLog { get; }
        public FrameSpriteResolver Sprites { get; }
        public ShadowTileLayers Tiles { get; } = new ShadowTileLayers();
        /// <summary>The re-simulated particle effects of the frame on screen (issue #431).</summary>
        public RecordedParticlePool Particles { get; } = new RecordedParticlePool();
        /// <summary>
        /// True while recorded sound events may play: set by the playback service for forward play at
        /// the low speed rungs only (silent while paused, scrubbing, rewinding and at high speeds).
        /// </summary>
        public bool SoundsEnabled { get; set; }
        private long _soundTick = -1; // last frame tick whose sound events were considered
        /// <summary>The scene's day-night material for graded sprites and terrain, or null.</summary>
        public Nez.Material GradedMaterial { get; private set; }
        /// <summary>The frame the renderers draw this frame (null when nothing is recorded at the cursor).</summary>
        public DecodedFrame CurrentFrame { get; private set; }
        /// <summary>True while the frozen copy is shown regardless of the cursor and the store.</summary>
        public bool IsFrozen { get; private set; }
        /// <summary>True while attached to a scene.</summary>
        public bool IsAttached => _scene != null;
        /// <summary>True when the frames are the live session's stream (Replay Current Session); false over a saved replay's cache.</summary>
        public bool IsLiveStream => _recorder != null;
        /// <summary>Something the viewer holds open for its store (a saved replay's sidecar reader), closed by <see cref="Dispose"/>.</summary>
        public IDisposable OwnedSource { get; set; }

        /// <summary>A viewer over the live session's stream.</summary>
        public ReplayFrameViewer(FrameRecorder recorder, long totalTicks)
            : this(recorder?.Store, recorder?.Registry, recorder?.ConsoleLog, recorder ?? throw new ArgumentNullException(nameof(recorder)), totalTicks)
        {
        }

        /// <summary>
        /// A viewer over any store: a saved replay's file-backed store with the tables and console lines
        /// read from its sidecar (<paramref name="liveRecorder"/> null), or the live recorder's stream.
        /// </summary>
        public ReplayFrameViewer(FrameStore store, SpriteKeyRegistry registry, RecordedConsoleLog consoleLog, FrameRecorder liveRecorder, long totalTicks)
        {
            Store = store ?? throw new ArgumentNullException(nameof(store));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            ConsoleLog = consoleLog ?? new RecordedConsoleLog();
            _recorder = liveRecorder;
            Sprites = new FrameSpriteResolver(Registry);
            Cursor = new ReplayFrameCursor(totalTicks);
            _filter = KeepsDrawingLive;
        }

        /// <summary>Detaches from its scene and closes what it holds open (a saved replay's reader).</summary>
        public void Dispose()
        {
            DetachFromScene();
            Particles.Clear();
            var owned = OwnedSource;
            OwnedSource = null;
            owned?.Dispose();
        }

        /// <summary>
        /// The renderables that keep drawing live while a recorded frame is shown: the UI canvas, the HUD
        /// (fed from the record) and every other screen-space <see cref="ILiveOnlyRenderable"/> (the
        /// hover markers). World-space live-only renderables are merged into the recorded frame by
        /// <see cref="RecordedFrameRenderer"/> instead, which skips screen-space layers.
        /// </summary>
        private static bool KeepsDrawingLive(IRenderable renderable)
        {
            if (renderable is UICanvas || renderable is GraphicalHUD)
                return true;
            return renderable is ILiveOnlyRenderable && renderable is RenderableComponent rc
                && FrameCaptureContext.IsScreenSpaceLayer(rc.RenderLayer);
        }

        /// <summary>Installs the filter and renderers on <paramref name="scene"/> (the live scene, or each scene of a rebuild).</summary>
        public void AttachToScene(Scene scene)
        {
            if (scene == null || ReferenceEquals(scene, _scene))
                return;
            DetachFromScene();
            _scene = scene;
            scene.RenderableFilter = _filter;
            _world = new RecordedFrameRenderer(this);
            _screen = new RecordedFrameScreenRenderer(this, _world);
            scene.AddRenderer(_world);
            scene.AddRenderer(_screen);
            Sprites.LearnAtlases();
            var map = Core.Services.GetService<PitHero.Util.TiledMapService>()?.CurrentMap;
            if (map != null && !Tiles.IsBound)
                Tiles.Bind(map);
            // The grading material belongs to a game scene; the transition scene of a rebuild draws ungraded
            GradedMaterial = scene is MainGameScene ? Core.Services.GetService<ColorGradingController>()?.Material : null;
            _console = null;
            _shownConsoleCount = -1;
        }

        /// <summary>Removes the filter and renderers from the attached scene, if any.</summary>
        public void DetachFromScene()
        {
            var scene = _scene;
            if (scene == null)
                return;
            _scene = null;
            if (ReferenceEquals(scene.RenderableFilter, _filter))
                scene.RenderableFilter = null;
            if (_world != null)
            {
                scene.RemoveRenderer(_world);
                _world = null;
            }
            if (_screen != null)
            {
                scene.RemoveRenderer(_screen);
                _screen = null;
            }
        }

        /// <summary>
        /// Pins a private copy of the frame at <paramref name="tick"/> (completed ticks) so it is drawn
        /// for the rest of the viewer's life, whatever happens to the store (a Time Travel rebuild
        /// truncates it). A frozen viewer is only ever disposed, never thawed.
        /// </summary>
        public void Freeze(long tick)
        {
            Cursor.Seek(tick);
            Refresh();
            _frozen.CopyFrom(CurrentFrame);
            CurrentFrame = _frozen.Tick >= 0 ? _frozen : null;
            if (_scene is MainGameScene game)
                game.CaptureMercenaryPortraits(out _frozenMerc1, out _frozenMerc2);
            IsFrozen = true;
            SoundsEnabled = false;
            if (_scene != null)
                _scene.RenderableFilter = _filter;
        }

        /// <summary>Decodes the frame at the cursor (clamped to what the store holds) and brings the tile copies to it.</summary>
        public void Refresh()
        {
            if (IsFrozen)
                return;
            long tick = Cursor.FrameTick;
            if (tick > Store.EndTick)
                tick = Store.EndTick;
            if (tick < 0)
            {
                CurrentFrame = null;
                return;
            }
            CurrentFrame = Store.TryGetFrame(tick, out var frame) ? frame : null;
            Tiles.SyncTo(Store, tick);
        }

        /// <summary>
        /// Once per rendered frame from the scene's presentation pass: refreshes the frame and feeds the
        /// HUD, labels, portrait and console from the record, and plays the sound events the playhead
        /// passed since the last frame when sounds are enabled.
        /// </summary>
        public void FeedPresentation(MainGameScene scene)
        {
            Refresh();
            PlaySoundsPassed();
            if (scene == null)
                return;
            if (CurrentFrame != null)
            {
                scene.ApplyRecordedHud(in CurrentFrame.Hud);
                scene.ApplyRecordedPortrait(CurrentFrame, Sprites);
            }
            if (IsFrozen)
                scene.ApplyMercenaryPortraits(in _frozenMerc1, in _frozenMerc2);
            SyncConsole(scene.EventConsole, Cursor.FrameTick);
        }

        /// <summary>
        /// Plays the recorded sound events in (last frame tick, current frame tick] through the live
        /// camera's falloff and pan (issue #431). Only a short forward move counts as play: a seek, a
        /// skipped pause span, a rewind or a disabled state just moves the mark, silently.
        /// </summary>
        private void PlaySoundsPassed()
        {
            long tick = Cursor.FrameTick;
            long from = _soundTick;
            _soundTick = tick;
            if (!SoundsEnabled || IsFrozen || from < 0 || tick <= from || tick - from > GameConfig.ReplayFrameViewSoundCatchupMaxTicks)
                return;
            if (Core.Instance == null || PitHero.Util.SoundEffectManager.Muted)
                return;
            var sfx = Core.GetGlobalManager<PitHero.Util.SoundEffectManager>();
            if (sfx == null)
                return;
            int firstChunk = Store.ChunkIndexOf(from + 1);
            int lastChunk = Store.ChunkIndexOf(tick);
            for (int c = firstChunk; c <= lastChunk; c++)
            {
                if (!Store.TryGetDecodedChunk(c, out var chunk))
                    continue;
                var events = chunk.SoundEvents;
                for (int i = 0; i < events.Count; i++)
                {
                    var e = events[i];
                    if (e.Tick <= from)
                        continue;
                    if (e.Tick > tick)
                        break;
                    sfx.PlayRecorded((PitHero.Util.SoundEffectTypes.SoundEffectType)e.Type, e.Variant, new Microsoft.Xna.Framework.Vector2(e.X, e.Y), e.IsPositional);
                }
            }
        }

        private void SyncConsole(EventConsolePanel panel, long tick)
        {
            if (panel == null || ConsoleLog == null)
                return;
            if (!ReferenceEquals(panel, _console))
            {
                _console = panel;
                _shownConsoleCount = -1;
            }
            int count = ConsoleLog.CountAtOrBefore(tick);
            if (count == _shownConsoleCount)
                return;
            if (_shownConsoleCount >= 0 && count > _shownConsoleCount && count - _shownConsoleCount <= ConsoleAppendLimit)
            {
                for (int i = _shownConsoleCount; i < count; i++)
                    panel.AppendRecorded(ConsoleLog[i].Segments);
                _shownConsoleCount = count;
            }
            else
            {
                _shownConsoleCount = panel.ShowRecorded(ConsoleLog, tick);
            }
        }
    }
}
