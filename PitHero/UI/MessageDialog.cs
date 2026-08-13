using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>Simple message dialog with a single OK button.</summary>
    public class MessageDialog : Window
    {
        public TextButton OkButton { get; }

        public MessageDialog(string title, string message, Skin skin, System.Action onOk = null) : base(title, skin)
        {
            var textService = Core.Services.GetService<TextService>();

            SetSize(350, 160);
            SetMovable(false);

            var dialogTable = new Table();
            dialogTable.Pad(20);

            // Message
            var label = new Label(message, skin);
            label.SetWrap(true);
            dialogTable.Add(label).Width(300f).SetPadBottom(20);
            dialogTable.Row();

            var okButton = new TextButton(textService.DisplayText(TextType.UI, UITextKey.ButtonOK), skin, "ph-default");
            okButton.OnClicked += (button) =>
            {
                onOk?.Invoke();
                Remove();
            };
            dialogTable.Add(okButton).Width(80);
            OkButton = okButton;

            Add(dialogTable).Expand().Fill();
        }

        /// <summary>Shows the dialog centered on the specified stage.</summary>
        public void Show(Stage stage)
        {
            var stageWidth = stage.GetWidth();
            var stageHeight = stage.GetHeight();
            SetPosition((stageWidth - GetWidth()) / 2f, (stageHeight - GetHeight()) / 2f);

            stage.AddElement(this);
            SetVisible(true);
        }
    }
}
