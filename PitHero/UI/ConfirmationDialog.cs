using Nez.UI;

namespace PitHero.UI
{
    /// <summary>Simple confirmation dialog for yes/no choices.</summary>
    public class ConfirmationDialog : Window
    {
        public ConfirmationDialog(string title, string message, Skin skin, System.Action onYes, System.Action onNo = null) : base(title, skin)
        {
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

            var yesButton = new TextButton("Yes", skin);
            yesButton.OnClicked += (button) =>
            {
                onYes?.Invoke();
                Remove();
            };
            buttonTable.Add(yesButton).Width(80).SetPadRight(10);

            var noButton = new TextButton("No", skin);
            noButton.OnClicked += (button) =>
            {
                onNo?.Invoke();
                Remove();
            };
            buttonTable.Add(noButton).Width(80);

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
        }
    }
}
