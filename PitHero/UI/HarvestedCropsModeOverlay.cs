using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.UI
{
    /// <summary>
    /// Shows the harvested-crop storage viewer: a scrollable 8-column grid of slots (8×4 per Crop
    /// Storage building, stacked) displaying each harvested crop's sprite and stack count. Clicking an
    /// occupied slot opens a read-only name/description box. View-only — no editing.
    /// Mirrors the SeedPlantingModeOverlay UI pattern.
    /// </summary>
    public class HarvestedCropsModeOverlay
    {
        private readonly Stage _stage;
        private readonly SpriteAtlas _cropsAtlas;

        private Window _inventoryWindow;
        private Table _slotTable;

        private Window _descWindow;
        private Label _descNameLabel;
        private Label _descDescLabel;

        private const float SlotSize = 40f;
        private const float WinPad   = 16f;
        private const int   Columns  = 8;

        /// <summary>Fired when the player dismisses the viewer; caller should exit harvested-crops mode.</summary>
        public event System.Action RequestExitHarvestedCropsMode;

        public HarvestedCropsModeOverlay(Scene scene, Stage stage)
        {
            _stage      = stage;
            _cropsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            CreateInventoryWindow();
            CreateDescriptionWindow();
        }

        /// <summary>Called when the player enters harvested-crops mode; rebuilds and shows the grid.</summary>
        public void OnEnterHarvestedCropsMode()
        {
            RebuildSlots();
            ShowInventoryWindow();
        }

        /// <summary>Called when the player exits harvested-crops mode; hides the windows.</summary>
        public void OnExitHarvestedCropsMode()
        {
            _inventoryWindow?.SetVisible(false);
            _descWindow?.SetVisible(false);
        }

        // ── Inventory window ──────────────────────────────────────────────────────

        private void CreateInventoryWindow()
        {
            var skin = PitHeroSkin.CreateSkin();
            _inventoryWindow = new Window("Harvested Crops", skin, "ph-default");
            _inventoryWindow.SetMovable(false);
            _inventoryWindow.SetResizable(false);

            var outer = new Table();
            outer.Pad(WinPad);

            _slotTable = new Table();
            var scroll = new ScrollPane(_slotTable, skin, "ph-default");
            scroll.SetScrollingDisabled(true, false);
            outer.Add(scroll).Width(SlotSize * Columns + 16f).Height(240f);
            outer.Row();

            var closeButton = new TextButton("Close", skin, "ph-default");
            closeButton.OnClicked += (_) => RequestExitHarvestedCropsMode?.Invoke();
            outer.Add(closeButton).Width(100f).SetPadTop(8f);

            _inventoryWindow.Add(outer).Expand().Fill();
            _inventoryWindow.SetVisible(false);
            _stage.AddElement(_inventoryWindow);
        }

        private void RebuildSlots()
        {
            _slotTable.Clear();

            var storage = Core.Services.GetService<CropStorageInventoryService>();
            var buildingService = Core.Services.GetService<BuildingService>();
            if (storage == null || buildingService == null)
                return;

            // Crop Storage buildings in stable UniqueId order so the grid layout never reshuffles.
            var all = buildingService.GetAll();
            var storageBuildings = new System.Collections.Generic.List<PlacedBuilding>();
            for (int i = 0; i < all.Count; i++)
                if (all[i].Type == BuildingType.CropStorage)
                    storageBuildings.Add(all[i]);
            storageBuildings.Sort((a, b) => a.UniqueId.CompareTo(b.UniqueId));

            int col = 0;
            for (int b = 0; b < storageBuildings.Count; b++)
            {
                var slots = storage.GetSlots(storageBuildings[b].UniqueId);
                for (int s = 0; s < slots.Count; s++)
                {
                    var slot = slots[s];
                    if (slot.IsEmpty)
                    {
                        var blank = new Element();
                        _slotTable.Add(blank).Size(SlotSize, SlotSize).Pad(2f);
                    }
                    else
                    {
                        var sprite = _cropsAtlas.GetSprite(CropConfig.GetHarvestSpriteName(slot.Type));
                        var cell = new HarvestSlotButton(sprite, slot.Type, slot.Count,
                            CropConfig.GetDisplayName(slot.Type));
                        var captured = slot.Type;
                        cell.OnClicked += () => ShowDescription(captured);
                        _slotTable.Add(cell).Size(SlotSize, SlotSize).Pad(2f);
                    }

                    col++;
                    if (col % Columns == 0)
                        _slotTable.Row();
                }
            }
        }

        private void ShowInventoryWindow()
        {
            _descWindow?.SetVisible(false);
            _inventoryWindow.Pack();
            float w = _inventoryWindow.GetWidth();
            float h = _inventoryWindow.GetHeight();
            _inventoryWindow.SetPosition(
                (_stage.GetWidth()  - w) / 2f,
                (_stage.GetHeight() - h) / 2f - 30f);
            _inventoryWindow.SetVisible(true);
        }

        // ── Description window ────────────────────────────────────────────────────

        private void CreateDescriptionWindow()
        {
            var skin = PitHeroSkin.CreateSkin();
            _descWindow = new Window("", skin, "ph-default");
            _descWindow.SetMovable(false);
            _descWindow.SetResizable(false);

            var content = new Table();
            content.Pad(WinPad);

            _descNameLabel = new Label("", skin, "ph-default");
            content.Add(_descNameLabel).SetPadBottom(6f);
            content.Row();

            _descDescLabel = new Label("", skin, "ph-default");
            _descDescLabel.SetWrap(true);
            content.Add(_descDescLabel).Width(200f).SetPadBottom(10f);
            content.Row();

            var closeButton = new TextButton("Close", skin, "ph-default");
            closeButton.OnClicked += (_) => _descWindow.SetVisible(false);
            content.Add(closeButton).Width(80f);

            _descWindow.Add(content).Expand().Fill();
            _descWindow.Pack();
            _descWindow.SetVisible(false);
            _stage.AddElement(_descWindow);
        }

        private void ShowDescription(CropType crop)
        {
            _descNameLabel.SetText(CropConfig.GetDisplayName(crop));
            _descDescLabel.SetText(CropConfig.GetDescription(crop));
            _descWindow.Pack();
            float w = _descWindow.GetWidth();
            float h = _descWindow.GetHeight();
            _descWindow.SetPosition(
                (_stage.GetWidth()  - w) / 2f,
                (_stage.GetHeight() - h) / 2f);
            _descWindow.SetVisible(true);
            _descWindow.ToFront();
        }

        // ── Slot element ──────────────────────────────────────────────────────────

        private class HarvestSlotButton : Element, IInputListener
        {
            private readonly SpriteDrawable _draw;
            private readonly int _count;
            private readonly string _tooltipText;
            private Sprite _selectBox;
            private bool _hovered;

            public event System.Action OnClicked;

            public HarvestSlotButton(Sprite sprite, CropType crop, int count, string tooltipText)
            {
                _draw        = sprite != null ? new SpriteDrawable(sprite) : null;
                _count       = count;
                _tooltipText = tooltipText;
                SetTouchable(Touchable.Enabled);
                SetSize(SlotSize, SlotSize);

                if (Core.Content != null)
                {
                    var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                    _selectBox  = uiAtlas?.GetSprite("SelectBox");
                }
            }

            public override void Draw(Batcher batcher, float parentAlpha)
            {
                _draw?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);

                if (_hovered && _selectBox != null)
                    new SpriteDrawable(_selectBox).Draw(
                        batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);

                var font = Nez.Graphics.Instance?.BitmapFont;
                if (font != null && _count > 1)
                {
                    string countStr = _count.ToString();
                    float tw = font.MeasureString(countStr).X;
                    batcher.DrawString(font, countStr,
                        new Vector2(GetX() + GetWidth() - tw - 2f, GetY() + GetHeight() - font.LineHeight - 1f),
                        Color.White);
                }
            }

            void IInputListener.OnMouseEnter()
            {
                _hovered = true;
                if (!string.IsNullOrEmpty(_tooltipText))
                {
                    var stage = GetStage();
                    var mp = stage != null ? stage.GetMousePosition() : new Vector2(GetX(), GetY());
                    HoverTextManager.ShowHoverText(_tooltipText, mp.X + 12f, mp.Y - 4f);
                }
            }

            void IInputListener.OnMouseExit()
            {
                _hovered = false;
                HoverTextManager.HideHoverText();
            }

            void IInputListener.OnMouseMoved(Vector2 mousePos) { }
            bool IInputListener.OnLeftMousePressed(Vector2 mousePos) => true;
            void IInputListener.OnLeftMouseUp(Vector2 mousePos) => OnClicked?.Invoke();
            bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;
            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
        }
    }
}
