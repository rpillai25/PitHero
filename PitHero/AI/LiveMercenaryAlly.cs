using Nez;
using PitHero.Combat;
using PitHero.ECS.Components;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Mercenaries;

namespace PitHero.AI
{
    /// <summary>
    /// Live Nez implementation of <see cref="IBattleAlly"/> for a mercenary entity.
    /// Wraps a Nez <see cref="Entity"/> and its <see cref="MercenaryComponent"/>,
    /// exposing the <see cref="Mercenary"/> as the combatant and computing presence
    /// from the component's alive-in-pit state.
    /// </summary>
    public sealed class LiveMercenaryAlly : IBattleAlly
    {
        private readonly Entity _entity;
        private readonly MercenaryComponent _component;

        /// <inheritdoc/>
        public ICombatant Combatant => _component.LinkedMercenary;

        /// <inheritdoc/>
        public bool IsHero => false;

        /// <inheritdoc/>
        /// <remarks>
        /// Deliberately does NOT check HP — the engine applies HP checks exactly where
        /// the original battle loop had them (mercenary turn skips include HP, the
        /// hero's does not).
        /// </remarks>
        public bool IsPresent =>
            _entity != null &&
            !_entity.IsDestroyed &&
            _component.LinkedMercenary != null &&
            _component.InsidePit;

        /// <inheritdoc/>
        public ActionQueue PlayerActionQueue => _component.BattleActionQueue;

        /// <summary>The underlying Nez entity (accessible to the live adapter for display).</summary>
        public Entity Entity => _entity;

        /// <summary>The underlying component (accessible to the live adapter for service calls).</summary>
        public MercenaryComponent Component => _component;

        /// <summary>Creates a mercenary ally wrapping the given entity and component.</summary>
        public LiveMercenaryAlly(Entity entity, MercenaryComponent component)
        {
            _entity = entity;
            _component = component;
        }
    }
}
