using Microsoft.Xna.Framework;
using Nez;
using PitHero.Config;
using PitHero.Dining;
using PitHero.ECS.Components;
using PitHero.UI;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;
using RolePlayingFramework.Combat;

namespace PitHero.Services
{
    /// <summary>
    /// Orchestrates three-meals-a-day party dining at the tavern (issue #319, extended in #392)
    /// on top of the existing Stop mode — no new GOAP surface. Implements IPartyOrderSource so
    /// kitchen servers take party orders with priority. Slot 0 = hero, slots 1/2 = hired
    /// mercenaries in MercenaryManager.GetHiredMercenaries() order.
    /// Meal periods: Breakfast (6 AM, wake-driven), Lunch (12 PM, edge-driven), Dinner (6 PM,
    /// edge-driven). Each meal resets HasEatenThisMeal; buff mirrors expire on their own 6-hour
    /// clock and are cleared lazily in Update().
    /// </summary>
    public sealed class PartyDiningService : IPartyOrderSource
    {
        /// <summary>One party member's dining state for the current meal period (persisted in save section 33).</summary>
        public struct MemberDining
        {
            public int OrderedDishId;       // -1 = none
            public bool HasPaid;
            public bool HasEatenThisMeal;   // true once the member finishes eating in the current meal period
            public int MealDishId;          // -1 = no active meal buffs
            public bool MealDeluxe;
            public float MealExpiresAtSeconds; // absolute InGameTimeService.AccumulatedSeconds; 0 = none
        }

        private const int PartySlots = 3;

        /// <summary>The hero's favorite dish chosen in the Food tab.</summary>
        public int FavoriteDishId = (int)DishType.RoastedOnionSkewers;

        /// <summary>When true, the party auto-dines at the tavern after waking each morning. On by
        /// default so new players see the dining system in action; saves restore the player's choice.</summary>
        public bool EatAtTavern = true;

        private readonly MemberDining[] _slots = new MemberDining[PartySlots];
        private readonly KitchenTicket[] _tickets = new KitchenTicket[PartySlots];
        private readonly bool[] _eating = new bool[PartySlots];
        private readonly float[] _eatElapsed = new float[PartySlots];
        private readonly bool[] _skippedThisSeating = new bool[PartySlots];

        private bool _autoResumeWhenDone;
        private bool _pendingReloadDining;

        public PartyDiningService()
        {
            for (int i = 0; i < PartySlots; i++)
            {
                _slots[i].OrderedDishId = -1;
                _slots[i].MealDishId = -1;
            }
        }

        /// <summary>Read access to a slot's dining record (save snapshot).</summary>
        public MemberDining GetSlot(int slot) => _slots[slot];

        /// <summary>Write access to a slot's dining record (save restore).</summary>
        public void SetSlot(int slot, MemberDining record) => _slots[slot] = record;

        /// <summary>
        /// Called after save restore when a member has an open order: forces Stop mode on the
        /// first Update and re-creates kitchen tickets without re-deducting crops or gold.
        /// </summary>
        public void MarkPendingReloadDining() => _pendingReloadDining = true;

        /// <summary>
        /// True while a breakfast trip should auto-resume adventuring once everyone has eaten
        /// or been skipped. Persisted: a save made mid-breakfast must restore this, or the
        /// reloaded party finishes eating and then sits at the tavern forever.
        /// </summary>
        public bool AutoResumeWhenDone
        {
            get => _autoResumeWhenDone;
            set => _autoResumeWhenDone = value;
        }

