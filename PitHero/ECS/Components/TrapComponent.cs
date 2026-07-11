using Microsoft.Xna.Framework;
using Nez;
using PitHero.Services;
using PitHero.Services.Analytics;
using System.Collections.Generic;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Hidden trap tile spawned in the pit. Triggers when the hero steps onto this tile,
    /// dealing out-of-battle chip damage scaled by pit level.
    ///
    /// Damage formula: 5 + pitLevel * 2
    ///   Rationale: at pit level 1 this is 7 damage (weak chip); at pit level 25 it is 55 damage
    ///   (~25-30% of a level-appropriate hero's HP), matching the MonsterBalanceGuide "chip damage"
    ///   intent — painful but not lethal on its own.
    ///
    /// Mercenaries are abstract while walking; damage is applied to the hero only.
    ///
    /// Out-of-battle HP floor: if damage would reduce the hero below 1 HP, it is clamped so the
    /// hero survives with 1 HP. No death-outside-battle flow exists in this codebase; clamping
    /// is the chosen convention for all out-of-battle chip damage.
    /// </summary>
    public class TrapComponent : Component
    {
        // ── Static active-trap registry ───────────────────────────────────────────────
        // Maintained by OnAddedToEntity/OnRemovedFromEntity so CheckTrapSenseDisarm can iterate
        // without calling FindEntitiesWithTag (which allocates a new list every invocation).

        /// <summary>
        /// All live TrapComponent instances in the current scene.
        /// Populated in <see cref="OnAddedToEntity"/> and cleared in <see cref="OnRemovedFromEntity"/>.
        /// Iterate this instead of <c>FindEntitiesWithTag(TAG_TRAP)</c> to avoid per-fog-step allocation.
        /// </summary>
        public static readonly List<TrapComponent> ActiveTraps = new List<TrapComponent>(32);

        // ── Instance state ────────────────────────────────────────────────────────────

        /// <summary>The pit level this trap was spawned on.</summary>
        public int PitLevel { get; private set; }

        /// <summary>
        /// Damage this trap deals when triggered (5 + pitLevel * 2).
        /// See class summary for formula rationale.
        /// </summary>
        public int Damage => 5 + PitLevel * 2;

        // Re-entry guards: Trigger and Disarm both call Entity.Destroy; guard prevents a double-destroy
        // if the Nez event fire order causes two calls in the same frame.
        private bool _triggered;
        private bool _disarmed;

        /// <summary>Creates a TrapComponent for the given pit level.</summary>
        public TrapComponent(int pitLevel)
        {
            PitLevel = pitLevel;
        }

        // ── Nez lifecycle ─────────────────────────────────────────────────────────────

        /// <summary>Registers this trap in the static active-trap registry.</summary>
        public override void OnAddedToEntity()
        {
            ActiveTraps.Add(this);
        }

        /// <summary>Removes this trap from the static active-trap registry.</summary>
        public override void OnRemovedFromEntity()
        {
            // Backward search so the common case (last added = first removed) is O(1)
            for (int i = ActiveTraps.Count - 1; i >= 0; i--)
            {
                if (ActiveTraps[i] == this)
                {
                    ActiveTraps.RemoveAt(i);
                    break;
                }
            }
        }

        // ── Trap actions ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires when the hero steps on this trap tile. Deals chip damage, emits a console event,
        /// logs analytics, then destroys this trap entity.
        /// Only the hero is damaged — mercenaries are abstract while the party walks the pit.
        /// Idempotent: subsequent calls after the first are ignored.
        /// </summary>
        public void Trigger(HeroComponent heroComponent)
        {
            if (_triggered || _disarmed) return;
            _triggered = true;

            if (heroComponent?.LinkedHero == null)
                return;

            var hero = heroComponent.LinkedHero;
            int damage = Damage;

            // Clamp so trap never reduces hero to 0 HP: out-of-battle chip damage does not insta-kill.
            // No existing death-outside-battle flow exists in this codebase; clamping is the convention.
            int actualDamage = System.Math.Min(damage, hero.CurrentHP - 1);
            if (actualDamage < 0)
                actualDamage = 0;

            if (actualDamage > 0)
                hero.TakeDamage(actualDamage);

            // Tile coordinates for analytics
            var pos = Entity.Transform.Position;
            int tileX = (int)System.Math.Floor(pos.X / GameConfig.TileSize);
            int tileY = (int)System.Math.Floor(pos.Y / GameConfig.TileSize);

            AnalyticsService.LogTrapTriggered(PitLevel, tileX, tileY, actualDamage);

            // Console event (high priority — player should notice losing HP unexpectedly)
            var gameEventService = Core.Services?.GetService<GameEventService>();
            gameEventService?.EmitLocalized(EventPriority.High, UITextKey.ConsoleTrapTriggered,
                (hero.Name, GameConfig.ConsoleColorHeroName),
                (actualDamage.ToString(), Color.Red));

            Debug.Log($"[TrapComponent] Hero '{hero.Name}' triggered trap at tile ({tileX},{tileY}) for {actualDamage} damage (capped from {damage})");

            Entity.Destroy();
        }

        /// <summary>
        /// Auto-disarms this trap when TrapSense reveals it via fog clearing.
        /// No damage is dealt. Emits a console event, logs analytics, then destroys the trap entity.
        /// Idempotent: subsequent calls after the first are ignored.
        /// </summary>
        public void Disarm()
        {
            if (_disarmed || _triggered) return;
            _disarmed = true;

            var pos = Entity.Transform.Position;
            int tileX = (int)System.Math.Floor(pos.X / GameConfig.TileSize);
            int tileY = (int)System.Math.Floor(pos.Y / GameConfig.TileSize);

            AnalyticsService.LogTrapDisarmed(PitLevel, tileX, tileY);

            var gameEventService = Core.Services?.GetService<GameEventService>();
            gameEventService?.EmitLocalized(UITextKey.ConsoleTrapDisarmed);

            Debug.Log($"[TrapComponent] Trap disarmed at tile ({tileX},{tileY}) via TrapSense");

            Entity.Destroy();
        }
    }
}
