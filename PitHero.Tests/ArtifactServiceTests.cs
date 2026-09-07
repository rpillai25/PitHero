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
        public void ShopAvailability_FollowsPrerequisiteChain()
        {
            var dir = NewTempDir();
            try
            {
                var service = new ArtifactService(dir, "system.bin");
                Assert.IsTrue(service.IsAvailableInShop(ArtifactType.SphereOfForesight));
                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.ChronosTimepiece), "The timepiece needs the sphere first");

                service.Grant(ArtifactType.SphereOfForesight);
                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.SphereOfForesight), "Owned artifacts leave the shop");
                Assert.IsTrue(service.IsAvailableInShop(ArtifactType.ChronosTimepiece));

                service.Grant(ArtifactType.ChronosTimepiece);
                Assert.IsFalse(service.IsAvailableInShop(ArtifactType.ChronosTimepiece));

                var owned = new List<ArtifactType>();
                service.GetOwnedInOrder(owned);
                CollectionAssert.AreEqual(new[] { ArtifactType.SphereOfForesight, ArtifactType.ChronosTimepiece }, owned);
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
