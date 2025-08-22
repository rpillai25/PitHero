namespace PitHero.ECS.Components
{
    /// <summary>
    /// Interface for components that can be paused
    /// </summary>
    public interface IPausableComponent
    {
        /// <summary>
        /// Gets whether this component should respect the global pause state
        /// </summary>
        bool ShouldPause { get; }
    }
}