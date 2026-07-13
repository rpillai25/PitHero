using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero;
using PitHero.ECS.Components;
using PitHero.ECS.Scenes;
using PitHero.UI;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Stats;
using System.Collections;
using System.Linq;

namespace PitHero.Services
{
    /// <summary>
    /// Service that runs the crystal ceremony: after a hero death the respawned hero walks to the
    /// hero statue and is imbued with a new crystal (from the queue, or randomly generated).
    /// </summary>
    public class HeroPromotionService
    {
        private Scene _scene;
        private bool _isGrantingCrystal;

        public HeroPromotionService(Scene scene)
        {
            _scene = scene;
            _isGrantingCrystal = false;
        }

        /// <summary>
        /// Checks if a living hero needs a crystal (spawned without one after death) and has arrived at the statue.
        /// When both conditions are true, plays the promotion ceremony and grants the hero a new crystal.
        /// </summary>
        public void CheckAndPromoteHeroIfNeeded()
        {
            if (_isGrantingCrystal)
                return;

            var heroEntity = _scene.FindEntity("hero");
            if (heroEntity == null)
                return;

            var heroComponent = heroEntity.GetComponent<HeroComponent>();
            if (heroComponent == null)
                return;

            if (!heroComponent.NeedsCrystal || !heroComponent.HasArrivedAtStatueForCrystal)
                return;

            Debug.Log("[HeroPromotionService] Hero has arrived at statue and needs a crystal — starting crystal ceremony");
            _isGrantingCrystal = true;
            Core.StartCoroutine(ExecuteHeroCrystalCeremony(heroEntity));
        }

        /// <summary>
        /// Plays the lightning strike at the hero's position and then grants the hero a new crystal
        /// </summary>
        private IEnumerator ExecuteHeroCrystalCeremony(Entity heroEntity)
        {
            var heroComponent = heroEntity.GetComponent<HeroComponent>();
            if (heroComponent == null)
            {
                _isGrantingCrystal = false;
                yield break;
            }

            // Brief pause before the ceremony
            yield return Coroutine.WaitForSeconds(0.5f);

            // Disable movement and AI while the ceremony plays
            var tileMover = heroEntity.GetComponent<TileByTileMover>();
            var stateMachine = heroEntity.GetComponent<AI.HeroStateMachine>();

            if (tileMover != null)
                tileMover.SetEnabled(false);
            if (stateMachine != null)
                stateMachine.SetEnabled(false);

            // Make hero face the statue
            var facingComponent = heroEntity.GetComponent<ActorFacingComponent>();
            if (facingComponent != null)
                facingComponent.SetFacing(Direction.Up);

            yield return Coroutine.WaitForSeconds(1.0f);

            // Play lightning strike animation on the hero entity
            yield return PlayLightningStrikeAtHero(heroEntity);

            Debug.Log("[HeroPromotionService] Crystal ceremony lightning complete — granting crystal to hero");

            // Get next crystal for hero (from pending, queue, or random)
            var nextCrystal = GetNextCrystalForHero();
            // LinkedHero is null when hero respawned without a crystal (needsCrystal path)
            var heroName = heroComponent.LinkedHero?.Name
                ?? Core.Services.GetService<HeroDesignService>()?.GetDesign().Name
                ?? "Hero";
            // When tier ≥ 2 the hero starts at least at the recorded tier base level.
            var pitWidthManagerForSpawn = Core.Services.GetService<PitWidthManager>();
            int tierBaseLevel = pitWidthManagerForSpawn?.TierBaseLevel ?? 1;
            int spawnLevel = nextCrystal.Level > tierBaseLevel ? nextCrystal.Level : tierBaseLevel;
            heroComponent.LinkedHero = new RolePlayingFramework.Heroes.Hero(
                heroName,
                nextCrystal.Job,
                spawnLevel,
                nextCrystal.BaseStats,
                nextCrystal
            );

            Debug.Log($"[HeroPromotionService] Hero granted crystal: {nextCrystal.Job.Name} Level {spawnLevel} (crystal={nextCrystal.Level}, tierBase={tierBaseLevel})");

            Core.Services.GetService<GameEventService>()?.EmitLocalized(UITextKey.ConsoleCrystalPromotion,
                (heroComponent.LinkedHero.Name, GameConfig.ConsoleColorHeroName),
                (nextCrystal.Job.Name, Color.White));

            // Clear the crystal-needed flags so GOAP resumes normal behavior
            heroComponent.NeedsCrystal = false;
            heroComponent.HasArrivedAtStatueForCrystal = false;

            // Re-enable movement and AI
            if (tileMover != null)
                tileMover.SetEnabled(true);
            if (stateMachine != null)
                stateMachine.SetEnabled(true);

            // Reconnect UI
            ReconnectUIToHero(heroEntity);

            // Re-enable the Save button now that the promotion ceremony is complete
            Core.Services.GetService<SettingsUI>()?.SetSaveEnabled(true);

            _isGrantingCrystal = false;
            Debug.Log("[HeroPromotionService] *** HERO CRYSTAL CEREMONY COMPLETE ***");
        }

