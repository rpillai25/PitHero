using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>
    /// Builds the confirmation for selling a bag item to the Second Chance vault.
    ///
    /// Shared by both routes to that one action — dragging the item onto the shop window, and the
    /// inventory right-click menu — so they cannot drift apart. Mirrors the buy side: a stack of
    /// consumables gets the quantity dialog and sells only the chosen amount, anything else gets the
    /// single-action card.
    /// </summary>
    public static class ItemSellPrompt
    {
        /// <summary>
        /// Shows the prompt for <paramref name="item"/>.
        /// </summary>
        /// <param name="onSell">
        /// Performs the sale for the chosen unit count. Receives <see cref="int.MaxValue"/> when the
        /// whole stack is being sold, which is what a non-consumable and the single-action card pass.
        /// </param>
        /// <param name="onCancelled">Invoked when the player backs out.</param>
        public static void Show(Stage stage, Skin skin, IItem item,
                                System.Action<int> onSell, System.Action onCancelled)
        {
            if (stage == null || skin == null || item == null)
                return;

            var textService = Core.Services?.GetService<TextService>();
            string Text(string key) => textService?.DisplayText(TextType.UI, key) ?? key;

            int stackCount = (item is Consumable consumable) ? consumable.StackCount : 1;

            if (stackCount > 1)
            {
                // Card, "Sell <item>?", < N >, running total, Yes/No — the buy dialog's shape.
                var qtyDialog = new ItemQuantityDialog(
                    Text(UITextKey.WindowSecondChanceShop),
                    item.Name,
                    item.GetSellPrice(),
                    stackCount,
                    skin,
                    onConfirm: (qty) => onSell?.Invoke(qty),
                    onCancel: onCancelled,
                    // Per-unit price on the card; the total row carries the price for the chosen count.
                    detailContent: ItemCardTooltip.BuildDetachedCard(item, null, showBuyPrice: false, skin: skin),
                    mode: QuantityDialogMode.Sell);
                qtyDialog.Show(stage);
                return;
            }

            var dialog = new ItemActionDialog(item,
                Text(UITextKey.ButtonSell),
                Text(UITextKey.ButtonCancel),
                skin,
                showBuyPrice: false,
                onConfirm: () => onSell?.Invoke(int.MaxValue),
                onCancel: onCancelled);
            dialog.ConfirmButton.SuppressGlobalClick = true;
            dialog.Show(stage);
        }
    }
}
