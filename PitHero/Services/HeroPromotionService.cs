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

            // A manual job change must never fall back to a random crystal: if the queue was
            // emptied during the walk to the statue, abort cleanly and keep the current job.
            if (heroComponent.PendingManualJobChange
                && Core.Services.GetService<CrystalCollectionService>()?.PeekQueue() == null)
            {
                Debug.Log("[HeroPromotionService] Crystal queue emptied before manual job change ceremony — aborting, hero keeps current job");
                heroComponent.PendingManualJobChange = false;
                heroComponent.NeedsCrystal = false;
                heroComponent.HasArrivedAtStatueForCrystal = false;
                Core.Services.GetService<SettingsUI>()?.SetSaveEnabled(true);
                return;
            }

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

            // Prayer bubble fires immediately; the pre-strike dwell (0.5 + 3.5 = 4.0 s) is long
            // enough to cover the reveal + linger: 38 chars @ 20 cps ≈ 1.9 s + 2 s linger = 3.9 s.
            SpeechBubbleDialogue.SayCeremony(heroEntity);

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

            // Extended dwell so the ceremony bubble finishes before the lightning strike
            yield return Coroutine.WaitForSeconds(3.5f);

            // Play lightning strike animation on the hero entity
            yield return PlayLightningStrikeAtHero(heroEntity);

            Debug.Log("[HeroPromotionService] Crystal ceremony lightning complete — granting crystal to hero");

            // A manual job change must never fall back to a random crystal — the queue may have
            // been emptied during the ceremony dwell. Abort cleanly; the hero keeps its job.
            if (heroComponent.PendingManualJobChange
                && Core.Services.GetService<CrystalCollectionService>()?.PeekQueue() == null)
            {
                Debug.Log("[HeroPromotionService] Crystal queue emptied during manual job change ceremony — aborting, hero keeps current job");
                heroComponent.PendingManualJobChange = false;
                heroComponent.NeedsCrystal = false;
                heroComponent.HasArrivedAtStatueForCrystal = false;
                if (tileMover != null)
                    tileMover.SetEnabled(true);
                if (stateMachine != null)
                    stateMachine.SetEnabled(true);
                Core.Services.GetService<SettingsUI>()?.SetSaveEnabled(true);
                _isGrantingCrystal = false;
                yield break;
            }

            // Get next crystal for hero (from pending, queue, or random)
            var nextCrystal = GetNextCrystalForHero();
            // LinkedHero is null when hero respawned without a crystal (needsCrystal path);
            // it is non-null when the player requested a manual job change on a living hero.
            var oldHero = heroComponent.LinkedHero;
            bool isManualJobChange = heroComponent.PendingManualJobChange && oldHero != null;
            var heroName = oldHero?.Name
                ?? Core.Services.GetService<HeroDesignService>()?.GetDesign().Name
                ?? "Hero";
            // When tier ≥ 2 the hero starts at least at the recorded tier base level.
            var pitWidthManagerForSpawn = Core.Services.GetService<PitWidthManager>();
            // A manual job change starts a fresh cycle: reset the tier BEFORE computing the spawn
            // level so the new hero's floor is 1, mirroring RespawnHero's ordering on the death path.
            if (isManualJobChange)
                pitWidthManagerForSpawn?.ResetTierForNewCycle();
            int tierBaseLevel = pitWidthManagerForSpawn?.TierBaseLevel ?? 1;
            int spawnLevel = nextCrystal.Level > tierBaseLevel ? nextCrystal.Level : tierBaseLevel;
            heroComponent.LinkedHero = new RolePlayingFramework.Heroes.Hero(
                heroName,
                nextCrystal.Job,
                spawnLevel,
                nextCrystal.BaseStats,
                nextCrystal
            );

            if (isManualJobChange)
                FinishManualJobChange(heroComponent, oldHero);

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
        /// Manual-job-change extras run right after the new Hero is constructed: the outgoing
        /// crystal returns to the crystal inventory (crystals only go to the Second Chance Shop
        /// on death — the vault is just the never-lose-it fallback for a full inventory), the
        /// hero keeps their equipment, and the pit resets for the new cycle.
        /// </summary>
        private void FinishManualJobChange(HeroComponent heroComponent, RolePlayingFramework.Heroes.Hero oldHero)
        {
            var secondChanceVault = Core.Services.GetService<SecondChanceMerchantVault>();

            var oldCrystal = oldHero.BoundCrystal;
            if (oldCrystal != null)
            {
                var crystalService = Core.Services.GetService<CrystalCollectionService>();
                bool inInventory = HeroJobChangeHelper.ReturnCrystalToInventory(oldCrystal, crystalService, secondChanceVault);
                Debug.Log(inInventory
                    ? $"[HeroPromotionService] Returned {oldCrystal.Name} to crystal inventory after manual job change"
                    : $"[HeroPromotionService] Crystal inventory full — {oldCrystal.Name} sent to Second Chance vault instead");
            }

            // The hero didn't die: carry the six equipment slots onto the new job's hero
            // (unequippable items fall back to bag, then vault). The bag itself lives on the
            // component and survives untouched.
            HeroJobChangeHelper.TransferEquipment(oldHero, heroComponent.LinkedHero, heroComponent.Bag, secondChanceVault);

            heroComponent.PendingManualJobChange = false;

            // New job, new cycle: shrink the pit back to level 1 (waits for any mercenaries
            // still inside; the party followed the hero out, so this is normally immediate)
            (_scene as MainGameScene)?.StartPitResetForNewCycle();
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
