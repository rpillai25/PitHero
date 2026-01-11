using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Services;
using System.Collections;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to sleep in bed and restore HP and MP to full
    /// </summary>
    public class SleepInBedAction : HeroActionBase
    {
        private ICoroutine _sleepCoroutine;
        private bool _sleepCompleted;
        private bool _isSleeping;
        private List<Point> _mercenaryOriginalPositions;

        public SleepInBedAction() : base(GoapConstants.SleepInBedAction, 5)
        {
            SetPrecondition(GoapConstants.OutsidePit, true);
            SetPrecondition(GoapConstants.HPCritical, true);

            SetPostcondition(GoapConstants.HPCritical, false);
            
            _isSleeping = false;
            _mercenaryOriginalPositions = new List<Point>();
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
                _isSleeping = false;
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
            _isSleeping = true;
            _sleepCoroutine = Core.StartCoroutine(SleepCoroutine(hero));
            return false; // Not complete yet
        }

        /// <summary>
        /// Coroutine that waits for 10 seconds and then heals the hero and hired mercenaries to full HP and MP
        /// </summary>
        private IEnumerator SleepCoroutine(HeroComponent hero)
        {
            Debug.Log("[SleepInBedAction] Hero is sleeping...");

            // Get hired mercenaries and position them in beds
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            var hiredMercenaries = mercenaryManager?.GetHiredMercenaries() ?? new List<Entity>();
            
            Debug.Log($"[SleepInBedAction] Found {hiredMercenaries.Count} hired mercenaries to position in beds");
            
            // Store original positions and disable following
            _mercenaryOriginalPositions.Clear();
            var mercBedPositions = new Point[] { new Point(76, 3), new Point(73, 7) };
            
            for (int i = 0; i < hiredMercenaries.Count && i < 2; i++)
            {
                var merc = hiredMercenaries[i];
                var mercTileMover = merc.GetComponent<TileByTileMover>();
                var mercFollowComp = merc.GetComponent<MercenaryFollowComponent>();
                
                if (mercTileMover != null)
                {
                    // Store original position
                    var originalPos = mercTileMover.GetCurrentTileCoordinates();
                    _mercenaryOriginalPositions.Add(originalPos);
                    
                    Debug.Log($"[SleepInBedAction] Mercenary {i + 1} original position: ({originalPos.X},{originalPos.Y})");
                    
                    // CRITICAL: Disable following component FIRST to prevent interference
                    if (mercFollowComp != null)
                    {
                        mercFollowComp.Enabled = false;
                        Debug.Log($"[SleepInBedAction] Disabled following for mercenary {i + 1}");
                    }
                    
                    // Stop any current movement immediately
                    if (mercTileMover.IsMoving)
                    {
                        Debug.Log($"[SleepInBedAction] Mercenary {i + 1} was moving, forcefully stopping movement");
                        mercTileMover.StopMoving(); // This will snap to current tile and clear movement state
                    }
                    
                    // Move mercenary to bed position
                    var bedPos = mercBedPositions[i];
                    var bedWorldPos = new Vector2(
                        bedPos.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                        bedPos.Y * GameConfig.TileSize + GameConfig.TileSize / 2
                    );
                    
                    Debug.Log($"[SleepInBedAction] Moving mercenary {i + 1} from ({originalPos.X},{originalPos.Y}) to bed at ({bedPos.X},{bedPos.Y})");
                    
                    merc.Transform.Position = bedWorldPos;
                    mercTileMover.SnapToTileGrid();
                    
                    // Update LastTilePosition to prevent pathfinding issues
                    var mercComp = merc.GetComponent<MercenaryComponent>();
                    if (mercComp != null)
                    {
                        mercComp.LastTilePosition = bedPos;
                    }
                    
                    Debug.Log($"[SleepInBedAction] Mercenary {i + 1} positioned at bed ({bedPos.X},{bedPos.Y})");
                }
            }

            // Wait for 10 seconds (sleep duration)
            float elapsed = 0f;
            while (elapsed < 10f)
            {
                elapsed += Time.DeltaTime;
                yield return null;
            }

            Debug.Log("[SleepInBedAction] Sleep complete, restoring HP and MP to full for hero and mercenaries");

            // Heal hero to full HP and MP
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
                    Debug.Log($"[SleepInBedAction] Restored {healAmount} HP to hero. Current HP: {hero.LinkedHero.CurrentHP}/{hero.LinkedHero.MaxHP}");
                }
                else
                {
                    Debug.Log("[SleepInBedAction] Hero already at full HP");
                }

                if (mpRestored)
                {
                    int mpRestoreAmount = hero.LinkedHero.CurrentMP - mpBefore;
                    Debug.Log($"[SleepInBedAction] Restored {mpRestoreAmount} MP to hero. Current MP: {hero.LinkedHero.CurrentMP}/{hero.LinkedHero.MaxMP}");
                }
                else
                {
                    Debug.Log("[SleepInBedAction] Hero already at full MP");
                }
            }

            // Heal hired mercenaries to full HP and MP
            for (int i = 0; i < hiredMercenaries.Count; i++)
            {
                var merc = hiredMercenaries[i];
                var mercComp = merc.GetComponent<MercenaryComponent>();
                
                if (mercComp?.LinkedMercenary != null)
                {
                    int hpBefore = mercComp.LinkedMercenary.CurrentHP;
                    int mpBefore = mercComp.LinkedMercenary.CurrentMP;
                    
                    // Calculate how much HP/MP to restore
                    int hpToRestore = mercComp.LinkedMercenary.MaxHP - mercComp.LinkedMercenary.CurrentHP;
                    int mpToRestore = mercComp.LinkedMercenary.MaxMP - mercComp.LinkedMercenary.CurrentMP;
                    
                    // Restore HP and MP to full
                    if (hpToRestore > 0)
                    {
                        mercComp.LinkedMercenary.Heal(hpToRestore);
                        Debug.Log($"[SleepInBedAction] Restored {hpToRestore} HP to mercenary {mercComp.LinkedMercenary.Name}. Current HP: {mercComp.LinkedMercenary.CurrentHP}/{mercComp.LinkedMercenary.MaxHP}");
                    }
                    else
                    {
                        Debug.Log($"[SleepInBedAction] Mercenary {mercComp.LinkedMercenary.Name} already at full HP");
                    }
                    
                    if (mpToRestore > 0)
                    {
                        mercComp.LinkedMercenary.RestoreMP(mpToRestore);
                        Debug.Log($"[SleepInBedAction] Restored {mpToRestore} MP to mercenary {mercComp.LinkedMercenary.Name}. Current MP: {mercComp.LinkedMercenary.CurrentMP}/{mercComp.LinkedMercenary.MaxMP}");
                    }
                    else
                    {
                        Debug.Log($"[SleepInBedAction] Mercenary {mercComp.LinkedMercenary.Name} already at full MP");
                    }
                }
            }

            // Wait a brief moment before waking up mercenaries
            yield return Coroutine.WaitForSeconds(0.5f);

            // Re-enable mercenary following
            for (int i = 0; i < hiredMercenaries.Count; i++)
            {
                var merc = hiredMercenaries[i];
                var mercFollowComp = merc.GetComponent<MercenaryFollowComponent>();
                
                if (mercFollowComp != null)
                {
                    mercFollowComp.Enabled = true;
                    mercFollowComp.ResetPathfinding(); // Reset pathfinding state
                    Debug.Log($"[SleepInBedAction] Re-enabled following for mercenary {i + 1}");
                }
            }

            // Mark sleep as completed and clear the coroutine reference
            _sleepCompleted = true;
            _sleepCoroutine = null;
            _isSleeping = false;

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
        public bool IsSleeping => _isSleeping;
    }
}
