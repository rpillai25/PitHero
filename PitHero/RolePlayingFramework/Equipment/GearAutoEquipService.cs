using Nez;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Mercenaries;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Service for automatically equipping optimal gear when looted from chests.</summary>
    public static class GearAutoEquipService
    {
        /// <summary>Maps gear Kind to the equipment slot it occupies.</summary>
        public static bool TryGetSlotForGear(IGear gear, out EquipmentSlot slot)
        {
            if (gear == null)
            {
                slot = EquipmentSlot.WeaponShield1;
                return false;
            }

            switch (gear.Kind)
            {
                case ItemKind.WeaponSword:
                case ItemKind.WeaponKnife:
                case ItemKind.WeaponKnuckle:
                case ItemKind.WeaponStaff:
                case ItemKind.WeaponRod:
                case ItemKind.WeaponBow:
                case ItemKind.WeaponHammer:
                    slot = EquipmentSlot.WeaponShield1;
                    return true;

                case ItemKind.Shield:
                    slot = EquipmentSlot.WeaponShield2;
                    return true;

                case ItemKind.ArmorMail:
                case ItemKind.ArmorGi:
                case ItemKind.ArmorRobe:
                    slot = EquipmentSlot.Armor;
                    return true;

                case ItemKind.HatHelm:
                case ItemKind.HatHeadband:
                case ItemKind.HatWizard:
                case ItemKind.HatPriest:
                    slot = EquipmentSlot.Hat;
                    return true;

                case ItemKind.Accessory:
                    // Accessory callers handle both slots directly; the returned slot value is intentionally unused for accessories.
                    slot = EquipmentSlot.Accessory1;
                    return true;

                default:
                    slot = EquipmentSlot.WeaponShield1;
                    return false;
            }
        }

        /// <summary>Calculates a normalized score for gear based on stat bonuses and flat bonuses.</summary>
        public static int GetGearScore(IGear gear)
        {
            if (gear == null) return 0;

            int score = 0;
            score += gear.StatBonus.Strength;
            score += gear.StatBonus.Agility;
            score += gear.StatBonus.Vitality;
            score += gear.StatBonus.Magic;
            score += gear.AttackBonus;
            score += gear.DefenseBonus;
            score += gear.HPBonus / 5;
            score += gear.MPBonus / 3;

            return score;
        }

        /// <summary>Calculates elemental resistance score by summing positive resistances.</summary>
        public static float GetElementalResistanceScore(IGear gear)
        {
            if (gear == null || gear.ElementalProps == null || gear.ElementalProps.Resistances == null)
                return 0f;

            float score = 0f;
            var resistances = gear.ElementalProps.Resistances;

            if (resistances.TryGetValue(ElementType.Fire, out float fireRes) && fireRes > 0)
                score += fireRes;
            if (resistances.TryGetValue(ElementType.Water, out float waterRes) && waterRes > 0)
                score += waterRes;
            if (resistances.TryGetValue(ElementType.Earth, out float earthRes) && earthRes > 0)
                score += earthRes;
            if (resistances.TryGetValue(ElementType.Wind, out float windRes) && windRes > 0)
                score += windRes;
            if (resistances.TryGetValue(ElementType.Light, out float lightRes) && lightRes > 0)
                score += lightRes;
            if (resistances.TryGetValue(ElementType.Dark, out float darkRes) && darkRes > 0)
                score += darkRes;

            return score;
        }

        /// <summary>Determines if new gear is better than existing gear.</summary>
        public static bool IsNewGearBetter(IGear newGear, IGear existing)
        {
            if (newGear == null) return false;
            if (existing == null) return true;

            int newScore = GetGearScore(newGear);
            int existingScore = GetGearScore(existing);

            if (newScore > existingScore) return true;
            if (newScore < existingScore) return false;

            float newResistanceScore = GetElementalResistanceScore(newGear);
            float existingResistanceScore = GetElementalResistanceScore(existing);

            return newResistanceScore > existingResistanceScore;
        }

        /// <summary>Attempts to auto-equip gear on the hero. Returns true if equipped.</summary>
        public static bool TryAutoEquipOnHero(Hero hero, ItemBag bag, IGear gear)
        {
            if (hero == null || bag == null || gear == null)
                return false;

            if (!hero.CanEquipItem(gear))
                return false;

            if (!TryGetSlotForGear(gear, out EquipmentSlot slot))
                return false;

            if (gear.Kind == ItemKind.Accessory)
            {
                if (hero.Accessory1 == null)
                {
                    if (hero.SetEquipmentSlot(EquipmentSlot.Accessory1, gear))
                    {
                        bag.Remove(gear);
                        Debug.Log($"[GearAutoEquip] Equipped {gear.Name} to hero's Accessory1 slot");
                        return true;
                    }
                }
                else if (hero.Accessory2 == null)
                {
                    if (hero.SetEquipmentSlot(EquipmentSlot.Accessory2, gear))
                    {
                        bag.Remove(gear);
                        Debug.Log($"[GearAutoEquip] Equipped {gear.Name} to hero's Accessory2 slot");
                        return true;
                    }
                }
                return false;
            }

            IGear currentGear = GetHeroItemInSlot(hero, slot);
            
            if (currentGear == null)
            {
                if (hero.SetEquipmentSlot(slot, gear))
                {
                    bag.Remove(gear);
                    Debug.Log($"[GearAutoEquip] Equipped {gear.Name} to hero's {slot} slot (empty)");
                    return true;
                }
                return false;
            }

            if (IsNewGearBetter(gear, currentGear))
            {
                if (hero.SetEquipmentSlot(slot, gear))
                {
                    bag.Remove(gear);
                    bag.TryAdd(currentGear);
                    Debug.Log($"[GearAutoEquip] Swapped {currentGear.Name} with {gear.Name} in hero's {slot} slot");
                    return true;
                }
            }

            return false;
        }

        /// <summary>Attempts to auto-equip gear on a mercenary. Returns true if equipped.</summary>
        public static bool TryAutoEquipOnMercenary(Mercenary merc, ItemBag heroBag, IGear gear)
        {
            if (merc == null || heroBag == null || gear == null)
                return false;

            if (!merc.CanEquipItem(gear))
                return false;

            if (!TryGetSlotForGear(gear, out EquipmentSlot slot))
                return false;

            if (gear.Kind == ItemKind.Accessory)
            {
                if (merc.Accessory1 == null)
                {
                    if (merc.SetEquipmentSlot(EquipmentSlot.Accessory1, gear))
                    {
                        heroBag.Remove(gear);
                        Debug.Log($"[GearAutoEquip] Equipped {gear.Name} to {merc.Name}'s Accessory1 slot");
                        return true;
                    }
                }
                else if (merc.Accessory2 == null)
                {
                    if (merc.SetEquipmentSlot(EquipmentSlot.Accessory2, gear))
                    {
                        heroBag.Remove(gear);
                        Debug.Log($"[GearAutoEquip] Equipped {gear.Name} to {merc.Name}'s Accessory2 slot");
                        return true;
                    }
                }
                return false;
            }

            IGear currentGear = GetMercItemInSlot(merc, slot);
            
            if (currentGear == null)
            {
                if (merc.SetEquipmentSlot(slot, gear))
                {
                    heroBag.Remove(gear);
                    Debug.Log($"[GearAutoEquip] Equipped {gear.Name} to {merc.Name}'s {slot} slot (empty)");
                    return true;
                }
                return false;
            }

            if (IsNewGearBetter(gear, currentGear))
            {
                if (merc.SetEquipmentSlot(slot, gear))
                {
                    heroBag.Remove(gear);
                    heroBag.TryAdd(currentGear);
                    Debug.Log($"[GearAutoEquip] Swapped {currentGear.Name} with {gear.Name} in {merc.Name}'s {slot} slot");
                    return true;
                }
            }

            return false;
        }

        /// <summary>Gets the gear item in the specified slot for a hero.</summary>
        private static IGear GetHeroItemInSlot(Hero hero, EquipmentSlot slot)
        {
            if (hero == null) return null;

            switch (slot)
            {
                case EquipmentSlot.WeaponShield1:
                    return hero.WeaponShield1 as IGear;
                case EquipmentSlot.WeaponShield2:
                    return hero.WeaponShield2 as IGear;
                case EquipmentSlot.Armor:
                    return hero.Armor as IGear;
                case EquipmentSlot.Hat:
                    return hero.Hat as IGear;
                case EquipmentSlot.Accessory1:
                    return hero.Accessory1 as IGear;
                case EquipmentSlot.Accessory2:
                    return hero.Accessory2 as IGear;
                default:
                    return null;
            }
        }

        /// <summary>Gets the gear item in the specified slot for a mercenary.</summary>
        private static IGear GetMercItemInSlot(Mercenary merc, EquipmentSlot slot)
        {
            if (merc == null) return null;

            switch (slot)
            {
                case EquipmentSlot.WeaponShield1:
                    return merc.WeaponShield1;
                case EquipmentSlot.WeaponShield2:
                    return merc.WeaponShield2;
                case EquipmentSlot.Armor:
                    return merc.Armor;
                case EquipmentSlot.Hat:
                    return merc.Hat;
                case EquipmentSlot.Accessory1:
                    return merc.Accessory1;
                case EquipmentSlot.Accessory2:
                    return merc.Accessory2;
                default:
                    return null;
            }
        }
    }
}
