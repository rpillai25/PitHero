namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores moderate HP and MP.</summary>
    public sealed class MixPotion : BaseHPMPPotion
    {
        public MixPotion() : base("MixPotion", ItemRarity.Normal, "Restores 100 HP and 100 MP", 30, 100, 100) { }
    }
}
