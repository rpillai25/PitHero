using Nez;
using Nez.Textures;
using Nez.UI;
using PitHero.Dining;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// "Unlock Requirements" card for a soft-unlocked dish (issue #417): one cell per required dish
    /// showing its sprite with a "served/required" badge and the dish name underneath (dimmed with
    /// "???" while that dish is itself not yet on the menu), plus a Close button. Opened from the
    /// Food tab. Registered as a UI prompt so the parent window's outside-click dismissal waits for it.
    /// </summary>
    public class DishUnlockRequirementsDialog : Window, IUIPrompt
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

        /// <summary>Builds the card for <paramref name="dish"/> from the live served totals.</summary>
        public DishUnlockRequirementsDialog(DishType dish, Skin skin)
            : base(GetText(UITextKey.WindowUnlockRequirements), skin)
        {
            SetMovable(false);
            SetResizable(false);

            var table = new Table();
            table.Pad(12f);

            string dishName = GetText(DishConfig.GetDefinition(dish).NameKey);
            var hint = new Label(string.Format(GetText(UITextKey.LabelDishUnlockRequirementsHint), dishName), skin, "ph-default");
            hint.SetWrap(true);
            hint.SetAlignment(Nez.UI.Align.Center);
            table.Add(hint).Width(TextWidth).SetPadBottom(8f);
            table.Row();

            var cropsAtlas = Core.Content?.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            var requirements = DishUnlockConfig.GetRequirements(dish);
            var grid = new Table();
            int col = 0;
            for (int i = 0; i < requirements.Length; i++)
            {
                var req = requirements[i];
                var reqDef = DishConfig.GetDefinition(req.Dish);
                Sprite sprite = cropsAtlas?.GetSprite(reqDef.BaseSpriteName + "_Large");
                int have = DishUnlockTracker.GetServedTotal(req.Dish);
                // A required dish that hasn't reached the menu itself stays a mystery: dimmed sprite,
                // "???" name, but the have/required badge is still shown.
                bool reqLocked = !DishUnlockTracker.IsSoftUnlocked(req.Dish);

                var cell = new Table();
                cell.Add(new RequirementCell(sprite, have, req.Required, reqLocked))
                    .Size(GameConfig.CropUnlockRequirementCellSize, GameConfig.CropUnlockRequirementCellSize);
                cell.Row();
                string nameText = reqLocked ? GetText(UITextKey.LabelCropUnknown) : GetText(reqDef.NameKey);
                var nameLabel = new Label(nameText, skin, reqLocked ? "ph-grayed" : "ph-default");
                nameLabel.SetAlignment(Nez.UI.Align.Center);
                nameLabel.SetWrap(true);
                cell.Add(nameLabel).Width(GameConfig.CropUnlockRequirementCellSize * 1.6f).SetPadTop(2f);
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
