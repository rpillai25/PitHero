using System;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.Dining;
using PitHero.Services;

namespace PitHero.ECS.Components
{
    /// <summary>States for an unhired mercenary dining as a tavern patron.</summary>
    public enum PatronState
    {
        /// <summary>Seated, waiting for a server to take the order.</summary>
        WaitingToOrder,
        /// <summary>Order has been taken; waiting for food to arrive.</summary>
        Ordered,
        /// <summary>Dish delivered to the table; patron is eating.</summary>
        FoodDelivered,
        /// <summary>Eating in progress.</summary>
        Eating,
        /// <summary>Meal complete; patron paid and tipped.</summary>
        FinishedEating,
    }

    /// <summary>
    /// Added to unhired merc entities when they sit in the tavern. Tracks patience timers,
    /// payment, and coordinates with KitchenTaskCoordinator for ordering and delivery.
    /// </summary>
    public class TavernPatronComponent : Component, IUpdatable, IPausableComponent
    {
        public bool ShouldPause => true;

        /// <summary>Current dining state.</summary>
        public PatronState State { get; private set; } = PatronState.WaitingToOrder;

        /// <summary>The ticket assigned to this patron's order; null until ordered.</summary>
        public KitchenTicket ActiveTicket { get; set; }

        /// <summary>Seat tile this patron occupies; used to derive the table plate position.</summary>
        public Point SeatTile { get; set; }

        private PauseService _pauseService;
        private KitchenTaskCoordinator _coordinator;
        private GameStateService _gameState;
        private float _elapsed;
        private bool _walkedOff;

        public override void OnAddedToEntity()
        {
            // Core.Services requires a running game instance; headless tests inject via SetHeadlessServices.
            if (Core.Instance != null)
            {
                _pauseService  = Core.Services.GetService<PauseService>();
                _coordinator   = Core.Services.GetService<KitchenTaskCoordinator>();
                _gameState     = Core.Services.GetService<GameStateService>();
            }
            _elapsed = 0f;
        }

        /// <summary>
        /// Injects service instances directly for headless tests (no running game instance).
        /// The live path resolves these through Core.Services in OnAddedToEntity.
        /// </summary>
        public void SetHeadlessServices(KitchenTaskCoordinator coordinator, GameStateService gameState)
        {
            _coordinator = coordinator;
            _gameState = gameState;
        }

        /// <summary>Transition to Ordered state once a server takes this patron's order.</summary>
        public void OnOrderTaken(KitchenTicket ticket)
        {
            ActiveTicket = ticket;
            State = PatronState.Ordered;
            _elapsed = 0f;
        }

        /// <summary>Transition to FoodDelivered/Eating state once the dish lands on the table.</summary>
        public void OnDishDelivered()
        {
            State = PatronState.FoodDelivered;
            _elapsed = 0f;
        }

        public void Update()
        {
            if (_pauseService?.IsPaused == true)
                return;

            _elapsed += Time.DeltaTime;

            switch (State)
            {
                case PatronState.WaitingToOrder:
                    // Kitchen needs to be open for orders — servers handle coming to us
                    if (_elapsed >= GameConfig.PatronPatiencePreOrderSeconds)
                    {
                        // Patience expired — cancel and leave
                        LeaveOnPatienceExpiry();
                    }
                    break;

                case PatronState.Ordered:
                    if (_elapsed >= GameConfig.PatronPatiencePostOrderSeconds)
                    {
                        LeaveOnPatienceExpiry();
                    }
                    break;

                case PatronState.FoodDelivered:
                    // Start eating immediately
                    State = PatronState.Eating;
                    _elapsed = 0f;
                    break;

                case PatronState.Eating:
                    if (ActiveTicket == null)
                    {
                        State = PatronState.FinishedEating;
                        break;
                    }
                    float eatTime = DishConfig.GetEatSeconds(ActiveTicket.Dish);
                    if (_elapsed >= eatTime)
                    {
                        FinishEating();
                    }
                    break;

                case PatronState.FinishedEating:
                    // Stick around a while after the meal before heading out
                    if (!_walkedOff && _elapsed >= GameConfig.PatronLingerAfterEatingSeconds)
                    {
                        _walkedOff = true;
                        WalkOffViaMercenaryManager();
                    }
                    break;
            }
        }

        private void FinishEating()
        {
            State = PatronState.FinishedEating;
            _elapsed = 0f; // linger timer starts now; the walk-off happens in Update

            if (ActiveTicket != null)
            {
                // Pay for the dish
                if (_gameState != null)
                {
                    int price = DishConfig.GetPrice(ActiveTicket.Dish);
                    _gameState.AddFunds(price, "dish_sale");

                    // Tip roll
                    int tip = 0;
                    if (Nez.Random.Chance(GameConfig.DishTipChance))
                    {
                        float tipPct = Nez.Random.Range(GameConfig.DishTipMinPercent, GameConfig.DishTipMaxPercent);
                        tip = (int)Math.Ceiling(price * tipPct);
                        if (tip > 0)
                            _gameState.AddFunds(tip, "dish_tip");
                    }

                    PitHero.Services.Analytics.AnalyticsService.LogDishServed(
                        ActiveTicket.Dish.ToString(), price, tip, false, ActiveTicket.IsDeluxe);
                }

                _coordinator?.NotifyPatronFinishedEating(ActiveTicket);
            }
        }

        private void LeaveOnPatienceExpiry()
        {
            // Cancel outstanding ticket (refund logic lives in coordinator)
            if (ActiveTicket != null)
                _coordinator?.CancelTicket(ActiveTicket);
            ActiveTicket = null;

            State = PatronState.FinishedEating;

            // Out of patience — no lingering, leave right away
            _walkedOff = true;
            WalkOffViaMercenaryManager();
        }

        private void WalkOffViaMercenaryManager()
        {
            var mercManager = Core.Instance != null ? Core.Services.GetService<MercenaryManager>() : null;
            mercManager?.WalkOffPatron(Entity);
        }
    }
}
