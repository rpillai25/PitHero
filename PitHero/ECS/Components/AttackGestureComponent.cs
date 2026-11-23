using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// A short render-only body gesture during a battle attack (issue #458 follow-up): it moves a character's
    /// composite sprite (<see cref="MultiSpriteAnimator"/>) away from where it stood and back, while the entity,
    /// its tile and its AI never move. It runs by itself on the fixed step, holds while paused and removes
    /// itself when done, so nothing waits on it and the battle's timing is unchanged.
    /// <list type="bullet">
    /// <item><see cref="Lunge"/>: a quick push toward the target and a snap back (melee effort).</item>
    /// <item><see cref="Return"/>: eases from an offset back to where it stood (after a dash or a leap).</item>
    /// </list>
    /// </summary>
    public class AttackGestureComponent : Component, IUpdatable
    {
        private MultiSpriteAnimator _body;
        private Vector2 _base;
        private Vector2 _vector;
        private float _duration;
        private float _elapsed;
        private bool _outAndBack;

        /// <summary>
        /// Pushes <paramref name="entity"/>'s sprite <paramref name="distance"/> px along <paramref name="direction"/>
        /// (fast, over the first <see cref="GameConfig.AttackLungePeak"/> of the time) and snaps it back.
        /// </summary>
        public static void Lunge(Entity entity, Vector2 direction, float distance, float duration)
            => Start(entity, direction * distance, duration, outAndBack: true, offsetIsCurrent: false);

        /// <summary>
        /// The sprite currently sits <paramref name="offset"/> px from where it stands; ease it back over
        /// <paramref name="duration"/> seconds.
        /// </summary>
        public static void Return(Entity entity, Vector2 offset, float duration)
            => Start(entity, offset, duration, outAndBack: false, offsetIsCurrent: true);

        /// <summary>Ends any gesture on <paramref name="entity"/> at once, its sprite back where it stood.</summary>
        public static void Cancel(Entity entity)
        {
            var g = entity?.GetComponent<AttackGestureComponent>();
            g?.Finish();
        }

        private static void Start(Entity entity, Vector2 vector, float duration, bool outAndBack, bool offsetIsCurrent)
        {
            if (entity == null || entity.IsDestroyed)
                return;
            var body = entity.GetComponent<MultiSpriteAnimator>();
            if (body == null)
                return;
            Cancel(entity);
            var g = entity.AddComponent(new AttackGestureComponent());
            g._body = body;
            g._base = offsetIsCurrent ? body.LocalOffset - vector : body.LocalOffset;
            g._vector = vector;
            g._duration = duration > 0f ? duration : 0.01f;
            g._outAndBack = outAndBack;
            g.Apply(0f);
        }

        /// <inheritdoc/>
        public void Update()
        {
            var pause = Core.Instance != null ? Core.Services.GetService<PitHero.Services.PauseService>() : null;
            if (pause?.IsPaused != true)
                _elapsed += Time.DeltaTime;
            float t = _elapsed / _duration;
            if (t >= 1f)
            {
                Finish();
                return;
            }
            Apply(t);
        }

        private void Apply(float t)
        {
            if (_body == null)
                return;
            float k;
            if (_outAndBack)
            {
                float peak = GameConfig.AttackLungePeak;
                k = t < peak ? t / peak : 1f - (t - peak) / (1f - peak);
                k = k * (2f - k); // ease out: fast push, settles into the snap back
            }
            else
            {
                float r = 1f - t;
                k = r * r; // quick at first, settling home
            }
            _body.LocalOffset = _base + _vector * k;
        }

        private void Finish()
        {
            if (_body != null)
                _body.LocalOffset = _base;
            _body = null;
            if (Entity != null && !Entity.IsDestroyed)
                Entity.RemoveComponent(this);
        }
    }
}
