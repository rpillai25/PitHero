using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez.Persistence.Binary;
using PitHero.Artifacts;
using PitHero.Services;
using System;
using System.Collections.Generic;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>System-level artifacts: ownership rules and the system save they persist in.</summary>
    [TestClass]
    public class ArtifactServiceTests
    {
        private static string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pithero_artifacts_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [TestMethod]
        public void Grant_PersistsToSystemSave_AndIsIdempotent()
        {
            var dir = NewTempDir();
            try
            {
                var first = new ArtifactService(dir, "system.bin");
                Assert.IsFalse(first.Owns(ArtifactType.SphereOfForesight));
                Assert.AreEqual(0, first.Version);

                Assert.IsTrue(first.Grant(ArtifactType.SphereOfForesight));
                Assert.IsFalse(first.Grant(ArtifactType.SphereOfForesight), "Granting an owned artifact is a no-op");
                Assert.AreEqual(1, first.Version);
                Assert.IsTrue(File.Exists(Path.Combine(dir, "system.bin")), "Grant writes the system save immediately");
                first.Detach();

                var second = new ArtifactService(dir, "system.bin");
                Assert.IsTrue(second.Owns(ArtifactType.SphereOfForesight), "Ownership survives a restart");
                Assert.IsFalse(second.Owns(ArtifactType.ChronosTimepiece));
                Assert.AreEqual(1, second.OwnedCount);
                second.Detach();
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void LocalArtifact_GrantGoesToSessionStore_NotSystemSave()
        {
            var dir = NewTempDir();
            try
            {
                var service = new ArtifactService(dir, "system.bin");
                var session = new GameStateService();
                service.AttachLocalStore(session);
                int versionBefore = service.Version;

                Assert.IsTrue(service.Grant(ArtifactType.FastGrowFertilizer));
                Assert.IsFalse(service.Grant(ArtifactType.FastGrowFertilizer), "Idempotent");
                Assert.IsTrue(service.Owns(ArtifactType.FastGrowFertilizer));
                Assert.IsTrue(session.OwnsLocalArtifact(ArtifactType.FastGrowFertilizer), "Ownership lives on the session state");
                Assert.AreEqual(versionBefore + 1, service.Version, "A local grant refreshes version-cached UI");
                Assert.IsFalse(File.Exists(Path.Combine(dir, "system.bin")), "A Local grant never touches the system save");
                Assert.AreEqual(1, service.OwnedCount);

                var owned = new List<ArtifactType>();
                service.GetOwnedInOrder(owned);
                CollectionAssert.AreEqual(new List<ArtifactType> { ArtifactType.FastGrowFertilizer }, owned);
                service.Detach();

                var restarted = new ArtifactService(dir, "system.bin");
                Assert.IsFalse(restarted.Owns(ArtifactType.FastGrowFertilizer), "Without the session store nothing Local is owned");
                restarted.AttachLocalStore(session);
                Assert.IsTrue(restarted.Owns(ArtifactType.FastGrowFertilizer), "The session store carries it");
                restarted.Detach();
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void LocalArtifact_NotOwnedWithoutLocalStore()
        {
            var dir = NewTempDir();
            try
            {
                var service = new ArtifactService(dir, "system.bin");
                Assert.IsFalse(service.Grant(ArtifactType.HermesBoots), "No session store: nothing to grant into");
                Assert.IsFalse(service.Owns(ArtifactType.HermesBoots));
                Assert.IsFalse(File.Exists(Path.Combine(dir, "system.bin")));
                service.Detach();
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Version_BumpsWhenLocalStoreLoads()
        {
            var dir = NewTempDir();
            try
            {
                var service = new ArtifactService(dir, "system.bin");
                var session = new GameStateService();
                service.AttachLocalStore(session);
                int before = service.Version;

                session.SetLocalArtifacts(new List<int> { (int)ArtifactType.HermesBoots });
                Assert.IsTrue(service.Version > before, "A load must invalidate the Party tab / shop caches");
                Assert.IsTrue(service.Owns(ArtifactType.HermesBoots));

                int afterLoad = service.Version;
                session.ClearLocalArtifacts();
                Assert.IsTrue(service.Version > afterLoad, "So must a new hero");
                Assert.IsFalse(service.Owns(ArtifactType.HermesBoots));
                service.Detach();
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Lightning_SupersedesFastGrowInOwnedGrid()
        {
            var dir = NewTempDir();
            try
            {
                var service = new ArtifactService(dir, "system.bin");
                service.AttachLocalStore(new GameStateService());
                service.Grant(ArtifactType.HermesBoots);
                service.Grant(ArtifactType.FastGrowFertilizer);

                var owned = new List<ArtifactType>();
                service.GetOwnedInOrder(owned);
                CollectionAssert.AreEqual(new List<ArtifactType> { ArtifactType.HermesBoots, ArtifactType.FastGrowFertilizer }, owned);
                Assert.IsFalse(service.IsSuperseded(ArtifactType.FastGrowFertilizer));

                service.Grant(ArtifactType.LightningGrowFertilizer);
                owned.Clear();
                service.GetOwnedInOrder(owned);
                CollectionAssert.AreEqual(new List<ArtifactType> { ArtifactType.HermesBoots, ArtifactType.LightningGrowFertilizer }, owned,
                    "The lightning fertilizer replaces the fast one in the grid");
                Assert.IsTrue(service.IsSuperseded(ArtifactType.FastGrowFertilizer));
                Assert.IsTrue(service.Owns(ArtifactType.FastGrowFertilizer), "Still owned underneath — it stays the prerequisite");
                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.FastGrowFertilizer), "…and never returns to the shop");
                service.Detach();
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Catalog_ScopesAndPrices()
        {
            Assert.AreEqual(6, ArtifactCatalog.Count);
            Assert.AreEqual(ArtifactScope.Global, ArtifactCatalog.GetScope(ArtifactType.SphereOfForesight));
            Assert.AreEqual(ArtifactScope.Global, ArtifactCatalog.GetScope(ArtifactType.ChronosTimepiece));
            Assert.AreEqual(ArtifactScope.Global, ArtifactCatalog.GetScope(ArtifactType.KairosMetronome));
            Assert.IsTrue(ArtifactCatalog.IsLocal(ArtifactType.FastGrowFertilizer));
            Assert.IsTrue(ArtifactCatalog.IsLocal(ArtifactType.LightningGrowFertilizer));
            Assert.IsTrue(ArtifactCatalog.IsLocal(ArtifactType.HermesBoots));
            Assert.AreEqual(250000, ArtifactCatalog.GetPrice(ArtifactType.FastGrowFertilizer));
            Assert.AreEqual(1000000, ArtifactCatalog.GetPrice(ArtifactType.LightningGrowFertilizer));
            Assert.AreEqual(500000, ArtifactCatalog.GetPrice(ArtifactType.HermesBoots));
            Assert.AreEqual("FastGrowFertilizer", ArtifactCatalog.GetSpriteName(ArtifactType.FastGrowFertilizer));
            Assert.AreEqual("LightningGrowFertilizer", ArtifactCatalog.GetSpriteName(ArtifactType.LightningGrowFertilizer));
            Assert.AreEqual("HermesBoots", ArtifactCatalog.GetSpriteName(ArtifactType.HermesBoots));
        }

        [TestMethod]
        public void Lightning_RequiresFastGrow()
        {
            var dir = NewTempDir();
            try
            {
                var service = new ArtifactService(dir, "system.bin");
                service.AttachLocalStore(new GameStateService());
                Assert.IsTrue(service.IsAvailableInShop(ArtifactType.FastGrowFertilizer));
                Assert.IsTrue(service.IsAvailableInShop(ArtifactType.HermesBoots));
                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.LightningGrowFertilizer), "Lightning needs the fast fertilizer first");

                service.Grant(ArtifactType.FastGrowFertilizer);
                Assert.IsTrue(service.IsAvailableInShop(ArtifactType.LightningGrowFertilizer));
                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.FastGrowFertilizer), "Owned artifacts leave the shop");
                service.Detach();
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void ShopAvailability_FollowsPrerequisiteChain()
        {
            var dir = NewTempDir();
            try
            {
                var service = new ArtifactService(dir, "system.bin");
                // The sphere and the metronome are both offered up front; the timepiece is the capstone
                Assert.IsTrue(service.IsAvailableInShop(ArtifactType.SphereOfForesight));
                Assert.IsTrue(service.IsAvailableInShop(ArtifactType.KairosMetronome));
                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.ChronosTimepiece), "The timepiece needs both others first");

                service.Grant(ArtifactType.SphereOfForesight);
                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.SphereOfForesight), "Owned artifacts leave the shop");
                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.ChronosTimepiece), "One prerequisite is not enough");

                service.Grant(ArtifactType.KairosMetronome);
                Assert.IsTrue(service.IsAvailableInShop(ArtifactType.ChronosTimepiece));

                service.Grant(ArtifactType.ChronosTimepiece);
                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.ChronosTimepiece));

                var owned = new List<ArtifactType>();
                service.GetOwnedInOrder(owned);
                CollectionAssert.AreEqual(
                    new[] { ArtifactType.SphereOfForesight, ArtifactType.KairosMetronome, ArtifactType.ChronosTimepiece },
                    owned);
                service.Detach();
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void SystemSaveData_RoundTrip_KeepsUnknownArtifacts()
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
            {
                var original = new SystemSaveData();
                original.OwnedArtifacts.Add((int)ArtifactType.ChronosTimepiece);
                original.OwnedArtifacts.Add(7); // written by a newer build
                writer.Write(original);
            }

            var loaded = new SystemSaveData();
            using (var reader = new BinaryPersistableReader(new MemoryStream(ms.ToArray())))
                reader.ReadPersistableInto(loaded);

            Assert.AreEqual(SystemSaveData.CurrentVersion, loaded.FormatVersion);
            CollectionAssert.AreEqual(new[] { (int)ArtifactType.ChronosTimepiece, 7 }, loaded.OwnedArtifacts,
                "Unknown ordinals are kept so an older build never drops a purchase");
        }
    }
}
