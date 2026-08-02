using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;
using RolePlayingFramework.Equipment;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to open an adjacent chest with timed sequence
    /// Face chest -> wait 1s -> open -> wait 1s -> done
    /// </summary>
    public class OpenChestAction : HeroActionBase
    {
        private enum Phase { NotStarted, FacingWait, OpenedWait, Done }
        private Phase _phase = Phase.NotStarted;
        private float _timer;
        private Entity _chestEntity; // cached chest entity for duration of action
        private TreasureComponent _treasureComponent;

        public OpenChestAction() : base(GoapConstants.OpenChest, 2)
        {
            // Preconditions: Hero must be adjacent to a chest
            SetPrecondition(GoapConstants.AdjacentToChest, true);

            // Postconditions: Chest is opened, recalculate adjacency
            SetPostcondition(GoapConstants.AdjacentToChest, false);
        }

        public override bool Execute(HeroComponent hero)
        {
            switch (_phase)
            {
                case Phase.NotStarted:
                    Debug.Log("[OpenChest] Starting chest opening sequence");
                    _chestEntity = FindNearestAdjacentClosedChest(hero);
                    if (_chestEntity == null)
                    {
                        Debug.Warn("[OpenChest] No adjacent CLOSED chest found - finishing");
                        hero.AdjacentToChest = hero.CheckAdjacentToChest();
                        Reset();
                        return true; // nothing to do
                    }

                    // Face chest (just logs currently)
                    FaceTarget(hero, _chestEntity.Transform.Position);
                    _phase = Phase.FacingWait;
                    _timer = GameConfig.TreasureOpenWait; // wait 1 second facing
                    return false; // still running

                case Phase.FacingWait:
                    if (!StillValid(hero))
                    {
                        Debug.Warn("[OpenChest] Chest no longer valid during facing wait - aborting");
                        hero.AdjacentToChest = hero.CheckAdjacentToChest();
                        Reset();
                        return true;
                    }
                    _timer -= Time.DeltaTime;
                    if (_timer <= 0f)
                    {
                        // Open chest
                        _treasureComponent = _chestEntity.GetComponent<TreasureComponent>();
                        if (_treasureComponent != null && _treasureComponent.State == TreasureComponent.TreasureState.CLOSED)
                        {
                            SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
                            soundEffectManager?.PlaySoundAt(SoundEffectType.ChestOpen, _chestEntity.Transform.Position);

                            _treasureComponent.State = TreasureComponent.TreasureState.OPEN;
                            Debug.Log("[OpenChest] Chest state changed to OPEN");

                            // Handle item pickup if there's a contained item
                            HandleItemPickup(hero, _treasureComponent);
                        }
                        else
                        {
                            Debug.Warn("[OpenChest] TreasureComponent missing or already open when attempting to open");
                        }
                        _phase = Phase.OpenedWait;
                        _timer = GameConfig.TreasureOpenWait; // wait another second after opening
                    }
                    return false;

                case Phase.OpenedWait:
                    if (!StillValidPostOpen())
                    {
                        Debug.Warn("[OpenChest] Chest entity lost after opening - continuing to finish");
                    }
                    _timer -= Time.DeltaTime;
                    if (_timer <= 0f)
                    {
                        hero.AdjacentToChest = hero.CheckAdjacentToChest();
                        Debug.Log("[OpenChest] Chest opening sequence complete");
                        _phase = Phase.Done;
                        Reset();
                        return true;
                    }
                    return false;

                case Phase.Done:
                    // Should not normally hit since we reset after completion
                    return true;
            }
            return true;
        }

        public override bool Execute(IGoapContext context)
        {
            // Virtual context: no timing for now, just immediate (can be expanded if virtual timing needed)
            context.LogDebug("[OpenChest] Virtual context immediate execution");
            return true;
        }

        /// <summary>
        /// Validate chest still exists and is adjacent & closed
        /// </summary>
        private bool StillValid(HeroComponent hero)
        {
            if (_chestEntity == null || _chestEntity.Transform == null)
                return false;
            var treasure = _chestEntity.GetComponent<TreasureComponent>();
            if (treasure == null || treasure.State != TreasureComponent.TreasureState.CLOSED)
                return false;
            // re-check adjacency (hero could have moved unexpectedly)
            var heroTile = GetCurrentTilePosition(hero);
            var chestTile = GetTileCoordinates(_chestEntity.Transform.Position);
            return IsCardinalAdjacent(heroTile, chestTile);
        }

        /// <summary>
        /// Validate chest entity presence after open (state may now be OPEN)
        /// </summary>
        private bool StillValidPostOpen()
        {
            return _chestEntity != null; // nothing else required
        }

        /// <summary>
        /// Reset internal state so action can be reused by planner
        /// </summary>
        private void Reset()
        {
            _phase = Phase.NotStarted;
            _timer = 0f;
            _chestEntity = null;
            _treasureComponent = null;
        }

        /// <summary>
        /// Find the nearest adjacent CLOSED chest to the hero (cardinal adjacency)
        /// </summary>
        private Entity FindNearestAdjacentClosedChest(HeroComponent hero)
        {
            var heroTile = GetCurrentTilePosition(hero);
            var scene = Core.Scene;
            if (scene == null) return null;

            var chestEntities = scene.FindEntitiesWithTag(GameConfig.TAG_TREASURE);
            Entity nearestChest = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < chestEntities.Count; i++)
            {
                var chest = chestEntities[i];
                var treasureComponent = chest.GetComponent<TreasureComponent>();
                if (treasureComponent == null || treasureComponent.State != TreasureComponent.TreasureState.CLOSED)
                    continue;

                var chestTile = GetTileCoordinates(chest.Transform.Position);
                if (IsCardinalAdjacent(heroTile, chestTile))
                {
                    float distance = Vector2.Distance(hero.Entity.Transform.Position, chest.Transform.Position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestChest = chest;
                    }
                }
            }

            return nearestChest;
        }

        /// <summary>
        /// Make hero face the target position
        /// </summary>
        private void FaceTarget(HeroComponent hero, Vector2 targetPosition)
        {
            var delta = targetPosition - hero.Entity.Transform.Position;
            Direction faceDir;
            if (System.Math.Abs(delta.X) >= System.Math.Abs(delta.Y))
                faceDir = delta.X < 0 ? Direction.Left : Direction.Right;
            else
                faceDir = delta.Y < 0 ? Direction.Up : Direction.Down;
            var facing = hero.Entity.GetComponent<ActorFacingComponent>();
            facing?.SetFacing(faceDir);
        }

        /// <summary>
        /// Check if two tile positions are adjacent in cardinal directions (N/S/E/W only)
        /// </summary>
        private bool IsCardinalAdjacent(Point tile1, Point tile2)
        {
            int dx = System.Math.Abs(tile1.X - tile2.X);
            int dy = System.Math.Abs(tile1.Y - tile2.Y);
            return (dx + dy) == 1;
        }

        /// <summary>
        /// Get current tile position from hero component
        /// </summary>
        private Point GetCurrentTilePosition(HeroComponent hero)
        {
            var tileMover = hero.Entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                return tileMover.GetCurrentTileCoordinates();
            }

            // Fallback to manual calculation
            return GetTileCoordinates(hero.Entity.Transform.Position);
        }

        /// <summary>
        /// Get the tile coordinates from a world position
        /// </summary>
        private Point GetTileCoordinates(Vector2 worldPosition)
        {
            return new Point((int)(worldPosition.X / GameConfig.TileSize), (int)(worldPosition.Y / GameConfig.TileSize));
        }

        /// <summary>
        /// Handle item pickup from opened treasure chest
        /// </summary>
        private void HandleItemPickup(HeroComponent hero, TreasureComponent treasureComponent)
        {
            // Seed chest: award seeds and show crop pickup animation before checking for a normal item.
            if (treasureComponent.ContainedSeedType.HasValue)
            {
                var crop = treasureComponent.ContainedSeedType.Value;
                int cnt  = treasureComponent.ContainedSeedCount;

                Core.Services.GetService<CropPlantingService>()?.AddSeeds(crop, cnt);

                // Pickup animation using the crop's fully-grown sprite from CropsProps.atlas
                var cropsAtlas = Core.Content?.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
                var cropSprite = cropsAtlas?.GetSprite(CropConfig.GetFullyGrownSpriteName(crop));
                if (cropSprite != null)
                {
                    var scene = Core.Scene;
                    var animEntity = scene?.CreateEntity("itemPickupAnimation");
                    if (animEntity != null)
                    {
                        animEntity.Transform.Position = _chestEntity.Transform.Position;
                        animEntity.AddComponent(new ItemPickupAnimationComponent(cropSprite, CropConfig.GetAtlasPrefix(crop)));
                    }
                }

                // Console event styled like ConsoleItemFound
                var gameEventSvc = Core.Services.GetService<GameEventService>();
                var textSvc = Core.Services.GetService<TextService>();
                string cropDisplayName = textSvc?.DisplayText(TextType.UI, CropConfig.GetDisplayNameKey(crop)) ?? crop.ToString();
                gameEventSvc?.EmitLocalized(UITextKey.ConsoleSeedsFound,
                    (hero.LinkedHero.Name, GameConfig.ConsoleColorHeroName),
                    (cnt.ToString(), Color.White),
                    (cropDisplayName, Color.Green));

                treasureComponent.ContainedSeedType  = null;
                treasureComponent.ContainedSeedCount = 0;
                return;
            }

            var containedItem = treasureComponent.ContainedItem;
            if (containedItem == null)
            {
                Debug.Log("[OpenChest] No item in treasure chest to pick up");
                return;
            }

            // Check if item has a sprite in Items.atlas (consumables and gear)
            if (IsItemVisualizable(containedItem))
            {
                // Create visual pickup animation entity at chest position
                var scene = Core.Scene;
                var animationEntity = scene.CreateEntity("itemPickupAnimation");
                animationEntity.Transform.Position = _chestEntity.Transform.Position;
                animationEntity.AddComponent(new ItemPickupAnimationComponent(containedItem));

            }

            // When the bag is full, auto-sell the weakest excess item to make room (if enabled).
            // SoldIncoming means the new item itself was the weakest and was sold directly.
            if (hero.Bag != null && hero.Bag.IsFull)
            {
                var autoSellSvc = Core.Services.GetService<Services.AutoSellExcessItemsService>();
                if (autoSellSvc?.TryMakeRoom(hero.Bag, containedItem) == Services.AutoSellOutcome.SoldIncoming)
                {
                    treasureComponent.ContainedItem = null;
                    return;
                }
            }

            // Try to add item using hero's TryAddItem method (handles consumable priority logic)
            if (hero.TryAddItem(containedItem))
            {
                Debug.Log($"[OpenChest] Added {containedItem.Name} to hero's main bag");

                Services.Analytics.AnalyticsService.LogItemAcquired(containedItem,
                    Core.Services.GetService<PitWidthManager>()?.CurrentPitLevel ?? 0, treasureComponent.Level);

                Core.Services.GetService<GameEventService>()?.EmitLocalized(UITextKey.ConsoleItemFound,
                    (hero.LinkedHero.Name, GameConfig.ConsoleColorHeroName),
                    (containedItem.Name, RarityUtils.GetRarityColor(containedItem.Rarity)));

                // Reset HealingItemExhausted if picked up item is a healing consumable
                if (containedItem is Consumable consumable && consumable.HPRestoreAmount > 0)
                {
                    hero.HealingItemExhausted = false;
                    Debug.Log($"[OpenChest] Reset HealingItemExhausted flag (picked up {containedItem.Name})");
                }

                // New gear sparkles in the inventory until viewed (survives auto-equip; same reference)
                Services.UnviewedGearTracker.MarkNew(containedItem);

                // Try to auto-equip if gear item
                PartyAutoEquipHelper.TryAutoEquipForParty(hero, containedItem);

                // Clear the item from the treasure chest
                treasureComponent.ContainedItem = null;
            }
            else
            {
                // Bag is full and nothing could be sold. The chest is already OPEN and is never
                // re-targeted, so send the item to the Second Chance vault instead of losing it.
                Debug.Warn($"[OpenChest] Hero's bags are full! Sending {containedItem.Name} to the Second Chance vault");
                Core.Services.GetService<PitHero.Services.SecondChanceMerchantVault>()?.AddItem(containedItem);
                Core.Services.GetService<GameEventService>()?.EmitLocalized(UITextKey.ConsoleItemSentToVault,
                    (containedItem.Name, RarityUtils.GetRarityColor(containedItem.Rarity)));
                treasureComponent.ContainedItem = null;
            }
        }

        /// <summary>
        /// Check if an item has a corresponding sprite in Items.atlas
        /// </summary>
        private bool IsItemVisualizable(IItem item)
        {
            return item is Consumable || item is IGear;
        }

}
}