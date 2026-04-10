using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;
using System;

namespace PitHero.UI
{
    /// <summary>Dialog for creating new crystals from primary jobs.</summary>
    public class CrystalCreationDialog : Window
    {
        private CrystalCollectionService _crystalService;
        private TextService _textService;
        private string _selectedJobName;
        private ButtonGroup _jobButtonGroup;

        public event Action<HeroCrystal> OnCrystalCreated;

        public CrystalCreationDialog(Skin skin, CrystalCollectionService crystalService) : base("", skin)
        {
            _crystalService = crystalService;
            _textService = Core.Services?.GetService<TextService>();

            SetMovable(true);
            SetResizable(false);

            var content = new Table();
            content.Pad(10f);

            // Title
            var titleText = GetText(TextType.UI, UITextKey.CrystalCreationTitle);
            content.Add(new Label(titleText, skin, "ph-default")).Pad(5);
            content.Row();
            content.Row();

            // Job selection label
            var selectText = GetText(TextType.UI, UITextKey.CrystalCreationJobLabel);
            content.Add(new Label(selectText, skin, "ph-default")).Left().Pad(5);
            content.Row();
            content.Row();

            // Job buttons
            _jobButtonGroup = new ButtonGroup();
            var jobNames = new[] { "Knight", "Mage", "Priest", "Thief", "Monk", "Archer" };
            
            for (int i = 0; i < jobNames.Length; i++)
            {
                var jobName = jobNames[i];
                var btn = new TextButton(jobName, skin, "ph-default");
                btn.OnClicked += b => _selectedJobName = jobName;
                _jobButtonGroup.Add(btn);
                content.Add(btn).Pad(2);
                if ((i + 1) % 3 == 0) content.Row();
            }

            content.Row();

            // Buttons
            var createBtn = new TextButton(GetText(TextType.UI, UITextKey.CrystalCreationCreateButton), skin, "ph-default");
            createBtn.OnClicked += OnCreateClicked;
            var cancelBtn = new TextButton(GetText(TextType.UI, UITextKey.ButtonCancel), skin, "ph-default");
            cancelBtn.OnClicked += b => Remove();

            content.Add(createBtn).Pad(5);
            content.Add(cancelBtn).Pad(5);

            Add(content);
            Pack();
            SetPosition(400, 200);
        }

        private string GetText(TextType type, string key)
        {
            return _textService?.DisplayText(type, key) ?? key;
        }

        private void OnCreateClicked(Button btn)
        {
            if (string.IsNullOrEmpty(_selectedJobName)) return;

            var job = JobFactory.CreateJob(_selectedJobName);
            var stats = new StatBlock(
                Nez.Random.Range(2, 6),
                Nez.Random.Range(2, 6),
                Nez.Random.Range(2, 6),
                Nez.Random.Range(2, 6)
            );
            var crystal = new HeroCrystal(_selectedJobName, job, 1, stats);
            
            if (_crystalService.TryAddToInventory(crystal))
            {
                OnCrystalCreated?.Invoke(crystal);
                Remove();
            }
        }
    }
}
