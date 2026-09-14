using Nez;
using Nez.Sprites;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.UI
{
    /// <summary>
    /// Job-levels card for one allied monster (issue #413): titled "[Type] [Name]", one row per job
    /// with its icon, name, level and progress toward the next level ("4/10", or MAX at the cap).
    /// Registered as a UI prompt so the roster window's outside-click dismissal waits for it.
    /// </summary>
    public class MonsterJobLevelsDialog : Window, IUIPrompt
    {
        private const float IconSize = 24f;
        private const float ColumnGap = 10f;

        private static readonly MonsterJob[] Jobs = { MonsterJob.Farming, MonsterJob.Cooking, MonsterJob.Fishing };
        private static readonly string[] JobSprites = { "JobFarming", "JobCooking", "JobFishing" };

        bool IUIPrompt.IsPromptVisible => GetParent() != null && IsVisible();

        void IUIPrompt.CancelPrompt() => Close();

        /// <summary>Removes the card from its stage.</summary>
        public void Close()
        {
            Remove();
        }

        /// <summary>Builds the card from the monster's current levels and task progress.</summary>
        public MonsterJobLevelsDialog(AlliedMonster monster, Skin skin)
            : base(GetText(TextType.Monster, monster.MonsterTypeName) + " " + monster.Name, skin)
        {
            SetMovable(false);
            SetResizable(false);

            var table = new Table();
            table.Pad(12f);

            var heading = new Label(GetText(TextType.UI, UITextKey.LabelJobLevelsHeading), skin, "ph-meal-header");
            table.Add(heading).Left().SetColspan(4).SetPadBottom(6f);
            table.Row();

            var uiAtlas = Core.Content?.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            string levelFormat = GetText(TextType.UI, UITextKey.LabelJobLevelFormat);
            string progressFormat = GetText(TextType.UI, UITextKey.LabelJobProgressFormat);
            string maxText = GetText(TextType.UI, UITextKey.LabelJobLevelMax);

            for (int i = 0; i < Jobs.Length; i++)
            {
                var job = Jobs[i];
                var sprite = uiAtlas?.GetSprite(JobSprites[i]);
                if (sprite != null)
                    table.Add(new Image(new SpriteDrawable(sprite), Scaling.Fit)).Size(IconSize, IconSize).SetPadRight(ColumnGap);
                else
                    table.Add().Size(IconSize, IconSize).SetPadRight(ColumnGap);

                table.Add(new Label(GetText(TextType.UI, MonsterJobTaskRecorder.GetJobNameKey(job)), skin, "ph-default"))
                    .Left().SetExpandX().SetPadRight(ColumnGap);
                table.Add(new Label(string.Format(levelFormat, monster.GetLevel(job)), skin, "ph-default"))
                    .Right().SetPadRight(ColumnGap);
                string progress = monster.IsMaxLevel(job)
                    ? maxText
                    : string.Format(progressFormat, monster.GetTasks(job), monster.GetTasksRequired(job));
                table.Add(new Label(progress, skin, "ph-default")).Right();
                table.Row();
            }

            var closeButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonClose), skin, "ph-default");
            closeButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            closeButton.OnClicked += (_) => Close();
            table.Add(closeButton).SetColspan(4).Width(80f).SetMinHeight(GameConfig.DialogButtonMinHeight).SetPadTop(10f);

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

        private static string GetText(TextType type, string key)
        {
            var textService = Core.Services?.GetService<TextService>();
            return textService?.DisplayText(type, key) ?? key;
        }
    }
}
