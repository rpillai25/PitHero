namespace RolePlayingFramework.Equipment
{
    /// <summary>Fully restores HP and MP.</summary>
    public sealed class FullMixPotion : BaseHPMPPotion
    {
        public FullMixPotion() : base("FullMixPotion", ItemRarity.Epic, "Fully restores HP and MP", 900, -1, -1) { }
    }
}
