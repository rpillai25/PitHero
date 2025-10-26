using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class GearElementTests
    {
        [TestMethod]
        public void Gear_DefaultConstructor_ShouldHaveNeutralElement()
        {
            var gear = new Gear(
                "Test Sword",
                ItemKind.WeaponSword,
                ItemRarity.Normal,
                "A test sword",
                100,
                StatBlock.Zero,
                atk: 5
            );

            Assert.AreEqual(ElementType.Neutral, gear.Element);
        }

        [TestMethod]
        public void Gear_WithFireElement_ShouldHaveFireElement()
        {
            var gear = new Gear(
                "Flame Sword",
                ItemKind.WeaponSword,
                ItemRarity.Rare,
                "A sword wreathed in flames",
                500,
                StatBlock.Zero,
                atk: 10,
                element: ElementType.Fire
            );

            Assert.AreEqual(ElementType.Fire, gear.Element);
        }

        [TestMethod]
        public void Gear_WithWaterElement_ShouldHaveWaterElement()
        {
            var gear = new Gear(
                "Aqua Shield",
                ItemKind.Shield,
                ItemRarity.Rare,
                "A shield infused with water",
                400,
                StatBlock.Zero,
                def: 8,
                element: ElementType.Water
            );

            Assert.AreEqual(ElementType.Water, gear.Element);
        }

        [TestMethod]
        public void Gear_WithEarthElement_ShouldHaveEarthElement()
        {
            var gear = new Gear(
                "Stone Armor",
                ItemKind.ArmorMail,
                ItemRarity.Epic,
                "Armor as hard as stone",
                800,
                StatBlock.Zero,
                def: 15,
                element: ElementType.Earth
            );

            Assert.AreEqual(ElementType.Earth, gear.Element);
        }

        [TestMethod]
        public void Gear_WithWindElement_ShouldHaveWindElement()
        {
            var gear = new Gear(
                "Gale Blade",
                ItemKind.WeaponSword,
                ItemRarity.Epic,
                "A blade swift as the wind",
                1000,
                StatBlock.Zero,
                atk: 18,
                element: ElementType.Wind
            );

            Assert.AreEqual(ElementType.Wind, gear.Element);
        }

        [TestMethod]
        public void Gear_WithLightElement_ShouldHaveLightElement()
        {
            var gear = new Gear(
                "Holy Helm",
                ItemKind.HatHelm,
                ItemRarity.Legendary,
                "A helm blessed by light",
                1500,
                StatBlock.Zero,
                def: 10,
                hp: 20,
                element: ElementType.Light
            );

            Assert.AreEqual(ElementType.Light, gear.Element);
        }

        [TestMethod]
        public void Gear_WithDarkElement_ShouldHaveDarkElement()
        {
            var gear = new Gear(
                "Shadow Cloak",
                ItemKind.Accessory,
                ItemRarity.Legendary,
                "A cloak woven from shadows",
                2000,
                new StatBlock(strength: 0, agility: 5, vitality: 0, magic: 3),
                element: ElementType.Dark
            );

            Assert.AreEqual(ElementType.Dark, gear.Element);
        }

        [TestMethod]
        public void Gear_AllPropertiesWithElement_ShouldRetainAllValues()
        {
            var stats = new StatBlock(strength: 2, agility: 1, vitality: 3, magic: 0);
            var gear = new Gear(
                "Elemental Sword",
                ItemKind.WeaponSword,
                ItemRarity.Epic,
                "A sword of pure fire",
                1200,
                stats,
                atk: 20,
                def: 5,
                hp: 15,
                mp: 10,
                element: ElementType.Fire
            );

            Assert.AreEqual("Elemental Sword", gear.Name);
            Assert.AreEqual(ItemKind.WeaponSword, gear.Kind);
            Assert.AreEqual(ItemRarity.Epic, gear.Rarity);
            Assert.AreEqual("A sword of pure fire", gear.Description);
            Assert.AreEqual(1200, gear.Price);
            Assert.AreEqual(2, gear.StatBonus.Strength);
            Assert.AreEqual(1, gear.StatBonus.Agility);
            Assert.AreEqual(3, gear.StatBonus.Vitality);
            Assert.AreEqual(0, gear.StatBonus.Magic);
            Assert.AreEqual(20, gear.AttackBonus);
            Assert.AreEqual(5, gear.DefenseBonus);
            Assert.AreEqual(15, gear.HPBonus);
            Assert.AreEqual(10, gear.MPBonus);
            Assert.AreEqual(ElementType.Fire, gear.Element);
        }

        [TestMethod]
        public void IGear_Interface_ShouldExposeElementProperty()
        {
            IGear gear = new Gear(
                "Test Item",
                ItemKind.Accessory,
                ItemRarity.Normal,
                "Test",
                50,
                StatBlock.Zero,
                element: ElementType.Water
            );

            Assert.AreEqual(ElementType.Water, gear.Element);
        }
    }
}
