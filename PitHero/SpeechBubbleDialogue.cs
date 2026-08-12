using Nez;
using PitHero.Dining;
using PitHero.ECS.Components;
using PitHero.Services;

namespace PitHero
{
    /// <summary>
    /// Centralized helper for emitting speech-bubble events. Every public Say* method
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

        // ── Gate ─────────────────────────────────────────────────────────────────

        /// <summary>Eligibility gate that may restrict an option to a specific context.</summary>
        private enum Gate
        {
            /// <summary>Always eligible.</summary>
            None,
            /// <summary>Eligible only when at least one mercenary is hired.</summary>
            Merc,
            /// <summary>Eligible only when a tip was paid (tipPaid == true).</summary>
            Tip,
            /// <summary>Eligible only when no tip was paid (tipPaid == false).</summary>
            NoTip,
        }

        // ── Option set type ──────────────────────────────────────────────────────

        /// <summary>
        /// One variant inside a multi-choice bubble event.
        /// A null <see cref="Key"/> represents the "show nothing" silent variant.
        /// </summary>
        private readonly struct Option
        {
            /// <summary>Dialogue key, or null for the silent variant.</summary>
            public readonly string Key;

            /// <summary>Eligibility gate for this option.</summary>
            public readonly Gate Gate;

            public Option(string key, Gate gate = Gate.None)
            {
                Key  = key;
                Gate = gate;
            }
        }

        // ── Option tables ────────────────────────────────────────────────────────

        // SayBreakfast — three variants, no gate
        private static readonly Option[] BreakfastOptions =
        {
            new Option(DialogueTextKey.HeroBreakfast),
            new Option(DialogueTextKey.HeroBreakfastJustWokeUp),
            new Option(DialogueTextKey.HeroBreakfastWhatsFor),
        };

        // SayPitAdventure — five variants, no gate
        private static readonly Option[] PitAdventureOptions =
        {
            new Option(DialogueTextKey.HeroPitAdventure),
            new Option(DialogueTextKey.HeroPitAdventureLetsGo),
            new Option(DialogueTextKey.HeroPitAdventureGoodRun),
            new Option(DialogueTextKey.HeroPitAdventureLoot),
            new Option(DialogueTextKey.HeroPitAdventureExcited),
        };

        // Event 2 — Pit entry: three non-silent + merc-gated + silent
        private static readonly Option[] PitEntryOptions =
        {
            new Option(DialogueTextKey.HeroPitEntryGreatRun),
            new Option(DialogueTextKey.HeroPitEntryWeGotThis, Gate.Merc),
            new Option(DialogueTextKey.HeroPitEntryWhatsAtEnd),
            new Option(null), // silent variant
        };

        // Event 4 — Pit rest: three variants, no gate
        private static readonly Option[] PitRestOptions =
        {
            new Option(DialogueTextKey.HeroRestSleepOff),
            new Option(DialogueTextKey.HeroRestHealAtInn),
            new Option(DialogueTextKey.HeroRestWouldBeGood),
        };

        // Event 6 — Boss defeated: merc-gated + always + silent
        private static readonly Option[] BossDefeatedOptions =
        {
            new Option(DialogueTextKey.HeroBossTeamwork, Gate.Merc),
            new Option(DialogueTextKey.HeroBossWorthyFoe),
            new Option(null), // silent variant
        };

        // Respawn — five variants, no gate
        private static readonly Option[] RespawnOptions =
        {
            new Option(DialogueTextKey.HeroRespawnToughBattle),
            new Option(DialogueTextKey.HeroRespawnNextRunBetter),
            new Option(DialogueTextKey.HeroRespawnStronger),
            new Option(DialogueTextKey.HeroRespawnOuch),
            new Option(DialogueTextKey.HeroRespawnNotAsPlanned),
        };

        // Patron order — two variants with {0} dish name
        private static readonly Option[] PatronOrderOptions =
        {
            new Option(DialogueTextKey.PatronOrderIllHave),
            new Option(DialogueTextKey.PatronOrderOnePlease),
        };

        // Patron paid — non-tip + tip-gated + no-tip-gated + silent
        private static readonly Option[] PatronPaidOptions =
        {
            new Option(DialogueTextKey.PatronPaidDelicious),
            new Option(DialogueTextKey.PatronPaidPrettyGood),
            new Option(DialogueTextKey.PatronPaidGreatService, Gate.Tip),
            new Option(DialogueTextKey.PatronPaidTellFriends),
            new Option(DialogueTextKey.PatronPaidHadBetter, Gate.NoTip),
            new Option(null), // silent
        };

