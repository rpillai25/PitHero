using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Heroes;

namespace PitHero.UI
{
    /// <summary>The main content of the Crystals collection tab.</summary>
    public class CrystalsTab
    {
        private const int INVENTORY_COLS = 8;
        private const int INVENTORY_ROWS = 5;
        private const int INVENTORY_TOTAL = INVENTORY_COLS * INVENTORY_ROWS; // 40 slots
        private const int QUEUE_SLOTS = 5;
        private const float SLOT_SIZE = 32f;
        private const float SLOT_PAD = 1f;
        private const float SWAP_TWEEN_DURATION = 0.2f;

        // Slot type enum for multi-slot selection tracking
        private enum SelType { None, ForgeA, ForgeB, Inventory, Queue }

        private CrystalSlotElement _forgeInputA;
        private CrystalSlotElement _forgeInputB;
        private CrystalSlotElement _forgeOutput;
        private TextButton _forgeButton;
        private CrystalSlotElement[] _inventorySlots;
        private CrystalSlotElement[] _queueSlots;
        private TextButton _createButton;
        private HeroCrystalCard _crystalCard;
        private CrystalCollectionService _crystalService;
        private Stage _stage;
        private Skin _skin;
        private TextService _textService;
        private Window _heroWindow;

        // Selection tracking (source slot for crystal movement)
        private SelType _selType = SelType.None;
        private int _selIndex = -1;
        private CrystalSlotElement _selElement = null;

        // Hover tooltip
        private Window _hoverTooltipWindow;
        private Label _hoverLabel;

        // Swap animation overlay (reused SwapAnimationOverlay from inventory)
        private SwapAnimationOverlay _swapOverlay;

        // Full-screen dismiss layer for the crystal card (hides card when clicking outside it)
        private Element _cardDismissLayer;

