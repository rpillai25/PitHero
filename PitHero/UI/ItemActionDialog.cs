using Microsoft.Xna.Framework;
using Nez.UI;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>
    /// The Second Chance buy/sell prompt: the item's own card panel with a confirm and a Cancel
    /// button inside it. Deliberately NOT a Window — nesting the card inside a second frame looked
    /// like two stacked boxes. The card already shows the price, so there is no prompt sentence.
    ///
    /// Registers with <see cref="UIPromptRegistry"/> like every other blocking prompt, so drag
    /// suppression, hover-card suppression, outside-click dismissal, and Escape all see it.
    /// </summary>
    public class ItemActionDialog : Table, IUIPrompt
    {
        private const float ButtonWidth = 70f;
        private const float ButtonGap = 8f;

        private readonly System.Action _onCancel;

        /// <summary>The confirm button ("Buy" / "Sell"), exposed so callers can tune click sound behaviour.</summary>
        public TextButton ConfirmButton { get; }

        /// <summary>The Cancel button.</summary>
        public TextButton CancelButton { get; }

        bool IUIPrompt.IsPromptVisible => GetParent() != null && IsVisible();

        void IUIPrompt.CancelPrompt()
        {
            _onCancel?.Invoke();
            Remove();
        }

        /// <summary>
        /// Builds the prompt around <paramref name="item"/>.
        /// </summary>
        /// <param name="confirmText">Label for the confirm button — "Buy" or "Sell".</param>
        /// <param name="showBuyPrice">True to show the item's buy price, false for its sell price.</param>
        public ItemActionDialog(IItem item, string confirmText, string cancelText, Skin skin,
                                bool showBuyPrice, System.Action onConfirm, System.Action onCancel = null)
        {
            _onCancel = onCancel;

            ConfirmButton = new TextButton(confirmText, skin, "ph-default");
            ConfirmButton.OnClicked += (_) =>
            {
                Remove();
                onConfirm?.Invoke();
            };

            CancelButton = new TextButton(cancelText, skin, "ph-default");
            CancelButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            CancelButton.OnClicked += (_) =>
            {
                Remove();
                onCancel?.Invoke();
            };

            var buttonRow = new Table();
            buttonRow.Add(ConfirmButton).Width(ButtonWidth)
                     .SetMinHeight(GameConfig.DialogButtonMinHeight).SetPadRight(ButtonGap);
            buttonRow.Add(CancelButton).Width(ButtonWidth)
                     .SetMinHeight(GameConfig.DialogButtonMinHeight);

            // The card panel IS the dialog frame; the buttons live inside it.
            var card = ItemCardTooltip.BuildDetachedCard(item, null, showBuyPrice, skin, buttonRow);
            if (card != null)
                Add(card);
            Pack();
        }

        /// <summary>Shows the prompt centred on the given stage.</summary>
        public void Show(Stage stage)
        {
            if (stage == null)
                return;

            Pack();
            SetPosition((stage.GetWidth() - GetWidth()) / 2f,
                        UILayout.CenterY(GetHeight(), stage.GetHeight(), 0f));
            stage.AddElement(this);
            SetVisible(true);
            ToFront();
            UIPromptRegistry.Register(this);
        }
    }
}
