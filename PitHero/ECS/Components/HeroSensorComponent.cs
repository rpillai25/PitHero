using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Single source of truth for hero spatial flags. Uses HeroComponent tile/rect helpers.
    /// </summary>
    public class HeroSensorComponent : Component, IUpdatable
    {
        private HeroComponent _hero;
        private bool _prevInside;

        public override void OnAddedToEntity()
        {
            _hero = Entity.GetComponent<HeroComponent>();
            if (_hero != null)
            {
                ForceRecompute();
                _prevInside = _hero.IsInsidePit;
            }
        }

        public void Update()
        {
            if (_hero == null) return;

            var pos = Entity.Transform.Position;

            // Inside / Adjacent via hero helpers (tile-based)
            var inside = _hero.CheckInsidePit(pos);
            var adjacent = !inside && _hero.CheckAdjacentToPit(pos);

            // Center test (pixel distance), only if not inside/adjacent
            var centerPos = HeroActionBase.GetMapCenterWorldPosition();
            var atCenter = !inside && !adjacent &&
                           Vector2.Distance(pos, centerPos) <= GameConfig.CenterRadiusPixels;

            // Transition: leaving pit sets JustJumpedOutOfPit
            if (_prevInside && !inside)
                _hero.JustJumpedOutOfPit = true;

            // If we settle at center, clear jump flag
            if (atCenter && _hero.JustJumpedOutOfPit)
                _hero.JustJumpedOutOfPit = false;

            _hero.IsInsidePit = inside;
            _hero.IsAdjacentToPit = adjacent;
            _hero.IsAtCenter = atCenter;

            _prevInside = inside;
        }

        public void ForceRecompute()
        {
            _prevInside = _hero.IsInsidePit;
            Update();
        }
    }
}