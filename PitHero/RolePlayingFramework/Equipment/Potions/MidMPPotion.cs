namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of MP.</summary>
    public sealed class MidMPPotion : BaseHPMPPotion
    {
        public MidMPPotion() : base("MidMPPotion", ItemRarity.Rare, "Restores 500 MP", 100, 0, 500) { }
    }
}
