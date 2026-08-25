using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// A confirmation dialog for vault item purchases that includes a < qty > selector
    /// when the vault stack contains more than one item.  The player can raise or lower
    /// the quantity (1 … maxQty; lowering below 1 wraps around to the purchasable max)
    /// before confirming; the total gold cost updates live.
    /// When availableFunds is provided, the selectable quantity is additionally capped at
    /// what the player can afford, and the arrow buttons grey out at the limits.
    /// When maxQty == 1 the selector row is hidden and the dialog behaves like a plain
    /// ConfirmationDialog.
    /// </summary>
    /// <summary>Whether the dialog is priced and worded as a purchase or a sale.</summary>
    public enum QuantityDialogMode { Buy, Sell }

    public class ItemQuantityDialog : Window, IUIPrompt
    {
        /// <summary>Minimum height (px) for every button in this dialog. Adjust here to retune all at once.</summary>
        private const float ButtonHeight = 16f;

        private readonly QuantityDialogMode _mode;

        private string TotalKey => _mode == QuantityDialogMode.Buy
            ? UITextKey.SecondChanceBuyTotal
            : UITextKey.SecondChanceSellTotal;

        bool IUIPrompt.IsPromptVisible => GetParent() != null && IsVisible();

        void IUIPrompt.CancelPrompt()
        {
            _onCancel?.Invoke();
            Remove();
        }

        private int _quantity = 1;
        private readonly int _unitPrice;
        private readonly int _maxQty;
        private readonly bool _canAffordAny;
        private readonly int _plannedCount;
        private readonly System.Action _onCancel;
        private Label _quantityLabel;
        private Label _totalCostLabel;
        private Label _plannedLabel;
        private TextButton _decreaseBtn;
        private TextButton _increaseBtn;
        private readonly System.Action<int> _onConfirm;

        /// <param name="title">Window title text.</param>
        /// <param name="itemName">Display name shown in the prompt row.</param>
        /// <param name="unitPrice">Gold cost for a single unit.</param>
        /// <param name="maxQty">Maximum selectable quantity (vault stack size capped per slot).</param>
        /// <param name="skin">Nez.UI skin to use.</param>
        /// <param name="onConfirm">Invoked with the chosen quantity when Yes is pressed.</param>
        /// <param name="onCancel">Invoked when No is pressed (nullable).</param>
        /// <param name="ownedCount">
        /// When &gt;= 0, an "Owned: N" row is rendered above the quantity selector.
        /// Pass -1 (default) to omit the row; existing call sites are unaffected.
        /// </param>
        /// <param name="availableFunds">
        /// When &gt;= 0, the selectable quantity is capped at availableFunds / unitPrice and the
        /// Yes button is disabled when the player cannot afford even one unit. Pass -1 (default)
        /// to skip the affordability cap.
        /// </param>
        /// <param name="plannedCount">
        /// When &gt; 0, a "Need: N" label is rendered to the right of the Owned row showing
        /// how many planned crops remain uncovered after buying the current quantity
        /// (max(0, plannedCount - quantity)); the label hides once the quantity covers them all.
        /// Pass 0 (default) to omit it; existing call sites are unaffected.
        /// </param>
        public ItemQuantityDialog(
            string title,
            string itemName,
            int unitPrice,
            int maxQty,
            Skin skin,
            System.Action<int> onConfirm,
            System.Action onCancel = null,
            int ownedCount = -1,
            int availableFunds = -1,
            int plannedCount = 0,
            Element detailContent = null,
            QuantityDialogMode mode = QuantityDialogMode.Buy)
            : base(title, skin)
        {
            _mode = mode;
            _unitPrice = unitPrice;
            _onConfirm = onConfirm;
            _plannedCount = plannedCount > 0 ? plannedCount : 0;
            _onCancel = onCancel;

            int affordableMax = (availableFunds >= 0 && unitPrice > 0)
                ? availableFunds / unitPrice
                : int.MaxValue;
            _canAffordAny = affordableMax >= 1;
            int cap = maxQty < affordableMax ? maxQty : affordableMax;
            _maxQty = cap > 1 ? cap : 1;

            var textService = Core.Services.GetService<TextService>();

            // Buttons need a disabled visual; ph-default has none, and shared styles must never
            // be mutated — clone it and grey the disabled font. Clone() skips the pressed
            // offsets, so restore them by hand.
            var baseStyle = skin.Get<TextButtonStyle>("ph-default");
            var buttonStyle = baseStyle.Clone();
            buttonStyle.PressedOffsetX = baseStyle.PressedOffsetX;
            buttonStyle.PressedOffsetY = baseStyle.PressedOffsetY;
            buttonStyle.DisabledFontColor = Color.Gray;

            bool showOwned = ownedCount >= 0;
            int extraHeight = showOwned ? 30 : 0;
            // With a detail card the box is no longer a fixed size — it is packed to fit at the end.
            if (detailContent == null)
                SetSize(380, (maxQty > 1 ? 220 : 180) + extraHeight);
            SetMovable(false);

            var dialogTable = new Table();
            dialogTable.Pad(20);

            // ── Item detail card (buy prompts reached by dragging, where hover cards are off) ────
            if (detailContent != null)
            {
                detailContent.SetTouchable(Touchable.Disabled);
                dialogTable.Add(detailContent).SetPadBottom(12f);
                dialogTable.Row();
            }

            // ── Item name prompt row ────────────────────────────────────────────
            string promptText = string.Format(
                textService.DisplayText(TextType.UI, mode == QuantityDialogMode.Buy
                    ? UITextKey.SecondChanceBuyQtyPrompt
                    : UITextKey.SecondChanceSellQtyPrompt),
                itemName);
            var promptLabel = new Label(promptText, skin);
            promptLabel.SetWrap(true);
            dialogTable.Add(promptLabel).Width(330f).SetPadBottom(10);
            dialogTable.Row();

            // ── Owned count row (seeds tab only) ────────────────────────────────
            if (showOwned)
            {
                string ownedText = string.Format(
                    textService.DisplayText(TextType.UI, UITextKey.SecondChanceOwnedCount),
                    ownedCount);
                var ownedLabel = new Label(ownedText, skin);

                var ownedRow = new Table();
                ownedRow.Left();
                ownedRow.Add(ownedLabel);

                // Planned demand label (seeds with outstanding plan coverage only).
                if (_plannedCount > 0)
                {
                    int remaining = _plannedCount - _quantity;
                    if (remaining < 0) remaining = 0;
                    string plannedText = string.Format(
                        textService.DisplayText(TextType.UI, UITextKey.SecondChanceNeedCount),
                        remaining);
                    _plannedLabel = new Label(plannedText, skin);
                    _plannedLabel.SetVisible(remaining > 0);
                    ownedRow.Add(_plannedLabel).SetPadLeft(24f);
                }

                dialogTable.Add(ownedRow).Width(330f).SetPadBottom(8);
                dialogTable.Row();
            }

            // ── Quantity selector row (hidden when maxQty == 1) ─────────────────
            if (maxQty > 1)
            {
                var qtyTable = new Table();

                _decreaseBtn = new TextButton("<", buttonStyle);
                _decreaseBtn.OnClicked += (b) => ChangeQuantity(-1);
                qtyTable.Add(_decreaseBtn).Width(40).SetMinHeight(ButtonHeight).SetPadRight(8);

                _quantityLabel = new Label("1", skin);
                qtyTable.Add(_quantityLabel).Width(40);

                _increaseBtn = new TextButton(">", buttonStyle);
                _increaseBtn.OnClicked += (b) => ChangeQuantity(1);
                qtyTable.Add(_increaseBtn).Width(40).SetMinHeight(ButtonHeight).SetPadLeft(8);

                dialogTable.Add(qtyTable).SetPadBottom(8);
                dialogTable.Row();

                // ── Total cost row ──────────────────────────────────────────────
                string totalText = string.Format(
                    textService.DisplayText(TextType.UI, TotalKey),
                    unitPrice);
                _totalCostLabel = new Label(totalText, skin);
                dialogTable.Add(_totalCostLabel).Width(330f).SetPadBottom(14);
                dialogTable.Row();
            }
            else
            {
                // Single-unit: show static "Buy/Sell for X gold?" line
                string totalText = string.Format(
                    textService.DisplayText(TextType.UI, mode == QuantityDialogMode.Buy
                        ? UITextKey.SecondChanceBuyPrompt
                        : UITextKey.SecondChanceSellPrompt),
                    unitPrice);
                var totalLabel = new Label(totalText, skin);
                dialogTable.Add(totalLabel).Width(330f).SetPadBottom(20);
                dialogTable.Row();
            }

            // ── Yes / No buttons ────────────────────────────────────────────────
            var buttonTable = new Table();

            var yesButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ButtonYes), buttonStyle);
            yesButton.SuppressGlobalClick = true;
            yesButton.SetDisabled(!_canAffordAny);
            yesButton.OnClicked += (b) =>
            {
                if (!_canAffordAny)
                    return;
                _onConfirm?.Invoke(_quantity);
                Remove();
            };
            buttonTable.Add(yesButton).Width(80).SetMinHeight(ButtonHeight).SetPadRight(10);

            var noButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ButtonNo), skin, "ph-default");
            noButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            noButton.OnClicked += (b) =>
            {
                onCancel?.Invoke();
                Remove();
            };
            buttonTable.Add(noButton).Width(80).SetMinHeight(ButtonHeight);

            dialogTable.Add(buttonTable);

            Add(dialogTable).Expand().Fill();

            if (detailContent != null)
                Pack();

            UpdateArrowStates();
        }

        /// <summary>Shows the dialog centred on the given stage.</summary>
        public void Show(Stage stage)
        {
            var w = stage.GetWidth();
            var h = stage.GetHeight();
            SetPosition((w - GetWidth()) / 2f, UILayout.CenterY(GetHeight(), h, 0f));
            stage.AddElement(this);
            SetVisible(true);
            UIPromptRegistry.Register(this);
        }

        // ─────────────────────────────────────────────────────────────────────────

        private void ChangeQuantity(int delta)
        {
            int next = _quantity + delta;
            // Both arrows wrap: "<" below 1 goes to the max, ">" past the max goes back to 1.
            // (Stock cap and, when buying, affordable funds are already folded into _maxQty.)
            if (next < 1) next = _maxQty;
            else if (next > _maxQty) next = 1;
            if (next == _quantity) return;

            _quantity = next;
            _quantityLabel?.SetText(_quantity.ToString());
            UpdateArrowStates();

            if (_totalCostLabel != null)
            {
                var textService = Core.Services.GetService<TextService>();
                string totalText = string.Format(
                    textService.DisplayText(TextType.UI, TotalKey),
                    _unitPrice * _quantity);
                _totalCostLabel.SetText(totalText);
            }

            if (_plannedLabel != null)
            {
                int remaining = _plannedCount - _quantity;
                if (remaining < 0) remaining = 0;
                var textService = Core.Services.GetService<TextService>();
                string plannedText = string.Format(
                    textService.DisplayText(TextType.UI, UITextKey.SecondChanceNeedCount),
                    remaining);
                _plannedLabel.SetText(plannedText);
                _plannedLabel.SetVisible(remaining > 0);
            }
        }

        /// <summary>
        /// Greys out both arrows only when there is nothing to choose between — a single selectable
        /// unit, or (when buying) nothing affordable. Neither arrow is disabled at a limit, because
        /// both wrap around from there.
        /// </summary>
        private void UpdateArrowStates()
        {
            bool nothingToChoose = _maxQty <= 1 || !_canAffordAny;
            _decreaseBtn?.SetDisabled(nothingToChoose);
            _increaseBtn?.SetDisabled(nothingToChoose);
        }
    }
}
