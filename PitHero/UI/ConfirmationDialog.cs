using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>Simple confirmation dialog for yes/no choices.</summary>
    public class ConfirmationDialog : Window
    {
        public TextButton YesButton { get; }
        public TextButton NoButton { get; }

        private readonly System.Action _onNo;

        // Shown dialogs, tracked so window-level outside-click/Escape handling can defer to an open
        // confirmation. Self-pruning: entries removed from the stage drop out on the next query.
        private static readonly System.Collections.Generic.List<ConfirmationDialog> _shown = new System.Collections.Generic.List<ConfirmationDialog>();

        /// <summary>True while any confirmation dialog is on a stage and visible.</summary>
        public static bool AnyVisible
        {
            get
            {
                Prune();
                return _shown.Count > 0;
            }
        }

        /// <summary>
        /// Cancels the most recently shown visible dialog as if its No button were clicked.
        /// Returns true if a dialog was cancelled (used by Escape handling).
        /// </summary>
        public static bool TryCancelTopMost()
        {
            Prune();
            if (_shown.Count == 0) return false;
            var dialog = _shown[_shown.Count - 1];
            _shown.RemoveAt(_shown.Count - 1);
            dialog._onNo?.Invoke();
            dialog.Remove();
            return true;
        }

        private static void Prune()
        {
            for (int i = _shown.Count - 1; i >= 0; i--)
            {
                var d = _shown[i];
                if (d.GetParent() == null || !d.IsVisible())
                    _shown.RemoveAt(i);
            }
        }

        public ConfirmationDialog(string title, string message, Skin skin, System.Action onYes, System.Action onNo = null) : base(title, skin)
        {
            _onNo = onNo;
            var textService = Core.Services.GetService<TextService>();
            
            SetSize(350, 180);
            SetMovable(false);
            // SetModal(true); // Not available in this version of Nez

            var dialogTable = new Table();
            dialogTable.Pad(20);

            // Message
            var label = new Label(message, skin);
            label.SetWrap(true);
            dialogTable.Add(label).Width(300f).SetPadBottom(20);
            dialogTable.Row();

            // Button row
            var buttonTable = new Table();

            var yesButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ButtonYes), skin, "ph-default");
            yesButton.OnClicked += (button) =>
            {
                onYes?.Invoke();
                Remove();
            };
            buttonTable.Add(yesButton).Width(80).SetPadRight(10);

            var noButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ButtonNo), skin, "ph-default");
            noButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            noButton.OnClicked += (button) =>
            {
                onNo?.Invoke();
                Remove();
            };
            buttonTable.Add(noButton).Width(80);

            YesButton = yesButton;
            NoButton = noButton;

            dialogTable.Add(buttonTable);

            Add(dialogTable).Expand().Fill();
        }

        /// <summary>Shows the dialog on the specified stage.</summary>
        public void Show(Stage stage)
        {
            // Center the dialog
            var stageWidth = stage.GetWidth();
            var stageHeight = stage.GetHeight();
            SetPosition((stageWidth - GetWidth()) / 2f, (stageHeight - GetHeight()) / 2f);

            stage.AddElement(this);
            SetVisible(true);
            if (!_shown.Contains(this))
                _shown.Add(this);
        }
    }
}
