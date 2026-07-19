using Microsoft.Xna.Framework;
using Nez;

namespace PitHero.Dining
{
    /// <summary>Lifecycle of a kitchen order ticket.</summary>
    public enum TicketState
    {
        /// <summary>The fridge lacks some ingredients; a runner is bringing them from storage.</summary>
        AwaitingIngredients,
        /// <summary>All ingredients are in the fridge; waiting for a cook to read the ticket.</summary>
        ReadyToCook,
        /// <summary>A cook at a station is preparing the dish.</summary>
        Cooking,
        /// <summary>Cooked dish is sitting on a serving table waiting for its server.</summary>
        Plated,
        /// <summary>A server is carrying the dish to the patron's table.</summary>
        Delivering,
        /// <summary>Dish is on the table; the patron is eating.</summary>
        Delivered,
        /// <summary>Ticket was canceled (patron left, was hired, or the party resumed play).</summary>
        Canceled,
    }

    /// <summary>
    /// One food order flowing through the kitchen. Ingredients are reserved physically at ticket
    /// creation — fridge stock is consumed first, and any shortfall is withdrawn from Crop Storage
    /// (crash-proof: it survives save/load with the storage inventory). The runner's trip to
    /// storage and back to the fridge is the visible half of that shortfall. Canceling before
    /// cooking starts refunds fridge-taken units to the fridge and storage-taken units to storage.
    /// </summary>
    public sealed class KitchenTicket
    {
        public int TicketId;
        public DishType Dish;

        /// <summary>True for hero/hired-merc orders; they get cook priority.</summary>
        public bool IsPartyTicket;

        /// <summary>0 = hero, 1/2 = hired mercenary index; -1 for tavern patrons.</summary>
        public int PartySlot = -1;

        /// <summary>Unhired patron entity this ticket belongs to (null for party tickets).</summary>
        public Entity PatronEntity;

        /// <summary>Seat tile the order was taken at — where the dish is delivered.</summary>
        public Point SeatTile;

        /// <summary>The table the seat belongs to; determines which server zone owns this ticket.</summary>
        public Point TableTile;

        public TicketState State = TicketState.AwaitingIngredients;

        /// <summary>True once the server has posted this order on the ticket board (cooks only see posted tickets).</summary>
        public bool PostedToBoard;

        /// <summary>True while a cook has this ticket in mind (read from the board).</summary>
        public bool CookClaimed;

        /// <summary>True once every ingredient is physically in the fridge.</summary>
        public bool IngredientsFetched;

        /// <summary>True until cooking starts — the window in which canceling refunds ingredients.</summary>
        public bool CropsRefundable = true;

        /// <summary>Units taken from the fridge at creation, parallel to the recipe entries (for refunds).</summary>
        public int[] FridgeTakenQty;

        /// <summary>Units withdrawn from Crop Storage at creation, parallel to the recipe entries (for refunds).</summary>
        public int[] StorageTakenQty;

        /// <summary>Cooking station index (0-2) claimed by a cook; -1 until claimed.</summary>
        public int StationIndex = -1;

        /// <summary>Serving table slot (0-2) this dish occupies or is headed to; -1 when none.</summary>
        public int ServingSlot = -1;

        /// <summary>Rolled at cook start from the cook's proficiency.</summary>
        public bool IsDeluxe;

        /// <summary>World dish entity on the serving table or on the patron's table.</summary>
        public Entity PlatedDishEntity;
    }
}
