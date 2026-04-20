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
        private int _quantity = 1;
        private readonly int _unitPrice;
        private readonly int _maxQty;
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
        public VaultBuyQuantityDialog(
            string title,
            string itemName,
            int unitPrice,
            int maxQty,
            Skin skin,
            System.Action<int> onConfirm,
            System.Action onCancel = null)
            : base(title, skin)
        {
            _unitPrice = unitPrice;
            _maxQty    = maxQty > 1 ? maxQty : 1;
            _onConfirm = onConfirm;

            var textService = Core.Services.GetService<TextService>();

            SetSize(380, maxQty > 1 ? 220 : 180);
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

            // ── Quantity selector row (hidden when maxQty == 1) ─────────────────
            if (maxQty > 1)
            {
                var qtyTable = new Table();

                var decreaseBtn = new TextButton("<", skin, "ph-default");
                decreaseBtn.OnClicked += (b) => ChangeQuantity(-1);
                qtyTable.Add(decreaseBtn).Width(40).SetPadRight(8);

                _quantityLabel = new Label("1", skin);
                qtyTable.Add(_quantityLabel).Width(40);

                var increaseBtn = new TextButton(">", skin, "ph-default");
                increaseBtn.OnClicked += (b) => ChangeQuantity(1);
                qtyTable.Add(increaseBtn).Width(40).SetPadLeft(8);

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
            buttonTable.Add(yesButton).Width(80).SetPadRight(10);

            var noButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ButtonNo), skin, "ph-default");
            noButton.OnClicked += (b) =>
            {
                onCancel?.Invoke();
                Remove();
            };
            buttonTable.Add(noButton).Width(80);

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
            if (next < 1) next = 1;
            if (next > _maxQty) next = _maxQty;
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
