namespace PitHero.Dining
{
    /// <summary>
    /// Contract between the kitchen system and the party meal phase (Phase 6).
    /// The KitchenTaskCoordinator calls into this interface when a party member wants to order
    /// or receives a dish. Phase 6 implements this (e.g., on a StopAdventureService or SceneState).
    /// </summary>
    public interface IPartyOrderSource
    {
        /// <summary>
        /// Returns the next seated party member wanting to order.
        /// <paramref name="partySlot"/> is 0=hero, 1/2=hired mercs. False if none.
        /// </summary>
        bool TryGetNextPartyOrder(out int partySlot, out DishType dish);

        /// <summary>
        /// Called when a server takes the order (party pays here). Ticket already created.
        /// </summary>
        void OnPartyOrderTaken(int partySlot, KitchenTicket ticket);

        /// <summary>
        /// Called when the dish lands on the party member's table (eat timer starts in the service).
        /// </summary>
        void OnPartyDishDelivered(int partySlot, KitchenTicket ticket);
    }
}