        /// <summary>
        /// Plays the lightning strike animation centered on the hero entity
        /// </summary>
        private IEnumerator PlayLightningStrikeAtHero(Entity heroEntity)
        {
            Debug.Log("[HeroPromotionService] Playing lightning strike on hero");

            var lightningEntity = _scene.CreateEntity("lightning-strike-hero");
            lightningEntity.SetPosition(heroEntity.Transform.Position);

            var actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
            if (actorsAtlas == null)
            {
                Debug.Error("[HeroPromotionService] Failed to load Actors.atlas for hero lightning strike");
                yield break;
            }

            var animator = lightningEntity.AddComponent<PausableSpriteAnimator>();
            animator.AddAnimationsFromAtlas(actorsAtlas);
            animator.SetRenderLayer(GameConfig.RenderLayerTop);

            animator.Play("LightningStrike", Nez.Sprites.SpriteAnimator.LoopMode.Once);

            float timeout = 5.0f;
            float elapsed = 0f;
            while (animator.IsRunning && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.DeltaTime;
            }

            lightningEntity.Destroy();
            Debug.Log("[HeroPromotionService] Hero lightning strike complete");
        }

        /// <summary>
        /// Reconnects the UI components to the hero after the crystal ceremony
        /// </summary>
        private void ReconnectUIToHero(Entity newHeroEntity)
        {
            // Cast scene to MainGameScene to access UI reconnection method
            if (_scene is MainGameScene mainGameScene)
            {
                mainGameScene.ReconnectUIToHero();
                Debug.Log("[HeroPromotionService] Reconnected UI to new hero");
            }
            else
            {
                Debug.Warn("[HeroPromotionService] Could not reconnect UI - scene is not MainGameScene");
            }
        }

        /// <summary>
        /// Gets the next crystal to use for the hero, prioritizing pending crystal from death, then queue, then random.
        /// </summary>
        private HeroCrystal GetNextCrystalForHero()
        {
            var crystalService = Core.Services.GetService<CrystalCollectionService>();

            // 1. Check queue — player may have rearranged between death and this ceremony
            var queued = crystalService?.Dequeue();
            if (queued != null)
            {
                Debug.Log($"[HeroPromotionService] Using queued crystal: {queued.Name}");
                return queued;
            }

            // 2. Random fallback
            return GenerateRandomHeroCrystal();
        }

        /// <summary>
        /// Generates a random hero crystal for the new hero
        /// In the future, this will use the crystal forge queue
        /// </summary>
        private HeroCrystal GenerateRandomHeroCrystal()
        {
            // For now, generate a random crystal with a random job and level 1
            var randomJob = GetRandomJob();
            var baseStats = new StatBlock(
                strength: Nez.Random.Range(2, 6),
                agility: Nez.Random.Range(2, 6),
                vitality: Nez.Random.Range(2, 6),
                magic: Nez.Random.Range(2, 6)
            );

            var crystal = new HeroCrystal("Generated Hero", randomJob, 1, baseStats);
            Debug.Log($"[HeroPromotionService] Generated random crystal: {randomJob.Name} Level 1");

            return crystal;
        }

        /// <summary>
        /// Gets a random job for hero crystal generation
        /// </summary>
        private RolePlayingFramework.Jobs.IJob GetRandomJob()
        {
            var jobs = new RolePlayingFramework.Jobs.IJob[]
            {
                new RolePlayingFramework.Jobs.Primary.Knight(),
                new RolePlayingFramework.Jobs.Primary.Monk(),
                new RolePlayingFramework.Jobs.Primary.Thief(),
                new RolePlayingFramework.Jobs.Primary.Archer(),
                new RolePlayingFramework.Jobs.Primary.Mage(),
                new RolePlayingFramework.Jobs.Primary.Priest()
            };

            return jobs.RandomItem();
        }
    }
}
