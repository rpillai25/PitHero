using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;

namespace PitHero.Services
{
    /// <summary>Pure helpers for the manual job change flow (no Core dependencies).</summary>
    public static class HeroJobChangeHelper
    {
        private static readonly EquipmentSlot[] AllSlots =
        {
            EquipmentSlot.WeaponShield1,
            EquipmentSlot.Armor,
            EquipmentSlot.Hat,
            EquipmentSlot.WeaponShield2,
            EquipmentSlot.Accessory1,
            EquipmentSlot.Accessory2
        };

        /// <summary>
        /// Returns the outgoing crystal to the hero's crystal inventory. Crystals only go to the
        /// Second Chance Shop on death — the vault is just the never-lose-it fallback when the
        /// inventory is full. Returns true when the crystal landed in the inventory.
        /// </summary>
        public static bool ReturnCrystalToInventory(HeroCrystal crystal, CrystalCollectionService crystalService, SecondChanceMerchantVault vault)
        {
            if (crystal == null)
                return false;

            if (crystalService != null && crystalService.TryAddToInventory(crystal))
                return true;

            vault?.AddCrystal(crystal);
            return false;
        }

        /// <summary>
        /// Copies the old hero's six equipment slots onto the new hero. Items the new job cannot
        /// equip go to the bag; if the bag is full they go to the Second Chance vault so nothing
        /// is ever lost.
        /// </summary>
        public static void TransferEquipment(Hero oldHero, Hero newHero, ItemBag bag, SecondChanceMerchantVault vault)
        {
            if (oldHero == null || newHero == null)
                return;

            for (int i = 0; i < AllSlots.Length; i++)
            {
                var slot = AllSlots[i];
                var item = GetSlotItem(oldHero, slot);
                if (item == null)
                    continue;

                if (newHero.SetEquipmentSlot(slot, item))
                    continue;

                if (bag != null && bag.TryAdd(item))
                    continue;

                vault?.AddItems(new[] { item });
            }
        }

        private static IItem GetSlotItem(Hero hero, EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.WeaponShield1: return hero.WeaponShield1;
                case EquipmentSlot.Armor: return hero.Armor;
                case EquipmentSlot.Hat: return hero.Hat;
                case EquipmentSlot.WeaponShield2: return hero.WeaponShield2;
                case EquipmentSlot.Accessory1: return hero.Accessory1;
                case EquipmentSlot.Accessory2: return hero.Accessory2;
                default: return null;
            }
        }
    }
}