        /// <summary>Creates and returns the tab content table.</summary>
        public Table CreateContent(Skin skin, Stage stage, Window heroWindow)
        {
            _skin = skin;
            _stage = stage;
            _heroWindow = heroWindow;
            _textService = Core.Services?.GetService<TextService>();
            _crystalService = Core.Services?.GetService<CrystalCollectionService>();

            _crystalCard = new HeroCrystalCard(skin, stage);
            stage.AddElement(_crystalCard);

            // ── Swap overlay (shares stage with inventory overlay) ───────────────
            _swapOverlay = new SwapAnimationOverlay();
            stage.AddElement(_swapOverlay);
            _swapOverlay.SetVisible(false);

            // ── Hover tooltip (shared across all slots) ───────────────────────
            _hoverTooltipWindow = new Window("", skin);
            _hoverTooltipWindow.SetMovable(false);
            _hoverTooltipWindow.SetResizable(false);
            _hoverTooltipWindow.SetKeepWithinStage(false);
            _hoverTooltipWindow.SetColor(GameConfig.TransparentMenu);
            _hoverLabel = new Label("", skin, "ph-default");
            _hoverTooltipWindow.Add(_hoverLabel).Pad(4f);
            _hoverTooltipWindow.Pack();
            _hoverTooltipWindow.SetVisible(false);

            // ── Root table ───────────────────────────────────────────────────────
            var mainTable = new Table();
            mainTable.Top().Left();
            mainTable.PadLeft(32f);

            // ── Forge section (spans both columns) ───────────────────────────────
            var forgeSection = new Table();
            forgeSection.Add(new Label(GetText(UITextKey.CrystalForgeTitle), skin, "ph-default")).Left().Pad(5);
            forgeSection.Row();

            var forgeRow = new Table();
            _forgeInputA = new CrystalSlotElement(CrystalSlotKind.Inventory);
            _forgeInputB = new CrystalSlotElement(CrystalSlotKind.Inventory);
            _forgeOutput = new CrystalSlotElement(CrystalSlotKind.Shortcut);
            _forgeInputA.OnSlotClicked += _ => OnForgeSlotClicked(SelType.ForgeA);
            _forgeInputA.OnSlotHovered += OnSlotHovered;
            _forgeInputA.OnSlotUnhovered += OnSlotUnhovered;
            _forgeInputB.OnSlotClicked += _ => OnForgeSlotClicked(SelType.ForgeB);
            _forgeInputB.OnSlotHovered += OnSlotHovered;
            _forgeInputB.OnSlotUnhovered += OnSlotUnhovered;
            forgeRow.Add(_forgeInputA).Size(SLOT_SIZE).Pad(2);
            forgeRow.Add(new Label("+", skin, "ph-default")).Pad(2);
            forgeRow.Add(_forgeInputB).Size(SLOT_SIZE).Pad(2);
            forgeRow.Add(new Label("=", skin, "ph-default")).Pad(2);
            forgeRow.Add(_forgeOutput).Size(SLOT_SIZE).Pad(2);

            _forgeButton = new TextButton(GetText(UITextKey.CrystalForgeButton), skin, "ph-default");
            _forgeButton.OnClicked += OnForgeClicked;
            _forgeButton.SetDisabled(true);
            forgeRow.Add(_forgeButton).Pad(4);

            forgeSection.Add(forgeRow).Left().Pad(2);

            // Forge spans both columns of mainTable so queue label and inv label share the same row
            mainTable.Add(forgeSection).Left().SetColspan(2).Pad(5);
            mainTable.Row();

            // ── Inventory section (left) ──────────────────────────────────────────
            var invCol = new Table();
            invCol.Add(new Label(GetText(UITextKey.CrystalInventoryTitle), skin, "ph-default")).Left().Pad(5);
            invCol.Row();

            var invGrid = new Table();
            _inventorySlots = new CrystalSlotElement[INVENTORY_TOTAL];
            for (int i = 0; i < INVENTORY_TOTAL; i++)
            {
                var slot = new CrystalSlotElement(CrystalSlotKind.Inventory);
                int idx = i;
                slot.OnSlotClicked += _ => OnInventorySlotClicked(idx);
                slot.OnSlotHovered += OnSlotHovered;
                slot.OnSlotUnhovered += OnSlotUnhovered;
                _inventorySlots[i] = slot;
                invGrid.Add(slot).Size(SLOT_SIZE).Pad(SLOT_PAD);
                if ((i + 1) % INVENTORY_COLS == 0) invGrid.Row();
            }
            invCol.Add(invGrid).Left().Pad(2);
            invCol.Row();

            _createButton = new TextButton(GetText(UITextKey.CrystalCreateButton), skin, "ph-default");
            _createButton.OnClicked += OnCreateClicked;
            invCol.Add(_createButton).Left().Pad(5);

            // ── Queue section (right) ─────────────────────────────────────────────
            var queueCol = new Table();
            queueCol.Add(new Label(GetText(UITextKey.CrystalQueueTitle), skin, "ph-default")).Left().Pad(5);
            queueCol.Row();

            _queueSlots = new CrystalSlotElement[QUEUE_SLOTS];
            for (int i = 0; i < QUEUE_SLOTS; i++)
            {
                var slot = new CrystalSlotElement(CrystalSlotKind.Shortcut);
                int idx = i;
                slot.OnSlotClicked += _ => OnQueueSlotClicked(idx);
                slot.OnSlotHovered += OnSlotHovered;
                slot.OnSlotUnhovered += OnSlotUnhovered;
                _queueSlots[i] = slot;
                queueCol.Add(slot).Size(SLOT_SIZE).Pad(SLOT_PAD);
                queueCol.Row();
            }

            mainTable.Add(invCol).Top().Left().Pad(2);
            mainTable.Add(queueCol).Top().Left().Pad(2, 0, 2, 5);

            RefreshAll();
            return mainTable;
        }

        private CrystalCollectionService GetCrystalService()
        {
            if (_crystalService == null)
                _crystalService = Core.Services?.GetService<CrystalCollectionService>();
            return _crystalService;
        }

        private string GetText(string key) => _textService?.DisplayText(TextType.UI, key) ?? key;