        /// <summary>
        /// Restores dining state from a save (call after hero + hired mercs are restored).
        /// Re-registers active meal buffs (no HP/MP re-restore) and, when a member has an open
        /// order, schedules the party's return trip to the tavern. Crops were already deducted
        /// before the save, and HasPaid guards against double payment.
        /// </summary>
        public void RestoreFromSave(SaveData data)
        {
            if (data == null)
                return;

            FavoriteDishId = data.FavoriteDishId;
            EatAtTavern = data.EatAtTavern;
            _autoResumeWhenDone = data.PartyAutoDineResume;

            if (data.PartyDining == null)
                return;

            var mealBuffs = Core.Services.GetService<MealBuffService>();
            var timeService = Core.Services.GetService<InGameTimeService>();
            float nowSeconds = timeService?.AccumulatedSeconds ?? 0f;

            bool anyOpenOrder = false;
            for (int slot = 0; slot < PartySlots && slot < data.PartyDining.Length; slot++)
            {
                var saved = data.PartyDining[slot];

                // Determine if the saved buff is still valid vs the restored clock
                bool buffStillActive = saved.MealDishId >= 0
                    && saved.MealExpiresAtSeconds > nowSeconds;

                _slots[slot] = new MemberDining
                {
                    OrderedDishId = saved.OrderedDishId,
                    HasPaid = saved.HasPaid,
                    HasEatenThisMeal = saved.HasEatenThisMeal,
                    MealDishId = buffStillActive ? saved.MealDishId : -1,
                    MealDeluxe = buffStillActive ? saved.MealDeluxe : false,
                    MealExpiresAtSeconds = buffStillActive ? saved.MealExpiresAtSeconds : 0f,
                };

                if (buffStillActive && saved.MealDishId < DishTypeInfo.Count)
                {
                    var combatant = GetCombatant(slot);
                    if (combatant != null)
                        mealBuffs?.RestoreRecord(combatant, (DishType)saved.MealDishId, saved.MealDeluxe, saved.MealExpiresAtSeconds);
                }

                if (saved.OrderedDishId >= 0 && !saved.HasEatenThisMeal)
                    anyOpenOrder = true;
            }

            if (anyOpenOrder)
                MarkPendingReloadDining();
        }

        // ── Meal-period reset ─────────────────────────────────────────────────────

        /// <summary>
        /// Resets HasEatenThisMeal and skipped flags so everyone may eat the next meal period.
        /// MealDishId/MealDeluxe/MealExpiresAtSeconds are deliberately NOT cleared here —
        /// buff mirrors expire on their own clock (a 6:30 AM breakfast buff must survive the
        /// 12 PM reset until 12:30 PM). Call before BeginAutoDine at each hour edge.
        /// </summary>
        public void ResetForNewMealPeriod()
        {
            for (int i = 0; i < PartySlots; i++)
            {
                _slots[i].HasEatenThisMeal = false;
                _skippedThisSeating[i] = false;
            }
        }

        /// <summary>Party members who still intend to eat at the tavern today (0 when EatAtTavern is off).</summary>
        public int CountPendingPartyDiners()
        {
            if (!EatAtTavern)
                return 0;
            int count = 0;
            for (int slot = 0; slot < PartySlots; slot++)
            {
                if (!_slots[slot].HasEatenThisMeal && GetCombatant(slot) != null)
                    count++;
            }
            return count;
        }

        // ── Entry points ────────────────────────────────────────────────────────

