using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.Services;
using RolePlayingFramework.Balance;

namespace PitHero.UI
{
    /// <summary>
    /// Popup dialog for hiring mercenaries
    /// </summary>
    public class MercenaryHireDialog : Table
    {
        private readonly Label _nameLabel;
        private readonly Label _jobLabel;
        private readonly Label _hpLabel;
        private readonly Label _mpLabel;
        private readonly Label _strLabel;
        private readonly Label _agiLabel;
        private readonly Label _vitLabel;
        private readonly Label _magLabel;
        private readonly Label _costLabel;
        private readonly TextButton _hireButton;
        private readonly TextButton _cancelButton;
        private readonly TextButtonStyle _hireButtonStyle;
        private TextTooltip _hireTooltip;
        private Entity _mercenaryEntity;
        private bool _isVisible;

        public MercenaryHireDialog()
        {
            var skin = PitHeroSkin.CreateSkin();

            // Use the same window background as PitHeroSkin WindowStyle
            var windowStyle = skin.Get<WindowStyle>("ph-default");
            SetBackground(windowStyle.Background);
            Pad(20f);

            // Name (with custom green color)
            var defaultLabelStyle = skin.Get<LabelStyle>("ph-default");
            var nameLabelStyle = new LabelStyle
            {
                Font = defaultLabelStyle.Font,
                FontColor = new Color(0, 80, 156),
                FontScaleX = defaultLabelStyle.FontScaleX,
                FontScaleY = defaultLabelStyle.FontScaleY
            };
            _nameLabel = new Label("Mercenary Name", nameLabelStyle);
            Add(_nameLabel).SetColspan(2).Center();
            Row().SetPadTop(5f);

            // Job
            _jobLabel = new Label("Job: Knight", skin);
            Add(_jobLabel).SetColspan(2).Left();
            Row().SetPadTop(10f);

            // HP
            _hpLabel = new Label("HP: 100", skin);
            Add(_hpLabel).SetColspan(2).Left();
            Row().SetPadTop(5f);

            // MP
            _mpLabel = new Label("MP: 50", skin);
            Add(_mpLabel).SetColspan(2).Left();
            Row().SetPadTop(5f);

            // STR
            _strLabel = new Label("STR: 10", skin);
            Add(_strLabel).SetColspan(2).Left();
            Row().SetPadTop(5f);

            // AGI
            _agiLabel = new Label("AGI: 10", skin);
            Add(_agiLabel).SetColspan(2).Left();
            Row().SetPadTop(5f);

            // VIT
            _vitLabel = new Label("VIT: 10", skin);
            Add(_vitLabel).SetColspan(2).Left();
            Row().SetPadTop(5f);

            // MAG
            _magLabel = new Label("MAG: 10", skin);
            Add(_magLabel).SetColspan(2).Left();
            Row().SetPadTop(5f);

            // Hire cost
            var costLabelStyle = new LabelStyle
            {
                Font = defaultLabelStyle.Font,
                FontColor = new Color(184, 138, 13)
            };
            _costLabel = new Label("Cost: 0 gold", costLabelStyle);
            Add(_costLabel).SetColspan(2).Left();
            Row().SetPadTop(15f);

            // Create hire button with a style that supports DisabledFontColor
            var baseStyle = skin.Get<TextButtonStyle>("ph-default");
            _hireButtonStyle = new TextButtonStyle
            {
                Up = baseStyle.Up,
                Down = baseStyle.Down,
                Over = baseStyle.Over,
                FontColor = baseStyle.FontColor,
                DownFontColor = baseStyle.DownFontColor,
                OverFontColor = baseStyle.OverFontColor,
                DisabledFontColor = new Color(180, 40, 40),
                PressedOffsetX = baseStyle.PressedOffsetX,
                PressedOffsetY = baseStyle.PressedOffsetY
            };

            _hireButton = new TextButton("Hire", _hireButtonStyle);
            _hireButton.OnClicked += OnHireClicked;
            Add(_hireButton).SetPadRight(10f).SetMinWidth(80f).SetMinHeight(30f);

            _cancelButton = new TextButton("Cancel", skin, "ph-default");
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
            var stats = merc.GetTotalStats();
            var maxHP = 25 + (stats.Vitality * 5);
            var maxMP = 10 + (stats.Magic * 3);

            _nameLabel.SetText(merc.Name);
            _jobLabel.SetText($"Job: {merc.Job.Name}  Lv: {merc.Level}");
            _hpLabel.SetText($"HP: {maxHP}");
            _mpLabel.SetText($"MP: {maxMP}");
            _strLabel.SetText($"STR: {stats.Strength}");
            _agiLabel.SetText($"AGI: {stats.Agility}");
            _vitLabel.SetText($"VIT: {stats.Vitality}");
            _magLabel.SetText($"MAG: {stats.Magic}");

            // Display hire cost and check affordability
            var hireCost = BalanceConfig.CalculateMercenaryHireCost(merc.Level);
            _costLabel.SetText($"Cost: {hireCost} gold");

            var gameState = Core.Services.GetService<GameStateService>();
            var canAfford = gameState != null && gameState.Funds >= hireCost;
            _hireButton.SetDisabled(!canAfford);

            // Add or remove tooltip based on affordability
            if (!canAfford)
            {
                if (_hireTooltip == null)
                {
                    var tooltipStyle = new TextTooltipStyle
                    {
                        LabelStyle = new LabelStyle
                        {
                            Font = Graphics.Instance.BitmapFont,
                            FontColor = Color.White
                        }
                    };
                    _hireTooltip = new TextTooltip("Not enough gold", _hireButton, tooltipStyle);
                    _hireTooltip.SetInstant(true);
                    _hireTooltip.SetAlways(true);
                }
            }
            else
            {
                ClearHireTooltip();
            }

            SetVisible(true);
            _isVisible = true;

            // Pause the game when dialog opens
            var pauseService = Core.Services.GetService<PauseService>();
            pauseService?.Pause();

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

            ClearHireTooltip();

            // Unpause the game when dialog closes
            var pauseService = Core.Services.GetService<PauseService>();
            pauseService?.Unpause();

            // Reset player interaction state when dialog closes
            var interactionService = Core.Services.GetService<PlayerInteractionService>();
            interactionService?.Reset();
        }

        /// <summary>Gets whether the dialog is currently visible</summary>
        public bool IsDialogVisible => _isVisible;

        private void OnHireClicked(Button button)
        {
            if (_mercenaryEntity == null) return;
            if (_hireButton.GetDisabled()) return;

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

        /// <summary>Removes the hire tooltip if present.</summary>
        private void ClearHireTooltip()
        {
            if (_hireTooltip != null)
            {
                _hireTooltip.GetContainer().Remove();
                _hireTooltip = null;
            }
        }
    }
}
