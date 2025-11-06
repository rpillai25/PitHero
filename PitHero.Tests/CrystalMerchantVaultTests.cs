using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class CrystalMerchantVaultTests
    {
        [TestMethod]
        public void CrystalMerchantVault_AddCrystal_ShouldIncreaseCount()
        {
            // Arrange
            var vault = new CrystalMerchantVault();
            var knight = new Knight();
            var baseStats = new StatBlock(strength: 5, agility: 3, vitality: 5, magic: 1);
            var crystal = new HeroCrystal("Fallen Knight", knight, 10, baseStats);

            // Act
            vault.AddCrystal(crystal);

            // Assert
            Assert.AreEqual(1, vault.Count);
            Assert.AreEqual(crystal, vault.Crystals[0]);
        }

        [TestMethod]
        public void CrystalMerchantVault_AddMultipleCrystals_ShouldStoreAll()
        {
            // Arrange
            var vault = new CrystalMerchantVault();
            var knight = new Knight();
            var mage = new Mage();
            var baseStats1 = new StatBlock(strength: 5, agility: 3, vitality: 5, magic: 1);
            var baseStats2 = new StatBlock(strength: 1, agility: 3, vitality: 3, magic: 7);
            var crystal1 = new HeroCrystal("Fallen Knight", knight, 10, baseStats1);
            var crystal2 = new HeroCrystal("Fallen Mage", mage, 15, baseStats2);

            // Act
            vault.AddCrystal(crystal1);
            vault.AddCrystal(crystal2);

            // Assert
            Assert.AreEqual(2, vault.Count);
        }

        [TestMethod]
        public void CrystalMerchantVault_RemoveCrystal_ShouldDecreaseCount()
        {
            // Arrange
            var vault = new CrystalMerchantVault();
            var knight = new Knight();
            var baseStats = new StatBlock(strength: 5, agility: 3, vitality: 5, magic: 1);
            var crystal = new HeroCrystal("Fallen Knight", knight, 10, baseStats);
            vault.AddCrystal(crystal);

            // Act
            var removed = vault.RemoveCrystal(crystal);

            // Assert
            Assert.IsTrue(removed);
            Assert.AreEqual(0, vault.Count);
        }

        [TestMethod]
        public void CrystalMerchantVault_RemoveNonExistentCrystal_ShouldReturnFalse()
        {
            // Arrange
            var vault = new CrystalMerchantVault();
            var knight = new Knight();
            var baseStats = new StatBlock(strength: 5, agility: 3, vitality: 5, magic: 1);
            var crystal = new HeroCrystal("Fallen Knight", knight, 10, baseStats);

            // Act
            var removed = vault.RemoveCrystal(crystal);

            // Assert
            Assert.IsFalse(removed);
        }

        [TestMethod]
        public void CrystalMerchantVault_Clear_ShouldRemoveAllCrystals()
        {
            // Arrange
            var vault = new CrystalMerchantVault();
            var knight = new Knight();
            var mage = new Mage();
            var baseStats1 = new StatBlock(strength: 5, agility: 3, vitality: 5, magic: 1);
            var baseStats2 = new StatBlock(strength: 1, agility: 3, vitality: 3, magic: 7);
            var crystal1 = new HeroCrystal("Fallen Knight", knight, 10, baseStats1);
            var crystal2 = new HeroCrystal("Fallen Mage", mage, 15, baseStats2);
            vault.AddCrystal(crystal1);
            vault.AddCrystal(crystal2);

            // Act
            vault.Clear();

            // Assert
            Assert.AreEqual(0, vault.Count);
        }

        [TestMethod]
        public void CrystalMerchantVault_AddNullCrystal_ShouldNotIncreaseCount()
        {
            // Arrange
            var vault = new CrystalMerchantVault();

            // Act
            vault.AddCrystal(null);

            // Assert
            Assert.AreEqual(0, vault.Count);
        }

        [TestMethod]
        public void CrystalMerchantVault_Crystals_ShouldBeReadOnly()
        {
            // Arrange
            var vault = new CrystalMerchantVault();
            var knight = new Knight();
            var baseStats = new StatBlock(strength: 5, agility: 3, vitality: 5, magic: 1);
            var crystal = new HeroCrystal("Fallen Knight", knight, 10, baseStats);
            vault.AddCrystal(crystal);

            // Act & Assert
            Assert.IsInstanceOfType(vault.Crystals, typeof(System.Collections.ObjectModel.ReadOnlyCollection<HeroCrystal>));
        }
    }
}
