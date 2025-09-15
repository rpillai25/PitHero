using Nez;
using Nez.Sprites;
using PitHero.Services;

namespace PitHero.ECS.Components
{
    public class PausableSpriteAnimator : SpriteAnimator, IPausableComponent
    {
        public bool ShouldPause => true;

        private static PauseService _pauseService;

        public override void OnAddedToEntity()
        {
            _pauseService = Core.Services.GetService<PauseService>();
            base.OnAddedToEntity();
        }

        public override void Update()
        {
            //var pauseService = Core.Services.GetService<PauseService>();
            if (_pauseService?.IsPaused == true && ShouldPause)
                return;

            base.Update();
        }
    }
}
