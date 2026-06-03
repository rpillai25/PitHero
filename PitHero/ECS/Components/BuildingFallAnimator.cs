using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Animates a building entity falling from above the screen to its final resting position.
    /// Removes itself once the entity reaches the target Y.
    /// </summary>
    public class BuildingFallAnimator : Component, IUpdatable
    {
        private readonly float _targetY;
        private readonly float _speed;
        private readonly float _snapPx;
        private float _currentY;

        public BuildingFallAnimator(float targetY, float speed = 500f, float snapPx = 4f)
        {
            _targetY = targetY;
            _speed   = speed;
            _snapPx  = snapPx;
        }

        public override void OnAddedToEntity()
        {
            _currentY = Entity.Position.Y;
        }

        public void Update()
        {
            float remaining = _targetY - _currentY;
            if (System.Math.Abs(remaining) <= _snapPx)
            {
                Entity.SetPosition(Entity.Position.X, _targetY);
                Entity.RemoveComponent(this);
                return;
            }

            float step = _speed * Time.DeltaTime;
            _currentY += step * System.Math.Sign(remaining);
            Entity.SetPosition(Entity.Position.X, _currentY);
        }
    }
}
