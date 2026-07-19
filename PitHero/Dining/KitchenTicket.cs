using Nez;

namespace PitHero.Dining
{
    /// <summary>Lifecycle of a kitchen order ticket.</summary>
    public enum TicketState
    {
        /// <summary>Waiting for a runner to fetch the reserved crops from storage.</summary>
        AwaitingIngredients,
        /// <summary>Ingredients are in the kitchen; waiting for a free cook.</summary>
        ReadyToCook,
        /// <summary>A cook at a stove is preparing the dish.</summary>
        Cooking,
        /// <summary>Cooked dish is sitting above the stove waiting for a server.</summary>
        Plated,
        /// <summary>A server is carrying the dish to the patron.</summary>
        Delivering,
        /// <summary>Dish is on the table; the patron is eating.</summary>
        Delivered,
        /// <summary>Ticket was canceled (patron left, was hired, or the party resumed play).</summary>
        Canceled,
    }

    /// <summary>
    /// One food order flowing through the kitchen. Crops are deducted from storage at ticket
    /// creation — that deduction IS the reservation (crash-proof: it survives save/load with
    /// the storage inventory). Canceling before cooking starts refunds the crops.
    /// </summary>
    public sealed class KitchenTicket
    {
        public int TicketId;
        public DishType Dish;

        /// <summary>True for hero/hired-merc orders; they get order-taking and cook priority.</summary>
        public bool IsPartyTicket;

        /// <summary>0 = hero, 1/2 = hired mercenary index; -1 for tavern patrons.</summary>
        public int PartySlot = -1;

        /// <summary>Unhired patron entity this ticket belongs to (null for party tickets).</summary>
        public Entity PatronEntity;

        public TicketState State = TicketState.AwaitingIngredients;

        /// <summary>True once a runner has physically delivered the crops to the kitchen.</summary>
        public bool IngredientsFetched;

        /// <summary>True until cooking starts — the window in which canceling refunds crops.</summary>
        public bool CropsRefundable = true;

        /// <summary>Stove index (0-2) claimed by a cook; -1 until claimed.</summary>
        public int StoveIndex = -1;

        /// <summary>Rolled at cook start from the cook's proficiency.</summary>
        public bool IsDeluxe;

        /// <summary>World dish entity above the stove or on the patron's table.</summary>
        public Entity PlatedDishEntity;
    }
}
