using System;
using System.Collections.Generic;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Maps item names to factory functions for save/load item reconstruction.</summary>
    public static class ItemRegistry
    {
        private static Dictionary<string, Func<IItem>> _registry;
        private static bool _initialized;

        /// <summary>Returns the registry, initializing on first access.</summary>
        private static Dictionary<string, Func<IItem>> Registry
        {
            get
            {
                if (!_initialized)
                {
                    Initialize();
                    _initialized = true;
                }
                return _registry;
            }
        }

        /// <summary>Registers a single factory by creating a temporary item to extract its name.</summary>
        private static void Register(Func<IItem> factory)
        {
            var item = factory();
            _registry[item.Name] = factory;
        }

        /// <summary>Populates the registry with all known items from GearItems and PotionItems.</summary>
        private static void Initialize()
        {
            _registry = new Dictionary<string, Func<IItem>>(100);

            // Gear items - Swords
            Register(() => GearItems.AbyssFang());
            Register(() => GearItems.CaveStalkersBlade());
            Register(() => GearItems.CavernCutter());
            Register(() => GearItems.CrystalEdge());
            Register(() => GearItems.DepthsReaver());
            Register(() => GearItems.DiamondEdge());
            Register(() => GearItems.EmberSword());
            Register(() => GearItems.GloomBlade());
            Register(() => GearItems.GraniteBlade());
            Register(() => GearItems.InfernoEdge());
            Register(() => GearItems.LavaForgedSword());
            Register(() => GearItems.LongSword());
            Register(() => GearItems.MagmaBlade());
            Register(() => GearItems.MinersPickSword());
            Register(() => GearItems.PitLordsSword());
            Register(() => GearItems.QuartzSaber());
            Register(() => GearItems.RustyBlade());
            Register(() => GearItems.ShadowFang());
            Register(() => GearItems.ShortSword());
            Register(() => GearItems.SpelunkersSaber());
            Register(() => GearItems.StalagmiteSword());
            Register(() => GearItems.StoneSword());
            Register(() => GearItems.TorchBlade());
            Register(() => GearItems.UndergroundRapier());
            Register(() => GearItems.VoidCutter());

            // Gear items - Axes
            Register(() => GearItems.CrystalCleaver());
            Register(() => GearItems.FlameHatchet());
            Register(() => GearItems.MinersAxe());
            Register(() => GearItems.ObsidianCleaver());
            Register(() => GearItems.ShadowSplitter());
            Register(() => GearItems.StoneHatchet());
            Register(() => GearItems.VolcanicAxe());
            Register(() => GearItems.WoodcuttersAxe());

            // Gear items - Daggers
            Register(() => GearItems.AssassinsEdge());
            Register(() => GearItems.CaveShiv());
            Register(() => GearItems.RustyDagger());
            Register(() => GearItems.SerpentsTooth());
            Register(() => GearItems.ShadowStiletto());
            Register(() => GearItems.SilentFang());

            // Gear items - Spears
            Register(() => GearItems.CavePike());
            Register(() => GearItems.FlameLance());
            Register(() => GearItems.InfernalPike());
            Register(() => GearItems.StalactiteSpear());
            Register(() => GearItems.StoneLance());
            Register(() => GearItems.WoodenSpear());

            // Gear items - Hammers
            Register(() => GearItems.GeologistsHammer());
            Register(() => GearItems.MagmaMaul());
            Register(() => GearItems.Mallet());
            Register(() => GearItems.QuakeHammer());
            Register(() => GearItems.StoneCrusher());

            // Gear items - Staves
            Register(() => GearItems.EarthenStaff());
            Register(() => GearItems.EmberRod());
            Register(() => GearItems.ShadowwoodStaff());
            Register(() => GearItems.TorchStaff());
            Register(() => GearItems.WalkingStick());

            // Gear items - Armor
            Register(() => GearItems.AbyssPlate());
            Register(() => GearItems.BurlapTunic());
            Register(() => GearItems.CaveExplorersVest());
            Register(() => GearItems.ChainShirt());
            Register(() => GearItems.CrystalGuard());
            Register(() => GearItems.DiamondMail());
            Register(() => GearItems.EmberguardMail());
            Register(() => GearItems.GranitePlate());
            Register(() => GearItems.HardenedLeather());
            Register(() => GearItems.HideVest());
            Register(() => GearItems.IronArmor());
            Register(() => GearItems.LavaplateArmor());
            Register(() => GearItems.LeatherArmor());
            Register(() => GearItems.MagmaBlastPlate());
            Register(() => GearItems.PaddedArmor());
            Register(() => GearItems.PitLordsArmor());
            Register(() => GearItems.ReinforcedPlate());
            Register(() => GearItems.ScaleMail());
            Register(() => GearItems.ShadowVest());
            Register(() => GearItems.SteelCuirass());
            Register(() => GearItems.StonePlate());
            Register(() => GearItems.StuddedLeather());
            Register(() => GearItems.TatteredCloth());
            Register(() => GearItems.Voidmail());
            Register(() => GearItems.VolcanicArmor());

            // Gear items - Shields
            Register(() => GearItems.AbyssWall());
            Register(() => GearItems.CaveGuard());
            Register(() => GearItems.CrystalBarrier());
            Register(() => GearItems.DiamondBarrier());
            Register(() => GearItems.EmberShield());
            Register(() => GearItems.GraniteGuard());
            Register(() => GearItems.HeaterShield());
            Register(() => GearItems.HideShield());
            Register(() => GearItems.InfernoGuard());
            Register(() => GearItems.IronBuckler());
            Register(() => GearItems.IronShield());
            Register(() => GearItems.KiteShield());
            Register(() => GearItems.LavaShield());
            Register(() => GearItems.MagmaWall());
            Register(() => GearItems.PitLordsAegis());
            Register(() => GearItems.QuartzWall());
            Register(() => GearItems.ReinforcedBuckler());
            Register(() => GearItems.RoundShield());
            Register(() => GearItems.ShadowGuard());
            Register(() => GearItems.SteelShield());
            Register(() => GearItems.StoneShield());
            Register(() => GearItems.TowerShield());
            Register(() => GearItems.VoidBarrier());
            Register(() => GearItems.WoodenPlank());
            Register(() => GearItems.WoodenShield());

            // Gear items - Helms
            Register(() => GearItems.AbyssHelm());
            Register(() => GearItems.Bascinet());
            Register(() => GearItems.CaveExplorersHood());
            Register(() => GearItems.ChainCoif());
            Register(() => GearItems.ClothCap());
            Register(() => GearItems.CrystalCirclet());
            Register(() => GearItems.DiamondCirclet());
            Register(() => GearItems.EmberHelm());
            Register(() => GearItems.GreatHelm());
            Register(() => GearItems.HideHood());
            Register(() => GearItems.InfernoCrown());
            Register(() => GearItems.IronHelm());
            Register(() => GearItems.LavaCrown());
            Register(() => GearItems.LeatherCap());
            Register(() => GearItems.MagmaHelm());
            Register(() => GearItems.PaddedCoif());
            Register(() => GearItems.PitLordsCrown());
            Register(() => GearItems.QuartzHelm());
            Register(() => GearItems.ReinforcedCap());
            Register(() => GearItems.ShadowCowl());
            Register(() => GearItems.SquireHelm());
            Register(() => GearItems.SteelHelm());
            Register(() => GearItems.StoneCrown());
            Register(() => GearItems.VoidMask());
            Register(() => GearItems.WingedHelm());

            // Gear items - Accessories
            Register(() => GearItems.RingOfPower());
            Register(() => GearItems.NecklaceOfHealth());
            Register(() => GearItems.ProtectRing());
            Register(() => GearItems.MagicChain());

            // Potion items
            Register(() => PotionItems.HPPotion());
            Register(() => PotionItems.MPPotion());
            Register(() => PotionItems.MixPotion());
            Register(() => PotionItems.MidHPPotion());
            Register(() => PotionItems.MidMPPotion());
            Register(() => PotionItems.MidMixPotion());
            Register(() => PotionItems.FullHPPotion());
            Register(() => PotionItems.FullMPPotion());
            Register(() => PotionItems.FullMixPotion());


        }

        /// <summary>Attempts to create an item by name. Returns true if found.</summary>
        public static bool TryCreateItem(string name, out IItem item)
        {
            if (Registry.TryGetValue(name, out var factory))
            {
                item = factory();
                return true;
            }

            item = null;
            return false;
        }
    }
}
