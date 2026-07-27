namespace RolePlayingFramework.Equipment
{
    /// <summary>
    /// Player-facing gear categories used by the auto-sell and auto-purchase filters (issue #345).
    /// Several <see cref="ItemKind"/> values collapse into one category (all WeaponX kinds are Weapon,
    /// all HatX kinds are Helm, all ArmorX kinds are Armor).
    /// </summary>
    public enum GearCategory
    {
        Weapon = 0,
        Helm = 1,
        Shield = 2,
        Armor = 3,
        Accessory = 4
    }
}
