namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a fixed amount of MP.</summary>
    public sealed class MPPotion : BaseHPMPPotion
    {
        public MPPotion() : base("MPPotion", ItemRarity.Normal, "Restores 100 MP", 20, 0, 100) { }
    }
}
