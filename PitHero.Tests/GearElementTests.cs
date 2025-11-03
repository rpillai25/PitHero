using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Equipment.Swords;
using RolePlayingFramework.Equipment.Armor;
using RolePlayingFramework.Equipment.Shields;
using RolePlayingFramework.Equipment.Helms;
using RolePlayingFramework.Equipment.Accessories;
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

            Assert.AreEqual(ElementType.Neutral, gear.ElementalProps.Element);
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
                elementalProps: new ElementalProperties(ElementType.Fire)
            );

            Assert.AreEqual(ElementType.Fire, gear.ElementalProps.Element);
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
                elementalProps: new ElementalProperties(ElementType.Water)
            );

            Assert.AreEqual(ElementType.Water, gear.ElementalProps.Element);
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
                elementalProps: new ElementalProperties(ElementType.Earth)
            );

            Assert.AreEqual(ElementType.Earth, gear.ElementalProps.Element);
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
                elementalProps: new ElementalProperties(ElementType.Wind)
            );

            Assert.AreEqual(ElementType.Wind, gear.ElementalProps.Element);
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
                elementalProps: new ElementalProperties(ElementType.Light)
            );

            Assert.AreEqual(ElementType.Light, gear.ElementalProps.Element);
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
                elementalProps: new ElementalProperties(ElementType.Dark)
            );

            Assert.AreEqual(ElementType.Dark, gear.ElementalProps.Element);
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
                elementalProps: new ElementalProperties(ElementType.Fire)
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
            Assert.AreEqual(ElementType.Fire, gear.ElementalProps.Element);
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
                elementalProps: new ElementalProperties(ElementType.Water)
            );

            Assert.AreEqual(ElementType.Water, gear.ElementalProps.Element);
        }

        [TestMethod]
        public void IGear_Interface_ShouldExposeElementalPropsProperty()
        {
            IGear gear = new Gear(
                "Test Item",
                ItemKind.Accessory,
                ItemRarity.Normal,
                "Test",
                50,
                StatBlock.Zero,
                elementalProps: new ElementalProperties(ElementType.Fire)
            );

            Assert.IsNotNull(gear.ElementalProps);
            Assert.AreEqual(ElementType.Fire, gear.ElementalProps.Element);
        }

        [TestMethod]
        public void Gear_WithElementalProperties_ShouldHaveCorrectResistances()
        {
            var resistances = new System.Collections.Generic.Dictionary<ElementType, float>
            {
                { ElementType.Fire, 0.3f },
                { ElementType.Water, -0.15f }
            };
            var elementalProps = new ElementalProperties(ElementType.Fire, resistances);
            
            var gear = new Gear(
                "Fire Resistant Armor",
                ItemKind.ArmorMail,
                ItemRarity.Rare,
                "Armor with fire resistance",
                800,
                StatBlock.Zero,
                def: 7,
                hp: 30,
                elementalProps: elementalProps
            );

            Assert.AreEqual(ElementType.Fire, gear.ElementalProps.Element);
            Assert.IsTrue(gear.ElementalProps.Resistances.ContainsKey(ElementType.Fire));
            Assert.AreEqual(0.3f, gear.ElementalProps.Resistances[ElementType.Fire]);
            Assert.IsTrue(gear.ElementalProps.Resistances.ContainsKey(ElementType.Water));
            Assert.AreEqual(-0.15f, gear.ElementalProps.Resistances[ElementType.Water]);
        }

        // Equipment Factory Tests
        [TestMethod]
        public void ShortSword_ShouldHaveNeutralElement()
        {
            var sword = ShortSword.Create();
            Assert.AreEqual(ElementType.Neutral, sword.ElementalProps.Element);
        }

        [TestMethod]
        public void LongSword_ShouldHaveFireElement()
        {
            var sword = LongSword.Create();
            Assert.AreEqual(ElementType.Fire, sword.ElementalProps.Element);
        }

        [TestMethod]
        public void LeatherArmor_ShouldHaveNeutralElement()
        {
            var armor = LeatherArmor.Create();
            Assert.AreEqual(ElementType.Neutral, armor.ElementalProps.Element);
        }

        [TestMethod]
        public void IronArmor_ShouldHaveEarthElementAndResistances()
        {
            var armor = IronArmor.Create();
            Assert.AreEqual(ElementType.Earth, armor.ElementalProps.Element);
            Assert.IsTrue(armor.ElementalProps.Resistances.ContainsKey(ElementType.Earth));
            Assert.AreEqual(0.25f, armor.ElementalProps.Resistances[ElementType.Earth]);
            Assert.IsTrue(armor.ElementalProps.Resistances.ContainsKey(ElementType.Wind));
            Assert.AreEqual(-0.15f, armor.ElementalProps.Resistances[ElementType.Wind]);
        }

        [TestMethod]
        public void WoodenShield_ShouldHaveNeutralElement()
        {
            var shield = WoodenShield.Create();
            Assert.AreEqual(ElementType.Neutral, shield.ElementalProps.Element);
        }

        [TestMethod]
        public void IronShield_ShouldHaveWaterElementAndResistances()
        {
            var shield = IronShield.Create();
            Assert.AreEqual(ElementType.Water, shield.ElementalProps.Element);
            Assert.IsTrue(shield.ElementalProps.Resistances.ContainsKey(ElementType.Water));
            Assert.AreEqual(0.30f, shield.ElementalProps.Resistances[ElementType.Water]);
            Assert.IsTrue(shield.ElementalProps.Resistances.ContainsKey(ElementType.Fire));
            Assert.AreEqual(-0.15f, shield.ElementalProps.Resistances[ElementType.Fire]);
        }

        [TestMethod]
        public void SquireHelm_ShouldHaveNeutralElement()
        {
            var helm = SquireHelm.Create();
            Assert.AreEqual(ElementType.Neutral, helm.ElementalProps.Element);
        }

        [TestMethod]
        public void IronHelm_ShouldHaveEarthElementAndResistances()
        {
            var helm = IronHelm.Create();
            Assert.AreEqual(ElementType.Earth, helm.ElementalProps.Element);
            Assert.IsTrue(helm.ElementalProps.Resistances.ContainsKey(ElementType.Earth));
            Assert.AreEqual(0.20f, helm.ElementalProps.Resistances[ElementType.Earth]);
            Assert.IsTrue(helm.ElementalProps.Resistances.ContainsKey(ElementType.Wind));
            Assert.AreEqual(-0.10f, helm.ElementalProps.Resistances[ElementType.Wind]);
        }

        [TestMethod]
        public void RingOfPower_ShouldHaveNeutralElement()
        {
            var ring = RingOfPower.Create();
            Assert.AreEqual(ElementType.Neutral, ring.ElementalProps.Element);
        }

        [TestMethod]
        public void NecklaceOfHealth_ShouldHaveLightElement()
        {
            var necklace = NecklaceOfHealth.Create();
            Assert.AreEqual(ElementType.Light, necklace.ElementalProps.Element);
        }

        [TestMethod]
        public void ProtectRing_ShouldHaveNeutralElement()
        {
            var ring = ProtectRing.Create();
            Assert.AreEqual(ElementType.Neutral, ring.ElementalProps.Element);
        }

        [TestMethod]
        public void MagicChain_ShouldHaveDarkElement()
        {
            var chain = MagicChain.Create();
            Assert.AreEqual(ElementType.Dark, chain.ElementalProps.Element);
        }
    }
}
