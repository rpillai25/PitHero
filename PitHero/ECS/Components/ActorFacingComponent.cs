using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>Tracks an actor's current facing direction independently of movement.</summary>
    public class ActorFacingComponent : Component, IUpdatable
    {
        private Direction _facing = Direction.Down;
        private bool _dirty;

        /// <summary>Current facing direction (default Down).</summary>
        public Direction Facing => _facing;

        /// <summary>Set facing direction explicitly (actions or systems call this).</summary>
        public void SetFacing(Direction direction)
        {
            if (_facing == direction)
                return;
            _facing = direction;
            _dirty = true;
            Debug.Log($"[ActorFacingComponent] Facing updated to {_facing}");
        }

        /// <summary>Consume dirty flag (returns true if a change occurred since last consume).</summary>
        public bool ConsumeDirtyFlag()
        {
            if (_dirty)
            {
                _dirty = false;
                return true;
            }
            return false;
        }

        /// <summary>Update hook (present for future smoothing; currently no per-frame logic).</summary>
        public void Update() { }
    }
}
