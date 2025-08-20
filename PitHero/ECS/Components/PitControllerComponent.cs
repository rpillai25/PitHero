using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Handles per-frame pit effects (crystal power growth, damage auras, etc.).
    /// </summary>
    public class PitControllerComponent : Component, IUpdatable
    {
        private PitComponent _pit;

        public override void OnAddedToEntity()
        {
            _pit = Entity.GetComponent<PitComponent>();
        }

        public void Update()
        {
            if (_pit == null || !_pit.IsActive)
                return;

            // Example: future crystal power drift or hero proximity logic.
            // Keep minimal now to avoid feature creep.
        }
    }
}