        /// <summary>
        /// Auto-dine for the given meal period. Breakfast is called from SleepInBedAction after
        /// night sleep; Lunch and Dinner are called from the hour-edge watcher in MainGameScene.
        /// Enters Stop mode and walks the party to the tavern — skipped when the hero, who leads
        /// the meal, can't order anything (favorite and job fallbacks all unmakeable or
        /// unaffordable, kitchen closed, or already ate). Every outcome is logged to analytics
        /// so a skipped meal is diagnosable from the session log.
        /// </summary>
        public void BeginAutoDine(MealPeriod meal)
        {
            if (!EatAtTavern)
                return;

            // Dead-hero guard: GetCombatant(0) may still return the combatant object on a dead
            // hero, so we check the hero component's alive state explicitly.
            var heroComponent = GetHeroComponent();
            if (heroComponent == null || heroComponent.LinkedHero == null)
            {
                Analytics.AnalyticsService.LogPartyMealTrip(meal.ToString(), "hero_not_present");
                Debug.Log("[PartyDiningService] Skipping meal trip — hero not present");
                return;
            }

            if (heroComponent.StoppedAdventure)
            {
                // A player-stopped party returns to the table on its own (SeatedInTavern goal)
                // and orders once seated; arming auto-resume here would cancel the player's stop.
                Analytics.AnalyticsService.LogPartyMealTrip(meal.ToString(), "already_stopped");
                Debug.Log($"[PartyDiningService] Skipping {meal} trip — party already stopped");
                return;
            }

            var timeService = Core.Services.GetService<InGameTimeService>();
            int hour = timeService?.Hour ?? TavernScheduleConfig.KitchenOpenHour;
            if (TavernScheduleConfig.IsKitchenClosed(hour))
            {
                Analytics.AnalyticsService.LogPartyMealTrip(meal.ToString(), "kitchen_closed");
                Debug.Log($"[PartyDiningService] Skipping {meal} trip — kitchen is closed");
                return;
            }

            var coordinator = Core.Services.GetService<KitchenTaskCoordinator>();
            if (coordinator == null || !coordinator.IsKitchenOpen)
            {
                Analytics.AnalyticsService.LogPartyMealTrip(meal.ToString(), "kitchen_unstaffed");
                Debug.Log($"[PartyDiningService] Skipping {meal} trip — kitchen cannot serve");
                return;
            }

            // The hero leads the meal — mercs eat free but only when he eats.
            if (_slots[0].HasEatenThisMeal || GetCombatant(0) == null)
            {
                Analytics.AnalyticsService.LogPartyMealTrip(meal.ToString(), "already_ate");
                Debug.Log($"[PartyDiningService] Skipping {meal} trip — hero already ate this meal");
                return;
            }

            var favorite = FavoriteDishId >= 0 && FavoriteDishId < DishTypeInfo.Count
                ? (DishType)FavoriteDishId
                : DishType.RoastedOnionSkewers;
            var gameState = Core.Services.GetService<GameStateService>();
            bool anyCoverable = false;
            if (gameState == null
                || !TryPickHeroDish(coordinator, gameState, favorite, out _, out anyCoverable))
            {
                if (gameState != null && anyCoverable)
                {
                    string noGoldKey = meal == MealPeriod.Breakfast ? UITextKey.ConsoleBreakfastSkippedGold
                        : meal == MealPeriod.Lunch ? UITextKey.ConsoleLunchSkippedGold
                        : UITextKey.ConsoleDinnerSkippedGold;
                    EmitMealSkipped(noGoldKey, favorite);
                    Analytics.AnalyticsService.LogPartyMealTrip(meal.ToString(), "no_gold");
                    Debug.Log($"[PartyDiningService] Skipping {meal} trip — not enough gold for any orderable dish");
                }
                else
                {
                    string noIngrKey = meal == MealPeriod.Breakfast ? UITextKey.ConsoleBreakfastSkipped
                        : meal == MealPeriod.Lunch ? UITextKey.ConsoleLunchSkipped
                        : UITextKey.ConsoleDinnerSkipped;
                    EmitMealSkipped(noIngrKey, favorite);
                    var heroEntity = Core.Scene?.FindEntity("hero");
                    if (meal == MealPeriod.Breakfast)
                        SpeechBubbleDialogue.SayBreakfastNoIngredients(heroEntity);
                    else if (meal == MealPeriod.Lunch)
                        SpeechBubbleDialogue.SayLunchSkipped(heroEntity);
                    else
                        SpeechBubbleDialogue.SayDinnerSkipped(heroEntity);
                    Analytics.AnalyticsService.LogPartyMealTrip(meal.ToString(), "no_ingredients");
                    Debug.Log($"[PartyDiningService] Skipping {meal} trip — no ingredients for any dish the hero can order");
                }
                return;
            }

            var stopUI = GetStopUI();
            if (stopUI == null)
            {
                Analytics.AnalyticsService.LogPartyMealTrip(meal.ToString(), "no_stop_ui");
                return;
            }

            _autoResumeWhenDone = true;
            stopUI.SetStopped(true);
            EmitMealBubble(meal);
            Analytics.AnalyticsService.LogPartyMealTrip(meal.ToString(), "started");
            Debug.Log($"[PartyDiningService] Party heading to the tavern for {meal}");
        }

        /// <summary>Called when Stop mode begins (player pressed Stop, or auto-dine).</summary>
        public void OnStopped()
        {
            for (int i = 0; i < PartySlots; i++)
                _skippedThisSeating[i] = false;
        }

        /// <summary>
        /// Called when Stop mode ends (player pressed Play, or auto-resume). Uncooked party
        /// tickets are canceled with a full refund; food already on the table is fast-tracked
        /// to eaten (buffs granted, payment kept).
        /// </summary>
        public void OnResumed()
        {
            _autoResumeWhenDone = false;
            CancelOrFastTrackOutstandingOrders();
        }

        /// <summary>
        /// Called when the party leaves the table for night sleep while Stop mode persists
        /// (10 PM). Outstanding orders are settled like a resume, but AutoResumeWhenDone is
        /// deliberately untouched so a manual stop survives the night.
        /// </summary>
        public void OnNightSleepDeparture()
        {
            CancelOrFastTrackOutstandingOrders();
        }

