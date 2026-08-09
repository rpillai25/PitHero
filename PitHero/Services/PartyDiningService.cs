using Microsoft.Xna.Framework;
using Nez;
using PitHero.Dining;
using PitHero.ECS.Components;
using PitHero.UI;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;
using RolePlayingFramework.Combat;

namespace PitHero.Services
{
    /// <summary>
    /// Orchestrates once-a-day party dining at the tavern (issue #319) on top of the existing
    /// Stop mode — no new GOAP surface. Implements IPartyOrderSource so kitchen servers take
    /// party orders with priority. Slot 0 = hero, slots 1/2 = hired mercenaries in
    /// MercenaryManager.GetHiredMercenaries() order.
    /// </summary>
    public sealed class PartyDiningService : IPartyOrderSource
    {
        /// <summary>One party member's dining state for the day (persisted in save section 33).</summary>
        public struct MemberDining
        {
            public int OrderedDishId;   // -1 = none
            public bool HasPaid;
            public bool HasEatenToday;
            public int MealDishId;      // -1 = no active meal buffs
            public bool MealDeluxe;
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
            bool anyOpenOrder = false;
            for (int slot = 0; slot < PartySlots && slot < data.PartyDining.Length; slot++)
            {
                var saved = data.PartyDining[slot];
                _slots[slot] = new MemberDining
                {
                    OrderedDishId = saved.OrderedDishId,
                    HasPaid = saved.HasPaid,
                    HasEatenToday = saved.HasEatenToday,
                    MealDishId = saved.MealDishId,
                    MealDeluxe = saved.MealDeluxe,
                };

                if (saved.MealDishId >= 0 && saved.MealDishId < DishTypeInfo.Count)
                {
                    var combatant = GetCombatant(slot);
                    if (combatant != null)
                        mealBuffs?.RestoreRecord(combatant, (DishType)saved.MealDishId, saved.MealDeluxe);
                }

                if (saved.OrderedDishId >= 0 && !saved.HasEatenToday)
                    anyOpenOrder = true;
            }

            if (anyOpenOrder)
                MarkPendingReloadDining();
        }

        // ── Daily reset ─────────────────────────────────────────────────────────

        /// <summary>6 AM reset: everyone may eat again; active meal records expire separately.</summary>
        public void ResetDaily()
        {
            for (int i = 0; i < PartySlots; i++)
            {
                _slots[i].HasEatenToday = false;
                _slots[i].MealDishId = -1;
                _slots[i].MealDeluxe = false;
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
                if (!_slots[slot].HasEatenToday && GetCombatant(slot) != null)
                    count++;
            }
            return count;
        }

        // ── Entry points ────────────────────────────────────────────────────────

