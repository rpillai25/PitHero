using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Nez;
using Nez.Textures;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>UI component displaying shortcut slots (y=3, x=0-7) at bottom center of game HUD. Can reference items or skills.</summary>
    public class ShortcutBar : Group
    {
        private const int SHORTCUT_COUNT = 8;
        private const float SLOT_SIZE = 32f;
        private const float SLOT_PADDING = 1f;
        private const float HOVER_OFFSET_Y = -16f;

        // Array of shortcut slot data (can be item reference or skill reference)
        private readonly ShortcutSlotData[] _shortcutSlots;

        // Track the items that are referenced (not the slots) so we can find them when they move
        private readonly IItem[] _referencedItems;

        // Pending shortcut slots to restore after inventory is loaded (set during save/load)
        private List<SavedShortcutSlot> _pendingShortcutSlots;

        // Reference to inventory grid for finding items that moved
        private InventoryGrid _inventoryGrid;

        // Visual slots for rendering the shortcuts (but they don't hold items themselves)
        private readonly FastList<ShortcutSlotVisual> _visualSlots;

        private HeroComponent _heroComponent;

        /// <summary>Shortcut slot index currently showing hover effect during drag.</summary>
        private int _dragHoveredIndex = -1;
        /// <summary>Drag-and-drop overlay for shortcut slot drags.</summary>
        private DragDropOverlay _shortcutDragOverlay;
        /// <summary>Index of the shortcut slot being dragged from.</summary>
        private int _dragSourceIndex = -1;

        // Track scaling for different window modes
        private float _currentScale = 1f;

        // Base position and offset for inventory window
        private float _baseX = 0f;
        private float _baseY = 0f;
        private float _offsetX = 0f;
        private float _slideOffsetY = 0f;

        // Public events for item/skill display
        public event System.Action<IItem> OnItemHovered;
        public event System.Action OnItemUnhovered;
        public event System.Action<IItem> OnItemSelected;
        public event System.Action OnItemDeselected;
        public event System.Action<ISkill> OnSkillHovered;
        public event System.Action OnSkillUnhovered;
        public event System.Action<ISkill> OnSkillSelected;
        public event System.Action OnSkillDeselected;

        public ShortcutBar()
        {
            _shortcutSlots = new ShortcutSlotData[SHORTCUT_COUNT];
            for (int i = 0; i < SHORTCUT_COUNT; i++)
            {
                _shortcutSlots[i] = new ShortcutSlotData();
            }
            _referencedItems = new IItem[SHORTCUT_COUNT];
            _visualSlots = new FastList<ShortcutSlotVisual>(SHORTCUT_COUNT);
            BuildVisualSlots();
            LayoutSlots();
        }

        /// <summary>Builds visual slot components (x=0-7).</summary>
        private void BuildVisualSlots()
        {
            for (int x = 0; x < SHORTCUT_COUNT; x++)
            {
                int index = x; // Capture the loop variable by value
                var slot = new ShortcutSlotVisual(x + 1); // Pass shortcut key (1-8)
                slot.OnSlotHovered += () => HandleSlotHovered(index);
                slot.OnSlotUnhovered += () => HandleSlotUnhovered(index);
                int capturedIndex = index;
                slot.OnDragStarted += (s, pos) => HandleShortcutDragStarted(capturedIndex, s, pos);
                slot.OnDragMoved += (s, pos) => HandleShortcutDragMoved(capturedIndex, s, pos);
                slot.OnDragDropped += (s, pos) => HandleShortcutDragDropped(capturedIndex, s, pos);

                _visualSlots.Add(slot);
                AddElement(slot);
            }
        }

        /// <summary>Positions slot components based on current scale.</summary>
        private void LayoutSlots()
        {
            for (int i = 0; i < _visualSlots.Length; i++)
            {
                var slot = _visualSlots.Buffer[i];
                if (slot == null) continue;

                float scaledSlotSize = SLOT_SIZE * _currentScale;
                float scaledPadding = SLOT_PADDING * _currentScale;
                slot.SetSize(scaledSlotSize, scaledSlotSize);
                slot.Scale = _currentScale;
                slot.SetPosition(i * (scaledSlotSize + scaledPadding), 0);
            }
        }

        /// <summary>Sets the scale of the shortcut bar (1x for Normal, 2x for Half).</summary>
        public void SetShortcutScale(float scale)
        {
            if (System.Math.Abs(_currentScale - scale) < 0.01f)
                return;

            _currentScale = scale;
            LayoutSlots();
        }

        /// <summary>Sets the base position of the shortcut bar.</summary>
        public void SetBasePosition(float x, float y)
        {
            _baseX = x;
            _baseY = y;
            UpdatePosition();
        }

        /// <summary>Sets the horizontal offset (used when inventory is open).</summary>
        public void SetOffsetX(float offsetX)
        {
            _offsetX = offsetX;
            UpdatePosition();
        }

        /// <summary>Sets a vertical slide offset used for the hide/show animation.</summary>
        public void SetSlideOffsetY(float offsetY)
        {
            _slideOffsetY = offsetY;
            UpdatePosition();
        }

        /// <summary>Updates the actual position based on base + offset + slide.</summary>
        private void UpdatePosition()
        {
            SetPosition(_baseX + _offsetX, _baseY + _slideOffsetY);
        }

        /// <summary>Connects shortcut bar to hero and inventory grid.</summary>
        public void ConnectToHero(HeroComponent heroComponent, InventoryGrid inventoryGrid = null)
        {
            _heroComponent = heroComponent;
            _inventoryGrid = inventoryGrid;

            // Subscribe to inventory changes to refresh visual display
            InventorySelectionManager.OnInventoryChanged += RefreshVisualSlots;
        }

        /// <summary>Sets a reference to an InventoryGrid slot at the specified shortcut index.</summary>
        public void SetShortcutReference(int shortcutIndex, InventorySlot referencedSlot)
        {
            if (shortcutIndex < 0 || shortcutIndex >= SHORTCUT_COUNT)
                return;

            _shortcutSlots[shortcutIndex] = ShortcutSlotData.CreateItemReference(referencedSlot);
            _referencedItems[shortcutIndex] = referencedSlot?.SlotData?.Item;

            // Reset HealingItemExhausted if a healing item is moved to shortcut bar
            if (_heroComponent != null && referencedSlot?.SlotData?.Item is Consumable consumable && consumable.HPRestoreAmount > 0)
            {
                _heroComponent.HealingItemExhausted = false;
                Debug.Log($"[ShortcutBar] Reset HealingItemExhausted flag (healing item added to shortcut bar)");
            }

            RefreshVisualSlots();
        }

        /// <summary>Sets a reference to a skill at the specified shortcut index.</summary>
        public void SetShortcutSkill(int shortcutIndex, ISkill skill)
        {
            if (shortcutIndex < 0 || shortcutIndex >= SHORTCUT_COUNT)
                return;

            _shortcutSlots[shortcutIndex] = ShortcutSlotData.CreateSkillReference(skill);
            _referencedItems[shortcutIndex] = null;

            // Reset HealingSkillExhausted if a healing skill is moved to shortcut bar
            if (_heroComponent != null && skill != null && skill.HPRestoreAmount > 0)
            {
                _heroComponent.HealingSkillExhausted = false;
                Debug.Log($"[ShortcutBar] Reset HealingSkillExhausted flag (healing skill added to shortcut bar)");
            }

            RefreshVisualSlots();
        }

        /// <summary>Gets the shortcut slot data at the specified index.</summary>
        public ShortcutSlotData GetShortcutSlotData(int shortcutIndex)
        {
            if (shortcutIndex < 0 || shortcutIndex >= SHORTCUT_COUNT)
                return null;

            return _shortcutSlots[shortcutIndex];
        }

        /// <summary>Gets the referenced slot at the specified shortcut index (for backward compatibility).</summary>
        public InventorySlot GetReferencedSlot(int shortcutIndex)
        {
            if (shortcutIndex < 0 || shortcutIndex >= SHORTCUT_COUNT)
                return null;

            return _shortcutSlots[shortcutIndex]?.ReferencedSlot;
        }

        /// <summary>Clears the reference at the specified shortcut index.</summary>
        public void ClearShortcutReference(int shortcutIndex)
        {
            if (shortcutIndex < 0 || shortcutIndex >= SHORTCUT_COUNT)
                return;

            _shortcutSlots[shortcutIndex].Clear();
            _referencedItems[shortcutIndex] = null;
            RefreshVisualSlots();
        }

        /// <summary>Sets pending shortcut slots to restore after inventory is loaded.</summary>
        public void SetPendingShortcutSlots(List<SavedShortcutSlot> slots)
        {
            _pendingShortcutSlots = slots;
        }

        /// <summary>Attempts to restore pending shortcut slots from save data. Called during RefreshVisualSlots.</summary>
        private void TryRestorePendingShortcuts()
        {
            if (_pendingShortcutSlots == null || _heroComponent == null)
                return;

            // Need the inventory grid to find item slots by bag index
            if (_inventoryGrid == null)
                return;

            // Wait until pending inventory items have been restored (they are cleared after restoration in OnAddedToEntity)
            if (_heroComponent.Bag == null || _heroComponent.PendingInventoryItems != null)
                return;

            Debug.Log("[ShortcutBar] Restoring " + _pendingShortcutSlots.Count + " pending shortcut slots");

            // Ensure the inventory grid has synced its slots from the bag.
            // InventoryGrid.ConnectToHero may have been called before the Bag was created
            // (Nez defers OnAddedToEntity), so grid slots may still have null items.
            _inventoryGrid.UpdateItemsFromBag();

            // Capture and clear pending slots before restoration to prevent infinite recursion.
            // SetShortcutReference/SetShortcutSkill → RefreshVisualSlots → TryRestorePendingShortcuts
            var pendingSlots = _pendingShortcutSlots;
            _pendingShortcutSlots = null;

            for (int i = 0; i < pendingSlots.Count && i < SHORTCUT_COUNT; i++)
            {
                var saved = pendingSlots[i];
                if (saved.SlotType == 1) // Item
                {
                    // Find the inventory grid slot that has this bag index
                    var item = _heroComponent.Bag.GetSlotItem(saved.ItemBagIndex);
                    if (item != null)
                    {
                        var inventorySlot = _inventoryGrid.FindSlotContainingItem(item);
                        if (inventorySlot != null)
                        {
                            SetShortcutReference(i, inventorySlot);
                            Debug.Log("[ShortcutBar] Restored shortcut " + i + " as item '" + item.Name + "' at bag index " + saved.ItemBagIndex);
                        }
                        else
                        {
                            Debug.Warn("[ShortcutBar] Could not find inventory slot for item at bag index " + saved.ItemBagIndex);
                        }
                    }
                    else
                    {
                        Debug.Warn("[ShortcutBar] No item at bag index " + saved.ItemBagIndex + " for shortcut " + i);
                    }
                }
                else if (saved.SlotType == 2) // Skill
                {
                    // Find the skill by ID in the hero's learned skills
                    if (_heroComponent.LinkedHero != null && !string.IsNullOrEmpty(saved.SkillId))
                    {
                        ISkill skill = null;
                        if (_heroComponent.LinkedHero.LearnedSkills.TryGetValue(saved.SkillId, out skill))
                        {
                            SetShortcutSkill(i, skill);
                            Debug.Log("[ShortcutBar] Restored shortcut " + i + " as skill '" + saved.SkillId + "'");
                        }
                        else
                        {
                            Debug.Warn("[ShortcutBar] Could not find learned skill '" + saved.SkillId + "' for shortcut " + i);
                        }
                    }
                }
            }
        }

        /// <summary>Refreshes all visual slots to display referenced items or skills.</summary>
        private void RefreshVisualSlots()
        {
            // Try to restore pending shortcuts from save data (deferred until inventory is ready)
            TryRestorePendingShortcuts();

            // First, update slot references to track item movements
            UpdateSlotReferences();

            for (int i = 0; i < SHORTCUT_COUNT; i++)
            {
                var visualSlot = _visualSlots.Buffer[i];
                var shortcutData = _shortcutSlots[i];

                if (visualSlot != null)
                {
                    // Update visual slot based on shortcut type
                    if (shortcutData.SlotType == ShortcutSlotType.Item)
                    {
                        var referencedSlot = shortcutData.ReferencedSlot;
                        visualSlot.SetReferencedItem(referencedSlot?.SlotData?.Item);
                        visualSlot.SetReferencedSkill(null);
                        visualSlot.SetStackCount(
                            referencedSlot?.SlotData?.Item is Consumable consumable ? consumable.StackCount : 0
                        );
                    }
                    else if (shortcutData.SlotType == ShortcutSlotType.Skill)
                    {
                        visualSlot.SetReferencedItem(null);
                        visualSlot.SetReferencedSkill(shortcutData.ReferencedSkill);
                        visualSlot.SetStackCount(0);
                    }
                    else
                    {
                        visualSlot.SetReferencedItem(null);
                        visualSlot.SetReferencedSkill(null);
                        visualSlot.SetStackCount(0);
                    }
                }
            }
        }

        /// <summary>Updates slot references to track item movements in the inventory grid.</summary>
        private void UpdateSlotReferences()
        {
            if (_heroComponent == null || _inventoryGrid == null)
                return;

            // For each shortcut, check if the item moved (only for item shortcuts)
            for (int i = 0; i < SHORTCUT_COUNT; i++)
            {
                if (_shortcutSlots[i].SlotType != ShortcutSlotType.Item)
                    continue;

                var trackedItem = _referencedItems[i];
                if (trackedItem == null)
                    continue;

                var currentSlot = _shortcutSlots[i].ReferencedSlot;

                // Check if the item is a consumable with zero or negative stack count
                if (trackedItem is Consumable consumable && consumable.StackCount <= 0)
                {
                    Debug.Log($"[ShortcutBar] Shortcut {i + 1} item '{trackedItem.Name}' has stack count {consumable.StackCount}, clearing reference");
                    _shortcutSlots[i].Clear();
                    _referencedItems[i] = null;
                    continue;
                }

                // Check if the current slot still has the tracked item
                if (currentSlot?.SlotData?.Item == trackedItem)
                {
                    // Item is still in the same slot, no update needed
                    continue;
                }

                // The item moved! Find its new location
                var newSlot = _inventoryGrid.FindSlotContainingItem(trackedItem);
                if (newSlot != null)
                {
                    Debug.Log($"[ShortcutBar] Shortcut {i + 1} item '{trackedItem.Name}' moved to new slot, updating reference");
                    _shortcutSlots[i].ReferencedSlot = newSlot;
                }
                else
                {
                    // Item was consumed or removed from inventory
                    Debug.Log($"[ShortcutBar] Shortcut {i + 1} item '{trackedItem.Name}' no longer in inventory, clearing reference");
                    _shortcutSlots[i].Clear();
                    _referencedItems[i] = null;
                }
            }
        }

        /// <summary>Swaps the item/skill references between two shortcut slots.</summary>
        private void SwapShortcuts(int indexA, int indexB)
        {
            if (indexA < 0 || indexA >= SHORTCUT_COUNT || indexB < 0 || indexB >= SHORTCUT_COUNT)
                return;

            Debug.Log($"[ShortcutBar] Swapping shortcuts {indexA + 1} and {indexB + 1}");

            // Swap the shortcut data
            var tempData = _shortcutSlots[indexA];
            _shortcutSlots[indexA] = _shortcutSlots[indexB];
            _shortcutSlots[indexB] = tempData;

            // Swap the tracked items (only relevant for item shortcuts)
            var tempItem = _referencedItems[indexA];
            _referencedItems[indexA] = _referencedItems[indexB];
            _referencedItems[indexB] = tempItem;

            // Refresh the visual display
            RefreshVisualSlots();
        }

        private void HandleSlotHovered(int index)
        {
            var shortcutData = _shortcutSlots[index];

            // Invoke appropriate hover event
            if (shortcutData.SlotType == ShortcutSlotType.Item && shortcutData.ReferencedSlot?.SlotData?.Item != null)
                OnItemHovered?.Invoke(shortcutData.ReferencedSlot.SlotData.Item);
            else if (shortcutData.SlotType == ShortcutSlotType.Skill && shortcutData.ReferencedSkill != null)
                OnSkillHovered?.Invoke(shortcutData.ReferencedSkill);
        }

        private void HandleSlotUnhovered(int index)
        {
            var visualSlot = _visualSlots.Buffer[index];
            visualSlot?.SetItemSpriteOffsetY(0f);
            OnItemUnhovered?.Invoke();
        }

        /// <summary>Gets the item referenced by the shortcut slot at the given index, or null if none.</summary>
        private IItem GetShortcutItemAt(int index)
        {
            if (index < 0 || index >= SHORTCUT_COUNT) return null;
            var data = _shortcutSlots[index];
            if (data.SlotType == ShortcutSlotType.Item)
                return data.ReferencedSlot?.SlotData?.Item;
            return null;
        }

        /// <summary>Returns the shortcut slot index at the given stage position, or -1 if none.</summary>
        private int GetShortcutIndexAtStagePosition(Vector2 stagePos)
        {
            for (int i = 0; i < _visualSlots.Length; i++)
            {
                var slot = _visualSlots.Buffer[i];
                if (slot == null) continue;
                var topLeft = slot.LocalToStageCoordinates(Vector2.Zero);
                if (stagePos.X >= topLeft.X && stagePos.X <= topLeft.X + slot.GetWidth() &&
                    stagePos.Y >= topLeft.Y && stagePos.Y <= topLeft.Y + slot.GetHeight())
                    return i;
            }
            return -1;
        }

        /// <summary>Initiates a drag from a shortcut slot.</summary>
        private void HandleShortcutDragStarted(int index, ShortcutSlotVisual slot, Vector2 mousePos)
        {
            var stage = slot.GetStage();
            if (stage == null) return;

            _dragSourceIndex = index;

            if (_shortcutDragOverlay == null)
            {
                _shortcutDragOverlay = new DragDropOverlay();
                stage.AddElement(_shortcutDragOverlay);
                _shortcutDragOverlay.SetVisible(false);
            }
            _shortcutDragOverlay.ToFront();

            SpriteDrawable dragDrawable = null;
            if (Core.Content != null)
            {
                var shortcutData = _shortcutSlots[index];
                try
                {
                    if (shortcutData.SlotType == ShortcutSlotType.Item)
                    {
                        var item = GetShortcutItemAt(index);
                        if (item != null)
                        {
                            var atlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                            var sprite = atlas.GetSprite(item.SpriteName);
                            if (sprite != null)
                                dragDrawable = new SpriteDrawable(sprite);
                        }
                    }
                    else if (shortcutData.SlotType == ShortcutSlotType.Skill)
                    {
                        var skill = shortcutData.ReferencedSkill;
                        if (skill != null)
                        {
                            var skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
                            var sprite = skillsAtlas.GetSprite(skill.Id);
                            if (sprite == null)
                            {
                                var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                                sprite = uiAtlas.GetSprite("SkillIcon1");
                            }
                            if (sprite != null)
                                dragDrawable = new SpriteDrawable(sprite);
                        }
                    }
                }
                catch
                {
                    // Silently ignore sprite load failures
                }
            }

            _shortcutDragOverlay.BeginDrag(dragDrawable);
            slot.SetItemSpriteHidden(true);
        }

        /// <summary>Updates the drag overlay and hover highlights while dragging a shortcut slot.</summary>
        private void HandleShortcutDragMoved(int index, ShortcutSlotVisual slot, Vector2 mousePos)
        {
            var stagePos = slot.LocalToStageCoordinates(mousePos);
            _shortcutDragOverlay?.UpdatePosition(stagePos);

            int targetIndex = GetShortcutIndexAtStagePosition(stagePos);

            if (_dragHoveredIndex >= 0 && _dragHoveredIndex != targetIndex && _dragHoveredIndex < _visualSlots.Length)
            {
                _visualSlots.Buffer[_dragHoveredIndex]?.SetItemSpriteOffsetY(0f);
                _dragHoveredIndex = -1;
            }

            if (targetIndex >= 0 && targetIndex != index && targetIndex < _visualSlots.Length)
            {
                _visualSlots.Buffer[targetIndex]?.SetItemSpriteOffsetY(HOVER_OFFSET_Y);
                _dragHoveredIndex = targetIndex;
            }
        }

        /// <summary>Handles drop from a shortcut slot — swaps with target shortcut, removes if dropped outside, or cancels.</summary>
        private void HandleShortcutDragDropped(int index, ShortcutSlotVisual slot, Vector2 mousePos)
        {
            if (_dragHoveredIndex >= 0 && _dragHoveredIndex < _visualSlots.Length)
            {
                _visualSlots.Buffer[_dragHoveredIndex]?.SetItemSpriteOffsetY(0f);
                _dragHoveredIndex = -1;
            }

            var stagePos = slot.LocalToStageCoordinates(mousePos);
            int targetIndex = GetShortcutIndexAtStagePosition(stagePos);

            slot.SetItemSpriteHidden(false);

            if (targetIndex >= 0 && targetIndex != index)
            {
                // Dropped onto a different shortcut slot — swap
                SwapShortcuts(index, targetIndex);
            }
            else if (targetIndex < 0)
            {
                // Dropped outside all shortcut slots — remove from shortcut bar
                ClearShortcutReference(index);
            }
            // Dropped on own slot — no-op (restore already done above)

            _shortcutDragOverlay?.EndDrag();
            _dragSourceIndex = -1;
        }

        /// <summary>Subscribes to InventoryDragManager to handle inventory-to-shortcut and skill-list-to-shortcut drops.</summary>
        public void ConnectToDragManager()
        {
            InventoryDragManager.OnDropRequested += HandleInventoryDropOnShortcut;
            InventoryDragManager.OnSkillDropRequested += HandleSkillDropOnShortcut;
        }

        /// <summary>
        /// Handles an inventory item dropped onto a shortcut slot.
        /// Only consumables are accepted; other item types are rejected.
        /// </summary>
        private void HandleInventoryDropOnShortcut(InventorySlot inventorySource, Vector2 stagePos)
        {
            int index = GetShortcutIndexAtStagePosition(stagePos);
            if (index < 0)
                return;

            var item = inventorySource?.SlotData?.Item;
            if (item is not Consumable)
            {
                // Non-consumable items cannot be placed on the shortcut bar — cancel drag
                InventoryDragManager.CancelDrag();
                return;
            }

            SetShortcutReference(index, inventorySource);
            InventoryDragManager.EndDrag();
        }

        /// <summary>Handles a skill dragged from the hero skill list dropped onto a shortcut slot.</summary>
        private void HandleSkillDropOnShortcut(ISkill skill, Vector2 stagePos)
        {
            int index = GetShortcutIndexAtStagePosition(stagePos);
            if (index < 0)
            {
                InventoryDragManager.CancelDrag();
                return;
            }

            SetShortcutSkill(index, skill);
            InventoryDragManager.EndDrag();
        }

        /// <summary>
        /// Uses a consumable item from the referenced inventory slot.
        /// When <see cref="HeroComponent.UseConsumablesOnMercenaries"/> is enabled and the
        /// hero is not in battle, targets the most critical party member instead of always
        /// targeting the hero. Mirrors the targeting logic of <see cref="PitHero.AI.UseHealingItemAction"/>.
        /// </summary>
        private void UseConsumable(IItem item, int bagIndex)
        {
            if (item is not Consumable consumable)
                return;

            var hero = _heroComponent?.LinkedHero;
            if (hero == null)
            {
                Debug.Log($"[ShortcutBar] Cannot use {item.Name}: No hero linked");
                return;
            }

            // Check if hero is in battle
            bool inBattle = PitHero.AI.HeroStateMachine.IsBattleInProgress;

            // If in battle, queue the action
            if (inBattle)
            {
                Debug.Log($"[ShortcutBar] Queueing {item.Name} for battle");
                if (!_heroComponent.BattleActionQueue.EnqueueItem(consumable, bagIndex))
                {
                    Debug.Log($"[ShortcutBar] Cannot queue {item.Name}: Queue is full");
                }
                return;
            }

            // Not in battle - check if item is battle-only
            if (consumable.BattleOnly)
            {
                Debug.Log($"[ShortcutBar] Cannot use {item.Name}: Item is battle-only");
                return;
            }

            // Determine the target: most critical party member when the option is enabled,
            // otherwise always the hero.
            object target = hero;
            if (_heroComponent.UseConsumablesOnMercenaries)
            {
                target = FindMostCriticalTargetForConsumable(consumable);
            }

            // Try to consume the item immediately (out of battle)
            if (consumable.Consume(target))
            {
                string targetName = target is RolePlayingFramework.Heroes.Hero h
                    ? h.Name
                    : ((RolePlayingFramework.Mercenaries.Mercenary)target).Name;
                Debug.Log($"[ShortcutBar] Used {item.Name} on {targetName}");

                // Reset HealingSkillExhausted if MP restoration item is used
                if (_heroComponent != null && consumable.MPRestoreAmount > 0)
                {
                    _heroComponent.HealingSkillExhausted = false;
                    Debug.Log($"[ShortcutBar] Reset HealingSkillExhausted flag (MP restoration item used)");
                }

                // Decrement stack or remove item from main inventory bag
                if (_heroComponent.Bag.ConsumeFromStack(bagIndex))
                {
                    // Refresh the visual slots
                    RefreshVisualSlots();

                    // Notify inventory changed so InventoryGrid also refreshes
                    InventorySelectionManager.OnInventoryChanged?.Invoke();
                }
            }
            else
            {
                Debug.Log($"[ShortcutBar] Failed to use {item.Name}");
            }
        }

        /// <summary>
        /// Finds the party member who can benefit most from the given consumable.
        /// Unlike the AI path, this does not use criticality thresholds — the player has
        /// already chosen to use the item. Selects whoever has the most missing HP (for HP
        /// potions), most missing MP (for MP potions), or lowest min(HP%,MP%) (for mix
        /// potions), restricted to targets the potion can actually help. Falls back to the
        /// hero if nobody has any missing resources of the relevant type.
        /// </summary>
        private object FindMostCriticalTargetForConsumable(Consumable consumable)
        {
            bool restoresHP = consumable.HPRestoreAmount != 0;
            bool restoresMP = consumable.MPRestoreAmount != 0;

            object bestTarget = null;
            float lowestPercent = 1f;

            // Evaluate hero
            var heroObj = _heroComponent.LinkedHero;
            if (heroObj != null)
            {
                float hpPct = heroObj.MaxHP > 0 ? (float)heroObj.CurrentHP / heroObj.MaxHP : 1f;
                float mpPct = heroObj.MaxMP > 0 ? (float)heroObj.CurrentMP / heroObj.MaxMP : 1f;
                bool canBenefit = (restoresHP && hpPct < 1f) || (restoresMP && mpPct < 1f);
                if (canBenefit)
                {
                    float relevantPct = RelevantPercent(hpPct, mpPct, restoresHP, restoresMP);
                    if (relevantPct < lowestPercent)
                    {
                        bestTarget = heroObj;
                        lowestPercent = relevantPct;
                    }
                }
            }

            // Evaluate each hired mercenary
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager != null)
            {
                var hiredMercenaries = mercenaryManager.GetHiredMercenaries();
                for (int i = 0; i < hiredMercenaries.Count; i++)
                {
                    var merc = hiredMercenaries[i];
                    var mercComp = merc.GetComponent<MercenaryComponent>();
                    if (mercComp?.LinkedMercenary == null)
                        continue;

                    var mercenary = mercComp.LinkedMercenary;
                    float hpPct = mercenary.MaxHP > 0 ? (float)mercenary.CurrentHP / mercenary.MaxHP : 1f;
                    float mpPct = mercenary.MaxMP > 0 ? (float)mercenary.CurrentMP / mercenary.MaxMP : 1f;
                    bool canBenefit = (restoresHP && hpPct < 1f) || (restoresMP && mpPct < 1f);
                    if (canBenefit)
                    {
                        float relevantPct = RelevantPercent(hpPct, mpPct, restoresHP, restoresMP);
                        if (relevantPct < lowestPercent)
                        {
                            bestTarget = mercenary;
                            lowestPercent = relevantPct;
                        }
                    }
                }
            }

            // Fall back to hero — Consume() will return false gracefully if at full HP/MP
            return bestTarget ?? _heroComponent.LinkedHero;
        }

        /// <summary>Returns the percentage most relevant to a potion's restore type.</summary>
        private static float RelevantPercent(float hpPct, float mpPct, bool restoresHP, bool restoresMP)
        {
            if (restoresHP && restoresMP)
                return System.Math.Min(hpPct, mpPct);
            if (restoresHP)
                return hpPct;
            return mpPct;
        }

        /// <summary>Uses a skill from the shortcut bar.</summary>
        private void UseSkill(ISkill skill)
        {
            if (skill == null)
                return;

            var hero = _heroComponent?.LinkedHero;
            if (hero == null)
            {
                Debug.Log($"[ShortcutBar] Cannot use {skill.Name}: No hero linked");
                return;
            }

            // Check if hero is in battle
            bool inBattle = PitHero.AI.HeroStateMachine.IsBattleInProgress;

            // If in battle, queue the action
            if (inBattle)
            {
                Debug.Log($"[ShortcutBar] Queueing skill {skill.Name} for battle");
                if (!_heroComponent.BattleActionQueue.EnqueueSkill(skill))
                {
                    Debug.Log($"[ShortcutBar] Cannot queue {skill.Name}: Queue is full");
                }
                return;
            }

            // Not in battle - check if skill is battle-only
            if (skill.BattleOnly)
            {
                Debug.Log($"[ShortcutBar] Cannot use {skill.Name}: Skill is battle-only");
                return;
            }

            // For non-battle skills used outside of battle, we would need to implement immediate execution
            // This depends on the specific skill's Execute method and requires a context (no enemies)
            Debug.Log($"[ShortcutBar] Cannot use {skill.Name} outside of battle (not yet implemented)");
        }

        /// <summary>Public method to refresh visual slots (called externally when inventory changes).</summary>
        public void RefreshItems()
        {
            RefreshVisualSlots();
        }

        /// <summary>Handles shortcut key presses (1-8).</summary>
        public void HandleKeyboardShortcuts()
        {
            for (int keyOffset = 0; keyOffset < SHORTCUT_COUNT; keyOffset++)
            {
                var key = (Keys)((int)Keys.D1 + keyOffset);
                if (!Input.IsKeyPressed(key)) continue;

                var shortcutData = _shortcutSlots[keyOffset];

                // Handle item shortcuts
                if (shortcutData.SlotType == ShortcutSlotType.Item)
                {
                    var referencedSlot = shortcutData.ReferencedSlot;
                    if (referencedSlot?.SlotData?.Item != null && referencedSlot.SlotData.BagIndex.HasValue)
                    {
                        Debug.Log($"[ShortcutBar] Activated shortcut slot {keyOffset + 1} with item: {referencedSlot.SlotData.Item.Name}");

                        // Use the consumable if it's a consumable
                        if (referencedSlot.SlotData.Item is Consumable)
                        {
                            UseConsumable(referencedSlot.SlotData.Item, referencedSlot.SlotData.BagIndex.Value);
                        }
                    }
                }
                // Handle skill shortcuts
                else if (shortcutData.SlotType == ShortcutSlotType.Skill)
                {
                    var skill = shortcutData.ReferencedSkill;
                    if (skill != null)
                    {
                        Debug.Log($"[ShortcutBar] Activated shortcut slot {keyOffset + 1} with skill: {skill.Name}");
                        UseSkill(skill);
                    }
                }

                break;
            }
        }
    }

    /// <summary>Visual representation of a shortcut slot that displays a referenced item or skill.</summary>
    public class ShortcutSlotVisual : Element, IInputListener
    {
        private readonly int _shortcutKey;
        private Sprite _backgroundSprite;
        private SpriteDrawable _backgroundDrawable;
        private Sprite _selectBoxSprite;
        private SpriteDrawable _selectBoxDrawable;
        private Sprite _highlightBoxSprite;
        private SpriteDrawable _highlightBoxDrawable;
        private Nez.BitmapFonts.BitmapFont _font;

        private IItem _referencedItem;
        private ISkill _referencedSkill;
        private int _stackCount;
        private bool _isHovered;
        private bool _isHighlighted;
        private float _itemSpriteOffsetY = 0f;

        // Drag detection
        private bool _mouseDown;
        private Vector2 _mousePressPos;
        private bool _isDraggingItem;
        private bool _hideItemSprite = false;

        public event System.Action OnSlotHovered;
        public event System.Action OnSlotUnhovered;

        public event System.Action<ShortcutSlotVisual, Vector2> OnDragStarted;
        public event System.Action<ShortcutSlotVisual, Vector2> OnDragMoved;
        public event System.Action<ShortcutSlotVisual, Vector2> OnDragDropped;

        public float Scale { get; set; } = 1f;

        public ShortcutSlotVisual(int shortcutKey)
        {
            _shortcutKey = shortcutKey;

            // Load visual assets
            if (Core.Content != null)
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

                _backgroundSprite = itemsAtlas.GetSprite("Shortcut");
                _backgroundDrawable = new SpriteDrawable(_backgroundSprite);

                _selectBoxSprite = uiAtlas.GetSprite("SelectBox");
                _selectBoxDrawable = new SpriteDrawable(_selectBoxSprite);

                _highlightBoxSprite = uiAtlas.GetSprite("HighlightBox");
                _highlightBoxDrawable = new SpriteDrawable(_highlightBoxSprite);

                try
                {
                    _font = Core.Content.LoadBitmapFont(GameConfig.FontPathHudSmall);
                }
                catch
                {
                    _font = Graphics.Instance.BitmapFont;
                }
            }

            SetSize(32f, 32f);
            SetTouchable(Touchable.Enabled);
        }

        public void SetReferencedItem(IItem item)
        {
            _referencedItem = item;
        }

        public void SetReferencedSkill(ISkill skill)
        {
            _referencedSkill = skill;
        }

        public void SetStackCount(int stackCount)
        {
            _stackCount = stackCount;
        }

        public void SetHighlighted(bool highlighted)
        {
            _isHighlighted = highlighted;
        }

        /// <summary>Sets the item sprite Y offset for visual effects (like hover).</summary>
        public void SetItemSpriteOffsetY(float offsetY)
        {
            _itemSpriteOffsetY = offsetY;
        }

        /// <summary>Shows or hides the item sprite without removing the item from the slot data.</summary>
        public void SetItemSpriteHidden(bool hidden)
        {
            _hideItemSprite = hidden;
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            // Draw background
            if (_backgroundDrawable != null)
            {
                _backgroundDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), new Color(255, 255, 255, 100));
            }

            // Draw referenced item sprite if exists
            if (_referencedItem != null && Core.Content != null && !_hideItemSprite)
            {
                try
                {
                    var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                    var itemSprite = itemsAtlas.GetSprite(_referencedItem.SpriteName);
                    if (itemSprite != null)
                    {
                        var itemDrawable = new SpriteDrawable(itemSprite);
                        itemDrawable.Draw(batcher, GetX(), GetY() + _itemSpriteOffsetY, GetWidth(), GetHeight(), Color.White);
                    }
                }
                catch
                {
                    // Silently ignore missing sprites
                }

                // Draw stack count if applicable
                if (_stackCount > 1 && _font != null)
                {
                    var stackText = _stackCount.ToString();
                    var textPosition = new Vector2(GetX() + 2f * Scale, GetY() + _itemSpriteOffsetY + GetHeight() - _font.LineHeight * Scale + 2f * Scale);
                    batcher.DrawString(_font, stackText, textPosition, Color.White, 0f, Vector2.Zero, Scale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
                }
            }
            // Draw referenced skill sprite if exists (also hidden during drag)
            else if (_referencedSkill != null && Core.Content != null && !_hideItemSprite)
            {
                try
                {
                    var skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
                    var skillSprite = skillsAtlas.GetSprite(_referencedSkill.Id);
                    if (skillSprite != null)
                    {
                        var skillDrawable = new SpriteDrawable(skillSprite);
                        skillDrawable.Draw(batcher, GetX(), GetY() + _itemSpriteOffsetY, GetWidth(), GetHeight(), Color.White);
                    }
                }
                catch
                {
                    // Silently ignore missing sprites
                }
            }

            // Draw select box if hovered
            if (_isHovered && _selectBoxDrawable != null)
            {
                _selectBoxDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            }

            // Draw highlight box if highlighted
            if (_isHighlighted && _highlightBoxDrawable != null)
            {
                _highlightBoxDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            }

            // Draw shortcut key number below slot
            if (_font != null)
            {
                var keyText = _shortcutKey.ToString();
                var textSize = _font.MeasureString(keyText) * Scale;
                var textX = GetX() + (GetWidth() - textSize.X) / 2f;
                var textY = GetY() + GetHeight() + 2f * Scale;
                batcher.DrawString(_font, keyText, new Vector2(textX, textY), Color.Goldenrod, 0f, Vector2.Zero, Scale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0f);
            }

            base.Draw(batcher, parentAlpha);
        }

        #region IInputListener Implementation

        void IInputListener.OnMouseEnter()
        {
            _isHovered = true;
            OnSlotHovered?.Invoke();
        }

        void IInputListener.OnMouseExit()
        {
            _isHovered = false;
            OnSlotUnhovered?.Invoke();
        }

        void IInputListener.OnMouseMoved(Vector2 mousePos)
        {
            if (!_mouseDown) return;
            if (_referencedItem == null && _referencedSkill == null) return;

            if (!_isDraggingItem)
            {
                float dx = mousePos.X - _mousePressPos.X;
                float dy = mousePos.Y - _mousePressPos.Y;
                float distSq = dx * dx + dy * dy;
                float threshold = GameConfig.DragThresholdPixels;
                if (distSq >= threshold * threshold)
                {
                    _isDraggingItem = true;
                    OnDragStarted?.Invoke(this, mousePos);
                }
            }

            if (_isDraggingItem)
                OnDragMoved?.Invoke(this, mousePos);
        }

        bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
        {
            _mouseDown = true;
            _mousePressPos = mousePos;
            _isDraggingItem = false;
            return true;
        }

        bool IInputListener.OnRightMousePressed(Vector2 mousePos)
        {
            // Right-click clears the reference
            // We need to access the parent ShortcutBar for this, but for now we'll handle it differently
            return false;
        }

        void IInputListener.OnLeftMouseUp(Vector2 mousePos)
        {
            bool wasDragging = _isDraggingItem;
            _mouseDown = false;
            _isDraggingItem = false;

            if (wasDragging)
            {
                OnDragDropped?.Invoke(this, mousePos);
            }
        }

        void IInputListener.OnRightMouseUp(Vector2 mousePos)
        {
        }

        bool IInputListener.OnMouseScrolled(int mouseWheelDelta)
        {
            return false;
        }

        #endregion
    }
}
