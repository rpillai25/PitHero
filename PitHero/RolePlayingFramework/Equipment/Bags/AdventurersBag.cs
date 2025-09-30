namespace RolePlayingFramework.Equipment
{
    /// <summary>Adventurer's Bag (capacity 24 upgrade trigger).</summary>
    public sealed class AdventurersBag : Bag
    {
        public AdventurersBag() : base("Adventurer's Bag", ItemRarity.Epic, "Large bag used by experienced adventurers. 24 slots", 1200) { }
    }
}
