using PitHero.RolePlayingSystem.Items.Armors;
using PitHero.RolePlayingSystem.Items.Consumable;
using System.Collections.Generic;

namespace PitHero.RolePlayingSystem.Items
{
    public static class ItemCache
    {
        private static Dictionary<ArmorCatalog, Armor> armorCache;
        private static Dictionary<ConsumableCatalog, Item> consumableCache;

        static ItemCache()
        {
            armorCache = new Dictionary<ArmorCatalog, Armor>();
            FillArmor();

            consumableCache = new Dictionary<ConsumableCatalog, Item>();
            FillConsumables();
        }

        public static Item Armor(ArmorCatalog armorCatalog)
        {
            return armorCache[armorCatalog];
        }

        public static Item Consumable(ConsumableCatalog consumableCatalog)
        {
            return consumableCache[consumableCatalog];
        }

        private static void FillArmor()
        {
            armorCache[ArmorCatalog.LeatherHelmet] = new LeatherHelmet();
        }

        private static void FillConsumables()
        {
            consumableCache[ConsumableCatalog.HealingHerb] = new HealingHerb();
            consumableCache[ConsumableCatalog.LargeHealingHerb] = new LargeHealingHerb();
            consumableCache[ConsumableCatalog.MaxHealingHerb] = new MaxHealingHerb();
        }
    }
}
