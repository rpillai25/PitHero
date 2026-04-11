using RolePlayingFramework.Heroes;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Contract for the CrystalCollectionService, defining the 80-slot inventory
    /// and 5-slot forge queue for hero crystals. The VGL mock and production service
    /// both implement this interface so the same tests exercise the real logic.
    /// </summary>
    public interface ICrystalCollectionService
    {
        // ── Inventory (80 slots) ──────────────────────────────────────────────

        /// <summary>Maximum number of crystals the inventory can hold.</summary>
        int InventoryCapacity { get; }

        /// <summary>Number of crystals currently in inventory.</summary>
        int InventoryCount { get; }

        /// <summary>
        /// Attempts to add a crystal to the first available inventory slot.
        /// Returns false when the inventory is full or the crystal is null.
        /// </summary>
        bool TryAddToInventory(HeroCrystal crystal);

        /// <summary>
        /// Removes the crystal at <paramref name="slotIndex"/> from inventory.
        /// Returns false if the slot is empty or the index is out of range.
        /// </summary>
        bool TryRemoveFromInventory(int slotIndex);

        /// <summary>
        /// Returns the crystal stored at <paramref name="slotIndex"/>, or null if empty.
        /// </summary>
        HeroCrystal? GetInventoryCrystal(int slotIndex);

        // ── Queue (5 slots) ───────────────────────────────────────────────────

        /// <summary>Maximum number of crystals the queue can hold.</summary>
        int QueueCapacity { get; }

        /// <summary>Number of crystals currently waiting in the queue.</summary>
        int QueueCount { get; }

        /// <summary>
        /// Adds a crystal to the back of the auto-infuse queue.
        /// Returns false when the queue is full or the crystal is null.
        /// </summary>
        bool TryEnqueue(HeroCrystal crystal);

        /// <summary>
        /// Removes and returns the crystal at the front of the queue, or null if empty.
        /// Called by HeroDeathComponent to populate PendingNextCrystal.
        /// </summary>
        HeroCrystal? Dequeue();

        /// <summary>
        /// Returns the crystal at the front of the queue without removing it, or null if empty.
        /// </summary>
        HeroCrystal? PeekQueue();

        // ── Pending crystal (set at hero death, consumed at promotion) ────────

        /// <summary>
        /// Crystal popped from the queue during hero death.
        /// HeroPromotionService reads this to bind the crystal to the new hero,
        /// then clears it to null.
        /// </summary>
        HeroCrystal? PendingNextCrystal { get; set; }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        /// <summary>Clears the entire inventory and queue and resets PendingNextCrystal.</summary>
        void Clear();
    }
}
