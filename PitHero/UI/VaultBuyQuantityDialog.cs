using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// A confirmation dialog for vault item purchases that includes a < qty > selector
    /// when the vault stack contains more than one item.  The player can raise or lower
    /// the quantity (1 … maxQty) before confirming; the total gold cost updates live.
    /// When maxQty == 1 the selector row is hidden and the dialog behaves like a plain
    /// ConfirmationDialog.
    /// </summary>
    public class VaultBuyQuantityDialog : Window
    {
        /// <summary>Minimum height (px) for every button in this dialog. Adjust here to retune all at once.</summary>
        private const float ButtonHeight = 16f;

        private int _quantity = 1;
        private readonly int _unitPrice;
        private readonly int _maxQty;
        private readonly bool _wrapQuantity;
        private Label _quantityLabel;
        private Label _totalCostLabel;
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
        /// <param name="wrapQuantity">
        /// When true, the quantity selector wraps around (1 → maxQty when decremented, maxQty → 1 when
        /// incremented) instead of clamping. Defaults to false so existing call sites keep clamping.
        /// </param>
        public VaultBuyQuantityDialog(
            string title,
            string itemName,
            int unitPrice,
            int maxQty,
            Skin skin,
            System.Action<int> onConfirm,
            System.Action onCancel = null,
            int ownedCount = -1,
            bool wrapQuantity = false)
            : base(title, skin)
        {
            _unitPrice = unitPrice;
            _maxQty    = maxQty > 1 ? maxQty : 1;
            _wrapQuantity = wrapQuantity;
            _onConfirm = onConfirm;

            var textService = Core.Services.GetService<TextService>();

            bool showOwned = ownedCount >= 0;
            int extraHeight = showOwned ? 30 : 0;
            SetSize(380, (maxQty > 1 ? 220 : 180) + extraHeight);
            SetMovable(false);

            var dialogTable = new Table();
            dialogTable.Pad(20);

            // ── Item name prompt row ────────────────────────────────────────────
            string promptText = string.Format(
                textService.DisplayText(TextType.UI, UITextKey.SecondChanceBuyQtyPrompt),
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
                dialogTable.Add(ownedLabel).Width(330f).SetPadBottom(8);
                dialogTable.Row();
            }

            // ── Quantity selector row (hidden when maxQty == 1) ─────────────────
            if (maxQty > 1)
            {
                var qtyTable = new Table();

                var decreaseBtn = new TextButton("<", skin, "ph-default");
                decreaseBtn.OnClicked += (b) => ChangeQuantity(-1);
                qtyTable.Add(decreaseBtn).Width(40).SetMinHeight(ButtonHeight).SetPadRight(8);

                _quantityLabel = new Label("1", skin);
                qtyTable.Add(_quantityLabel).Width(40);

                var increaseBtn = new TextButton(">", skin, "ph-default");
                increaseBtn.OnClicked += (b) => ChangeQuantity(1);
                qtyTable.Add(increaseBtn).Width(40).SetMinHeight(ButtonHeight).SetPadLeft(8);

                dialogTable.Add(qtyTable).SetPadBottom(8);
                dialogTable.Row();

                // ── Total cost row ──────────────────────────────────────────────
                string totalText = string.Format(
                    textService.DisplayText(TextType.UI, UITextKey.SecondChanceBuyTotal),
                    unitPrice);
                _totalCostLabel = new Label(totalText, skin);
                dialogTable.Add(_totalCostLabel).Width(330f).SetPadBottom(14);
                dialogTable.Row();
            }
            else
            {
                // Single-unit: show static "Buy for X gold?" line
                string totalText = string.Format(
                    textService.DisplayText(TextType.UI, UITextKey.SecondChanceBuyPrompt),
                    unitPrice);
                var totalLabel = new Label(totalText, skin);
                dialogTable.Add(totalLabel).Width(330f).SetPadBottom(20);
                dialogTable.Row();
            }

            // ── Yes / No buttons ────────────────────────────────────────────────
            var buttonTable = new Table();

            var yesButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ButtonYes), skin, "ph-default");
            yesButton.OnClicked += (b) =>
            {
                _onConfirm?.Invoke(_quantity);
                Remove();
            };
            buttonTable.Add(yesButton).Width(80).SetMinHeight(ButtonHeight).SetPadRight(10);

            var noButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ButtonNo), skin, "ph-default");
            noButton.OnClicked += (b) =>
            {
                onCancel?.Invoke();
                Remove();
            };
            buttonTable.Add(noButton).Width(80).SetMinHeight(ButtonHeight);

            dialogTable.Add(buttonTable);

            Add(dialogTable).Expand().Fill();
        }

        /// <summary>Shows the dialog centred on the given stage.</summary>
        public void Show(Stage stage)
        {
            var w = stage.GetWidth();
            var h = stage.GetHeight();
            SetPosition((w - GetWidth()) / 2f, (h - GetHeight()) / 2f);
            stage.AddElement(this);
            SetVisible(true);
        }

        // ─────────────────────────────────────────────────────────────────────────

        private void ChangeQuantity(int delta)
        {
            int next = _quantity + delta;
            if (_wrapQuantity)
            {
                if (next < 1) next = _maxQty;
                else if (next > _maxQty) next = 1;
            }
            else
            {
                if (next < 1) next = 1;
                if (next > _maxQty) next = _maxQty;
            }
            if (next == _quantity) return;

            _quantity = next;
            _quantityLabel?.SetText(_quantity.ToString());

            if (_totalCostLabel != null)
            {
                var textService = Core.Services.GetService<TextService>();
                string totalText = string.Format(
                    textService.DisplayText(TextType.UI, UITextKey.SecondChanceBuyTotal),
                    _unitPrice * _quantity);
                _totalCostLabel.SetText(totalText);
            }
        }
    }
}
