using Nez;
using PitHero.Combat;
using PitHero.ECS.Components;
using RolePlayingFramework.Combat;

namespace PitHero.AI
{
    /// <summary>
    /// Live Nez implementation of <see cref="IBattleAlly"/> for the hero entity.
    /// Wraps a <see cref="HeroComponent"/> and exposes presence and combatant state
    /// to the <see cref="BattleEngine"/> without requiring a direct Nez entity reference.
    /// </summary>
    public sealed class LiveHeroAlly : IBattleAlly
    {
        private readonly HeroComponent _component;

        /// <inheritdoc/>
        public ICombatant Combatant => _component.LinkedHero;

        /// <inheritdoc/>
        public bool IsHero => true;

        /// <inheritdoc/>
        /// <remarks>
        /// Deliberately does NOT check HP: the original battle loop skipped the hero's
        /// turn only when outside the pit, so a hero that dies mid-round still takes
        /// their queued turn that round.  The engine adds HP checks only where the
        /// original code had them.
        /// </remarks>
        public bool IsPresent =>
            _component.Entity != null &&
            !_component.Entity.IsDestroyed &&
            _component.InsidePit;

        /// <inheritdoc/>
        /// <remarks>The engine drives the hero's queue via the <c>Run</c> parameter, not this property.</remarks>
        public ActionQueue PlayerActionQueue => _component.BattleActionQueue;

        /// <summary>The underlying Nez entity (accessible to the live adapter for display).</summary>
        public Entity Entity => _component.Entity;

        /// <summary>Creates a hero ally wrapping the given component.</summary>
        public LiveHeroAlly(HeroComponent component)
        {
            _component = component;
        }
    }
}
