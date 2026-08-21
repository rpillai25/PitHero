using System.Collections;
using Nez;
using PitHero.ECS.Components;
using PitHero.ECS.Scenes;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;

namespace PitHero.Services
{
    /// <summary>
    /// Scripted new-game opening (issue #396): the hero drops from above the screen onto the tile at
    /// the hero statue's feet, turns to the statue, prays, and is struck by the ceremony lightning.
    /// The scene hides the HUD and blocks input for the duration (see
    /// <see cref="MainGameScene.BeginIntroPresentation"/>) and adds the hero's GOAP state machine only
    /// when the sequence ends, so the first pit trip starts from the statue.
    /// Timing uses scaled time like the crystal ceremony; the sequence is not pause-aware (nothing
    /// can pause the game while the HUD is locked).
    /// </summary>
    public class NewGameIntroService
    {
        private readonly MainGameScene _scene;
        private readonly CameraControllerComponent _cameraController;

        public NewGameIntroService(MainGameScene scene, CameraControllerComponent cameraController)
        {
            _scene = scene;
            _cameraController = cameraController;
        }

        /// <summary>True from Start() until the sequence has handed control back to the scene</summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Launches the intro coroutine for the freshly spawned hero. The scene must already have
        /// disabled the hero's MultiSpriteAnimator so no standing frame renders before the drop.
        /// </summary>
        public void Start(Entity hero)
        {
            if (IsActive || hero == null)
                return;

            IsActive = true;
            Core.StartCoroutine(Run(hero));
        }

        /// <summary>
        /// Gravity-style drop: remaining height above the ground at normalized time t (clamped to [0,1]).
        /// </summary>
        public static float ComputeFallHeight(float startHeight, float t)
        {
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return startHeight * (1f - t * t);
        }

        /// <summary>
        /// Lift that starts the sprite fully above the visible top edge. Never less than
        /// spriteHeightPx + marginPx so the drop reads even when the hero is already near the top.
        /// </summary>
        public static float ComputeFallStartHeight(float heroWorldY, float visibleWorldTop, float spriteHeightPx, float marginPx)
        {
            float height = heroWorldY - visibleWorldTop + spriteHeightPx + marginPx;
            float minimum = spriteHeightPx + marginPx;
            return height < minimum ? minimum : height;
        }

        private IEnumerator Run(Entity hero)
        {
            // Components initialise on the first scene update after creation; wait for the paperdoll
            // animators (and the jump component's atlas/shadow) before posing. The camera controller
            // also finishes its deferred init during this wait, so the visible-top query below is valid.
            for (int frame = 0; frame < GameConfig.IntroAnimatorReadyMaxFrames; frame++)
            {
                yield return null;
                if (hero.IsDestroyed)
                    break;
                var anim = hero.GetComponent<HeroAnimationComponent>();
                if (anim != null && anim.Animations != null && anim.Animations.Count > 0)
                    break;
            }

            if (hero.IsDestroyed)
            {
                Finish(hero);
                yield break;
            }

            var jump = hero.GetComponent<HeroJumpComponent>();
            var multiAnimator = hero.GetComponent<MultiSpriteAnimator>();
            float heroY = hero.Transform.Position.Y;
            float visibleTop = _cameraController != null ? _cameraController.GetVisibleWorldTop() : heroY;
            float startHeight = ComputeFallStartHeight(heroY, visibleTop, MultiSpriteAnimator.RT_HEIGHT, GameConfig.IntroFallOffscreenMarginPx);

            // Pose, lift off-screen and reveal in the same tick so no frame shows the hero standing
            bool posed = jump != null && jump.BeginAirbornePose(Direction.Down);
            if (posed)
                jump.SetAirborneHeight(startHeight);
            multiAnimator?.SetEnabled(true);

            if (posed)
            {
                float elapsed = 0f;
                while (elapsed < GameConfig.IntroFallDurationSeconds)
                {
                    yield return null;
                    if (hero.IsDestroyed)
                    {
                        Finish(hero);
                        yield break;
                    }
                    elapsed += Time.DeltaTime;
                    jump.SetAirborneHeight(ComputeFallHeight(startHeight, elapsed / GameConfig.IntroFallDurationSeconds));
                }
                jump.EndAirbornePose();
            }

            Core.GetGlobalManager<SoundEffectManager>()?.PlaySoundAt(SoundEffectType.Land, hero.Transform.Position);

            yield return Coroutine.WaitForSeconds(GameConfig.IntroPostLandingDelaySeconds);
            if (hero.IsDestroyed)
            {
                Finish(hero);
                yield break;
            }

            // Face the statue and pray. IntroPrayerDwellSeconds is sized to the line's reveal + linger
            // (44 chars @ 20 cps ≈ 2.2 s + 2 s linger = 4.2 s) so the bubble finishes before the strike.
            hero.GetComponent<ActorFacingComponent>()?.SetFacing(Direction.Up);
            SpeechBubbleDialogue.SayIntro(hero);
            yield return Coroutine.WaitForSeconds(GameConfig.IntroPrayerDwellSeconds);
            if (hero.IsDestroyed)
            {
                Finish(hero);
                yield break;
            }

            // Purely for show — the hero already has its crystal
            yield return LightningStrikeEffect.PlayAt(_scene, hero.Transform.Position);
            yield return Coroutine.WaitForSeconds(GameConfig.IntroPostLightningDelaySeconds);

            Finish(hero);
        }

        private void Finish(Entity hero)
        {
            IsActive = false;
            _scene.EndIntroPresentation(hero);
            Debug.Log("[NewGameIntroService] New-game intro complete — hero AI enabled");
        }
    }
}
