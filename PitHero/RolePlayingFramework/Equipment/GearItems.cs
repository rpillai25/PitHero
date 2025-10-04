namespace RolePlayingFramework.Equipment
{
    /// <summary>Factory for creating concrete gear items.</summary>
    public static class GearItems
    {
        /// <summary>Create Short Sword.</summary>
        public static Gear ShortSword() => Swords.ShortSword.Create();
        
        /// <summary>Create Wooden Shield.</summary>
        public static Gear WoodenShield() => Shields.WoodenShield.Create();
        
        /// <summary>Create Squire Helm.</summary>
        public static Gear SquireHelm() => Helms.SquireHelm.Create();
        
        /// <summary>Create Leather Armor.</summary>
        public static Gear LeatherArmor() => Armor.LeatherArmor.Create();
        
        /// <summary>Create Ring of Power.</summary>
        public static Gear RingOfPower() => Accessories.RingOfPower.Create();
        
        /// <summary>Create Necklace of Health.</summary>
        public static Gear NecklaceOfHealth() => Accessories.NecklaceOfHealth.Create();
    }
}
