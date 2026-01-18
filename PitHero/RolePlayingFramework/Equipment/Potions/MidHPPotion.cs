namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a large amount of HP.</summary>
    public sealed class MidHPPotion : BaseHPMPPotion
    {
        public MidHPPotion() : base("MidHPPotion", ItemRarity.Rare, "Restores 500 HP", 100, 500, 0) { }
    }
}