        /// <summary>Refreshes all slots from the service.</summary>
        public void RefreshAll()
        {
            var svc = GetCrystalService();
            if (svc == null) return;

            for (int i = 0; i < INVENTORY_TOTAL; i++)
                _inventorySlots[i].SetCrystal(svc.Inventory[i]);

            for (int i = 0; i < QUEUE_SLOTS; i++)
                _queueSlots[i].SetCrystal(svc.Queue[i]);

            // Refresh forge display — forge slots are now direct crystal references
            _forgeInputA.SetCrystal(svc.ForgeSlotA);
            _forgeInputB.SetCrystal(svc.ForgeSlotB);

            // Update forge button enabled state
            bool canForge = svc.ForgeSlotA != null && svc.ForgeSlotB != null;
            _forgeButton.SetDisabled(!canForge);
        }

        // ── Hover tooltip ────────────────────────────────────────────────────────

        private void OnSlotHovered(CrystalSlotElement slot)
        {
            if (slot.Crystal == null) return;
            var jobName = slot.Crystal.IsCombo
                ? GetText(UITextKey.CrystalTooltipCombo)
                : slot.Crystal.Job.Name;
            _hoverLabel.SetText(jobName);
            _hoverTooltipWindow.Pack();

            if (_hoverTooltipWindow.GetParent() == null)
                _stage.AddElement(_hoverTooltipWindow);
            _hoverTooltipWindow.SetVisible(true);

            var mousePos = _stage.GetMousePosition();
            float tx = mousePos.X + 10f;
            float ty = mousePos.Y + 10f;
            float stageH = _stage.GetHeight();
            if (ty + _hoverTooltipWindow.GetHeight() > stageH)
                ty = stageH - _hoverTooltipWindow.GetHeight();
            _hoverTooltipWindow.SetPosition(tx, ty);
            _hoverTooltipWindow.ToFront();
        }

        private void OnSlotUnhovered(CrystalSlotElement slot)
        {
            _hoverTooltipWindow.SetVisible(false);
            _hoverTooltipWindow.Remove();
        }

        // ── Selection helpers ────────────────────────────────────────────────────

        private void SetSelection(SelType type, int index, CrystalSlotElement element)
        {
            // Deselect previous
            if (_selElement != null) _selElement.SetSelected(false);

            _selType = type;
            _selIndex = index;
            _selElement = element;

            if (_selElement != null) _selElement.SetSelected(true);
        }

        private void ClearSelection()
        {
            if (_selElement != null) _selElement.SetSelected(false);
            _selType = SelType.None;
            _selIndex = -1;
            _selElement = null;
        }

        /// <summary>Maps the current selection to a CrystalSlotType and index for SwapSlots.</summary>
        private bool GetSelectionSlot(out CrystalSlotType slotType, out int slotIdx)
        {
            slotIdx = _selIndex;
            switch (_selType)
            {
                case SelType.Inventory: slotType = CrystalSlotType.Inventory; return true;
                case SelType.ForgeA:    slotType = CrystalSlotType.ForgeA;    slotIdx = 0; return true;
                case SelType.ForgeB:    slotType = CrystalSlotType.ForgeB;    slotIdx = 0; return true;
                case SelType.Queue:     slotType = CrystalSlotType.Queue;     return true;
                default:                slotType = CrystalSlotType.Inventory; return false;
            }
        }

        /// <summary>Returns the UI element for the current selection, if still valid.</summary>
        private CrystalSlotElement GetSelectionElement() => _selElement;

        // ── Swap animation ────────────────────────────────────────────────────────

        /// <summary>Plays a tween swap animation between two CrystalSlotElements, then calls onCompleted.</summary>
        private void AnimateCrystalSwap(CrystalSlotElement slotA, CrystalSlotElement slotB, System.Action onCompleted)
        {
            if (_swapOverlay == null || slotA == null || slotB == null)
            {
                onCompleted?.Invoke();
                return;
            }

            var crystalA = slotA.Crystal;
            var crystalB = slotB.Crystal;

            if (crystalA == null && crystalB == null)
            {
                onCompleted?.Invoke();
                return;
            }

            if (Core.Content == null)
            {
                onCompleted?.Invoke();
                return;
            }

            SpriteDrawable drawableA = null;
            SpriteDrawable drawableB = null;
            try
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                var baseSprite = itemsAtlas.GetSprite("HeroCrystal");
                if (crystalA != null && baseSprite != null)
                    drawableA = new SpriteDrawable(baseSprite);
                if (crystalB != null && baseSprite != null)
                    drawableB = new SpriteDrawable(baseSprite);
            }
            catch (System.Exception ex)
            {
                Debug.Log("[CrystalsTab] AnimateCrystalSwap: atlas load failed — " + ex.Message);
                onCompleted?.Invoke();
                return;
            }

