using Nez;

namespace PitHero.Rendering
{
    /// <summary>
    /// Draws the party while it floats above the twisting pit during the wizard-orb warp (issue #469).
    /// The party's composites move to <see cref="GameConfig.RenderLayerPitWarpParty"/> for the warp;
    /// the scene's world renderer excludes that layer and this renderer draws it after the
    /// post-processors (so <see cref="PitWarpPostProcessor"/> never twists the party), with the world
    /// camera and before the screen-space UI. Honours the scene's renderable filter, so a replay's
    /// frame viewer hides the live party here exactly as it does in the world pass.
    /// </summary>
    public sealed class PitWarpPartyRenderer : RenderLayerRenderer
    {
        /// <summary>After the post-processors, before the replay screen pass (99) and the screen-space UI (100).</summary>
        public const int Order = 50;

        public PitWarpPartyRenderer() : base(Order, GameConfig.RenderLayerPitWarpParty)
        {
            WantsToRenderAfterPostProcessors = true;
            ShouldDebugRender = false;
        }
    }
}
