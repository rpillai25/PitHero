using Nez;
using Nez.UI;
using PitHero.Artifacts;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// The artifact card: the artifact's sprite, name and description. From the Party tab it closes
    /// with OK; from the shop it also offers a Buy button that hands the purchase back to the caller.
    /// Registered as a UI prompt so outside-click dismissal of the parent window waits for it.
    /// </summary>
    public class ArtifactInfoDialog : Window, IUIPrompt
    {
        private const float DialogWidth = 360f;
        private const float DialogHeight = 200f;
        private const float TextWidth = 300f;

        private readonly TextButton _closeButton;

        bool IUIPrompt.IsPromptVisible => GetParent() != null && IsVisible();

        void IUIPrompt.CancelPrompt() => Remove();

        /// <summary>
        /// Builds the card. When <paramref name="grantPrice"/> is set a Grant button is shown and
        /// <paramref name="onGrant"/> runs (after the card closes) when it is clicked; with
        /// <paramref name="canAfford"/> false the button is grayed and dead. The price is the wealth
        /// the player must show, not a cost.
        /// </summary>
        public ArtifactInfoDialog(ArtifactType artifact, Skin skin, int? grantPrice = null, System.Action onGrant = null, bool canAfford = true)
            : base(GetText(ArtifactCatalog.GetNameKey(artifact)), skin)
        {
            SetSize(DialogWidth, DialogHeight);
            SetMovable(false);

            var table = new Table();
            table.Pad(12f);

            if (Core.Content != null)
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                var sprite = itemsAtlas?.GetSprite(ArtifactCatalog.GetSpriteName(artifact));
                if (sprite != null)
                {
                    var image = new Image(sprite, Scaling.Fit);
                    table.Add(image).Size(GameConfig.ArtifactDialogSpriteSize, GameConfig.ArtifactDialogSpriteSize).SetPadBottom(8f);
                    table.Row();
                }
            }

            var description = new Label(GetText(ArtifactCatalog.GetDescriptionKey(artifact)), skin, "ph-default");
            description.SetWrap(true);
            description.SetAlignment(Nez.UI.Align.Center);
            table.Add(description).Width(TextWidth).SetPadBottom(12f);
            table.Row();

            var buttons = new Table();
            if (grantPrice.HasValue)
            {
                string grantText = string.Format(GetText(UITextKey.ArtifactGrantButtonFormat),
                    grantPrice.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
                var grantButton = new TextButton(grantText, skin, canAfford ? "ph-default" : "ph-grayed");
                if (canAfford)
                {
                    grantButton.OnClicked += (_) =>
                    {
                        Remove();
                        onGrant?.Invoke();
                    };
                }
                else
                {
                    // Not enough gold to show: the amount on the button says why, so it just goes dead
                    grantButton.SetDisabled(true);
                    grantButton.SetTouchable(Touchable.Disabled);
                }
                buttons.Add(grantButton).SetMinHeight(GameConfig.DialogButtonMinHeight).SetPadRight(10f);
                _closeButton = new TextButton(GetText(UITextKey.ButtonClose), skin, "ph-default");
            }
            else
            {
                _closeButton = new TextButton(GetText(UITextKey.ButtonOK), skin, "ph-default");
            }
            _closeButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            _closeButton.OnClicked += (_) => Remove();
            buttons.Add(_closeButton).Width(80f).SetMinHeight(GameConfig.DialogButtonMinHeight);
            table.Add(buttons);

            Add(table).Expand().Fill();
            UIPromptRegistry.Register(this);
        }

        /// <summary>Shows the card centered on the stage.</summary>
        public void Show(Stage stage)
        {
            SetPosition((stage.GetWidth() - GetWidth()) / 2f, UILayout.CenterY(GetHeight(), stage.GetHeight(), 0f));
            stage.AddElement(this);
            SetVisible(true);
            ToFront();
        }

        private static string GetText(string key)
        {
            var textService = Core.Services?.GetService<TextService>();
            return textService?.DisplayText(TextType.UI, key) ?? key;
        }
    }
}
