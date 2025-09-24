using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that tracks which direction an enemy is facing for animation purposes
    /// </summary>
    public class EnemyFacingComponent : Component
    {
        private Direction _facing = Direction.Down; // Default facing down
        private bool _isDirty = true; // Flag to signal facing changed

        /// <summary>
        /// Current facing direction
        /// </summary>
        public Direction Facing
        {
            get => _facing;
            set
            {
                if (_facing != value)
                {
                    _facing = value;
                    _isDirty = true;
                }
            }
        }

        /// <summary>
        /// Consumes the dirty flag, returning true if the facing direction has changed since last check
        /// </summary>
        public bool ConsumeDirtyFlag()
        {
            bool wasDirty = _isDirty;
            _isDirty = false;
            return wasDirty;
        }

        /// <summary>
        /// Force mark the facing as dirty (useful for initial animation setup)
        /// </summary>
        public void MarkDirty()
        {
            _isDirty = true;
        }
    }
}