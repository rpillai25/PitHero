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
    /// Modes: normally the store is read at the cursor every frame. <see cref="Passthrough"/> hands the
    /// picture back to the live scene while the simulation itself runs at the cursor (the simulated
    /// future). <see cref="Freeze"/> pins a private copy of the current frame so it survives a scene
    /// rebuild and a stream truncation (Time Travel Here re-simulates behind it).
    /// </summary>
    public sealed class ReplayFrameViewer
    {
        private const int ConsoleAppendLimit = 50; // more new lines than the panel shows: rebuild instead of appending

        private readonly FrameRecorder _recorder;
        private readonly DecodedFrame _frozen = new DecodedFrame();
        private readonly Func<IRenderable, bool> _filter;
        private Scene _scene;
        private RecordedFrameRenderer _world;
        private RecordedFrameScreenRenderer _screen;
        private bool _passthrough;
        private int _shownConsoleCount = -1;
        private EventConsolePanel _console;

        public ReplayFrameCursor Cursor { get; }
        public FrameStore Store { get; }
        public SpriteKeyRegistry Registry { get; }
        public RecordedConsoleLog ConsoleLog { get; }
        public FrameSpriteResolver Sprites { get; }
        public ShadowTileLayers Tiles { get; } = new ShadowTileLayers();
        /// <summary>The scene's day-night material for graded sprites and terrain, or null.</summary>
        public Nez.Material GradedMaterial { get; private set; }
        /// <summary>The frame the renderers draw this frame (null when nothing is recorded at the cursor).</summary>
        public DecodedFrame CurrentFrame { get; private set; }
        /// <summary>True while the frozen copy is shown regardless of the cursor and the store.</summary>
        public bool IsFrozen { get; private set; }
        /// <summary>True while attached to a scene.</summary>
        public bool IsAttached => _scene != null;

        /// <summary>
        /// When true the live scene draws itself (filter off, renderers idle) and the HUD reads live
        /// values: the simulation is running at the cursor. The console keeps following the log.
        /// </summary>
        public bool Passthrough
        {
            get => _passthrough;
            set
            {
                if (_passthrough == value)
                    return;
                _passthrough = value;
                if (_scene != null)
                    _scene.RenderableFilter = value ? null : _filter;
                if (!value)
                {
                    // Ticks still in the recorder's builder become visible frames now
                    _recorder.FlushPending();
                    Tiles.Invalidate();
                }
            }
        }

        /// <summary>True when the renderers should draw the recorded frame this frame.</summary>
        public bool DrawsRecordedFrame => !_passthrough || IsFrozen;

        public ReplayFrameViewer(FrameRecorder recorder, long totalTicks)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            Store = recorder.Store;
            Registry = recorder.Registry;
            ConsoleLog = recorder.ConsoleLog;
            Sprites = new FrameSpriteResolver(Registry);
            Cursor = new ReplayFrameCursor(totalTicks);
            _filter = KeepsDrawingLive;
        }

        /// <summary>The renderables that keep drawing live while a recorded frame is shown: the UI canvas and the HUD (fed from the record).</summary>
        private static bool KeepsDrawingLive(IRenderable renderable)
        {
            return renderable is UICanvas || renderable is GraphicalHUD;
        }

        /// <summary>Installs the filter and renderers on <paramref name="scene"/> (the live scene, or each scene of a rebuild).</summary>
        public void AttachToScene(Scene scene)
        {
            if (scene == null || ReferenceEquals(scene, _scene))
                return;
            DetachFromScene();
            _scene = scene;
            if (!_passthrough || IsFrozen)
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

        /// <summary>Pins a private copy of the frame at <paramref name="tick"/> (completed ticks) so it is drawn until <see cref="Unfreeze"/>, whatever happens to the store.</summary>
        public void Freeze(long tick)
        {
            Cursor.Max = Math.Max(Cursor.Max, tick);
            Cursor.Seek(tick);
            _passthrough = false;
            Refresh();
            _frozen.CopyFrom(CurrentFrame);
            CurrentFrame = _frozen.Tick >= 0 ? _frozen : null;
            IsFrozen = true;
            if (_scene != null)
                _scene.RenderableFilter = _filter;
        }

        public void Unfreeze()
        {
            IsFrozen = false;
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
        /// HUD, labels and console from the record (HUD and labels stay live in passthrough).
        /// </summary>
        public void FeedPresentation(MainGameScene scene)
        {
            Refresh();
            if (scene == null)
                return;
            if (!_passthrough && CurrentFrame != null)
                scene.ApplyRecordedHud(in CurrentFrame.Hud);
            SyncConsole(scene.EventConsole, Cursor.FrameTick);
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
