namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of HP and MP.</summary>
    public sealed class MidMixPotion : BaseHPMPPotion
    {
        public MidMixPotion() : base("MidMixPotion", ItemRarity.Rare, "Restores 500 HP and 500 MP", 180, 500, 500) { }
    }
}
