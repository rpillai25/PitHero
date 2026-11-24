using PitHero;

namespace RolePlayingFramework.Equipment
{
    /// <summary>
    /// Maps <see cref="ItemKind"/> values onto the five player-facing <see cref="GearCategory"/>
    /// buckets and supplies their localization keys. Keep the switch here consistent with
    /// <see cref="GearAutoEquipService.TryGetSlotForGear"/> — both describe the same grouping.
    /// </summary>
    public static class GearCategoryUtils
    {
        /// <summary>Number of gear categories; the length of every category-indexed filter array.</summary>
        public const int Count = 5;

        /// <summary>Resolves the gear category for an item kind. Returns false for non-gear kinds (e.g. Consumable).</summary>
        public static bool TryGetCategory(ItemKind kind, out GearCategory category)
        {
            switch (kind)
            {
                case ItemKind.WeaponSword:
                case ItemKind.WeaponKnife:
                case ItemKind.WeaponKnuckle:
                case ItemKind.WeaponStaff:
                case ItemKind.WeaponRod:
                case ItemKind.WeaponBow:
                case ItemKind.WeaponHammer:
                    category = GearCategory.Weapon;
                    return true;

                case ItemKind.HatHelm:
                case ItemKind.HatHeadband:
                case ItemKind.HatWizard:
                case ItemKind.HatPriest:
                    category = GearCategory.Helm;
                    return true;

                case ItemKind.Shield:
                    category = GearCategory.Shield;
                    return true;

                case ItemKind.ArmorMail:
                case ItemKind.ArmorGi:
                case ItemKind.ArmorRobe:
                    category = GearCategory.Armor;
                    return true;

                case ItemKind.Accessory:
                    category = GearCategory.Accessory;
                    return true;

                default:
                    category = GearCategory.Weapon;
                    return false;
            }
        }

        /// <summary>
        /// True for weapons that take both hands (bows, issue #317): the holder cannot also carry a
        /// shield, so the WeaponShield2 slot must stay empty while one is equipped.
        /// </summary>
        public static bool IsTwoHanded(ItemKind kind) => kind == ItemKind.WeaponBow;

        /// <summary>True when the item is a two-handed weapon (null-safe).</summary>
        public static bool IsTwoHanded(IItem item) => item != null && IsTwoHanded(item.Kind);

        /// <summary>
        /// True when the pair can be worn together: a two-handed weapon in WeaponShield1 leaves
        /// WeaponShield2 empty. Every equip path checks this before writing either slot.
        /// </summary>
        public static bool IsHandsPairValid(IItem weaponSlotItem, IItem shieldSlotItem)
            => shieldSlotItem == null || !IsTwoHanded(weaponSlotItem);

        /// <summary>Localization key for a category's display name.</summary>
        public static string GetDisplayNameKey(GearCategory category)
        {
            switch (category)
            {
                case GearCategory.Weapon: return UITextKey.GearTypeWeapon;
                case GearCategory.Helm: return UITextKey.GearTypeHelm;
                case GearCategory.Shield: return UITextKey.GearTypeShield;
                case GearCategory.Armor: return UITextKey.GearTypeArmor;
                case GearCategory.Accessory: return UITextKey.GearTypeAccessory;
                default: return UITextKey.GearTypeWeapon;
            }
        }

        /// <summary>
        /// True when the kind's category is enabled in a category-indexed filter array.
        /// Non-gear kinds and malformed arrays are always allowed, matching the rarity-filter convention.
        /// </summary>
        public static bool IsAllowed(bool[] allowedByCategory, ItemKind kind)
        {
            if (allowedByCategory == null)
                return true;
            if (!TryGetCategory(kind, out GearCategory category))
                return true;
            int index = (int)category;
            return index < 0 || index >= allowedByCategory.Length || allowedByCategory[index];
        }
    }
}