        /// <summary>
        /// Morning auto-dine (called from SleepInBedAction after night sleep). Enters Stop mode
        /// and walks the party to the tavern for breakfast — skipped entirely when the hero,
        /// who leads the meal, can't order (gold, storage coverage, or the kitchen can't serve).
        /// </summary>
        public void BeginAutoDine()
        {
            if (!EatAtTavern)
                return;

            var heroComponent = GetHeroComponent();
            if (heroComponent != null && heroComponent.StoppedAdventure)
            {
                // A player-stopped party returns to the table on its own (SeatedInTavern goal)
                // and orders once seated; arming auto-resume here would cancel the player's stop.
                Debug.Log("[PartyDiningService] Skipping breakfast trip — party already stopped");
                return;
            }

            var coordinator = Core.Services.GetService<KitchenTaskCoordinator>();
            if (coordinator == null || !coordinator.IsKitchenOpen)
            {
                Debug.Log("[PartyDiningService] Skipping breakfast trip — kitchen cannot serve");
                return;
            }

            // The hero leads the meal — mercs eat free but only when he eats — so the trip
            // hinges entirely on his favorite dish being makeable and affordable.
            if (_slots[0].HasEatenToday || GetCombatant(0) == null)
            {
                Debug.Log("[PartyDiningService] Skipping breakfast trip — hero already ate");
                return;
            }

            var favorite = FavoriteDishId >= 0 && FavoriteDishId < DishTypeInfo.Count
                ? (DishType)FavoriteDishId
                : DishType.RoastedOnionSkewers;
            if (!coordinator.CanCoverRecipe(favorite))
            {
                EmitBreakfastSkipped(UITextKey.ConsoleBreakfastSkipped, favorite);
                Debug.Log("[PartyDiningService] Skipping breakfast trip — no ingredients for favorite dish");
                return;
            }

            var gameState = Core.Services.GetService<GameStateService>();
            if (gameState == null || gameState.Funds < DishConfig.GetPrice(favorite))
            {
                EmitBreakfastSkipped(UITextKey.ConsoleBreakfastSkippedGold, favorite);
                Debug.Log("[PartyDiningService] Skipping breakfast trip — not enough gold for favorite dish");
                return;
            }

            var stopUI = GetStopUI();
            if (stopUI == null)
                return;

            _autoResumeWhenDone = true;
            stopUI.SetStopped(true);
            Debug.Log("[PartyDiningService] Party heading to the tavern for breakfast");
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

        /// <summary>Ticks eat timers and handles the deferred reload-mid-dining restart.</summary>
        public void Update()
        {
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true)
                return;

            if (_pendingReloadDining)
                HandlePendingReload();

            var hero = GetHeroComponent();
            bool seated = hero != null && hero.StoppedAdventure && hero.SeatedInTavern;

            // Re-create tickets for restored open orders once the party is seated again
            if (seated)
            {
                var coordinator = Core.Services.GetService<KitchenTaskCoordinator>();
                for (int slot = 0; slot < PartySlots; slot++)
                {
                    if (_slots[slot].OrderedDishId >= 0 && !_slots[slot].HasEatenToday && _tickets[slot] == null)
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
        /// favorite dish only and pays for it; if it can't be made or afforded, the servers
        /// skip the entire party — mercenaries never eat unless the hero eats. Mercenary meals
        /// are free (job favorite, then the job's cheap fallbacks, ingredient-gated only).
        /// Skips are re-evaluated on every poll, so the party is still served if ingredients
        /// or gold appear while they remain seated.
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

            bool heroLeads = _slots[0].OrderedDishId >= 0 || _slots[0].HasEatenToday;
            if (!heroLeads)
            {
                var heroCombatant = GetCombatant(0);
                if (heroCombatant == null || !TryGetFavorite(0, out var heroFavorite))
                    return false;

                if (!coordinator.CanCoverRecipe(heroFavorite)
                    || gameState.Funds < DishConfig.GetPrice(heroFavorite))
                {
                    MarkPartySkippedByHero(coordinator, heroFavorite, heroCombatant.Name);
                    return false;
                }

                _skippedThisSeating[0] = false;
                partySlot = 0;
                dish = heroFavorite;
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

                if (_slots[slot].HasEatenToday)
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

            var combatant = GetCombatant(slot);
            if (combatant != null)
            {
                Core.Services.GetService<MealBuffService>()?.ApplyMeal(combatant, ticket.Dish, ticket.IsDeluxe);
                Debug.Log($"[PartyDiningService] Slot {slot} finished eating {ticket.Dish}");
            }

            Analytics.AnalyticsService.LogDishServed(
                ticket.Dish.ToString(), DishConfig.GetPrice(ticket.Dish), 0, true, ticket.IsDeluxe);

            _slots[slot].HasEatenToday = true;
            _slots[slot].OrderedDishId = -1;
            _slots[slot].HasPaid = false;
            _slots[slot].MealDishId = (int)ticket.Dish;
            _slots[slot].MealDeluxe = ticket.IsDeluxe;
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
                if (canStillServe && !_slots[slot].HasEatenToday && !_skippedThisSeating[slot]
                    && GetCombatant(slot) != null && TryGetFavorite(slot, out _))
                    return;
            }

            _autoResumeWhenDone = false;
            Debug.Log("[PartyDiningService] Breakfast finished — party resuming adventure");
            PlaySoundAtHero(SoundEffectType.PartyFinishedEating);
            GetStopUI()?.SetStopped(false);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>Session-log line explaining why the wake-up breakfast trip was skipped.</summary>
        private void EmitBreakfastSkipped(string textKey, DishType favorite)
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
                if (_slots[slot].OrderedDishId < 0 && !_slots[slot].HasEatenToday)
                    _skippedThisSeating[slot] = true;
            }
        }

        /// <summary>
        /// Picks the free dish a mercenary would order right now: their job favorite, or — when
        /// it can't be made — the job class's two cheap fallback dishes, in order. Ingredient-
        /// gated only; mercenary meals never touch gold.
        /// </summary>
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
