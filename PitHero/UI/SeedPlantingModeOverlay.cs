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
    /// Manages the seed planting flow: scrollable crop inventory window, description panel,
    /// world-space ghost entity, tile indicator, and confirmed grayscale plan placement.
    /// Mirrors the BuildingModeOverlay pattern.
    /// </summary>
    public class SeedPlantingModeOverlay
    {
        private enum PlacementState { Choosing, Placing, Removing }

        // ── World / scene references ──────────────────────────────────────────────
        private readonly Scene _scene;
        private readonly Stage _stage;
        private readonly SpriteAtlas _cropsAtlas;

        // ── State ─────────────────────────────────────────────────────────────────
        private PlacementState _state = PlacementState.Choosing;
        private CropType _selectedCrop;

        // ── Seed inventory ────────────────────────────────────────────────────────
        private int[] _seedInventory;

        // ── Ghost entity ──────────────────────────────────────────────────────────
        private Entity _ghostEntity;
        private SpriteRenderer _ghostRenderer;

        // ── Tile indicator ────────────────────────────────────────────────────────
        private Entity _tileIndicatorEntity;
        private PrototypeSpriteRenderer _tileIndicatorRenderer;

        // ── Stack count label (stage space) ───────────────────────────────────────
        private Label _stackCountLabel;

        // ── Inventory window ──────────────────────────────────────────────────────
        private Window _inventoryWindow;

        // ── Drag-tracking ─────────────────────────────────────────────────────────
        private static readonly Point NoTile = new Point(int.MinValue, int.MinValue);
        private Point _lastDragTile = NoTile;

        // ── Growing-crop overlays (shown in all farm sub-modes, separate from plans) ──
        private readonly System.Collections.Generic.List<Entity> _growingCropOverlays =
            new System.Collections.Generic.List<Entity>();

        // ── Constants ─────────────────────────────────────────────────────────────
        private const float SlotSize   = 40f;
        private const float WinPad     = 16f;
        private const int   CropsPerRow = 4;

        // ── Localization ──────────────────────────────────────────────────────────
        private TextService _textService;

        /// <summary>Resolves a localized UI string, falling back to the key if the service is unavailable.</summary>
        private string GetText(string key)
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }

        /// <summary>Returns true when the overlay is actively in the placing sub-state.</summary>
        public bool IsInPlacingState => _state == PlacementState.Placing;

        /// <summary>Fired when the Cancel button is clicked; caller should invoke FarmUI.ExitSeedMode().</summary>
        public event System.Action RequestExitSeedMode;

        /// <summary>Fired when the player clicks UI while in remove-crops mode; caller should invoke FarmUI.ExitRemoveCropsMode().</summary>
        public event System.Action RequestExitRemoveCropsMode;

        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Creates a new SeedPlantingModeOverlay bound to the given scene and stage.</summary>
        public SeedPlantingModeOverlay(Scene scene, Stage stage)
        {
            _scene      = scene;
            _stage      = stage;
            _cropsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");

            _seedInventory = new int[CropTypeInfo.Count];
            for (int i = 0; i < CropTypeInfo.Count; i++)
                _seedInventory[i] = 16;

            CreateInventoryWindow();

            // Expose the inventory array via the service so SaveLoadService can persist it.
            var svc = Core.Services.GetService<CropPlantingService>();
            if (svc != null)
                svc.SeedInventory = _seedInventory;
        }

        // ── Public lifecycle ──────────────────────────────────────────────────────

        /// <summary>Called when the player enters seed mode; shows the crop inventory.</summary>
        public void OnEnterSeedMode()
        {
            _state = PlacementState.Choosing;
            ShowInventoryWindow();
        }

        /// <summary>Called when the player exits seed mode; tears down UI/ghost.</summary>
        public void OnExitSeedMode()
        {
            DestroyGhost();
            DestroyTileIndicator();
            HideStackCountLabel();
            _inventoryWindow?.SetVisible(false);
            _state = PlacementState.Choosing;
            _lastDragTile = NoTile;
        }

        /// <summary>Creates grayscale world entities for all plans and planted crops. Called when any farm sub-mode is entered.</summary>
        public void ShowPlanVisuals()
        {
            RestorePlanVisuals();
            RestoreGrowingCropOverlays();
        }

        /// <summary>Destroys grayscale world entities for all plans and planted-crop overlays. Called when all farm sub-modes are exited.</summary>
        public void HidePlanVisuals()
        {
            Core.Services.GetService<CropPlantingService>()?.DestroyPlanVisuals();
            DestroyGrowingCropOverlays();
        }

        /// <summary>Called when the player enters remove-crops mode; creates the tile indicator.</summary>
        public void OnEnterRemoveCropsMode()
        {
            _state = PlacementState.Removing;
            CreateTileIndicator();
        }

        /// <summary>Called when the player exits remove-crops mode; tears down the tile indicator.</summary>
        public void OnExitRemoveCropsMode()
        {
            DestroyTileIndicator();
            _state = PlacementState.Choosing;
        }

        /// <summary>Per-frame update; runs during Placing and Removing states.</summary>
        public void Update()
        {
            if (_state != PlacementState.Placing && _state != PlacementState.Removing)
                return;

            var worldPos = _scene.Camera.MouseToWorldPoint();
            int tileX = (int)(worldPos.X / GameConfig.TileSize);
            int tileY = (int)(worldPos.Y / GameConfig.TileSize);

            // UI hit check — if mouse is over a UI element and user presses, exit
            if (_stage.Hit(_stage.GetMousePosition()) != null)
            {
                if (Input.LeftMouseButtonPressed)
                {
                    if (_state == PlacementState.Placing) RequestExitSeedMode?.Invoke();
                    else                                  RequestExitRemoveCropsMode?.Invoke();
                }
                _lastDragTile = NoTile;
                return;
            }

            if (_state == PlacementState.Placing)
            {
                // Ghost: bottom-center of sprite aligns to bottom of tile
                float ghostX = tileX * GameConfig.TileSize + GameConfig.TileSize / 2f;
                if (_ghostEntity != null && _ghostRenderer?.Sprite != null)
                {
                    float ghostH = _ghostRenderer.Sprite.SourceRect.Height;
                    float ghostY = tileY * GameConfig.TileSize + GameConfig.TileSize - ghostH / 2f;
                    _ghostEntity.SetPosition(ghostX, ghostY);
                }

                // Tile indicator: green = valid placement, red = invalid
                if (_tileIndicatorEntity != null)
                {
                    _tileIndicatorEntity.SetPosition(
                        tileX * GameConfig.TileSize + GameConfig.TileSize / 2f,
                        tileY * GameConfig.TileSize + GameConfig.TileSize / 2f);
                    bool valid = IsValidPlacement(tileX, tileY);
                    if (_tileIndicatorRenderer != null)
                        _tileIndicatorRenderer.Color = valid
                            ? new Color(0, 255, 0, 128)
                            : new Color(255, 0, 0, 128);
                }

                // Stack count label: follow cursor in stage space
                if (_stackCountLabel != null)
                {
                    var mouseStage = _stage.GetMousePosition();
                    _stackCountLabel.SetPosition(mouseStage.X + 6f, mouseStage.Y - 14f);
                    _stackCountLabel.SetText(_seedInventory[(int)_selectedCrop].ToString());
                    _stackCountLabel.SetVisible(true);
                }

                // Place crop on valid click / drag
                var tile = new Point(tileX, tileY);
                if (Input.LeftMouseButtonDown && tile != _lastDragTile)
                {
                    _lastDragTile = tile;
                    if (IsValidPlacement(tileX, tileY))
                        PlaceCrop(tileX, tileY);
                }
                else if (!Input.LeftMouseButtonDown)
                {
                    _lastDragTile = NoTile;
                }
            }
            else // Removing
            {
                var cropService = Core.Services.GetService<CropPlantingService>();
                var tile = new Point(tileX, tileY);
                bool hasPlan = cropService != null && cropService.HasPlan(tile);

                // Tile indicator: green = plan exists and can be removed, red = nothing here
                if (_tileIndicatorEntity != null)
                {
                    _tileIndicatorEntity.SetPosition(
                        tileX * GameConfig.TileSize + GameConfig.TileSize / 2f,
                        tileY * GameConfig.TileSize + GameConfig.TileSize / 2f);
                    if (_tileIndicatorRenderer != null)
                        _tileIndicatorRenderer.Color = hasPlan
                            ? new Color(0, 255, 0, 128)
                            : new Color(255, 0, 0, 128);
                }

                // Single click to remove (no drag)
                if (Input.LeftMouseButtonPressed && hasPlan)
                {
                    var removed = cropService.RemovePlan(tile);
                    if (removed.HasValue)
                        _seedInventory[(int)removed.Value]++;
                }
            }
        }

        // ── Seed inventory access ─────────────────────────────────────────────────

        /// <summary>Returns the current seed inventory array (direct reference).</summary>
        public int[] GetSeedInventory() => _seedInventory;

        /// <summary>Loads saved counts into the existing inventory array in place. Missing slots default to 16.</summary>
        public void SetSeedInventory(int[] counts)
        {
            if (counts == null)
                return;

            // Mutate in place so CropSlotButton._inventory references remain valid.
            int copy = counts.Length < CropTypeInfo.Count ? counts.Length : CropTypeInfo.Count;
            for (int i = 0; i < copy; i++)
                _seedInventory[i] = counts[i];
            for (int i = copy; i < CropTypeInfo.Count; i++)
                _seedInventory[i] = 16;
        }

        // ── Load-restore ──────────────────────────────────────────────────────────

        /// <summary>
        /// Spawns a previously-saved crop plan at its world position with no inventory change.
        /// Called by MainGameScene.ApplyPendingLoadData() for each plan in the save file.
        /// </summary>
        public void SpawnRestoredCropPlan(CropType type, int tileX, int tileY)
        {
            // Only registers plan data — no entity created here.
            // RestorePlanVisuals() (called from OnEnterSeedMode) spawns visuals on demand.
            Core.Services.GetService<CropPlantingService>()?.AddPlan(new PlacedCropPlan
            {
                Type        = type,
                TileX       = tileX,
                TileY       = tileY,
                WorldEntity = null,
            });
        }

        // ── Plan visual helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Recreates world-space grayscale entities for any plans whose entity was destroyed
        /// on the previous OnExitSeedMode call.  Does not re-register plans with the service.
        /// </summary>
        private void RestorePlanVisuals()
        {
            var service = Core.Services.GetService<CropPlantingService>();
            if (service == null)
                return;

            var plans = service.GetAllPlans();
            for (int i = 0; i < plans.Count; i++)
            {
                var plan = plans[i];
                if (plan.WorldEntity != null)
                    continue;

                var sprite  = _cropsAtlas.GetSprite(CropConfig.GetFullyGrownSpriteName(plan.Type));
                float sprH  = sprite != null ? sprite.SourceRect.Height : GameConfig.TileSize;
                float wx    = plan.TileX * GameConfig.TileSize + GameConfig.TileSize / 2f;
                float wy    = plan.TileY * GameConfig.TileSize + GameConfig.TileSize - sprH / 2f;

                var entity   = _scene.CreateEntity("crop-plan-" + plan.Type + "-" + plan.TileX + "-" + plan.TileY);
                entity.SetPosition(wx, wy);
                var renderer = entity.AddComponent(new SpriteRenderer(sprite));
                renderer.SetRenderLayer(GameConfig.RenderLayerSingleTileObject - 1);
                renderer.SetMaterial(new Material(new GrayscaleEffect()));

                service.SetPlanEntity(new Microsoft.Xna.Framework.Point(plan.TileX, plan.TileY), entity);
            }
        }

        /// <summary>
        /// Spawns grayscale overlays for all actively planted crops so players can identify crop
        /// types while in any farm planning sub-mode. Destroyed on HidePlanVisuals.
        /// </summary>
        private void RestoreGrowingCropOverlays()
        {
            DestroyGrowingCropOverlays();

            var cropGrowthService = Core.Services.GetService<CropGrowthService>();
            if (cropGrowthService == null)
                return;

            foreach (var kvp in cropGrowthService.GetAllData())
            {
                var tile = kvp.Key;
                var cropType = kvp.Value.Type;

                var sprite = _cropsAtlas.GetSprite(CropConfig.GetFullyGrownSpriteName(cropType));
                float sprH = sprite != null ? sprite.SourceRect.Height : GameConfig.TileSize;
                float wx = tile.X * GameConfig.TileSize + GameConfig.TileSize / 2f;
                float wy = tile.Y * GameConfig.TileSize + GameConfig.TileSize - sprH / 2f;

                var entity = _scene.CreateEntity("growing-crop-overlay-" + tile.X + "-" + tile.Y);
                entity.SetPosition(wx, wy);
                var renderer = entity.AddComponent(new SpriteRenderer(sprite));
                renderer.SetRenderLayer(GameConfig.RenderLayerSingleTileObject - 1);
                renderer.SetMaterial(new Material(new GrayscaleEffect()));

                _growingCropOverlays.Add(entity);
            }
        }

        private void DestroyGrowingCropOverlays()
        {
            for (int i = 0; i < _growingCropOverlays.Count; i++)
                _growingCropOverlays[i]?.Destroy();
            _growingCropOverlays.Clear();
        }

        // ── Inventory window ──────────────────────────────────────────────────────

        private Table _slotTable;

        private void CreateInventoryWindow()
        {
            var skin = PitHeroSkin.CreateSkin();
            _inventoryWindow = new Window(GetText(UITextKey.WindowSeeds), skin, "ph-default");
            _inventoryWindow.SetMovable(false);
            _inventoryWindow.SetResizable(false);

            var outer = new Table();
            outer.Pad(WinPad);

            _slotTable = new Table();
            var scroll = new ScrollPane(_slotTable, skin, "ph-default");
            scroll.SetScrollingDisabled(true, false);
            outer.Add(scroll).Width(SlotSize * CropsPerRow + 16f).Height(200f);
            outer.Row();

            var cancelButton = new TextButton(GetText(UITextKey.ButtonCancel), skin, "ph-default");
            cancelButton.OnClicked += (_) => RequestExitSeedMode?.Invoke();
            outer.Add(cancelButton).Width(100f).SetPadTop(8f);

            _inventoryWindow.Add(outer).Expand().Fill();
            _inventoryWindow.SetVisible(false);
            _stage.AddElement(_inventoryWindow);

            // Create the stage-space stack count label used during placing
            _stackCountLabel = new Label("", new LabelStyle { Font = Nez.Graphics.Instance.BitmapFont, FontColor = Microsoft.Xna.Framework.Color.White });
            _stackCountLabel.SetVisible(false);
            _stage.AddElement(_stackCountLabel);
        }

        private void ShowInventoryWindow()
        {
            // Rebuild slot table every time: skip zero-count crops and show live counts.
            _slotTable.Clear();
            int col = 0;
            for (int i = 0; i < CropTypeInfo.Count; i++)
            {
                if (_seedInventory[i] <= 0) continue;
                var cropType = (CropType)i;
                var sprite   = _cropsAtlas.GetSprite(CropConfig.GetFullyGrownSpriteName(cropType));
                var slot     = new CropSlotButton(sprite, GetText(CropConfig.GetDisplayNameKey(cropType)), _seedInventory, i);
                slot.OnClicked += () => OnCropSlotClicked(cropType);
                _slotTable.Add(slot).Size(SlotSize, SlotSize).Pad(2f);
                col++;
                if (col % CropsPerRow == 0)
                    _slotTable.Row();
            }

            _inventoryWindow.Pack();
            float w = _inventoryWindow.GetWidth();
            float h = _inventoryWindow.GetHeight();
            _inventoryWindow.SetPosition(
                (_stage.GetWidth()  - w) / 2f,
                (_stage.GetHeight() - h) / 2f - 30f);
            _inventoryWindow.SetVisible(true);
        }

        // ── State transitions ─────────────────────────────────────────────────────

        private void OnCropSlotClicked(CropType crop)
        {
            _selectedCrop = crop;
            _inventoryWindow.SetVisible(false);
            CreateGhost(_selectedCrop);
            CreateTileIndicator();
            _stackCountLabel?.SetVisible(true);
            _state = PlacementState.Placing;
        }

        // ── Placement logic ───────────────────────────────────────────────────────

        private bool IsValidPlacement(int tx, int ty)
        {
            var tileService = Core.Services.GetService<TileStateService>();
            var cropService = Core.Services.GetService<CropPlantingService>();

            var tile = new Microsoft.Xna.Framework.Point(tx, ty);

            if (tileService == null)
                return false;
            bool readyOrTilled = tileService.HasFlag(tile, TileStateFlag.ReadyToTill)
                              || tileService.HasFlag(tile, TileStateFlag.Tilled);
            if (!readyOrTilled)
                return false;

            // Reject if a plan already exists or a crop is already planted/growing/grown
            if (cropService != null && cropService.HasPlan(tile))
                return false;
            var cropGrowthService = Core.Services.GetService<CropGrowthService>();
            if (cropGrowthService != null && cropGrowthService.HasCrop(tile))
                return false;

            if (_seedInventory[(int)_selectedCrop] <= 0)
                return false;

            // Reject if any of the 8 neighboring tiles has a different crop type (planned or planted)
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var neighbor = new Microsoft.Xna.Framework.Point(tx + dx, ty + dy);
                    var neighborType = cropService?.GetPlanType(neighbor) ?? cropGrowthService?.GetCropType(neighbor);
                    if (neighborType.HasValue && neighborType.Value != _selectedCrop)
                        return false;
                }
            }

            return true;
        }

        private void PlaceCrop(int tileX, int tileY)
        {
            _seedInventory[(int)_selectedCrop]--;

            var sprite = _cropsAtlas.GetSprite(CropConfig.GetFullyGrownSpriteName(_selectedCrop));
            float spriteH = sprite != null ? sprite.SourceRect.Height : GameConfig.TileSize;
            float wx = tileX * GameConfig.TileSize + GameConfig.TileSize / 2f;
            float wy = tileY * GameConfig.TileSize + GameConfig.TileSize - spriteH / 2f;

            var entity = _scene.CreateEntity(
                "crop-plan-" + _selectedCrop.ToString() + "-" + tileX + "-" + tileY);
            entity.SetPosition(wx, wy);

            var renderer = entity.AddComponent(new SpriteRenderer(sprite));
            renderer.SetRenderLayer(GameConfig.RenderLayerSingleTileObject - 1);
            renderer.SetMaterial(new Material(new GrayscaleEffect()));

            var tile = new Point(tileX, tileY);
            Core.Services.GetService<CropPlantingService>()?.AddPlan(new PlacedCropPlan
            {
                Type        = _selectedCrop,
                TileX       = tileX,
                TileY       = tileY,
                WorldEntity = entity,
            });

            // If the tile is already tilled, HandleTileTilled won't fire again — notify directly
            var tileStateService = Core.Services.GetService<TileStateService>();
            if (tileStateService != null && tileStateService.HasFlag(tile, TileStateFlag.Tilled))
                Core.Services.GetService<FarmTaskCoordinator>()?.NotifyPlanAddedOnTilledTile(tile);

            // If seeds are exhausted, return to the inventory window
            if (_seedInventory[(int)_selectedCrop] <= 0)
            {
                DestroyGhost();
                DestroyTileIndicator();
                HideStackCountLabel();
                ShowInventoryWindow();
                _state = PlacementState.Choosing;
            }
        }

        // ── Ghost management ──────────────────────────────────────────────────────

        private void CreateGhost(CropType crop)
        {
            DestroyGhost();
            var sprite     = _cropsAtlas.GetSprite(CropConfig.GetFullyGrownSpriteName(crop));
            _ghostEntity   = _scene.CreateEntity("seed-ghost");
            _ghostRenderer = _ghostEntity.AddComponent(new SpriteRenderer(sprite));
            _ghostRenderer.SetRenderLayer(GameConfig.RenderLayerTop);
            _ghostRenderer.Color = Color.White;
            _ghostRenderer.SetMaterial(new Material(new GrayscaleEffect()));
        }

        private void DestroyGhost()
        {
            _ghostEntity?.Destroy();
            _ghostEntity   = null;
            _ghostRenderer = null;
        }

        // ── Tile indicator management ─────────────────────────────────────────────

        private void CreateTileIndicator()
        {
            DestroyTileIndicator();
            _tileIndicatorEntity   = _scene.CreateEntity("seed-tile-indicator");
            _tileIndicatorRenderer = _tileIndicatorEntity.AddComponent(
                new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
            _tileIndicatorRenderer.Color = new Color(0, 255, 0, 128);
            _tileIndicatorRenderer.SetRenderLayer(GameConfig.RenderLayerTop);
        }

        private void DestroyTileIndicator()
        {
            _tileIndicatorEntity?.Destroy();
            _tileIndicatorEntity   = null;
            _tileIndicatorRenderer = null;
        }

        // ── Stack count label helpers ─────────────────────────────────────────────

        private void HideStackCountLabel()
        {
            _stackCountLabel?.SetVisible(false);
        }

        // ── CropSlotButton ────────────────────────────────────────────────────────

        private class CropSlotButton : Element, IInputListener
        {
            private readonly Sprite     _sprite;
            private readonly string     _tooltipText;
            private readonly int[]      _inventory;
            private readonly int        _inventoryIndex;
            private readonly SpriteDrawable _draw;
            private Sprite _selectBox;
            private bool   _hovered;

            public event System.Action OnClicked;

            public CropSlotButton(Sprite sprite, string tooltipText, int[] inventory, int inventoryIndex)
            {
                _sprite         = sprite;
                _tooltipText    = tooltipText;
                _inventory      = inventory;
                _inventoryIndex = inventoryIndex;
                _draw           = sprite != null ? new SpriteDrawable(sprite) : null;
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

                // Stack count in bottom-right
                int count = _inventory != null ? _inventory[_inventoryIndex] : 0;
                // Stack count display is handled by the font on the label; use a simple string overlay
                // We draw the count string using Nez's Batcher. Use Graphics.Instance.BitmapFont which
                // may be null; if so skip drawing to avoid crashes.
                var font = Nez.Graphics.Instance?.BitmapFont;
                if (font != null)
                {
                    string countStr = count.ToString();
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
                    if (stage != null)
                    {
                        var mp = stage.GetMousePosition();
                        HoverTextManager.ShowHoverText(_tooltipText, mp.X + 12f, mp.Y - 4f);
                    }
                    else
                    {
                        HoverTextManager.ShowHoverText(_tooltipText, GetX(), GetY() + GetHeight() + 4f);
                    }
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
