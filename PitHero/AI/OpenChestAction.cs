using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
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
                            soundEffectManager?.PlaySound(SoundEffectType.ChestOpen);

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
            Debug.Log($"[OpenChest] Hero facing direction set to {faceDir} using delta ({delta.X},{delta.Y})");
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

                Debug.Log($"[OpenChest] Created pickup animation for {containedItem.Name} at position X: {_chestEntity.Transform.Position.X}, Y: {_chestEntity.Transform.Position.Y}");
            }

            // Try to add item using hero's TryAddItem method (handles consumable priority logic)
            if (hero.TryAddItem(containedItem))
            {
                Debug.Log($"[OpenChest] Added {containedItem.Name} to hero's main bag. Bag contents:");
                LogBagContents(hero.Bag);

                // Reset HealingItemExhausted if picked up item is a healing consumable
                if (containedItem is Consumable consumable && consumable.HPRestoreAmount > 0)
                {
                    hero.HealingItemExhausted = false;
                    Debug.Log($"[OpenChest] Reset HealingItemExhausted flag (picked up {containedItem.Name})");
                }

                // Try to auto-equip if gear item
                TryAutoEquipFromChest(hero, containedItem);

                // Clear the item from the treasure chest
                treasureComponent.ContainedItem = null;
            }
            else
            {
                Debug.Warn($"[OpenChest] Hero's bags are full! Could not add {containedItem.Name}");
            }
        }

        /// <summary>
        /// Check if an item has a corresponding sprite in Items.atlas
        /// </summary>
        private bool IsItemVisualizable(IItem item)
        {
            return item is Consumable || item is IGear;
        }

        /// <summary>
        /// Debug log the contents of the hero's bag
        /// </summary>
        private void LogBagContents(RolePlayingFramework.Inventory.ItemBag bag)
        {
            Debug.Log($"[OpenChest] Hero bag contains {bag.Count}/{bag.Capacity} items:");
            for (int i = 0; i < bag.Items.Count; i++)
            {
                var item = bag.Items[i];
                Debug.Log($"[OpenChest]   {i + 1}. {item.Name} ({item.Rarity})");
            }
        }

        /// <summary>
        /// Returns true if the bag currently contains the provided item instance
        /// </summary>
        private bool BagContains(RolePlayingFramework.Inventory.ItemBag bag, IItem item)
        {
            var items = bag.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == item)
                    return true;
            }
            return false;
        }

        /// <summary>Attempts to auto-equip gear on hero and mercenaries.</summary>
        private void TryAutoEquipFromChest(HeroComponent heroComp, IItem item)
        {
            if (!(item is IGear gear))
                return;

            if (heroComp.LinkedHero == null)
                return;

            if (heroComp.AutoEquipHero)
            {
                if (GearAutoEquipService.TryAutoEquipOnHero(heroComp.LinkedHero, heroComp.Bag, gear))
                {
                    Debug.Log($"[OpenChest] Auto-equipped {gear.Name} on hero");
                    return;
                }
            }

            if (!heroComp.AutoEquipMercenaries)
                return;

            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager == null)
                return;

            var hiredMercenaries = mercenaryManager.GetHiredMercenaries();
            for (int i = 0; i < hiredMercenaries.Count; i++)
            {
                var mercEntity = hiredMercenaries[i];
                var mercComp = mercEntity.GetComponent<MercenaryComponent>();
                if (mercComp?.LinkedMercenary != null)
                {
                    if (GearAutoEquipService.TryAutoEquipOnMercenary(mercComp.LinkedMercenary, heroComp.Bag, gear))
                    {
                        Debug.Log($"[OpenChest] Auto-equipped {gear.Name} on mercenary {mercComp.LinkedMercenary.Name}");
                        return;
                    }
                }
            }
        }
    }
}