using Nez;

namespace PitHero.Rendering
{
    /// <summary>
    /// Running render counters for measuring compositor cost (issue #459). Counters only grow; <see cref="Sample"/>
    /// logs per-frame averages over <see cref="GameConfig.RenderStatsWindowFrames"/> frames when
    /// <see cref="GameConfig.LogRenderStats"/> is on.
    /// </summary>
    public static class RenderStats
    {
        /// <summary>Render-target switches made by character compositing (bakes and the per-frame fallback path).</summary>
        public static int CompositorTargetSwitches;

        /// <summary>Poses baked into the shared character frame atlas.</summary>
        public static int Bakes;

        /// <summary>Characters drawn from a baked cell.</summary>
        public static int BakedDraws;

        /// <summary>Characters (MultiSpriteAnimator) drawn through their own render target (fallback path).</summary>
        public static int FallbackDraws;

        /// <summary>Any compositor drawn through its own per-frame render target: fallback characters plus StaticSpriteCompositor (treasure chests).</summary>
        public static int RtComposites;

        private static int _frames;
        private static int _drawCallsStart;
        private static int _switchesStart;
        private static int _bakesStart;
        private static int _bakedStart;
        private static int _fallbackStart;
        private static int _rtStart;

        /// <summary>Called once per rendered frame; logs a summary line at the end of each window.</summary>
        public static void Sample()
        {
            if (!GameConfig.LogRenderStats)
                return;
            if (_frames == 0)
            {
                _drawCallsStart = Batcher.DrawCallCount;
                _switchesStart = CompositorTargetSwitches;
                _bakesStart = Bakes;
                _bakedStart = BakedDraws;
                _fallbackStart = FallbackDraws;
                _rtStart = RtComposites;
            }
            _frames++;
            if (_frames <= GameConfig.RenderStatsWindowFrames)
                return;

            float n = _frames - 1;
            Debug.Log($"[RenderStats] per frame: drawCalls={(Batcher.DrawCallCount - _drawCallsStart) / n:F1} " +
                      $"compositorRtSwitches={(CompositorTargetSwitches - _switchesStart) / n:F2} " +
                      $"bakedChars={(BakedDraws - _bakedStart) / n:F1} fallbackChars={(FallbackDraws - _fallbackStart) / n:F1} " +
                      $"staticComposites={(RtComposites - _rtStart - (FallbackDraws - _fallbackStart)) / n:F1} " +
                      $"baking={(GameConfig.BakeCharacterFrames ? "on" : "off")} " +
                      $"bakes={Bakes - _bakesStart} pages={CharacterFrameAtlas.PageCount} cells={CharacterFrameAtlas.CellsInUse}");
            _frames = 0;
        }
    }
}
