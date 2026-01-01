using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// Popup dialog for hiring mercenaries
    /// </summary>
    public class MercenaryHireDialog : Table
    {
        private readonly Label _titleLabel;
        private readonly Label _jobLabel;
        private readonly Label _levelLabel;
        private readonly TextButton _hireButton;
        private readonly TextButton _cancelButton;
        private Entity _mercenaryEntity;
        private bool _isVisible;

        public MercenaryHireDialog()
        {
            SetBackground(new PrimitiveDrawable(new Color(40, 40, 40, 240)));
            Pad(20f);

            _titleLabel = new Label("Hire Mercenary?", new LabelStyle(Core.Content.LoadBitmapFont("Content/Fonts/HUD.fnt"), Color.White));
            Add(_titleLabel).SetColspan(2).Center();
            Row().SetPadTop(10f);

            _jobLabel = new Label("Job: Knight", new LabelStyle(Core.Content.LoadBitmapFont("Content/Fonts/HUD.fnt"), Color.White));
            Add(_jobLabel).SetColspan(2).Left();
            Row().SetPadTop(5f);

            _levelLabel = new Label("Level: 1", new LabelStyle(Core.Content.LoadBitmapFont("Content/Fonts/HUD.fnt"), Color.White));
            Add(_levelLabel).SetColspan(2).Left();
            Row().SetPadTop(15f);

            var buttonStyle = new TextButtonStyle(
                new PrimitiveDrawable(new Color(60, 60, 60)),
                new PrimitiveDrawable(new Color(80, 80, 80)),
                new PrimitiveDrawable(new Color(40, 40, 40))
            )
            {
                Font = Core.Content.LoadBitmapFont("Content/Fonts/HUD.fnt"),
                FontColor = Color.White
            };

            _hireButton = new TextButton("Hire", buttonStyle);
            _hireButton.OnClicked += OnHireClicked;
            Add(_hireButton).SetPadRight(10f).SetMinWidth(80f).SetMinHeight(30f);

            _cancelButton = new TextButton("Cancel", buttonStyle);
            _cancelButton.OnClicked += OnCancelClicked;
            Add(_cancelButton).SetMinWidth(80f).SetMinHeight(30f);

            SetVisible(false);
        }

        /// <summary>Shows the dialog for a specific mercenary</summary>
        public void Show(Entity mercenaryEntity)
        {
            _mercenaryEntity = mercenaryEntity;
            var mercComponent = mercenaryEntity.GetComponent<MercenaryComponent>();
            if (mercComponent == null) return;

            var merc = mercComponent.LinkedMercenary;
            _jobLabel.SetText($"Job: {merc.Job.Name}");
            _levelLabel.SetText($"Level: {merc.Level}");

            SetVisible(true);
            _isVisible = true;

            // Center the dialog on screen
            var stage = GetStage();
            if (stage != null)
            {
                Pack();
                SetPosition(
                    (stage.GetWidth() - GetWidth()) / 2f,
                    (stage.GetHeight() - GetHeight()) / 2f
                );
            }
        }

        /// <summary>Hides the dialog</summary>
        public void Hide()
        {
            SetVisible(false);
            _isVisible = false;
            _mercenaryEntity = null;
        }

        /// <summary>Gets whether the dialog is currently visible</summary>
        public bool IsDialogVisible => _isVisible;

        private void OnHireClicked(Button button)
        {
            if (_mercenaryEntity == null) return;

            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager != null)
            {
                if (mercenaryManager.HireMercenary(_mercenaryEntity))
                {
                    Debug.Log("[MercenaryHireDialog] Successfully hired mercenary");
                }
                else
                {
                    Debug.Warn("[MercenaryHireDialog] Failed to hire mercenary");
                }
            }

            Hide();
        }

        private void OnCancelClicked(Button button)
        {
            Hide();
        }
    }
}