        private void CancelOrFastTrackOutstandingOrders()
        {
            var coordinator = Core.Services.GetService<KitchenTaskCoordinator>();
            var gameState = Core.Services.GetService<GameStateService>();

            for (int slot = 0; slot < PartySlots; slot++)
            {
                var ticket = _tickets[slot];
                if (ticket == null)
                    continue;

                if (ticket.State == TicketState.Delivered)
                {
                    // Food is on the table — fast-track to eaten
                    FinishMember(slot);
                }
                else
                {
                    bool refundGold = ticket.CropsRefundable; // not yet cooked → no penalty
                    coordinator?.CancelTicket(ticket);
                    if (refundGold && _slots[slot].HasPaid && gameState != null)
                        gameState.Funds += DishConfig.GetPrice(ticket.Dish);
                    _slots[slot].OrderedDishId = -1;
                    _slots[slot].HasPaid = false;
                    _tickets[slot] = null;
                    _eating[slot] = false;
                }
            }
        }

        // ── Per-frame update ────────────────────────────────────────────────────

        /// <summary>Ticks eat timers, prunes expired meal buffs, and handles the deferred reload-mid-dining restart.</summary>
        public void Update()
        {
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true)
                return;

            if (_pendingReloadDining)
                HandlePendingReload();

            // Prune expired meal buff records and sync slot mirrors so save snapshots stay accurate
            var timeService = Core.Services.GetService<InGameTimeService>();
            float nowSeconds = timeService?.AccumulatedSeconds ?? 0f;
            var mealBuffsForPrune = Core.Services.GetService<MealBuffService>();
            mealBuffsForPrune?.Prune(nowSeconds);
            for (int p = 0; p < PartySlots; p++)
            {
                if (_slots[p].MealDishId >= 0 && _slots[p].MealExpiresAtSeconds > 0f
                    && _slots[p].MealExpiresAtSeconds <= nowSeconds)
                {
                    _slots[p].MealDishId = -1;
                    _slots[p].MealDeluxe = false;
                    _slots[p].MealExpiresAtSeconds = 0f;
                }
            }

            var hero = GetHeroComponent();
            bool seated = hero != null && hero.StoppedAdventure && hero.SeatedInTavern;

            // Re-create tickets for restored open orders once the party is seated again
            if (seated)
            {
                var coordinator = Core.Services.GetService<KitchenTaskCoordinator>();
                for (int slot = 0; slot < PartySlots; slot++)
                {
                    if (_slots[slot].OrderedDishId >= 0 && !_slots[slot].HasEatenThisMeal && _tickets[slot] == null)
                        _tickets[slot] = coordinator?.CreateTicketPreReserved((DishType)_slots[slot].OrderedDishId, slot);
                }
            }

            for (int slot = 0; slot < PartySlots; slot++)
            {
                if (!_eating[slot])
                    continue;
                _eatElapsed[slot] += Time.DeltaTime;
                var ticket = _tickets[slot];
                if (ticket == null)
                {
                    _eating[slot] = false;
                    continue;
                }
                if (_eatElapsed[slot] >= DishConfig.GetEatSeconds(ticket.Dish))
                    FinishMember(slot);
            }

            CheckAllDone();
        }

        private void HandlePendingReload()
        {
            var stopUI = GetStopUI();
            if (stopUI == null)
                return; // UI not built yet — retry next frame
            _pendingReloadDining = false;
            stopUI.SetStopped(true);
            Debug.Log("[PartyDiningService] Reload with open orders — party returning to the tavern");
        }

        // ── IPartyOrderSource ───────────────────────────────────────────────────

