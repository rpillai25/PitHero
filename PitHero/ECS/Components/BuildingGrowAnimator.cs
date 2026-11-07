using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Grows a newly placed building in place: the entity starts at a fraction of its size and eases up
    /// to full size, then the component removes itself. Used for the Digging Field (issue #452), which is
    /// laid on the ground instead of falling from the sky. Purely cosmetic: the building is registered at
    /// full size from the start, and the UI treats it as unclickable while this component is attached
    /// (<see cref="IsGrowing"/>), so no sim state depends on it.
    /// </summary>
    public class BuildingGrowAnimator : Component, IUpdatable
    {
        private readonly float _startScale;
        private readonly float _duration;
        private float _elapsed;

        public BuildingGrowAnimator(float startScale, float durationSeconds)
        {
            _startScale = startScale;
            _duration = durationSeconds;
        }

        /// <summary>True while the entity is still growing in.</summary>
        public static bool IsGrowing(Entity entity)
        {
            return entity != null && entity.GetComponent<BuildingGrowAnimator>() != null;
        }

        /// <summary>Scale at a normalized time t in [0,1]: ease-out cubic from the start scale to 1.</summary>
        public static float ScaleAt(float startScale, float t)
        {
            if (t <= 0f) return startScale;
            if (t >= 1f) return 1f;
            float inv = 1f - t;
            float eased = 1f - inv * inv * inv;
            return startScale + (1f - startScale) * eased;
        }

        public override void OnAddedToEntity()
        {
            Entity.SetScale(_startScale);
        }

        public void Update()
        {
            // replay seek: finish instantly so the building does not grow when the seek ends
            if (Core.CosmeticUpdatesSuspended || _duration <= 0f)
            {
                Finish();
                return;
            }

            _elapsed += Time.DeltaTime;
            if (_elapsed >= _duration)
            {
                Finish();
                return;
            }
            Entity.SetScale(ScaleAt(_startScale, _elapsed / _duration));
        }

        private void Finish()
        {
            Entity.SetScale(Vector2.One);
            Entity.RemoveComponent(this);
        }
    }
}
