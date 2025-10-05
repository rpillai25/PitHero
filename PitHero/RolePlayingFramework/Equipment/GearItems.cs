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
        
        /// <summary>Create Iron Helm.</summary>
        public static Gear IronHelm() => Helms.IronHelm.Create();
        
        /// <summary>Create Iron Armor.</summary>
        public static Gear IronArmor() => Armor.IronArmor.Create();
        
        /// <summary>Create Iron Shield.</summary>
        public static Gear IronShield() => Shields.IronShield.Create();
        
        /// <summary>Create Long Sword.</summary>
        public static Gear LongSword() => Swords.LongSword.Create();
        
        /// <summary>Create Protect Ring.</summary>
        public static Gear ProtectRing() => Accessories.ProtectRing.Create();
        
        /// <summary>Create Magic Chain.</summary>
        public static Gear MagicChain() => Accessories.MagicChain.Create();
    }
}
