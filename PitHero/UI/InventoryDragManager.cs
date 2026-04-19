using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Skills;

namespace PitHero.UI
{
    /// <summary>
    /// Manages global drag-and-drop state for inventory, equip, shortcut, and skill-list slots.
    /// Works alongside InventorySelectionManager for cross-component coordination.
    /// </summary>
    public static class InventoryDragManager
    {
        private static bool _isDragging;
        private static InventorySlot _sourceSlot;
        private static IItem _dragItem;
        private static ISkill _dragSkill;
        private static CrystalSlotElement _sourceCrystalSlot;
        private static HeroCrystal _dragCrystal;
        private static DragDropOverlay _overlay;
        private static Stage _stage;
        private static SecondChanceMerchantVault.StackedItem _sourceVaultStack;
        private static HeroCrystal _sourceVaultCrystal;

        /// <summary>Gets whether a drag operation is currently in progress.</summary>
        public static bool IsDragging => _isDragging;

        /// <summary>Gets the inventory slot that the drag originated from.</summary>
        public static InventorySlot SourceSlot => _sourceSlot;

        /// <summary>Gets the item being dragged (null when dragging a skill).</summary>
        public static IItem DragItem => _dragItem;

        /// <summary>Gets the skill being dragged from the skill list (null when dragging an item).</summary>
        public static ISkill DragSkill => _dragSkill;

        /// <summary>Gets the crystal slot element that the drag originated from.</summary>
        public static CrystalSlotElement SourceCrystalSlot => _sourceCrystalSlot;

        /// <summary>Gets the crystal being dragged (null when dragging an item or skill).</summary>
        public static HeroCrystal DragCrystal => _dragCrystal;

        /// <summary>Gets the vault stack that is being dragged (null when not a vault item drag).</summary>
        public static SecondChanceMerchantVault.StackedItem SourceVaultStack => _sourceVaultStack;

        /// <summary>Gets whether the current drag originated from the Second Chance vault (item).</summary>
        public static bool IsVaultItemDrag => _sourceVaultStack != null;

        /// <summary>Gets the vault crystal that is being dragged (null when not a vault crystal drag).</summary>
        public static HeroCrystal SourceVaultCrystal => _sourceVaultCrystal;

        /// <summary>Gets whether the current drag originated from the Second Chance vault (crystal).</summary>
        public static bool IsVaultCrystalDrag => _sourceVaultCrystal != null;

        /// <summary>
        /// Fired when the source component found no local drop target for an item drag.
        /// ShortcutBar subscribes to handle inventory-to-shortcut drops.
        /// </summary>
        public static System.Action<InventorySlot, Vector2> OnDropRequested;

        /// <summary>
        /// Fired when a skill was dragged from the skill list with no local drop target.
        /// ShortcutBar subscribes to handle skill-list-to-shortcut drops.
        /// </summary>
        public static System.Action<ISkill, Vector2> OnSkillDropRequested;

        /// <summary>Begins a drag from a Second Chance vault item slot.</summary>
        public static void BeginVaultItemDrag(SecondChanceMerchantVault.StackedItem source, Stage stage)
        {
            if (_isDragging) return;

            _sourceVaultStack = source;
            _sourceSlot = null;
            _dragItem = source?.ItemTemplate;
            _dragSkill = null;
            _sourceCrystalSlot = null;
            _dragCrystal = null;
            _stage = stage;
            _isDragging = true;

            EnsureOverlay(stage);

            if (_dragItem != null && Core.Content != null)
            {
                try
                {
                    var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                    var sprite = itemsAtlas.GetSprite(_dragItem.SpriteName);
                    if (sprite != null)
                        _overlay.BeginDrag(new SpriteDrawable(sprite));
                    else
                        _overlay.BeginDrag(null);
                }
                catch
                {
                    _overlay.BeginDrag(null);
                }
            }
        }

        /// <summary>Begins a drag from a Second Chance vault crystal slot.</summary>
        public static void BeginVaultCrystalDrag(HeroCrystal crystal, Stage stage)
        {
            if (_isDragging) return;

            _sourceVaultCrystal = crystal;
            _sourceVaultStack = null;
            _sourceSlot = null;
            _dragItem = null;
            _dragSkill = null;
            _sourceCrystalSlot = null;
            _dragCrystal = crystal;
            _stage = stage;
            _isDragging = true;

            EnsureOverlay(stage);

            if (crystal != null && Core.Content != null)
            {
                try
                {
                    var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                    var sprite = itemsAtlas.GetSprite("HeroCrystal");
                    if (sprite != null)
                        _overlay.BeginDrag(new SpriteDrawable(sprite), crystal.Color);
                    else
                        _overlay.BeginDrag(null);
                }
                catch
                {
                    _overlay.BeginDrag(null);
                }
            }
        }

