namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores HP.</summary>
    public sealed class FullHPPotion : BaseHPMPPotion
    {
        public FullHPPotion() : base("FullHPPotion", ItemRarity.Epic, "Fully restores HP", 500, -1, 0) { }
    }
}
