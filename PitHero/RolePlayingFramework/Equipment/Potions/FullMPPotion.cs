namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores MP.</summary>
    public sealed class FullMPPotion : BaseHPMPPotion
    {
        public FullMPPotion() : base("FullMPPotion", ItemRarity.Epic, "Fully restores MP", 500, 0, -1) { }
    }
}
