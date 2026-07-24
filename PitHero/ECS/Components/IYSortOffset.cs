namespace PitHero.ECS.Components
{
    /// <summary>
    /// Implemented by a Y-sorted RenderableComponent whose depth-sort point is not its entity
    /// position Y but an offset below it — e.g. a tall building whose ground / front-face line
    /// sits below its sprite centre. <see cref="YSortManager"/> adds <see cref="YSortOffset"/> to
    /// the entity's Y before computing the pixel-granular layer depth.
    /// </summary>
    public interface IYSortOffset
    {
        /// <summary>Pixels added to entity.Y before Y-sort depth is computed. Positive = sort lower.</summary>
        float YSortOffset { get; }
    }
}
