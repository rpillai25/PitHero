using Nez;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// Shared entry point for the manual job change flow (issue #379). Used by both the Hero
    /// Statue world click and the Hero Info tab's Change Job button: confirms with the player,
    /// then flags the hero so GOAP routes it to the statue for the crystal ceremony.
    /// </summary>
    public static class JobChangeFlow
    {
        /// <summary>True while a job change may be requested: a living hero with a crystal and
        /// no ceremony already in flight (covers both respawn and a pending manual change).</summary>
        public static bool CanRequestJobChange()
        {
            var heroComponent = Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();
            return heroComponent != null
                && heroComponent.LinkedHero != null
                && !heroComponent.NeedsCrystal;
        }

        /// <summary>
        /// Shows the job change confirmation, or the "no crystal lined up" notice when the queue
        /// is empty. Safe to call from any click handler.
        /// </summary>
        public static void ShowChangeJobDialog(Stage stage, Skin skin)
        {
            if (stage == null || !CanRequestJobChange())
                return;

            var textService = Core.Services.GetService<TextService>();
            string GetText(string key) => textService?.DisplayText(TextType.UI, key) ?? key;

            var crystalService = Core.Services.GetService<CrystalCollectionService>();
            if (crystalService?.PeekQueue() == null)
            {
                var notice = new MessageDialog(GetText(UITextKey.DialogChangeJobTitle),
                    GetText(UITextKey.DialogNoCrystalQueued), skin);
                notice.OkButton.SuppressGlobalClick = true;
                notice.Show(stage);
                return;
            }

            var dialog = new ConfirmationDialog(GetText(UITextKey.DialogChangeJobTitle),
                GetText(UITextKey.DialogChangeJobPrompt), skin,
                onYes: BeginManualJobChange);
            dialog.YesButton.SuppressGlobalClick = true;
            dialog.Show(stage);
        }

        /// <summary>
        /// Kicks off the manual job change: clears Stop mode so the statue goal isn't shadowed,
        /// sets the ceremony flags (the state machine replans off the NeedsCrystal flip), and
        /// disables saving for the duration of the transition, mirroring the respawn path.
        /// </summary>
        public static void BeginManualJobChange()
        {
            if (!CanRequestJobChange())
                return;

            // Queue may have been emptied while the confirmation sat open
            var crystalService = Core.Services.GetService<CrystalCollectionService>();
            if (crystalService?.PeekQueue() == null)
            {
                Debug.Log("[JobChangeFlow] Crystal queue emptied before confirmation - job change not started");
                return;
            }

            var heroComponent = Core.Scene.FindEntity("hero").GetComponent<HeroComponent>();
            var settingsUI = Core.Services.GetService<SettingsUI>();

            // StoppedAdventure is a higher-priority goal than NeedsCrystal; resume adventuring
            // first so the planner actually targets the statue
            if (heroComponent.StoppedAdventure)
                settingsUI?.StopAdventuringUI?.SetStopped(false);

            heroComponent.PendingManualJobChange = true;
            heroComponent.NeedsCrystal = true;

            // Same transitional-state protection as RespawnHero; re-enabled when the ceremony
            // completes (or aborts because the queue emptied during the walk)
            settingsUI?.SetSaveEnabled(false);

            Debug.Log("[JobChangeFlow] Manual job change requested - hero heading to statue");
        }
    }
}