            var stageA = slotA.GetStage();
            if (stageA == null)
            {
                onCompleted?.Invoke();
                return;
            }

            var startA = slotA.LocalToStageCoordinates(Vector2.Zero);
            var startB = slotB.LocalToStageCoordinates(Vector2.Zero);

            var colorA = crystalA?.Color ?? Color.White;
            var colorB = crystalB?.Color ?? Color.White;

            if (crystalA != null) slotA.SetCrystalHidden(true);
            if (crystalB != null) slotB.SetCrystalHidden(true);

            _swapOverlay.ToFront();
            _swapOverlay.Begin(
                drawableA, colorA, startA, startB,
                drawableB, colorB, startB, startA,
                SWAP_TWEEN_DURATION,
                () =>
                {
                    slotA.SetCrystalHidden(false);
                    slotB.SetCrystalHidden(false);
                    onCompleted?.Invoke();
                });
        }

        // ── Slot click handlers ──────────────────────────────────────────────────

        private void OnInventorySlotClicked(int idx)
        {
            var svc = GetCrystalService();
            if (svc == null) return;

            if (_selType == SelType.None)
            {
                var crystal = svc.GetInventoryCrystal(idx);
                if (crystal != null)
                {
                    SetSelection(SelType.Inventory, idx, _inventorySlots[idx]);
                    ShowCrystalCard(crystal);
                }
                else
                {
                    HideCrystalCard();
                }
            }
            else
            {
                if (GetSelectionSlot(out var srcType, out var srcIdx))
                {
                    var srcEl = GetSelectionElement();
                    var dstEl = _inventorySlots[idx];

                    // Don't animate if same slot
                    if (srcType == CrystalSlotType.Inventory && srcIdx == idx)
                    {
                        ClearSelection();
                        HideCrystalCard();
                        return;
                    }

                    svc.SwapSlots(srcType, srcIdx, CrystalSlotType.Inventory, idx);
                    RefreshAll();
                    AnimateCrystalSwap(srcEl, dstEl, null);
                }
                ClearSelection();
                HideCrystalCard();
            }
        }

        private void OnQueueSlotClicked(int queueIdx)
        {
            var svc = GetCrystalService();
            if (svc == null) return;

            if (_selType == SelType.None)
            {
                var crystal = svc.Queue[queueIdx];
                if (crystal != null)
                {
                    SetSelection(SelType.Queue, queueIdx, _queueSlots[queueIdx]);
                    ShowCrystalCard(crystal);
                }
                else
                {
                    HideCrystalCard();
                }
            }
            else
            {
                if (GetSelectionSlot(out var srcType, out var srcIdx))
                {
                    var srcEl = GetSelectionElement();
                    var dstEl = _queueSlots[queueIdx];

                    // Don't animate if same slot
                    if (srcType == CrystalSlotType.Queue && srcIdx == queueIdx)
                    {
                        ClearSelection();
                        HideCrystalCard();
                        return;
                    }

                    svc.SwapSlots(srcType, srcIdx, CrystalSlotType.Queue, queueIdx);
                    RefreshAll();
                    AnimateCrystalSwap(srcEl, dstEl, null);
                }
                ClearSelection();
                HideCrystalCard();
            }
        }

