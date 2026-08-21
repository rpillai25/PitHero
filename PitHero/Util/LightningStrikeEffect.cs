using System.Collections;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.ECS.Components;

namespace PitHero.Util
{
    /// <summary>
    /// Cosmetic lightning strike: plays the Actors.atlas "LightningStrike" animation once at a world
    /// position on RenderLayerTop, then destroys it. Silent. Shared by the crystal ceremony and the
    /// new-game intro (issue #396).
    /// </summary>
    public static class LightningStrikeEffect
    {
        private const string EntityName = "lightning-strike";
        private const string AtlasPath = "Content/Atlases/Actors.atlas";
        private const string AnimationName = "LightningStrike";
        private const float SafetyTimeoutSeconds = 5f;

        /// <summary>
        /// Coroutine that plays the strike centered on worldPosition and completes when the
        /// animation finishes (5 s safety timeout).
        /// </summary>
        public static IEnumerator PlayAt(Scene scene, Vector2 worldPosition)
        {
            var lightningEntity = scene.CreateEntity(EntityName);
            lightningEntity.SetPosition(worldPosition);

            var actorsAtlas = Core.Content.LoadSpriteAtlas(AtlasPath);
            if (actorsAtlas == null)
            {
                Debug.Error("[LightningStrikeEffect] Failed to load Actors.atlas for lightning strike");
                lightningEntity.Destroy();
                yield break;
            }

            var animator = lightningEntity.AddComponent<PausableSpriteAnimator>();
            animator.AddAnimationsFromAtlas(actorsAtlas);
            animator.SetRenderLayer(GameConfig.RenderLayerTop);
            animator.Play(AnimationName, SpriteAnimator.LoopMode.Once);

            float elapsed = 0f;
            while (animator.IsRunning && elapsed < SafetyTimeoutSeconds)
            {
                yield return null;
                elapsed += Time.DeltaTime;
            }

            lightningEntity.Destroy();
        }
    }
}
