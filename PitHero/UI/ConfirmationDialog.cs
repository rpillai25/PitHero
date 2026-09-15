using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>Simple confirmation dialog for yes/no choices.</summary>
    public class ConfirmationDialog : Window, IUIPrompt
    {
        public TextButton YesButton { get; }
        public TextButton NoButton { get; }

        private readonly System.Action _onNo;

        /// <summary>
        /// True while ANY blocking prompt is on a stage and visible — not just confirmation dialogs.
        /// Kept here because callers across the UI already ask this question through this name.
        /// </summary>
        public static bool AnyVisible => UIPromptRegistry.AnyVisible;

        /// <summary>
        /// Cancels the most recently shown visible prompt as if its Cancel/No button were clicked.
        /// Returns true if one was cancelled (used by Escape handling).
        /// </summary>
        public static bool TryCancelTopMost() => UIPromptRegistry.TryCancelTopMost();

        bool IUIPrompt.IsPromptVisible => GetParent() != null && IsVisible();

        void IUIPrompt.CancelPrompt()
        {
            _onNo?.Invoke();
            Remove();
        }

        public ConfirmationDialog(string title, string message, Skin skin, System.Action onYes, System.Action onNo = null)
            : this(title, message, skin, onYes, onNo, null)
        {
        }

        /// <summary>
        /// Creates a confirmation dialog that also displays <paramref name="detailContent"/> above the
        /// message — used to show an item's card in the Second Chance buy/sell prompts, so the player
        /// still sees the item's details now that hover cards are suppressed during a drag.
        /// When detail content is supplied the dialog sizes itself to fit instead of using the fixed
        /// 350x180 box.
        /// </summary>
        public ConfirmationDialog(string title, string message, Skin skin, System.Action onYes,
                                  System.Action onNo, Element detailContent)
            : this(title, message, skin, onYes, onNo, detailContent, null)
        {
        }

        /// <summary>
        /// Adds an optional <paramref name="warning"/> paragraph in red under the message (its own
        /// line, wrapped like the message). With a warning the dialog fits its content instead of
        /// the fixed 350x180 box, since the extra paragraph would otherwise clip.
        /// </summary>
        public ConfirmationDialog(string title, string message, Skin skin, System.Action onYes,
                                  System.Action onNo, Element detailContent, string warning,
                                  string warningStyle = "ph-warning") : base(title, skin)
        {
            _onNo = onNo;
            var textService = Core.Services.GetService<TextService>();
            bool fitToContent = detailContent != null || !string.IsNullOrEmpty(warning);

            if (!fitToContent)
                SetSize(350, 180);
            SetMovable(false);
            // SetModal(true); // Not available in this version of Nez

            var dialogTable = new Table();
            dialogTable.Pad(20);

            if (detailContent != null)
            {
                detailContent.SetTouchable(Touchable.Disabled);
                dialogTable.Add(detailContent).SetPadBottom(12f);
                dialogTable.Row();
            }

            // Message
            var label = new Label(message, skin);
            label.SetWrap(true);
            dialogTable.Add(label).Width(300f).SetPadBottom(string.IsNullOrEmpty(warning) ? 20f : 10f);
            dialogTable.Row();

            if (!string.IsNullOrEmpty(warning))
            {
                var warningLabel = new Label(warning, skin, warningStyle);
                warningLabel.SetWrap(true);
                dialogTable.Add(warningLabel).Width(300f).SetPadBottom(20);
                dialogTable.Row();
            }

            // Button row
            var buttonTable = new Table();

            var yesButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ButtonYes), skin, "ph-default");
            yesButton.OnClicked += (button) =>
            {
                onYes?.Invoke();
                Remove();
            };
            buttonTable.Add(yesButton).Width(80).SetMinHeight(GameConfig.DialogButtonMinHeight).SetPadRight(10);

            var noButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ButtonNo), skin, "ph-default");
            noButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            noButton.OnClicked += (button) =>
            {
                onNo?.Invoke();
                Remove();
            };
            buttonTable.Add(noButton).Width(80).SetMinHeight(GameConfig.DialogButtonMinHeight);

            YesButton = yesButton;
            NoButton = noButton;

            dialogTable.Add(buttonTable);

            Add(dialogTable).Expand().Fill();

            // With detail content or a warning paragraph the box is no longer a fixed size — fit it.
            if (fitToContent)
                Pack();
        }

        /// <summary>Shows the dialog on the specified stage.</summary>
        public void Show(Stage stage)
        {
            // Center the dialog
            var stageWidth = stage.GetWidth();
            var stageHeight = stage.GetHeight();
            SetPosition((stageWidth - GetWidth()) / 2f, UILayout.CenterY(GetHeight(), stageHeight, 0f));

            stage.AddElement(this);
            SetVisible(true);
            UIPromptRegistry.Register(this);
        }
    }
}