        private void OnForgeSlotClicked(SelType forgeSlot)
        {
            var svc = GetCrystalService();
            if (svc == null) return;

            var dstType = forgeSlot == SelType.ForgeA ? CrystalSlotType.ForgeA : CrystalSlotType.ForgeB;
            var dstEl = forgeSlot == SelType.ForgeA ? _forgeInputA : _forgeInputB;

            if (_selType == SelType.None)
            {
                var crystal = forgeSlot == SelType.ForgeA ? svc.ForgeSlotA : svc.ForgeSlotB;
                if (crystal != null)
                {
                    SetSelection(forgeSlot, 0, dstEl);
                    ShowCrystalCard(crystal);
                }
                else
                {
                    HideCrystalCard();
                }
            }
            else
            {
                if (GetSelectionSlot(out var srcType, out var srcIdx))
                {
                    var srcEl = GetSelectionElement();

                    // Don't animate if same slot
                    if (srcType == dstType)
                    {
                        ClearSelection();
                        HideCrystalCard();
                        return;
                    }

                    svc.SwapSlots(srcType, srcIdx, dstType, 0);
                    RefreshAll();
                    AnimateCrystalSwap(srcEl, dstEl, null);
                }
                ClearSelection();
                HideCrystalCard();
            }
        }

        private void ShowCrystalCard(HeroCrystal crystal)
        {
            if (_crystalCard == null) return;
            _crystalCard.ShowCrystal(crystal);
            _crystalCard.Pack();
            _crystalCard.PositionAtWindowRight(_heroWindow);

            // Ensure card dismiss layer exists and is wired up
            if (_cardDismissLayer == null)
            {
                _cardDismissLayer = new DismissLayer(() => { HideCrystalCard(); ClearSelection(); });
                _stage.AddElement(_cardDismissLayer);
            }
            _cardDismissLayer.SetSize(_stage.GetWidth(), _stage.GetHeight());
            _cardDismissLayer.SetVisible(true);
            _cardDismissLayer.ToFront();
            _crystalCard.ToFront();
        }

        private void HideCrystalCard()
        {
            _crystalCard?.Hide();
            if (_cardDismissLayer != null)
                _cardDismissLayer.SetVisible(false);
        }

        private void OnForgeClicked(Button b)
        {
            var svc = GetCrystalService();
            if (svc != null)
            {
                var result = svc.TryForge("Combo Crystal");
                if (result != null)
                {
                    svc.TryAddToInventory(result);
                    RefreshAll();
                    ClearSelection();
                    HideCrystalCard();
                }
            }
        }

        private void OnCreateClicked(Button b)
        {
            var svc = GetCrystalService();
            var dialog = new CrystalCreationDialog(_skin, _stage, svc);
            dialog.OnCrystalCreated += c => RefreshAll();

            // Add a full-screen dismiss layer behind the dialog so clicking outside closes it.
            Element dismissLayer = null;
            dismissLayer = new DismissLayer(() =>
            {
                dismissLayer?.SetVisible(false);
                dismissLayer?.Remove();
                dismissLayer = null;
                dialog.Remove();
            });
            dismissLayer.SetSize(_stage.GetWidth(), _stage.GetHeight());

            // Clean up dismiss layer when dialog is closed via its own buttons.
            dialog.OnClosed += () =>
            {
                dismissLayer?.SetVisible(false);
                dismissLayer?.Remove();
                dismissLayer = null;
            };

            _stage.AddElement(dismissLayer);
            _stage.AddElement(dialog);
        }

        /// <summary>Full-screen transparent element that invokes an action when clicked (consuming the click).</summary>
        private class DismissLayer : Element, IInputListener
        {
            private System.Action _onDismiss;

            public DismissLayer(System.Action onDismiss)
            {
                _onDismiss = onDismiss;
                SetTouchable(Touchable.Enabled);
            }

            bool IInputListener.OnLeftMousePressed(Vector2 mousePos)  { _onDismiss?.Invoke(); return true; }
            bool IInputListener.OnRightMousePressed(Vector2 mousePos) { _onDismiss?.Invoke(); return true; }
            void IInputListener.OnLeftMouseUp(Vector2 mousePos)  { }
            void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
            void IInputListener.OnMouseEnter()               { }
            void IInputListener.OnMouseExit()                { }
            void IInputListener.OnMouseMoved(Vector2 mousePos) { }
            bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
        }
    }
}

