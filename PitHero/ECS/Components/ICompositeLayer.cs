using Microsoft.Xna.Framework;
using Nez.Textures;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Contract for a single renderable layer in a SpriteCompositorBase / MultiSpriteAnimator stack.
    /// Implementors must also call base.IsVisibleFromCamera() conditional on OwnedByComposite so the
    /// DefaultRenderer skips them when they are composited.
    /// </summary>
    public interface ICompositeLayer
    {
        /// <summary>Current frame sprite to draw.</summary>
        Sprite Sprite { get; }

        /// <summary>Draw offset relative to the entity world-position.</summary>
        Vector2 LocalOffset { get; }

        /// <summary>When true the sprite is drawn flipped horizontally.</summary>
        bool FlipX { get; }

        /// <summary>Tint color applied when drawing this layer.</summary>
        Color LayerColor { get; }

        /// <summary>
        /// Set to true by MultiSpriteAnimator so the DefaultRenderer skips this layer.
        /// Implementors must honour this in IsVisibleFromCamera.
        /// </summary>
        bool OwnedByComposite { get; set; }
    }

    /// <summary>
    /// A composite layer drawn as several parts, each with its own sprite and tint (issue #462: the tone
    /// layers of a hero paperdoll part). <see cref="MultiSpriteAnimator"/> lists the parts in place of the
    /// layer, so the pose cache, afterimages and replay capture see every part. The part count is fixed for
    /// the layer's lifetime; a part with no sprite on a frame draws nothing.
    /// </summary>
    public interface ICompositeLayerGroup
    {
        /// <summary>Number of parts, back to front.</summary>
        int PartCount { get; }

        /// <summary>The part at <paramref name="index"/>; same instance on every call.</summary>
        ICompositeLayer GetPart(int index);
    }
}