        /// <summary>
        /// Next seated party member wanting to order. The hero leads the meal: he orders his
        /// favorite dish (falling back through his job's cheap fallbacks when it can't be made
        /// or afforded) and pays for it; only when nothing he can order is available do the
        /// servers skip the entire party — mercenaries never eat unless the hero eats.
        /// Mercenary meals are free (job favorite, then the job's cheap fallbacks,
        /// ingredient-gated only). Skips are re-evaluated on every poll, so the party is still
        /// served if ingredients or gold appear while they remain seated.
        /// </summary>
        public bool TryGetNextPartyOrder(out int partySlot, out DishType dish)
        {
            partySlot = -1;
            dish = default;

            // Eat-at-tavern off: the party may sit at the table, but servers ignore them and
            // focus on walk-in patrons. Toggling it back on while seated (and not yet fed
            // today) makes them orderable again on the next poll.
            if (!EatAtTavern)
                return false;

            var hero = GetHeroComponent();
            if (hero == null || !hero.StoppedAdventure || !hero.SeatedInTavern)
                return false;

            var coordinator = Core.Services.GetService<KitchenTaskCoordinator>();
            var gameState = Core.Services.GetService<GameStateService>();
            if (coordinator == null || gameState == null)
                return false;

            bool heroLeads = _slots[0].OrderedDishId >= 0 || _slots[0].HasEatenThisMeal;
            if (!heroLeads)
            {
                var heroCombatant = GetCombatant(0);
                if (heroCombatant == null || !TryGetFavorite(0, out var heroFavorite))
                    return false;

                if (!TryPickHeroDish(coordinator, gameState, heroFavorite, out var heroDish, out _))
                {
                    MarkPartySkippedByHero(coordinator, heroFavorite, heroCombatant.Name);
                    return false;
                }

                _skippedThisSeating[0] = false;
                partySlot = 0;
                dish = heroDish;
                return true;
            }

            for (int slot = 1; slot < PartySlots; slot++)
            {
                if (_slots[slot].OrderedDishId >= 0)
                    continue;
                var combatant = GetCombatant(slot);
                if (combatant == null)
                    continue;
                if (!TryGetFavorite(slot, out var favorite))
                    continue;

                if (_slots[slot].HasEatenThisMeal)
                {
                    if (!_skippedThisSeating[slot])
                    {
                        _skippedThisSeating[slot] = true;
                        Analytics.AnalyticsService.LogPartyDineSkipped(slot, combatant.Name,
                            favorite.ToString(), "already_ate");
                    }
                    continue;
                }

                if (!TryPickMercDish(slot, favorite, coordinator, out var chosen))
                {
                    if (!_skippedThisSeating[slot])
                    {
                        _skippedThisSeating[slot] = true;
                        Analytics.AnalyticsService.LogPartyDineSkipped(slot, combatant.Name,
                            favorite.ToString(), "no_ingredients");
                    }
                    continue;
                }

                _skippedThisSeating[slot] = false;
                partySlot = slot;
                dish = chosen;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The hero pays for his own meal at order time (unlike walk-in patrons, who pay when
        /// they finish). Mercenary meals are free — no gold moves in either direction.
        /// </summary>
        public void OnPartyOrderTaken(int partySlot, KitchenTicket ticket)
        {
            if (partySlot == 0)
            {
                var gameState = Core.Services.GetService<GameStateService>();
                if (gameState != null)
                {
                    gameState.Funds -= DishConfig.GetPrice(ticket.Dish);
                    PlaySoundAtHero(SoundEffectType.PayGold);
                }
            }

            _slots[partySlot].OrderedDishId = (int)ticket.Dish;
            _slots[partySlot].HasPaid = partySlot == 0;
            _tickets[partySlot] = ticket;
        }

        /// <summary>Dish landed on the party member's table — start their eat timer.</summary>
        public void OnPartyDishDelivered(int partySlot, KitchenTicket ticket)
        {
            _tickets[partySlot] = ticket;
            _eating[partySlot] = true;
            _eatElapsed[partySlot] = 0f;
        }

        // ── Finishing ───────────────────────────────────────────────────────────

        private void FinishMember(int slot)
        {
            var ticket = _tickets[slot];
            if (ticket == null)
                return;

            var timeService = Core.Services.GetService<InGameTimeService>();
            float nowSeconds = timeService?.AccumulatedSeconds ?? 0f;
            float expiresAtSeconds = timeService != null
                ? nowSeconds + GameConfig.MealBuffDurationSeconds
                : float.MaxValue;

            var combatant = GetCombatant(slot);
            if (combatant != null)
            {
                Core.Services.GetService<MealBuffService>()?.ApplyMeal(combatant, ticket.Dish, ticket.IsDeluxe, expiresAtSeconds);
                Debug.Log($"[PartyDiningService] Slot {slot} finished eating {ticket.Dish}");
            }

            Analytics.AnalyticsService.LogDishServed(
                ticket.Dish.ToString(), DishConfig.GetPrice(ticket.Dish), 0, true, ticket.IsDeluxe);

            _slots[slot].HasEatenThisMeal = true;
            _slots[slot].OrderedDishId = -1;
            _slots[slot].HasPaid = false;
            _slots[slot].MealDishId = (int)ticket.Dish;
            _slots[slot].MealDeluxe = ticket.IsDeluxe;
            _slots[slot].MealExpiresAtSeconds = expiresAtSeconds;
            _eating[slot] = false;
            _tickets[slot] = null;

            Core.Services.GetService<KitchenTaskCoordinator>()?.NotifyPartyMemberFinishedEating(ticket);
        }

        private void CheckAllDone()
        {
            if (!_autoResumeWhenDone)
                return;

            // Reload-in-progress: Stop mode hasn't been re-entered yet, so the guard below
            // would wrongly clear the restored auto-resume flag
            if (_pendingReloadDining)
                return;

            var hero = GetHeroComponent();
            if (hero == null || !hero.StoppedAdventure)
            {
                _autoResumeWhenDone = false;
                return;
            }

            // Still waiting on anything? (open order, eating, or an un-skipped eligible member)
            // Eligible members only hold the trip while they can actually be served: if the
            // player disabled EatAtTavern mid-trip or the kitchen lost its staff, they can
            // never order, so only in-flight meals keep the party seated.
            bool canStillServe = EatAtTavern
                && Core.Services.GetService<KitchenTaskCoordinator>()?.IsKitchenOpen == true;
            for (int slot = 0; slot < PartySlots; slot++)
            {
                if (_tickets[slot] != null || _eating[slot])
                    return;
                if (canStillServe && !_slots[slot].HasEatenThisMeal && !_skippedThisSeating[slot]
                    && GetCombatant(slot) != null && TryGetFavorite(slot, out _))
                    return;
            }

            _autoResumeWhenDone = false;
            Debug.Log("[PartyDiningService] Breakfast finished — party resuming adventure");
            PlaySoundAtHero(SoundEffectType.PartyFinishedEating);
            GetStopUI()?.SetStopped(false);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>Shows the appropriate meal speech bubble on the hero entity.</summary>
        private void EmitMealBubble(MealPeriod meal)
        {
            var hero = Core.Scene?.FindEntity("hero");
            switch (meal)
            {
                case MealPeriod.Breakfast:
                    SpeechBubbleDialogue.SayBreakfast(hero);
                    break;
                case MealPeriod.Lunch:
                    SpeechBubbleDialogue.SayLunch(hero);
                    break;
                case MealPeriod.Dinner:
                    SpeechBubbleDialogue.SayDinner(hero);
                    break;
            }
        }

        /// <summary>Session-log line explaining why the meal trip was skipped.</summary>
        private void EmitMealSkipped(string textKey, DishType favorite)
        {
            var events = Core.Services.GetService<GameEventService>();
            if (events == null)
                return;
            string dishName = events.LocalizeUI(DishConfig.GetDefinition(favorite).NameKey);
            events.EmitLocalized(textKey, (dishName, Color.Green));
        }

        /// <summary>
        /// The hero can't order (ingredients or gold), so nobody eats this poll. Marks every
        /// un-fed, order-free slot skipped so CheckAllDone can end a breakfast trip instead of
        /// waiting forever; flags don't block later polls, so the party is still served if
        /// supplies appear while seated.
        /// </summary>
        private void MarkPartySkippedByHero(KitchenTaskCoordinator coordinator,
            DishType heroFavorite, string heroName)
        {
            if (!_skippedThisSeating[0])
            {
                string reason = !coordinator.CanCoverRecipe(heroFavorite) ? "no_ingredients" : "no_gold";
                Analytics.AnalyticsService.LogPartyDineSkipped(0, heroName,
                    heroFavorite.ToString(), reason);
            }

            for (int slot = 0; slot < PartySlots; slot++)
            {
                if (_slots[slot].OrderedDishId < 0 && !_slots[slot].HasEatenThisMeal)
                    _skippedThisSeating[slot] = true;
            }
        }

        /// <summary>
        /// Picks the free dish a mercenary would order right now: their job favorite, or — when
        /// it can't be made — the job class's two cheap fallback dishes, in order. Ingredient-
        /// gated only; mercenary meals never touch gold.
        /// </summary>
        /// <summary>
        /// Picks the dish the hero orders: his chosen favorite first, then his job's cheap
        /// fallbacks — each candidate must be makeable AND affordable since the hero pays.
        /// One missing crop no longer starves the whole party (issue #392 follow-up).
        /// </summary>
        private bool TryPickHeroDish(KitchenTaskCoordinator coordinator, GameStateService gameState,
            DishType favorite, out DishType dish, out bool anyCoverable)
        {
            return TryPickHeroDishCore(favorite, GetJobName(0), gameState.Funds,
                coordinator.CanCoverRecipe, out dish, out anyCoverable);
        }

        /// <summary>
        /// Pure candidate ladder for the hero's order (public static for headless tests):
        /// favorite → job fallback 0 → job fallback 1, first candidate that is coverable and
        /// within <paramref name="funds"/> wins. <paramref name="anyCoverable"/> reports
        /// whether any candidate had ingredients, so a full failure can be attributed to
        /// ingredients vs gold.
        /// </summary>
        public static bool TryPickHeroDishCore(DishType favorite, string jobName, int funds,
            System.Predicate<DishType> canCover, out DishType dish, out bool anyCoverable)
        {
            anyCoverable = false;
            for (int c = 0; c < 3; c++)
            {
                var candidate = c == 0 ? favorite : DishConfig.GetFallbackForJob(jobName, c - 1);
                if (c > 0 && candidate == favorite)
                    continue; // favorite already failed — don't re-check it
                if (!canCover(candidate))
                    continue;
                anyCoverable = true;
                if (funds < DishConfig.GetPrice(candidate))
                    continue;
                dish = candidate;
                return true;
            }
            dish = default;
            return false;
        }

        private bool TryPickMercDish(int slot, DishType favorite,
            KitchenTaskCoordinator coordinator, out DishType dish)
        {
            string jobName = GetJobName(slot);
            for (int c = 0; c < 3; c++)
            {
                var candidate = c == 0 ? favorite : DishConfig.GetFallbackForJob(jobName, c - 1);
                if (c > 0 && candidate == favorite)
                    continue; // favorite already failed — don't re-check it
                if (!coordinator.CanCoverRecipe(candidate))
                    continue;
                dish = candidate;
                return true;
            }
            dish = default;
            return false;
        }

        private string GetJobName(int slot)
        {
            if (slot == 0)
                return GetHeroComponent()?.LinkedHero?.Job?.Name;

            var mercManager = Core.Services.GetService<MercenaryManager>();
            var hired = mercManager?.GetHiredMercenaries();
            int index = slot - 1;
            if (hired == null || index >= hired.Count)
                return null;
            return hired[index].GetComponent<MercenaryComponent>()?.LinkedMercenary?.Job?.Name;
        }

        private bool TryGetFavorite(int slot, out DishType dish)
        {
            if (slot == 0)
            {
                if (FavoriteDishId < 0 || FavoriteDishId >= DishTypeInfo.Count)
                {
                    dish = default;
                    return false;
                }
                dish = (DishType)FavoriteDishId;
                return true;
            }

            var mercManager = Core.Services.GetService<MercenaryManager>();
            var hired = mercManager?.GetHiredMercenaries();
            int index = slot - 1;
            if (hired == null || index >= hired.Count)
            {
                dish = default;
                return false;
            }
            var merc = hired[index].GetComponent<MercenaryComponent>()?.LinkedMercenary;
            if (merc == null)
            {
                dish = default;
                return false;
            }
            dish = DishConfig.GetFavoriteForJob(merc.Job?.Name);
            return true;
        }

        private ICombatant GetCombatant(int slot)
        {
            if (slot == 0)
                return GetHeroComponent()?.LinkedHero;

            var mercManager = Core.Services.GetService<MercenaryManager>();
            var hired = mercManager?.GetHiredMercenaries();
            int index = slot - 1;
            if (hired == null || index >= hired.Count)
                return null;
            return hired[index].GetComponent<MercenaryComponent>()?.LinkedMercenary;
        }

        private HeroComponent GetHeroComponent()
            => Core.Scene?.FindEntity("hero")?.GetComponent<HeroComponent>();

        /// <summary>Positional sound at the hero's seat; guarded because this service also runs headless in tests.</summary>
        private static void PlaySoundAtHero(SoundEffectType soundEffectType)
        {
            if (Core.Instance == null)
                return;
            var heroEntity = Core.Scene?.FindEntity("hero");
            if (heroEntity == null)
                return;
            Core.GetGlobalManager<SoundEffectManager>()?.PlaySoundAt(soundEffectType, heroEntity.Transform.Position);
        }

        private StopAdventuringUI GetStopUI()
            => Core.Services.GetService<SettingsUI>()?.StopAdventuringUI;
    }
}
