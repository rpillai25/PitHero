using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.Config;
using PitHero.ECS.Components;
using PitHero.Farming;
using PitHero.Util;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services
{
    /// <summary>
    /// Runs the Fishing job: keeps fisher entities in sync with job assignments (a monster whose job is
    /// Fishing, who is awake, while a Fishing Pond is placed and its shift has fewer than
    /// <see cref="GameConfig.FishingMaxWorkersPerShift"/> fishers out), and hands out the pond's four
    /// spots (<see cref="FishingPondSpots"/>) so two fishers never share one. Day and night shifts never
    /// work at once, so the four spots serve both crews. Owns its own farm pathfinder (walls rebuilt
    /// when buildings change) and spawns the cosmetic catch effect.
    /// </summary>
    public class FishingTaskCoordinator : IMonsterWorkerHost
    {
        private struct ActiveWorker
        {
            public AlliedMonster Monster;
            public Entity Entity;
            public FishingMonsterStateMachine Fsm;
            public bool Nocturnal;
        }

        private readonly AlliedMonsterManager _alliedMonsters;
        private readonly BuildingService _buildingService;
        private readonly List<ActiveWorker> _workers = new List<ActiveWorker>(8);
        private readonly List<IMonsterWorkerHost> _peers = new List<IMonsterWorkerHost>(2);
        private readonly FishingMonsterStateMachine[] _spotOwner = new FishingMonsterStateMachine[FishingPondSpots.Count];
        private Scene _scene;

        /// <summary>A* grid for fishers (static map walls plus building walls; the pond is all water).</summary>
        public FarmPathfinder Pathfinder { get; }

        public FishingTaskCoordinator(AlliedMonsterManager alliedMonsters, BuildingService buildingService,
            int mapWidthTiles, int mapHeightTiles, Nez.Tiled.TmxLayer collisionLayer = null)
        {
            _alliedMonsters = alliedMonsters;
            _buildingService = buildingService;
            Pathfinder = new FarmPathfinder(mapWidthTiles, mapHeightTiles);
            Pathfinder.SeedStaticWalls(collisionLayer);
            if (_buildingService != null)
            {
                Pathfinder.RebuildWalls(_buildingService);
                _buildingService.BuildingsChanged += HandleBuildingsChanged;
            }
        }

        /// <summary>Provides the scene used to spawn fisher entities and catch effects.</summary>
        public void Initialize(Scene scene) => _scene = scene;

        /// <summary>Unsubscribes from service events. Call when the scene is torn down.</summary>
        public void Detach()
        {
            if (_buildingService != null)
                _buildingService.BuildingsChanged -= HandleBuildingsChanged;
        }

        /// <summary>
        /// Registers a coordinator for another job. A fisher is only spawned once no peer still has a
        /// live entity for the monster (its old-job worker walked home and despawned).
        /// </summary>
        public void AddPeer(IMonsterWorkerHost peer)
        {
            if (peer != null && !ReferenceEquals(peer, this))
                _peers.Add(peer);
        }

        /// <inheritdoc/>
        public bool HasLiveWorkerFor(AlliedMonster monster)
        {
            for (int i = 0; i < _workers.Count; i++)
                if (ReferenceEquals(_workers[i].Monster, monster) && !_workers[i].Entity.IsDestroyed)
                    return true;
            return false;
        }

        private bool AnyPeerHasLiveWorkerFor(AlliedMonster monster)
        {
            for (int i = 0; i < _peers.Count; i++)
                if (_peers[i].HasLiveWorkerFor(monster))
                    return true;
            return false;
        }

        /// <summary>The placed Fishing Pond, or null when none is placed.</summary>
        public PlacedBuilding GetPond() => FindPond(_buildingService);

        /// <summary>The placed Fishing Pond in <paramref name="buildings"/>, or null.</summary>
        public static PlacedBuilding FindPond(BuildingService buildings)
        {
            if (buildings == null)
                return null;
            var all = buildings.GetAll();
            for (int i = 0; i < all.Count; i++)
                if (all[i].Type == BuildingType.FishingPond)
                    return all[i];
            return null;
        }

        /// <summary>True when the spot's stand tile is on the map and walkable.</summary>
        public bool IsSpotUsable(int spot)
        {
            var pond = GetPond();
            return pond != null && Pathfinder.IsPassable(FishingPondSpots.GetStandTile(spot, pond.TileX, pond.TileY));
        }

        /// <summary>How many of the pond's four spots a fisher can reach (0 with no pond).</summary>
        public int UsableSpotCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < FishingPondSpots.Count; i++)
                    if (IsSpotUsable(i))
                        count++;
                return count;
            }
        }

        /// <summary>Claims the first free, usable spot (left, bottom, right, top) for a fisher.</summary>
        public bool TryClaimSpot(FishingMonsterStateMachine fsm, out int spot)
        {
            for (int i = 0; i < FishingPondSpots.Count; i++)
            {
                if (_spotOwner[i] == null && IsSpotUsable(i))
                {
                    _spotOwner[i] = fsm;
                    spot = i;
                    return true;
                }
            }
            spot = -1;
            return false;
        }

        /// <summary>Frees whatever spot the fisher holds (no-op when it holds none).</summary>
        public void ReleaseSpot(FishingMonsterStateMachine fsm)
        {
            for (int i = 0; i < _spotOwner.Length; i++)
                if (ReferenceEquals(_spotOwner[i], fsm))
                    _spotOwner[i] = null;
        }

        /// <summary>
        /// Fixed-step tick: spawns a fisher for every eligible Fishing monster (up to the per-shift cap),
        /// sends home fishers whose job changed, who fell asleep, who were dismissed, or whose pond is
        /// gone, and reaps despawned entities.
        /// </summary>
        public void Update()
        {
            if (_alliedMonsters == null || _scene == null)
                return;

            var timeService = Core.Services.GetService<InGameTimeService>();
            bool pondPlaced = GetPond() != null;

            var roster = _alliedMonsters.AlliedMonsters;
            for (int i = 0; i < roster.Count; i++)
            {
                var monster = roster[i];
                int workerIndex = FindWorkerIndex(monster);
                bool awake = !MonsterScheduleConfig.IsAsleep(monster.MonsterTypeName, timeService);

                if (monster.Job == MonsterJob.Fishing && awake && pondPlaced)
                {
                    if (workerIndex >= 0)
                        _workers[workerIndex].Fsm.CancelReturnHome();
                    else if (!AnyPeerHasLiveWorkerFor(monster)
                        && CountActiveFishers(MonsterScheduleConfig.IsNocturnal(monster.MonsterTypeName)) < GameConfig.FishingMaxWorkersPerShift)
                        SpawnWorker(monster);
                }
                else if (workerIndex >= 0)
                {
                    _workers[workerIndex].Fsm.RequestReturnHome();
                }
            }

            // Dismissed monsters are gone from the roster: their live fisher still walks home.
            for (int i = 0; i < _workers.Count; i++)
                if (!_alliedMonsters.Contains(_workers[i].Monster))
                    _workers[i].Fsm.RequestReturnHome();

            for (int i = _workers.Count - 1; i >= 0; i--)
            {
                if (_workers[i].Entity.IsDestroyed)
                {
                    ReleaseSpot(_workers[i].Fsm);
                    _workers.RemoveAt(i);
                }
            }
        }

        /// <summary>Fishers of one shift that are out and not heading home.</summary>
        private int CountActiveFishers(bool nocturnal)
        {
            int count = 0;
            for (int i = 0; i < _workers.Count; i++)
                if (_workers[i].Nocturnal == nocturnal && !_workers[i].Fsm.IsReturningHome && !_workers[i].Entity.IsDestroyed)
                    count++;
            return count;
        }

        private int FindWorkerIndex(AlliedMonster monster)
        {
            for (int i = 0; i < _workers.Count; i++)
                if (ReferenceEquals(_workers[i].Monster, monster))
                    return i;
            return -1;
        }

        private void SpawnWorker(AlliedMonster monster)
        {
            var house = FindMonsterHouse(monster.MonsterHouseId) ?? FindMonsterHouseWithCapacity();
            if (house == null)
                return; // no monster house available: retried next step
            if (house.UniqueId != monster.MonsterHouseId)
                monster.MonsterHouseId = house.UniqueId;

            var doorTile = new Point(house.TileX, house.TileY + 2);
            var position = new Vector2(
                doorTile.X * GameConfig.TileSize + GameConfig.TileSize / 2f,
                doorTile.Y * GameConfig.TileSize + GameConfig.TileSize / 2f);

            // Animations in Actors.atlas are named without the "Monster_" prefix
            string typeName = monster.MonsterTypeName.StartsWith("Monster_")
                ? monster.MonsterTypeName.Substring("Monster_".Length)
                : monster.MonsterTypeName;

            var entity = _scene.CreateEntity("fish-monster-" + monster.Name);
            entity.SetPosition(position);

            // Deliberately no collider and no TAG_MONSTER: workers must never trigger battles
            var body = entity.AddComponent(new NamedMonsterAnimationComponent(typeName, Color.White));
            body.SetRenderLayer(GameConfig.RenderLayerActors);
            entity.AddComponent(new ActorFacingComponent());
            entity.AddComponent(new FarmMonsterMover());

            var line = entity.AddComponent(new FishingLineRenderer());
            line.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);

            var bubble = entity.AddComponent(new SpeechBubbleComponent());
            bubble.AnchorRenderer = body;

            var fsm = entity.AddComponent(new FishingMonsterStateMachine(monster, this, new Point(house.TileX, house.TileY)));
            fsm.BodyAnimator = body;
            fsm.Line = line;

            _workers.Add(new ActiveWorker
            {
                Monster = monster,
                Entity = entity,
                Fsm = fsm,
                Nocturnal = MonsterScheduleConfig.IsNocturnal(monster.MonsterTypeName),
            });
            Debug.Log($"[FishingTaskCoordinator] Spawned fisher '{monster.Name}' ({typeName}) at house {house.UniqueId}");
        }

        private PlacedBuilding FindMonsterHouse(int uniqueId)
        {
            if (uniqueId < 0 || _buildingService == null)
                return null;
            var all = _buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
                if (all[i].UniqueId == uniqueId && all[i].Type == BuildingType.MonsterHouse)
                    return all[i];
            return null;
        }

        private PlacedBuilding FindMonsterHouseWithCapacity()
        {
            if (_buildingService == null)
                return null;
            var all = _buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
                if (all[i].Type == BuildingType.MonsterHouse && !_alliedMonsters.IsHouseFull(all[i].UniqueId))
                    return all[i];
            return null;
        }

        private void HandleBuildingsChanged() => Pathfinder.RebuildWalls(_buildingService);

        /// <summary>
        /// Shows a catch coming out of the water: a splash at the bobber, then the catch sprite flying to
        /// the fisher with its "Name (+N G)" label (<see cref="FishCatchEffectComponent"/>). Cosmetic:
        /// skipped entirely during replay seeks.
        /// </summary>
        public void SpawnCatchEffect(FishCatchType type, Vector2 bobber, Vector2 target, bool fisherFacesRight)
        {
            if (_scene == null || Core.CosmeticUpdatesSuspended)
                return;

            ParticleEffectManager.Current?.SpawnEffectAtPosition(ParticleEffectType.Splash,
                FishingCatchPath.WaterExit(bobber), _scene, GameConfig.RenderLayerActorPropOverlay);

            var atlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
            var sprite = atlas?.GetSprite(FishingConfig.GetSpriteName(type));

            var entity = _scene.CreateEntity("fish-catch-effect");
            entity.SetPosition(bobber);
            SpriteRenderer renderer = null;
            if (sprite != null)
            {
                renderer = entity.AddComponent(new SpriteRenderer(sprite));
                renderer.SetRenderLayer(GameConfig.RenderLayerActorPropOverlay);
                // Catches face right; one flying back toward a fisher on its left turns around
                renderer.FlipX = fisherFacesRight;
            }

            var text = entity.AddComponent(new GemValueTextComponent(GetCatchLabel(type),
                FishingConfig.IsJunk(type) ? FishingConfig.JunkTextColor : FishingConfig.FishTextColor,
                sprite != null ? -sprite.SourceRect.Height / 2f : 0f));
            text.SetRenderLayer(GameConfig.RenderLayerSpeechBubble);
            entity.AddComponent(new FishCatchEffectComponent(renderer, text, bobber, target));
        }

        /// <summary>"Largemouth Bass (+190G)" for a fish, just the name for junk.</summary>
        private static string GetCatchLabel(FishCatchType type)
        {
            var texts = Core.Services.GetService<TextService>();
            string name = texts?.DisplayText(TextType.UI, FishingConfig.GetNameKey(type)) ?? type.ToString();
            int gold = FishingConfig.GetGold(type);
            if (gold <= 0)
                return name;
            string format = texts?.DisplayText(TextType.UI, UITextKey.FishCatchValueFormat) ?? "{0} (+{1}G)";
            return string.Format(format, name, gold);
        }
    }
}
