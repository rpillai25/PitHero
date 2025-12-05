using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Nez;
using Nez.Textures;
using Nez.UI;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;

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
        
        // Reference to inventory grid for finding items that moved
        private InventoryGrid _inventoryGrid;
        
        // Visual slots for rendering the shortcuts (but they don't hold items themselves)
        private readonly FastList<ShortcutSlotVisual> _visualSlots;
        
        private HeroComponent _heroComponent;
        private int _highlightedIndex = -1; // Index of highlighted shortcut (-1 = none)
        
        // Track scaling for different window modes
        private float _currentScale = 1f;
        
        // Base position and offset for inventory window
        private float _baseX = 0f;
        private float _baseY = 0f;
        private float _offsetX = 0f;
        
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
                slot.OnSlotClicked += () => HandleSlotClicked(index);
                slot.OnSlotDoubleClicked += () => HandleSlotDoubleClicked(index);
                slot.OnSlotHovered += () => HandleSlotHovered(index);
                slot.OnSlotUnhovered += () => HandleSlotUnhovered(index);
                
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
        
        /// <summary>Updates the actual position based on base + offset.</summary>
        private void UpdatePosition()
        {
            SetPosition(_baseX + _offsetX, _baseY);
        }
        
        /// <summary>Connects shortcut bar to hero and inventory grid.</summary>
        public void ConnectToHero(HeroComponent heroComponent, InventoryGrid inventoryGrid = null)
        {
            _heroComponent = heroComponent;
            _inventoryGrid = inventoryGrid;
            
            // Subscribe to inventory changes to refresh visual display
            InventorySelectionManager.OnInventoryChanged += RefreshVisualSlots;
            
            // Subscribe to selection cleared event
            InventorySelectionManager.OnSelectionCleared += ClearLocalSelectionState;
        }
        
        /// <summary>Sets a reference to an InventoryGrid slot at the specified shortcut index.</summary>
        public void SetShortcutReference(int shortcutIndex, InventorySlot referencedSlot)
        {
            if (shortcutIndex < 0 || shortcutIndex >= SHORTCUT_COUNT)
                return;
                
            _shortcutSlots[shortcutIndex] = ShortcutSlotData.CreateItemReference(referencedSlot);
            _referencedItems[shortcutIndex] = referencedSlot?.SlotData?.Item;
            RefreshVisualSlots();
        }
        
        /// <summary>Sets a reference to a skill at the specified shortcut index.</summary>
        public void SetShortcutSkill(int shortcutIndex, ISkill skill)
        {
            if (shortcutIndex < 0 || shortcutIndex >= SHORTCUT_COUNT)
                return;
                
            _shortcutSlots[shortcutIndex] = ShortcutSlotData.CreateSkillReference(skill);
            _referencedItems[shortcutIndex] = null;
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
        
        /// <summary>Refreshes all visual slots to display referenced items or skills.</summary>
        private void RefreshVisualSlots()
        {
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
        
        /// <summary>Clears only the local highlighted slot without invoking events.</summary>
        private void ClearLocalSelectionState()
        {
            if (_highlightedIndex >= 0)
            {
                var visualSlot = _visualSlots.Buffer[_highlightedIndex];
                if (visualSlot != null)
                    visualSlot.SetHighlighted(false);
                _highlightedIndex = -1;
            }
        }
        
        /// <summary>Handles slot click highlighting.</summary>
        private void HandleSlotClicked(int index)
        {
            // Check if there's a cross-component selection (from InventoryGrid or HeroCrystalTab)
            if (InventorySelectionManager.HasSelection() && !InventorySelectionManager.IsSelectionFromShortcutBar())
            {
                // Check if it's a skill from HeroCrystalTab
                if (InventorySelectionManager.IsSelectionFromHeroCrystalTab())
                {
                    var selectedSkill = InventorySelectionManager.GetSelectedSkill();
                    if (selectedSkill != null)
                    {
                        // Set this shortcut to reference that skill
                        SetShortcutSkill(index, selectedSkill);
                        
                        // Clear the selection
                        InventorySelectionManager.ClearSelection();
                        OnSkillDeselected?.Invoke();
                        return;
                    }
                }
                else
                {
                    // Get the selected inventory slot
                    var inventorySlot = InventorySelectionManager.GetSelectedSlot();
                    
                    // Set this shortcut to reference that inventory slot
                    SetShortcutReference(index, inventorySlot);
                    
                    // Clear the selection
                    InventorySelectionManager.ClearSelection();
                    OnItemDeselected?.Invoke();
                    return;
                }
            }
            
            // Handle shortcut-to-shortcut interaction (swapping)
            if (_highlightedIndex >= 0 && _highlightedIndex != index)
            {
                // We have a selected shortcut and clicked a different one - swap them
                SwapShortcuts(_highlightedIndex, index);
                
                // Clear the highlight
                var oldVisualSlot = _visualSlots.Buffer[_highlightedIndex];
                if (oldVisualSlot != null)
                    oldVisualSlot.SetHighlighted(false);
                _highlightedIndex = -1;
                
                InventorySelectionManager.ClearSelection();
                OnItemDeselected?.Invoke();
                OnSkillDeselected?.Invoke();
                return;
            }
            
            // Toggle highlight on the shortcut itself
            if (_highlightedIndex == -1)
            {
                _highlightedIndex = index;
                var visualSlot = _visualSlots.Buffer[index];
                if (visualSlot != null)
                {
                    visualSlot.SetHighlighted(true);
                    InventorySelectionManager.SetSelectedFromShortcut(index, _heroComponent);
                    
                    var shortcutData = _shortcutSlots[index];
                    if (shortcutData.SlotType == ShortcutSlotType.Item && shortcutData.ReferencedSlot?.SlotData?.Item != null)
                        OnItemSelected?.Invoke(shortcutData.ReferencedSlot.SlotData.Item);
                    else if (shortcutData.SlotType == ShortcutSlotType.Skill && shortcutData.ReferencedSkill != null)
                        OnSkillSelected?.Invoke(shortcutData.ReferencedSkill);
                }
            }
            else if (_highlightedIndex == index)
            {
                // Clicking the same slot clears the highlight
                var visualSlot = _visualSlots.Buffer[_highlightedIndex];
                if (visualSlot != null)
                    visualSlot.SetHighlighted(false);
                _highlightedIndex = -1;
                InventorySelectionManager.ClearSelection();
                OnItemDeselected?.Invoke();
                OnSkillDeselected?.Invoke();
            }
            else
            {
                // Clicking a different shortcut - just move the highlight
                var oldVisualSlot = _visualSlots.Buffer[_highlightedIndex];
                if (oldVisualSlot != null)
                    oldVisualSlot.SetHighlighted(false);
                    
                _highlightedIndex = index;
                var newVisualSlot = _visualSlots.Buffer[index];
                if (newVisualSlot != null)
                {
                    newVisualSlot.SetHighlighted(true);
                    InventorySelectionManager.SetSelectedFromShortcut(index, _heroComponent);
                    
                    var shortcutData = _shortcutSlots[index];
                    if (shortcutData.SlotType == ShortcutSlotType.Item && shortcutData.ReferencedSlot?.SlotData?.Item != null)
                        OnItemSelected?.Invoke(shortcutData.ReferencedSlot.SlotData.Item);
                    else if (shortcutData.SlotType == ShortcutSlotType.Skill && shortcutData.ReferencedSkill != null)
                        OnSkillSelected?.Invoke(shortcutData.ReferencedSkill);
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
        
        /// <summary>Handles double-click to use consumables or skills.</summary>
        private void HandleSlotDoubleClicked(int index)
        {
            var shortcutData = _shortcutSlots[index];
            
            // Handle item double-click
            if (shortcutData.SlotType == ShortcutSlotType.Item)
            {
                var referencedSlot = shortcutData.ReferencedSlot;
                if (referencedSlot?.SlotData?.Item == null || !referencedSlot.SlotData.BagIndex.HasValue)
                    return;
                    
                var item = referencedSlot.SlotData.Item;
                var bagIndex = referencedSlot.SlotData.BagIndex.Value;
                
                // Only consumables can be used from shortcut bar
                if (item is Consumable)
                {
                    UseConsumable(item, bagIndex);
                }
            }
            // Handle skill double-click (same as pressing the shortcut key)
            else if (shortcutData.SlotType == ShortcutSlotType.Skill)
            {
                UseSkill(shortcutData.ReferencedSkill);
            }
        }
        
        private void HandleSlotHovered(int index)
        {
            var visualSlot = _visualSlots.Buffer[index];
            var shortcutData = _shortcutSlots[index];
            
            // Show hover effect if there's a cross-component selection or local highlight
            bool hasContent = (shortcutData.SlotType == ShortcutSlotType.Item && shortcutData.ReferencedSlot?.SlotData?.Item != null) ||
                              (shortcutData.SlotType == ShortcutSlotType.Skill && shortcutData.ReferencedSkill != null);
            
            if ((InventorySelectionManager.HasSelection() && hasContent) || 
                (_highlightedIndex >= 0 && _highlightedIndex != index && hasContent))
            {
                visualSlot?.SetItemSpriteOffsetY(HOVER_OFFSET_Y);
            }
            
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
        
        /// <summary>Uses a consumable item from the referenced inventory slot.</summary>
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

            // Try to consume the item immediately (out of battle)
            if (consumable.Consume(hero))
            {
                Debug.Log($"[ShortcutBar] Used {item.Name}");
                
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
        
        /// <summary>Public method to clear selection state (called when closing inventory UI).</summary>
        public void ClearSelection()
        {
            // Just call the manager's clear - it will notify this component via callback
            InventorySelectionManager.ClearSelection();
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
        
        // Double-click detection
        private float _lastClickTime = -1f;
        
        public event System.Action OnSlotClicked;
        public event System.Action OnSlotDoubleClicked;
        public event System.Action OnSlotHovered;
        public event System.Action OnSlotUnhovered;
        
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
                    _font = Core.Content.LoadBitmapFont("Content/Fonts/HudSmall.fnt");
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
        
        public void SetItemSpriteOffsetY(float offsetY)
        {
            _itemSpriteOffsetY = offsetY;
        }
        
        public override void Draw(Batcher batcher, float parentAlpha)
        {
            // Draw background
            if (_backgroundDrawable != null)
            {
                _backgroundDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), new Color(255, 255, 255, 100));
            }
            
            // Draw referenced item sprite if exists
            if (_referencedItem != null && Core.Content != null)
            {
                try
                {
                    var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                    var itemSprite = itemsAtlas.GetSprite(_referencedItem.Name);
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
            // Draw referenced skill sprite if exists
            else if (_referencedSkill != null && Core.Content != null)
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
        }
        
        bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
        {
            float currentTime = Time.TotalTime;
            if (_lastClickTime >= 0 && (currentTime - _lastClickTime) <= GameConfig.DoubleClickThresholdSeconds)
            {
                OnSlotDoubleClicked?.Invoke();
                _lastClickTime = -1f;
            }
            else
            {
                OnSlotClicked?.Invoke();
                _lastClickTime = currentTime;
            }
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
