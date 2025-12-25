using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using System.Collections;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to sleep in bed and restore HP and MP to full
    /// </summary>
    public class SleepInBedAction : HeroActionBase
    {
        private ICoroutine _sleepCoroutine;
        private bool _sleepCompleted;

        public SleepInBedAction() : base(GoapConstants.SleepInBedAction, 5)
        {
            SetPrecondition(GoapConstants.OutsidePit, true);
            SetPrecondition(GoapConstants.HPCritical, true);

            SetPostcondition(GoapConstants.HPCritical, false);
        }

        /// <summary>
        /// Execute the sleep action - sleep for 10 seconds and restore full HP and MP
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            // If we've already completed sleeping, return true
            if (_sleepCompleted)
            {
                Debug.Log("[SleepInBedAction] Sleep previously completed, resetting for next use");
                _sleepCompleted = false; // Reset for next time this action is used
                return true;
            }

            // If coroutine is still running, return false (not complete)
            if (_sleepCoroutine != null)
            {
                Debug.Log("[SleepInBedAction] Sleep in progress...");
                return false;
            }

            // Start the sleep coroutine
            Debug.Log("[SleepInBedAction] Starting sleep action");
            _sleepCoroutine = Core.StartCoroutine(SleepCoroutine(hero));
            return false; // Not complete yet
        }

        /// <summary>
        /// Coroutine that waits for 10 seconds and then heals the hero to full HP and MP
        /// </summary>
        private IEnumerator SleepCoroutine(HeroComponent hero)
        {
            Debug.Log("[SleepInBedAction] Hero is sleeping...");

            // Wait for 10 seconds (sleep duration)
            float elapsed = 0f;
            while (elapsed < 10f)
            {
                elapsed += Time.DeltaTime;
                yield return null;
            }

            Debug.Log("[SleepInBedAction] Sleep complete, restoring HP and MP to full");

            // Heal to full HP and MP
            if (hero.LinkedHero != null)
            {
                int hpBefore = hero.LinkedHero.CurrentHP;
                int mpBefore = hero.LinkedHero.CurrentMP;

                // Restore HP to full
                bool hpRestored = hero.LinkedHero.RestoreHP(hero.LinkedHero.MaxHP - hero.LinkedHero.CurrentHP);
                
                // Restore MP to full (negative amount = full restore)
                bool mpRestored = hero.LinkedHero.RestoreMP(-1);

                if (hpRestored)
                {
                    int healAmount = hero.LinkedHero.CurrentHP - hpBefore;
                    Debug.Log($"[SleepInBedAction] Restored {healAmount} HP. Current HP: {hero.LinkedHero.CurrentHP}/{hero.LinkedHero.MaxHP}");
                }
                else
                {
                    Debug.Log("[SleepInBedAction] Already at full HP");
                }

                if (mpRestored)
                {
                    int mpRestoreAmount = hero.LinkedHero.CurrentMP - mpBefore;
                    Debug.Log($"[SleepInBedAction] Restored {mpRestoreAmount} MP. Current MP: {hero.LinkedHero.CurrentMP}/{hero.LinkedHero.MaxMP}");
                }
                else
                {
                    Debug.Log("[SleepInBedAction] Already at full MP");
                }
            }

            // Mark sleep as completed and clear the coroutine reference
            _sleepCompleted = true;
            _sleepCoroutine = null;

            Debug.Log("[SleepInBedAction] Sleep action completed, ready to return to pit");
        }

        /// <summary>
        /// Execute action using interface-based context (new approach)
        /// </summary>
        public override bool Execute(IGoapContext context)
        {
            context.LogDebug("[SleepInBedAction] Sleep action executed (interface-based context)");
            // For virtual game context, just restore HP immediately
            // In real game, the coroutine handles the delay
            return true;
        }

        /// <summary>
        /// Check if the sleep action is still in progress
        /// </summary>
        public bool IsSleeping => _sleepCoroutine != null;
    }
}
