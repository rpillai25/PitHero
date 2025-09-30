using PitHero.ECS.Components;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Standard Bag (capacity 12 upgrade trigger).</summary>
    public sealed class StandardBag : Bag
    {
        public StandardBag() : base("Standard Bag", ItemRarity.Normal) { }
    }
}
