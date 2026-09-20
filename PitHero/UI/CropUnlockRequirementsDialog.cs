using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.UI
{
    /// <summary>
    /// "Unlock Requirements" card for a locked crop (issue #413): one cell per required crop showing
    /// its harvest sprite with a "harvested/required" badge, the crop name underneath (dimmed with
    /// "???" while that crop is itself still locked), and a Close
    /// button. Opened from the shop Seeds tab and the Farm > Seeds planting palette. Registered as a
    /// UI prompt so the parent window's outside-click dismissal waits for it.
    /// </summary>
    public class CropUnlockRequirementsDialog : Window, IUIPrompt
    {
        private const float TextWidth = 300f;
        private const float CellPad = 4f;

        bool IUIPrompt.IsPromptVisible => GetParent() != null && IsVisible();

        void IUIPrompt.CancelPrompt() => Close();

        /// <summary>Removes the card from its stage.</summary>
        public void Close()
        {
            Remove();
        }

        /// <summary>Builds the card for <paramref name="crop"/> from the live harvest totals.</summary>
        public CropUnlockRequirementsDialog(CropType crop, Skin skin)
            : base(GetText(UITextKey.WindowUnlockRequirements), skin)
        {
            SetMovable(false);
            SetResizable(false);

            var table = new Table();
            table.Pad(12f);

            string cropName = GetText(CropConfig.GetDisplayNameKey(crop));
            var hint = new Label(string.Format(GetText(UITextKey.LabelUnlockRequirementsHint), cropName), skin, "ph-default");
            hint.SetWrap(true);
            hint.SetAlignment(Nez.UI.Align.Center);
            table.Add(hint).Width(TextWidth).SetPadBottom(8f);
            table.Row();

            var cropsAtlas = Core.Content?.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            var requirements = CropUnlockConfig.GetRequirements(crop);
            var grid = new Table();
            int col = 0;
            for (int i = 0; i < requirements.Length; i++)
            {
                var req = requirements[i];
                Sprite sprite = cropsAtlas?.GetSprite(CropConfig.GetHarvestSpriteName(req.Crop));
                int have = CropUnlockTracker.GetHarvestedTotal(req.Crop);
                // A required crop that is itself still locked stays a mystery: dimmed sprite, "???" name,
                // but the have/required badge is still shown.
                bool reqLocked = !CropUnlockTracker.IsUnlocked(req.Crop);

                var cell = new Table();
                cell.Add(new RequirementCell(sprite, have, req.Required, reqLocked,
                        CropUnlockTracker.GetUnlockProgress(req.Crop)))
                    .Size(GameConfig.CropUnlockRequirementCellSize, GameConfig.CropUnlockRequirementCellSize);
                cell.Row();
                string nameText = reqLocked
                    ? GetText(UITextKey.LabelCropUnknown)
                    : GetText(CropConfig.GetHarvestDisplayNameKey(req.Crop));
                var nameLabel = new Label(nameText, skin, reqLocked ? "ph-grayed" : "ph-default");
                nameLabel.SetAlignment(Nez.UI.Align.Center);
                cell.Add(nameLabel).SetPadTop(2f);
                grid.Add(cell).Pad(CellPad).Top();

                col++;
                if (col >= GameConfig.CropUnlockRequirementsPerRow)
                {
                    grid.Row();
                    col = 0;
                }
            }
            table.Add(grid).SetPadBottom(10f);
            table.Row();

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), skin, "ph-default");
            closeButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            closeButton.OnClicked += (_) => Close();
            table.Add(closeButton).Width(80f).SetMinHeight(GameConfig.DialogButtonMinHeight);

            Add(table).Expand().Fill();
            Pack();
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