        // Server farewell — three variants + silent
        private static readonly Option[] ServerFarewellOptions =
        {
            new Option(DialogueTextKey.ServerFarewellComeBack),
            new Option(DialogueTextKey.ServerFarewellComeAgain),
            new Option(DialogueTextKey.ServerFarewellGladToHaveYou),
            new Option(null), // silent
        };

        // Cook places dish — four variants with {0} dish name
        private static readonly Option[] CookServedOptions =
        {
            new Option(DialogueTextKey.CookOrderUp),
            new Option(DialogueTextKey.CookHandsPlease),
            new Option(DialogueTextKey.CookNeedHands),
            new Option(DialogueTextKey.CookDishReady),
        };

        // Runner fetch — four variants + silent
        private static readonly Option[] RunnerFetchOptions =
        {
            new Option(DialogueTextKey.RunnerBusy),
            new Option(DialogueTextKey.RunnerOffIGo),
            new Option(DialogueTextKey.RunnerQuick),
            new Option(DialogueTextKey.RunnerGoGetIt),
            new Option(null), // silent
        };

        // Farmer reaches storage — three variants + silent
        private static readonly Option[] FarmerStoreOptions =
        {
            new Option(DialogueTextKey.FarmerStorePuttingAway),
            new Option(DialogueTextKey.FarmerStoreAnotherHarvest),
            new Option(DialogueTextKey.FarmerStoreInYouGo),
            new Option(null), // silent
        };

        // Worker shift end — three variants + silent
        private static readonly Option[] WorkerShiftEndOptions =
        {
            new Option(DialogueTextKey.WorkerShiftDone),
            new Option(DialogueTextKey.WorkerTimeForRest),
            new Option(DialogueTextKey.WorkerGoodWork),
            new Option(null), // silent
        };

        // Worker shift start — three variants + silent
        private static readonly Option[] WorkerShiftStartOptions =
        {
            new Option(DialogueTextKey.WorkerDoMyBest),
            new Option(DialogueTextKey.WorkerGoingToWork),
            new Option(DialogueTextKey.WorkerHappyToHelp),
            new Option(null), // silent
        };

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Shows a randomly-picked breakfast bubble.</summary>
        public static void SayBreakfast(Entity entity)
        {
            SayFromOptions(entity, BreakfastOptions);
        }

