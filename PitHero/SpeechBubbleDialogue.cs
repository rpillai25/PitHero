using Nez;
using PitHero.ECS.Components;
using PitHero.Services;

namespace PitHero
{
    /// <summary>
    /// Centralized helper for emitting hero speech-bubble events. Every public Say* method
    /// is fully headless-safe (guards <see cref="Core.Instance"/> first).
    /// </summary>
    public static class SpeechBubbleDialogue
    {
        // IMPORTANT: Use System.Random, NOT Nez.Random.
        // The boss-defeated trigger fires inside BattleEngine.Run via
        // IBattleEventSink.OnEnemyDefeated. BattleEngine uses the global Nez.Random stream
        // as a seeded determinism contract (see PitHero/Combat/BattleEngine.cs:64-68;
        // the virtual sim seeds it at VirtualGameSimulation.cs:75). Any Nez.Random call
        // mid-battle would break BattleEngineTests and virtual/live run parity.
        private static readonly System.Random _rng = new System.Random();

        // ── Option set type ──────────────────────────────────────────────────────

        /// <summary>
        /// One variant inside a multi-choice bubble event.
        /// A null <see cref="Key"/> represents the "show nothing" silent variant.
        /// </summary>
        private readonly struct Option
        {
            /// <summary>Dialogue key, or null for the silent variant.</summary>
            public readonly string Key;

            /// <summary>When true this option is only eligible if at least one mercenary is hired.</summary>
            public readonly bool NeedsMerc;

            public Option(string key, bool needsMerc = false)
            {
                Key      = key;
                NeedsMerc = needsMerc;
            }
        }

        // ── Option tables ────────────────────────────────────────────────────────

        // Event 2 — Pit entry: "This will be a great run!" / [G]"We got this!" / (silent)
        private static readonly Option[] PitEntryOptions =
        {
            new Option(DialogueTextKey.HeroPitEntryGreatRun),
            new Option(DialogueTextKey.HeroPitEntryWeGotThis, needsMerc: true),
            new Option(null), // silent variant
        };

        // Event 4 — Pit rest: three variants, no merc gate
        private static readonly Option[] PitRestOptions =
        {
            new Option(DialogueTextKey.HeroRestSleepOff),
            new Option(DialogueTextKey.HeroRestHealAtInn),
            new Option(DialogueTextKey.HeroRestWouldBeGood),
        };

        // Event 6 — Boss defeated: [G]"Team work really pays off!" / "A worthy foe!" / (silent)
        private static readonly Option[] BossDefeatedOptions =
        {
            new Option(DialogueTextKey.HeroBossTeamwork, needsMerc: true),
            new Option(DialogueTextKey.HeroBossWorthyFoe),
            new Option(null), // silent variant
        };

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Shows the pit-adventure speech bubble (one-shot per trip; existing behavior).
        /// </summary>
        public static void SayPitAdventure(Entity entity)
        {
            SaySingle(entity, DialogueTextKey.HeroPitAdventure);
        }

        /// <summary>
        /// Shows the breakfast speech bubble (existing behavior).
        /// </summary>
        public static void SayBreakfast(Entity entity)
        {
            SaySingle(entity, DialogueTextKey.HeroBreakfast);
        }

        /// <summary>
        /// Shows the "Better gear up..." bubble when auto-purchases were made before a pit jump.
        /// </summary>
        public static void SayGearUp(Entity entity)
        {
            SaySingle(entity, DialogueTextKey.HeroGearUp);
        }

        /// <summary>
        /// Shows a randomly-picked pit-entry bubble right after landing in the pit.
        /// The merc-gated variant is only eligible when at least one mercenary is hired.
        /// </summary>
        public static void SayPitEntry(Entity entity)
        {
            SayFromOptions(entity, PitEntryOptions);
        }

        /// <summary>
        /// Shows the bedtime bubble when the hero decides to jump out for night sleep.
        /// </summary>
        public static void SayBedtime(Entity entity)
        {
            SaySingle(entity, DialogueTextKey.HeroBedtime);
        }

        /// <summary>
        /// Shows a randomly-picked rest/heal bubble when the hero decides to jump out
        /// because HP or MP is critical.
        /// </summary>
        public static void SayPitRest(Entity entity)
        {
            SayFromOptions(entity, PitRestOptions);
        }

        /// <summary>
        /// Shows the no-ingredients breakfast bubble when breakfast is skipped because
        /// ingredients are missing (not the no-gold path).
        /// </summary>
        public static void SayBreakfastNoIngredients(Entity entity)
        {
            SaySingle(entity, DialogueTextKey.HeroBreakfastNoIngredients);
        }

        /// <summary>
        /// Shows a randomly-picked boss-defeated bubble.
        /// The merc-gated variant is only eligible when at least one mercenary is hired.
        /// </summary>
        public static void SayBossDefeated(Entity entity)
        {
            SayFromOptions(entity, BossDefeatedOptions);
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Emits a single fixed dialogue key on <paramref name="entity"/>'s bubble.
        /// </summary>
        private static void SaySingle(Entity entity, string key)
        {
            if (Core.Instance == null || entity == null)
                return;
            var textService = Core.Services?.GetService<TextService>();
            if (textService == null)
                return;
            entity.GetComponent<SpeechBubbleComponent>()
                ?.Say(textService.DisplayText(TextType.Dialogue, key));
        }

        /// <summary>
        /// Picks a uniformly-random eligible option from <paramref name="options"/> and displays it.
        /// Merc-gated options are excluded when no mercenaries are hired.
        /// A null key silently returns without showing anything.
        /// AOT-safe: no LINQ; single one-shot <see cref="MercenaryManager.GetHiredMercenaries"/> call.
        /// </summary>
        private static void SayFromOptions(Entity entity, Option[] options)
        {
            if (Core.Instance == null || entity == null || options == null || options.Length == 0)
                return;

            bool hasMerc = HasHiredMercenary();

            // Count eligible options (no LINQ)
            int eligibleCount = 0;
            for (int i = 0; i < options.Length; i++)
            {
                if (!options[i].NeedsMerc || hasMerc)
                    eligibleCount++;
            }

            if (eligibleCount == 0)
                return;

            // Pick uniformly at random among eligible options
            int pick   = _rng.Next(eligibleCount);
            int walked = 0;
            string key = null;
            for (int i = 0; i < options.Length; i++)
            {
                if (!options[i].NeedsMerc || hasMerc)
                {
                    if (walked == pick)
                    {
                        key = options[i].Key;
                        break;
                    }
                    walked++;
                }
            }

            // Null key = silent variant — show nothing
            if (key == null)
                return;

            SaySingle(entity, key);
        }

        /// <summary>
        /// Returns true when at least one mercenary is currently hired.
        /// One-shot call per bubble event; allocation acceptable (see
        /// usage precedent in AutoItemPurchaseService.cs:117-127).
        /// </summary>
        private static bool HasHiredMercenary()
        {
            var hired = Core.Services?.GetService<MercenaryManager>()?.GetHiredMercenaries();
            return hired != null && hired.Count > 0;
        }
    }
}
