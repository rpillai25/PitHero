using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Farming;
using PitHero.Util;
using PitHero.Services;
using RolePlayingFramework.Equipment;

namespace PitHero.Tests
{
    /// <summary>
    /// ConfirmationDialog is NOT modal (SetModal is unavailable in this Nez fork) and dialogs are
    /// stage-centered, so several can stack on the same target and all be confirmed. Every handler
    /// that grants gold or items must therefore be idempotent: it has to derive the payout from a
    /// state transition that can only happen once, never from a total priced before the dialog opened.
    /// These tests pin that property for the services behind those handlers.
    /// </summary>
    [TestClass]
    public class ConfirmationDialogIdempotencyTests
    {
        #region Crop storage sell paths

        /// <summary>
        /// Mirrors HarvestedCropsModeOverlay.SellAvailableInBuilding: take what is live in each slot
        /// and total the gold actually realized. Repeating it must realize nothing the second time.
        /// </summary>
        private static int SellAvailable(CropStorageInventoryService storage, int buildingId, HarvestSlot[] buffer)
        {
            storage.CopyDisplaySlots(buildingId, buffer);
            int realized = 0;
            for (int s = 0; s < buffer.Length; s++)
            {
                if (buffer[s].IsEmpty)
                    continue;
                int sold = storage.TakeFromSlot(buildingId, s, buffer[s].Count);
                if (sold > 0)
                    realized += CropConfig.GetHarvestStackSellPrice(buffer[s].Type, sold);
            }
            return realized;
        }

        [TestMethod]
        public void SellStorageCrops_SecondConfirmationRealizesNothing()
        {
            var storage = new CropStorageInventoryService(buildingService: null);
            const int buildingId = 1;
            storage.TryDeposit(buildingId, CropType.Wheat, 10);

            var buffer = new HarvestSlot[CropStorageInventoryService.SlotsPerBuilding];

            int first = SellAvailable(storage, buildingId, buffer);
            Assert.IsTrue(first > 0, "The first sale should realize gold for the stored crops");

            int second = SellAvailable(storage, buildingId, buffer);
            Assert.AreEqual(0, second,
                "A second stacked sell confirmation must realize 0 — paying a pre-dialog snapshot here mints gold");
        }

        [TestMethod]
        public void SellStorageCrops_RealizedGoldMatchesWhatWasActuallyTaken()
        {
            var storage = new CropStorageInventoryService(buildingService: null);
            const int buildingId = 2;
            storage.TryDeposit(buildingId, CropType.Wheat, 6);

            var buffer = new HarvestSlot[CropStorageInventoryService.SlotsPerBuilding];

            // Price the sale the way the dialog prompt does, before anything is taken.
            storage.CopyDisplaySlots(buildingId, buffer);
            int quoted = 0;
            for (int s = 0; s < buffer.Length; s++)
                if (!buffer[s].IsEmpty)
                    quoted += CropConfig.GetHarvestStackSellPrice(buffer[s].Type, buffer[s].Count);

            // Something drains the storage while the confirmation sits open (auto-sell, a runner,
            // or a first stacked dialog). The quote is now stale.
            storage.CopyDisplaySlots(buildingId, buffer);
            for (int s = 0; s < buffer.Length; s++)
                if (!buffer[s].IsEmpty)
                    storage.TakeFromSlot(buildingId, s, buffer[s].Count);

            int realized = SellAvailable(storage, buildingId, buffer);
            Assert.IsTrue(quoted > 0, "Sanity: the pre-dialog quote should have been non-zero");
            Assert.AreEqual(0, realized,
                "Payout must follow what was actually taken, not the stale quote of " + quoted);
        }

        #endregion

        #region Second Chance vault claim ordering

        [TestMethod]
        public void VaultCrystal_CanOnlyBeClaimedOnce()
        {
            var vault = new SecondChanceMerchantVault();
            var crystal = new RolePlayingFramework.Heroes.HeroCrystal(
                "TestCrystal", new RolePlayingFramework.Jobs.Primary.Knight(), 5,
                new RolePlayingFramework.Stats.StatBlock(10, 10, 10, 10));
            vault.AddCrystal(crystal);

            Assert.IsTrue(vault.RemoveCrystal(crystal), "The first purchase claims the crystal");
            Assert.IsFalse(vault.RemoveCrystal(crystal),
                "A second stacked purchase must fail to claim — granting anyway duplicates the crystal");
            Assert.AreEqual(0, vault.CrystalCount);
        }

        [TestMethod]
        public void VaultItemStack_CanOnlyBeClaimedUpToItsQuantity()
        {
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(new Gear("VaultSword", ItemKind.WeaponSword, ItemRarity.Normal, "desc", 100,
                                   new RolePlayingFramework.Stats.StatBlock(0, 0, 0, 0), atk: 5));

            var stack = vault.Stacks[0];
            int quantity = stack.Quantity;

            for (int i = 0; i < quantity; i++)
                Assert.IsTrue(vault.RemoveQuantity(stack, 1), $"Claim {i + 1} of {quantity} should succeed");

            Assert.IsFalse(vault.RemoveQuantity(stack, 1),
                "Claiming past the stock must fail — granting anyway duplicates vault stock");
        }

        #endregion
    }
}
