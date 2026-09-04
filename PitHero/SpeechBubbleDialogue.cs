using Nez;
using PitHero.Dining;
using PitHero.ECS.Components;
using PitHero.Services;
using RolePlayingFramework.Utils;

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
        // The number of _rng draws per bubble event is variable (bounded draw-and-skip),
        // which is fine — _rng is private to dialogue and not contract-bound.
        private static System.Random _rng = new System.Random();

        /// <summary>
        /// Reseeds the dialogue stream. Called at session start with a value derived from the master
        /// seed so a replay shows the same bubble variants (cosmetic, but nice to reproduce).
        /// </summary>
        public static void Reseed(int seed)
        {
            _rng = new System.Random(seed);
            // The option bags are static and survive a scene restart; put every marble back in its
            // starting order so the seeded stream reproduces the same lines on a replay restart
            for (int i = 0; i < _allBags.Count; i++)
                _allBags[i].Bag.Reset();
        }

        // Every OptionBag registers here at construction so Reseed can reset them all
        private static readonly System.Collections.Generic.List<OptionBag> _allBags = new System.Collections.Generic.List<OptionBag>(24);

        // ── Gate ─────────────────────────────────────────────────────────────────

        /// <summary>Eligibility gate that may restrict an option to a specific context.</summary>
        public enum Gate
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

        // ── Option set types ─────────────────────────────────────────────────────

        /// <summary>
        /// One variant inside a multi-choice bubble event.
        /// A null <see cref="Key"/> represents the "show nothing" silent variant.
        /// </summary>
        public readonly struct Option
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

        /// <summary>
        /// An option table paired with its shared shuffle bag of option indices (issue #385).
        /// Tables are static, so each bag is a single pool shared by every speaker of the
        /// event — two cooks draw from the same bag and cannot repeat a line within a cycle.
        /// </summary>
        public sealed class OptionBag
        {
            public readonly Option[] Options;
            public readonly ShuffleBag<int> Bag;

            /// <summary>Precomputed so hasMerc stays a lazy one-shot resolve per call.</summary>
            public readonly bool HasMercGate;

            public OptionBag(Option[] options)
            {
                Options = options;
                Bag = new ShuffleBag<int>(options.Length);
                for (int i = 0; i < options.Length; i++)
                {
                    Bag.Add(i);
                    if (options[i].Gate == Gate.Merc)
                        HasMercGate = true;
                }
                _allBags.Add(this);
            }
        }

        // ── Option tables ────────────────────────────────────────────────────────

        // SayBreakfast — three variants, no gate
        private static readonly OptionBag BreakfastOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.HeroBreakfast),
            new Option(DialogueTextKey.HeroBreakfastJustWokeUp),
            new Option(DialogueTextKey.HeroBreakfastWhatsFor),
        });

        // SayLunch — two variants, no gate (issue #392)
        private static readonly OptionBag LunchOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.HeroLunchTime),
            new Option(DialogueTextKey.HeroLunchOptions),
        });

        // SayDinner — two variants, no gate (issue #392)
        private static readonly OptionBag DinnerOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.HeroDinnerTime),
            new Option(DialogueTextKey.HeroDinnerServing),
        });

        // SayPitAdventure — five variants, no gate
        private static readonly OptionBag PitAdventureOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.HeroPitAdventure),
            new Option(DialogueTextKey.HeroPitAdventureLetsGo),
            new Option(DialogueTextKey.HeroPitAdventureGoodRun),
            new Option(DialogueTextKey.HeroPitAdventureLoot),
            new Option(DialogueTextKey.HeroPitAdventureExcited),
        });

        // Event 2 — Pit entry: three non-silent + merc-gated + silent
        private static readonly OptionBag PitEntryOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.HeroPitEntryGreatRun),
            new Option(DialogueTextKey.HeroPitEntryWeGotThis, Gate.Merc),
            new Option(DialogueTextKey.HeroPitEntryWhatsAtEnd),
            new Option(null), // silent variant
        });

        // Event 4 — Pit rest: three variants, no gate
        private static readonly OptionBag PitRestOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.HeroRestSleepOff),
            new Option(DialogueTextKey.HeroRestHealAtInn),
            new Option(DialogueTextKey.HeroRestWouldBeGood),
        });

        // Event 6 — Boss defeated: merc-gated + always + silent
        private static readonly OptionBag BossDefeatedOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.HeroBossTeamwork, Gate.Merc),
            new Option(DialogueTextKey.HeroBossWorthyFoe),
            new Option(null), // silent variant
        });

        // Respawn — five variants, no gate
        private static readonly OptionBag RespawnOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.HeroRespawnToughBattle),
            new Option(DialogueTextKey.HeroRespawnNextRunBetter),
            new Option(DialogueTextKey.HeroRespawnStronger),
            new Option(DialogueTextKey.HeroRespawnOuch),
            new Option(DialogueTextKey.HeroRespawnNotAsPlanned),
        });

        // Patron order — two variants with {0} dish name
        private static readonly OptionBag PatronOrderOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.PatronOrderIllHave),
            new Option(DialogueTextKey.PatronOrderOnePlease),
        });

        // Patron paid — non-tip + tip-gated + no-tip-gated + silent
        private static readonly OptionBag PatronPaidOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.PatronPaidDelicious),
            new Option(DialogueTextKey.PatronPaidPrettyGood),
            new Option(DialogueTextKey.PatronPaidGreatService, Gate.Tip),
            new Option(DialogueTextKey.PatronPaidTellFriends),
            new Option(DialogueTextKey.PatronPaidHadBetter, Gate.NoTip),
            new Option(null), // silent
        });

        // Server farewell — three variants + silent
        private static readonly OptionBag ServerFarewellOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.ServerFarewellComeBack),
            new Option(DialogueTextKey.ServerFarewellComeAgain),
            new Option(DialogueTextKey.ServerFarewellGladToHaveYou),
            new Option(null), // silent
        });

        // Cook places dish — four variants with {0} dish name
        private static readonly OptionBag CookServedOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.CookOrderUp),
            new Option(DialogueTextKey.CookHandsPlease),
            new Option(DialogueTextKey.CookNeedHands),
            new Option(DialogueTextKey.CookDishReady),
        });

        // Runner fetch — four variants + silent
        private static readonly OptionBag RunnerFetchOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.RunnerBusy),
            new Option(DialogueTextKey.RunnerOffIGo),
            new Option(DialogueTextKey.RunnerQuick),
            new Option(DialogueTextKey.RunnerGoGetIt),
            new Option(null), // silent
        });

        // Farmer reaches storage — three variants + silent
        private static readonly OptionBag FarmerStoreOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.FarmerStorePuttingAway),
            new Option(DialogueTextKey.FarmerStoreAnotherHarvest),
            new Option(DialogueTextKey.FarmerStoreInYouGo),
            new Option(null), // silent
        });

        // Worker shift end — three variants + silent
        private static readonly OptionBag WorkerShiftEndOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.WorkerShiftDone),
            new Option(DialogueTextKey.WorkerTimeForRest),
            new Option(DialogueTextKey.WorkerGoodWork),
            new Option(null), // silent
        });

        // Worker shift start — three variants + silent
        private static readonly OptionBag WorkerShiftStartOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.WorkerDoMyBest),
            new Option(DialogueTextKey.WorkerGoingToWork),
            new Option(DialogueTextKey.WorkerHappyToHelp),
            new Option(null), // silent
        });

        // Innkeeper farewell — three variants, no silent (issue #385)
        private static readonly OptionBag InnkeeperFarewellOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.InnkeeperFarewellGoodLuck),
            new Option(DialogueTextKey.InnkeeperFarewellPleasantAdventures),
            new Option(DialogueTextKey.InnkeeperFarewellComeBackSoon),
        });

        // Second Chance merchant greeting — three variants, no silent (issue #385)
        private static readonly OptionBag SecondChanceGreetingOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.SecondChanceInterestedWares),
            new Option(DialogueTextKey.SecondChanceBuyBackSomething),
            new Option(DialogueTextKey.SecondChanceMissSomething),
        });

        // Knight Provoke (ThreatSystem.md) — two variants, no silent: the bubble IS the feedback
        private static readonly OptionBag ProvokeOptions = new OptionBag(new Option[]
        {
            new Option(DialogueTextKey.KnightProvokeOverHere),
            new Option(DialogueTextKey.KnightProvokeFocusOnMe),
        });

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Shows the Provoke shout over the provoking Knight (hero or mercenary entity).</summary>
        public static void SayProvoke(Entity entity)
        {
            SayFromOptions(entity, ProvokeOptions);
        }

        /// <summary>Shows a randomly-picked breakfast bubble.</summary>
        public static void SayBreakfast(Entity entity)
        {
            SayFromOptions(entity, BreakfastOptions);
        }

        /// <summary>Shows a randomly-picked lunch bubble (issue #392).</summary>
        public static void SayLunch(Entity entity)
        {
            SayFromOptions(entity, LunchOptions);
        }

        /// <summary>Shows a randomly-picked dinner bubble (issue #392).</summary>
        public static void SayDinner(Entity entity)
        {
            SayFromOptions(entity, DinnerOptions);
        }

        /// <summary>
        /// Shows the skip-lunch bubble when lunch is skipped because no dish the hero
        /// can order is makeable (not the no-gold path).
        /// </summary>
        public static void SayLunchSkipped(Entity entity)
        {
            SaySingle(entity, DialogueTextKey.HeroLunchSkipped);
        }

        /// <summary>
        /// Shows the skip-dinner bubble when dinner is skipped because no dish the hero
        /// can order is makeable (not the no-gold path).
        /// </summary>
        public static void SayDinnerSkipped(Entity entity)
        {
            SaySingle(entity, DialogueTextKey.HeroDinnerSkipped);
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

        /// <summary>Shows the new-game intro prayer bubble at the hero statue (issue #396).</summary>
        public static void SayIntro(Entity entity)
        {
            SaySingle(entity, DialogueTextKey.HeroIntroDestiny);
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

        /// <summary>Shows the "Have a good rest" bubble on the innkeeper after the party pays.</summary>
        public static void SayInnkeeperGoodRest(Entity innkeeper)
        {
            SaySingle(innkeeper, DialogueTextKey.InnkeeperGoodRest);
        }

        /// <summary>
        /// Shows a randomly-picked innkeeper farewell bubble when the hero crosses the
        /// inn-farewell tile on a pit-bound trip that originated at the inn.
        /// </summary>
        public static void SayInnkeeperFarewell(Entity innkeeper)
        {
            SayFromOptions(innkeeper, InnkeeperFarewellOptions);
        }

        /// <summary>
        /// Returns the localized Second Chance merchant greeting for the shop UI bubble,
        /// drawn from the shared greeting bag. Returns null when unavailable (headless).
        /// The merchant is a UI sprite, not an entity, so the caller displays the text itself.
        /// </summary>
        public static string GetSecondChanceGreeting()
        {
            if (Core.Instance == null)
                return null;
            var textService = Core.Services?.GetService<TextService>();
            if (textService == null)
                return null;
            string key = SelectKey(SecondChanceGreetingOptions, hasMerc: false, tipPaid: null, _rng);
            if (key == null)
                return null;
            return textService.DisplayText(TextType.Dialogue, key);
        }

        // ── Selection core ───────────────────────────────────────────────────────

        /// <summary>
        /// Pure selection core: draws option indices from the set's shared shuffle bag,
        /// skipping gate-ineligible marbles (bounded draw-and-skip, precedent
        /// KitchenTaskCoordinator.PickPatronDish). Ineligible marbles are consumed and
        /// return next cycle — statistically harmless. Returns the chosen key, or null
        /// for the silent variant / no eligible option. When nothing is eligible the bag
        /// is left untouched. AOT-safe: no LINQ; no per-call heap allocation.
        /// </summary>
        public static string SelectKey(OptionBag set, bool hasMerc, bool? tipPaid, System.Random rng)
        {
            if (set == null || set.Options.Length == 0)
                return null;

            // Count eligible options first — never draw when nothing can match
            int eligibleCount = 0;
            for (int i = 0; i < set.Options.Length; i++)
            {
                if (IsEligible(set.Options[i].Gate, hasMerc, tipPaid))
                    eligibleCount++;
            }

            if (eligibleCount == 0)
                return null;

            // Bounded draw-and-skip: at most Remaining draws finish the current cycle,
            // then one full refilled cycle sees every marble, and eligibleCount > 0
            // guarantees a hit within Count * 2 draws.
            int limit = set.Bag.Count * 2;
            for (int n = 0; n < limit; n++)
            {
                int idx = set.Bag.Next(rng);
                var opt = set.Options[idx];
                if (IsEligible(opt.Gate, hasMerc, tipPaid))
                    return opt.Key; // null Key = silent variant (a real, consumed outcome)
            }

            return null; // unreachable when eligibleCount > 0; defensive
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        /// <summary>Evaluates a gate against the current context.</summary>
        private static bool IsEligible(Gate gate, bool hasMerc, bool? tipPaid)
        {
            switch (gate)
            {
                case Gate.Merc:  return hasMerc;
                case Gate.Tip:   return tipPaid == true;
                case Gate.NoTip: return tipPaid == false;
                default:         return true;
            }
        }

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
        /// Draws an eligible option from the set's shared shuffle bag and displays it.
        /// Gate.Merc options are excluded when no mercenaries are hired; hasMerc is resolved at most
        /// once per call and only when the table has a merc gate (one-shot-call property preserved).
        /// Gate.Tip/NoTip filter by <paramref name="tipPaid"/>. A null key is the silent
        /// variant — shows nothing. If <paramref name="formatArg"/> is non-null the localized
        /// text is formatted with it. Headless calls bail before any draw so bags never
        /// advance in tests.
        /// </summary>
        private static void SayFromOptions(Entity entity, OptionBag set,
            bool? tipPaid = null, string formatArg = null)
        {
            if (Core.Instance == null || entity == null || set == null || set.Options.Length == 0)
                return;

            bool hasMerc = set.HasMercGate && HasHiredMercenary();

            string key = SelectKey(set, hasMerc, tipPaid, _rng);

            // Null key = silent variant (or nothing eligible) — show nothing
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
