using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using System.Collections;
using PitHero.Services;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component that handles the hero death animation and moves the crystal to the vault.
    /// The hero faces downward, rises upward while fading, and a shadow remains at the death location.
    /// </summary>
    public class HeroDeathComponent : Component
    {
        // Death animation configuration
        private const float DeathFadeOutDuration = 2.0f; // 2 seconds for rise and fade
        private const float DeathRiseDistance = 64f; // Distance hero rises in pixels
        
        // HeroShadow sprite coordinates from Actors.atlas
        private const int ShadowSpriteX = 198;
        private const int ShadowSpriteY = 499;
        private const int ShadowSpriteWidth = 27;
        private const int ShadowSpriteHeight = 5;
        
        private Entity _shadowEntity;
        private bool _deathAnimationStarted;
        
        /// <summary>
        /// Starts the hero death animation sequence.
        /// </summary>
        public void StartDeathAnimation()
        {
            if (_deathAnimationStarted)
                return;
                
            _deathAnimationStarted = true;
            Core.StartCoroutine(ExecuteDeathAnimation());
        }
        
        private IEnumerator ExecuteDeathAnimation()
        {
            var heroComponent = Entity.GetComponent<HeroComponent>();
            if (heroComponent?.LinkedHero == null)
            {
                Debug.Warn("[HeroDeathComponent] Hero has no LinkedHero, cannot perform death animation");
                yield break;
            }
            
            var hero = heroComponent.LinkedHero;
            var crystal = hero.BoundCrystal;
            
            if (crystal == null)
            {
                Debug.Warn("[HeroDeathComponent] Hero has no bound crystal, cannot add to vault");
                yield break;
            }
            
            Debug.Log($"[HeroDeathComponent] Starting death animation for {hero.Name}");
            
            // Face hero downward
            var facing = Entity.GetComponent<ActorFacingComponent>();
            facing?.SetFacing(Direction.Down);
            
            // Create shadow entity at hero's death location
            CreateShadowAtDeathLocation();
            
            // Get initial position
            var startPosition = Entity.Transform.Position;
            var endPosition = startPosition + new Vector2(0, -DeathRiseDistance);
            
            // Get all renderers to fade them out
            var bodyRenderer = Entity.GetComponent<HeroBodyAnimationComponent>();
            var hairRenderer = Entity.GetComponent<HeroHairAnimationComponent>();
            var shirtRenderer = Entity.GetComponent<HeroShirtAnimationComponent>();
            var pantsRenderer = Entity.GetComponent<HeroPantsAnimationComponent>();
            var hand1Renderer = Entity.GetComponent<HeroHand1AnimationComponent>();
            var hand2Renderer = Entity.GetComponent<HeroHand2AnimationComponent>();
            
            // Animate rise and fade
            float elapsed = 0f;
            while (elapsed < DeathFadeOutDuration)
            {
                // Respect pause
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService?.IsPaused == true)
                {
                    yield return null;
                    continue;
                }
                
                elapsed += Time.DeltaTime;
                float progress = elapsed / DeathFadeOutDuration;
                if (progress > 1f) progress = 1f;
                
                // Lerp position upward
                Entity.Transform.Position = Vector2.Lerp(startPosition, endPosition, progress);
                
                // Fade out alpha
                byte alpha = (byte)(255 * (1f - progress));
                
                // Apply alpha to all renderers
                if (bodyRenderer != null)
                {
                    var color = bodyRenderer.Color;
                    bodyRenderer.Color = new Color(color.R, color.G, color.B, alpha);
                }
                if (hairRenderer != null)
                {
                    var color = hairRenderer.Color;
                    hairRenderer.Color = new Color(color.R, color.G, color.B, alpha);
                }
                if (shirtRenderer != null)
                {
                    var color = shirtRenderer.Color;
                    shirtRenderer.Color = new Color(color.R, color.G, color.B, alpha);
                }
                if (pantsRenderer != null)
                {
                    var color = pantsRenderer.Color;
                    pantsRenderer.Color = new Color(color.R, color.G, color.B, alpha);
                }
                if (hand1Renderer != null)
                {
                    var color = hand1Renderer.Color;
                    hand1Renderer.Color = new Color(color.R, color.G, color.B, alpha);
                }
                if (hand2Renderer != null)
                {
                    var color = hand2Renderer.Color;
                    hand2Renderer.Color = new Color(color.R, color.G, color.B, alpha);
                }
                
                yield return null;
            }
            
            Debug.Log($"[HeroDeathComponent] Death animation complete for {hero.Name}");
            
            // Remove shadow
            if (_shadowEntity != null)
            {
                _shadowEntity.Destroy();
                _shadowEntity = null;
            }
            
            // Add crystal to vault
            var vault = Core.Services.GetService<CrystalMerchantVault>();
            if (vault != null)
            {
                vault.AddCrystal(crystal);
                Debug.Log($"[HeroDeathComponent] Added crystal to vault. Crystal sell value: {crystal.CalculateSellValue()} gold");
            }
            else
            {
                Debug.Warn("[HeroDeathComponent] CrystalMerchantVault service not found");
            }
            
            // Destroy hero entity
            Entity.Destroy();
        }
        
        private void CreateShadowAtDeathLocation()
        {
            // Load the HeroShadow sprite from the Actors atlas
            var shadowTexture = Entity.Scene.Content.LoadTexture("Content/Atlases/Actors.png");
            
            // Create sprite from atlas region: HeroShadow coordinates
            var shadowRegion = new Sprite(shadowTexture, new Rectangle(ShadowSpriteX, ShadowSpriteY, ShadowSpriteWidth, ShadowSpriteHeight));
            shadowRegion.Origin = new Vector2(ShadowSpriteWidth * 0.5f, ShadowSpriteHeight * 0.5f); // Center origin
            
            // Create shadow entity
            _shadowEntity = Entity.Scene.CreateEntity("hero-shadow");
            _shadowEntity.Transform.Position = Entity.Transform.Position;
            
            // Add sprite renderer
            var shadowRenderer = _shadowEntity.AddComponent(new SpriteRenderer(shadowRegion));
            shadowRenderer.SetRenderLayer(GameConfig.RenderLayerLowest);
            shadowRenderer.Color = new Color(0, 0, 0, 128); // Semi-transparent black shadow
            
            Debug.Log($"[HeroDeathComponent] Created shadow at {Entity.Transform.Position}");
        }
        
        public override void OnRemovedFromEntity()
        {
            // Clean up shadow if it still exists
            if (_shadowEntity != null)
            {
                _shadowEntity.Destroy();
                _shadowEntity = null;
            }
            
            base.OnRemovedFromEntity();
        }
    }
}
