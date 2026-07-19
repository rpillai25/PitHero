using Nez;
using PitHero.Dining;
using PitHero.ECS.Components;
using PitHero.UI;
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

        /// <summary>When true, the party auto-dines at the tavern after waking each morning.</summary>
        public bool EatAtTavern;

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

        // ── Entry points ────────────────────────────────────────────────────────

        /// <summary>
        /// Morning auto-dine (called from SleepInBedAction after night sleep). Enters Stop mode
        /// and walks the party to the tavern for breakfast — skipped entirely when no member
        /// could actually order (gold, storage coverage, or the kitchen can't serve).
        /// </summary>
        public void BeginAutoDine()
        {
            if (!EatAtTavern)
                return;

            var coordinator = Core.Services.GetService<KitchenTaskCoordinator>();
            if (coordinator == null || !coordinator.IsKitchenOpen)
            {
                Debug.Log("[PartyDiningService] Skipping breakfast trip — kitchen cannot serve");
                return;
            }

            if (!AnyMemberCanDine(coordinator))
            {
                Debug.Log("[PartyDiningService] Skipping breakfast trip — no party member can order");
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
        /// Next seated party member wanting to order, gold-gated hero-first. Members whose
        /// favorite can't be covered by storage are skipped without substitution.
        /// </summary>
        public bool TryGetNextPartyOrder(out int partySlot, out DishType dish)
        {
            partySlot = -1;
            dish = default;

            var hero = GetHeroComponent();
            if (hero == null || !hero.StoppedAdventure || !hero.SeatedInTavern)
                return false;

            var coordinator = Core.Services.GetService<KitchenTaskCoordinator>();
            var gameState = Core.Services.GetService<GameStateService>();
            if (coordinator == null || gameState == null)
                return false;

            for (int slot = 0; slot < PartySlots; slot++)
            {
                if (_slots[slot].HasEatenToday || _slots[slot].OrderedDishId >= 0 || _skippedThisSeating[slot])
                    continue;
                if (GetCombatant(slot) == null)
                    continue;
                if (!TryGetFavorite(slot, out var favorite))
                    continue;

                if (!coordinator.CanCoverRecipe(favorite))
                {
                    // No substitutions — this member simply doesn't eat this seating
                    _skippedThisSeating[slot] = true;
                    continue;
                }
                if (gameState.Funds < DishConfig.GetPrice(favorite))
                {
                    _skippedThisSeating[slot] = true;
                    continue;
                }

                partySlot = slot;
                dish = favorite;
                return true;
            }
            return false;
        }

        /// <summary>Party pays at order time (unlike walk-in patrons).</summary>
        public void OnPartyOrderTaken(int partySlot, KitchenTicket ticket)
        {
            var gameState = Core.Services.GetService<GameStateService>();
            if (gameState != null)
                gameState.Funds -= DishConfig.GetPrice(ticket.Dish);

            _slots[partySlot].OrderedDishId = (int)ticket.Dish;
            _slots[partySlot].HasPaid = true;
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

            var hero = GetHeroComponent();
            if (hero == null || !hero.StoppedAdventure)
            {
                _autoResumeWhenDone = false;
                return;
            }

            // Still waiting on anything? (open order, eating, or an un-skipped eligible member)
            for (int slot = 0; slot < PartySlots; slot++)
            {
                if (_tickets[slot] != null || _eating[slot])
                    return;
                if (!_slots[slot].HasEatenToday && !_skippedThisSeating[slot]
                    && GetCombatant(slot) != null && TryGetFavorite(slot, out _))
                    return;
            }

            _autoResumeWhenDone = false;
            Debug.Log("[PartyDiningService] Breakfast finished — party resuming adventure");
            GetStopUI()?.SetStopped(false);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private bool AnyMemberCanDine(KitchenTaskCoordinator coordinator)
        {
            var gameState = Core.Services.GetService<GameStateService>();
            if (gameState == null)
                return false;

            for (int slot = 0; slot < PartySlots; slot++)
            {
                if (_slots[slot].HasEatenToday || GetCombatant(slot) == null)
                    continue;
                if (!TryGetFavorite(slot, out var favorite))
                    continue;
                if (coordinator.CanCoverRecipe(favorite) && gameState.Funds >= DishConfig.GetPrice(favorite))
                    return true;
            }
            return false;
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

        private StopAdventuringUI GetStopUI()
            => Core.Services.GetService<SettingsUI>()?.StopAdventuringUI;
    }
}