        /// <summary>Begins a drag from a crystal slot.</summary>
        public static void BeginCrystalDrag(CrystalSlotElement source, Stage stage)
        {
            if (_isDragging) return;

            _sourceCrystalSlot = source;
            _dragCrystal = source?.Crystal;
            _sourceSlot = null;
            _dragItem = null;
            _dragSkill = null;
            _stage = stage;
            _isDragging = true;

            EnsureOverlay(stage);

            if (_dragCrystal != null && Core.Content != null)
            {
                try
                {
                    var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                    var sprite = itemsAtlas.GetSprite("HeroCrystal");
                    if (sprite != null)
                        _overlay.BeginDrag(new SpriteDrawable(sprite), _dragCrystal.Color);
                    else
                        _overlay.BeginDrag(null);
                }
                catch
                {
                    _overlay.BeginDrag(null);
                }
            }
        }

        /// <summary>Begins a drag from an inventory slot.</summary>
        public static void BeginDrag(InventorySlot source, Stage stage)
        {
            if (_isDragging) return;

            _sourceSlot = source;
            _dragItem = source?.SlotData?.Item;
            _dragSkill = null;
            _stage = stage;
            _isDragging = true;

            EnsureOverlay(stage);

            if (_dragItem != null && Core.Content != null)
            {
                try
                {
                    var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                    var sprite = itemsAtlas.GetSprite(_dragItem.SpriteName);
                    if (sprite != null)
                        _overlay.BeginDrag(new SpriteDrawable(sprite));
                    else
                        _overlay.BeginDrag(null);
                }
                catch
                {
                    _overlay.BeginDrag(null);
                }
            }
        }

        /// <summary>Begins a drag from the hero skill list (not from a shortcut slot).</summary>
        public static void BeginSkillDrag(ISkill skill, Stage stage)
        {
            if (_isDragging) return;

            _sourceSlot = null;
            _dragItem = null;
            _dragSkill = skill;
            _stage = stage;
            _isDragging = true;

            EnsureOverlay(stage);

            if (skill != null && Core.Content != null)
            {
                try
                {
                    var skillsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/SkillsStencils.atlas");
                    var sprite = skillsAtlas.GetSprite(skill.Id);
                    if (sprite == null)
                    {
                        var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                        sprite = uiAtlas.GetSprite("SkillIcon1");
                    }
                    if (sprite != null)
                        _overlay.BeginDrag(new SpriteDrawable(sprite));
                    else
                        _overlay.BeginDrag(null);
                }
                catch
                {
                    _overlay.BeginDrag(null);
                }
            }
        }

        /// <summary>Updates the overlay position to follow the cursor.</summary>
        public static void UpdateDrag(Vector2 stagePos)
        {
            _overlay?.UpdatePosition(stagePos);
        }

        /// <summary>Ends the drag without cancelling — used after a successful drop.</summary>
        public static void EndDrag()
        {
            // Restore source slot sprite visibility after a successful drop
            _sourceSlot?.SetItemSpriteHidden(false);
            _sourceCrystalSlot?.SetCrystalHidden(false);
            _isDragging = false;
            _sourceSlot = null;
            _dragItem = null;
            _dragSkill = null;
            _sourceCrystalSlot = null;
            _dragCrystal = null;
            _sourceVaultStack = null;
            _sourceVaultCrystal = null;
            _overlay?.EndDrag();
        }

        /// <summary>Cancels the drag and makes the item reappear in its source slot.</summary>
        public static void CancelDrag()
        {
            if (_sourceSlot != null)
                _sourceSlot.SetItemSpriteHidden(false);
            _sourceCrystalSlot?.SetCrystalHidden(false);
            _isDragging = false;
            _sourceSlot = null;
            _dragItem = null;
            _dragSkill = null;
            _sourceCrystalSlot = null;
            _dragCrystal = null;
            _sourceVaultStack = null;
            _sourceVaultCrystal = null;
            _overlay?.EndDrag();
        }

        /// <summary>Called by a component when it could not handle an item drop locally.</summary>
        public static void NotifyDropRequested(InventorySlot source, Vector2 stagePos)
        {
            OnDropRequested?.Invoke(source, stagePos);
        }

        /// <summary>Called by a component when it could not handle a skill drag drop locally.</summary>
        public static void NotifySkillDropRequested(ISkill skill, Vector2 stagePos)
        {
            OnSkillDropRequested?.Invoke(skill, stagePos);
        }

        /// <summary>Ensures the overlay exists on the given stage and is brought to front.</summary>
        private static void EnsureOverlay(Stage stage)
        {
            if (_overlay == null)
            {
                _overlay = new DragDropOverlay();
                stage.AddElement(_overlay);
                _overlay.SetVisible(false);
            }
            else if (_overlay.GetStage() != stage)
            {
                _overlay.Remove();
                stage.AddElement(_overlay);
                _overlay.SetVisible(false);
            }
            _overlay.ToFront();
        }
    }
}