        /// <summary>Shows a randomly-picked pit-adventure bubble (one-shot per trip).</summary>
        public static void SayPitAdventure(Entity entity)
        {
            SayFromOptions(entity, PitAdventureOptions);
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

        /// <summary>Shows the bedtime bubble when the hero decides to jump out for night sleep.</summary>
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

        /// <summary>Shows a randomly-picked hero-respawn bubble.</summary>
        public static void SayRespawn(Entity entity)
        {
            SayFromOptions(entity, RespawnOptions);
        }

        /// <summary>Shows the crystal-ceremony prayer bubble.</summary>
        public static void SayCeremony(Entity entity)
        {
            SaySingle(entity, DialogueTextKey.HeroCeremonyGrantStrength);
        }

        /// <summary>
        /// Shows a randomly-picked patron-order bubble formatted with the localized dish name.
        /// </summary>
        public static void SayPatronOrder(Entity entity, DishType dish)
        {
            if (Core.Instance == null || entity == null)
                return;
            var textService = Core.Services?.GetService<TextService>();
            if (textService == null)
                return;
            string dishName = textService.DisplayText(TextType.UI, DishConfig.GetDefinition(dish).NameKey);
            SayFromOptions(entity, PatronOrderOptions, formatArg: dishName);
        }

        /// <summary>
        /// Shows a randomly-picked post-meal bubble.
        /// Tip-gated and no-tip-gated options are filtered by <paramref name="tipped"/>.
        /// </summary>
        public static void SayPatronPaid(Entity entity, bool tipped)
        {
            SayFromOptions(entity, PatronPaidOptions, tipPaid: tipped);
        }

        /// <summary>Shows a randomly-picked server farewell bubble on the server entity.</summary>
        public static void SayServerFarewell(Entity entity)
        {
            SayFromOptions(entity, ServerFarewellOptions);
        }

        /// <summary>
        /// Shows a randomly-picked cook-places-dish bubble formatted with the localized dish name.
        /// </summary>
        public static void SayCookServed(Entity entity, DishType dish)
        {
            if (Core.Instance == null || entity == null)
                return;
            var textService = Core.Services?.GetService<TextService>();
            if (textService == null)
                return;
            string dishName = textService.DisplayText(TextType.UI, DishConfig.GetDefinition(dish).NameKey);
            SayFromOptions(entity, CookServedOptions, formatArg: dishName);
        }

        /// <summary>Shows a randomly-picked runner fetch bubble (one per trip).</summary>
        public static void SayRunnerFetch(Entity entity)
        {
            SayFromOptions(entity, RunnerFetchOptions);
        }

        /// <summary>Shows a randomly-picked farmer-store bubble when the worker arrives at storage.</summary>
        public static void SayFarmerStore(Entity entity)
        {
            SayFromOptions(entity, FarmerStoreOptions);
        }

        /// <summary>Shows a randomly-picked shift-end bubble (real shift end only).</summary>
        public static void SayWorkerShiftEnd(Entity entity)
        {
            SayFromOptions(entity, WorkerShiftEndOptions);
        }

        /// <summary>Shows a randomly-picked shift-start bubble when a worker emerges from the house.</summary>
        public static void SayWorkerShiftStart(Entity entity)
        {
            SayFromOptions(entity, WorkerShiftStartOptions);
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Emits a single fixed dialogue key on <paramref name="entity"/>'s bubble.
        /// If <paramref name="formatArg"/> is non-null the localized text is formatted with it.
        /// </summary>
        private static void SaySingle(Entity entity, string key, string formatArg = null)
        {
            if (Core.Instance == null || entity == null)
                return;
            var textService = Core.Services?.GetService<TextService>();
            if (textService == null)
                return;
            string text = textService.DisplayText(TextType.Dialogue, key);
            if (formatArg != null)
                text = string.Format(text, formatArg);
            entity.GetComponent<SpeechBubbleComponent>()?.Say(text);
        }

        /// <summary>
        /// Picks a uniformly-random eligible option from <paramref name="options"/> and displays it.
        /// Gate.Merc options are excluded when no mercenaries are hired; hasMerc is resolved at most
        /// once per call (one-shot-call property preserved). Gate.Tip/NoTip filter by
        /// <paramref name="tipPaid"/>. A null key is the silent variant — shows nothing.
        /// If <paramref name="formatArg"/> is non-null the localized text is formatted with it.
        /// AOT-safe: no LINQ; no per-call heap allocation.
        /// </summary>
        private static void SayFromOptions(Entity entity, Option[] options,
            bool? tipPaid = null, string formatArg = null)
        {
            if (Core.Instance == null || entity == null || options == null || options.Length == 0)
                return;

            // Lazily resolve hasMerc — only when at least one option has Gate.Merc
            bool hasMercKnown = false;
            bool hasMerc      = false;

            // Count eligible options (no LINQ)
            int eligibleCount = 0;
            for (int i = 0; i < options.Length; i++)
            {
                switch (options[i].Gate)
                {
                    case Gate.None:
                        eligibleCount++;
                        break;
                    case Gate.Merc:
                        if (!hasMercKnown) { hasMerc = HasHiredMercenary(); hasMercKnown = true; }
                        if (hasMerc) eligibleCount++;
                        break;
                    case Gate.Tip:
                        if (tipPaid == true) eligibleCount++;
                        break;
                    case Gate.NoTip:
                        if (tipPaid == false) eligibleCount++;
                        break;
                }
            }

            if (eligibleCount == 0)
                return;

            // Pick uniformly at random among eligible options
            int pick   = _rng.Next(eligibleCount);
            int walked = 0;
            string key = null;
            for (int i = 0; i < options.Length; i++)
            {
                bool eligible = false;
                switch (options[i].Gate)
                {
                    case Gate.None:  eligible = true;             break;
                    case Gate.Merc:  eligible = hasMerc;          break;
                    case Gate.Tip:   eligible = tipPaid == true;  break;
                    case Gate.NoTip: eligible = tipPaid == false; break;
                }

                if (eligible)
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

            SaySingle(entity, key, formatArg);
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
