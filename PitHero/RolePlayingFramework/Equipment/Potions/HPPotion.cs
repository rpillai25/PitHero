namespace RolePlayingFramework.Equipment
{
    /// <summary>Restores a fixed amount of HP.</summary>
    public sealed class HPPotion : BaseHPMPPotion
    {
        public HPPotion() : base("HPPotion", ItemRarity.Normal, "Restores 100 HP", 20, 100, 0) { }
    }
}
