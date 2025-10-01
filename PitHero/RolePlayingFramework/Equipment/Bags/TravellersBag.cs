namespace RolePlayingFramework.Equipment
{
    /// <summary>Traveller's Bag (capacity 20 upgrade trigger).</summary>
    public sealed class TravellersBag : Bag
    {
        public TravellersBag() : base("Traveller's Bag", ItemRarity.Rare, "Spacious bag used by travellers. 20 slots", 500) { }
    }
}
