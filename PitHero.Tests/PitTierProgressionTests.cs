using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.VirtualGame;

namespace PitHero.Tests
{
    /// <summary>
    /// Unit tests for pit-tier progression mechanics: tier-2 treasure scaling via
    /// <see cref="VirtualPitGenerator"/> and related tier-decomposition helpers.
    /// These are plain unit tests — not BalanceTraversal — and run in the default test suite.
    /// </summary>
    [TestClass]
    public class PitTierProgressionTests
    {
        /// <summary>
        /// Verifies that regenerating tier-2 depths (36–50) via <see cref="VirtualPitGenerator"/>
        /// produces at least one treasure whose item name ends with "+2" — the tier-2 suffix
        /// that <see cref="RolePlayingFramework.Equipment.Gear"/> appends in headless mode.
        ///
        /// <para>
        /// The generator applies <see cref="RolePlayingFramework.Equipment.Gear.CreateTierScaledCopy"/>
        /// to all Gear drops at tier ≥ 2.  Cave displayed levels 11+ (depths 36–50 in tier 2)
        /// produce loot level 2 (Uncommon) and level 3 (Rare) drops, which generate Gear items.
        /// Across several depth samples we must see at least one tier-scaled Gear.
        /// </para>
        ///
        /// <para>
        /// In headless mode (no TextService) <c>Gear.Name</c> returns <c>nameKey + "+2"</c>,
        /// and <see cref="VirtualWorldState.LastGeneratedEquipmentTypes"/> stores this name
        /// string, so checking for the "+2" suffix is equivalent to checking <c>Gear.Tier == 2</c>.
        /// </para>
        /// </summary>
        [TestMethod]
        public void Tier2_CaveChest_ContainsTierScaledGear()
        {
            // Sample depths in tier-2 range where loot level ≥ 2 gear can appear
            // (displayed levels 11+ have ≥35 % chance for Uncommon+ loot that becomes Gear).
            // Depth 36 = displayed 11, depth 40 = displayed 15 (boss), depth 45 = displayed 20 (boss),
            // depth 50 = displayed 25 (boss — highest loot table, 20 % level-3 chance).
            int[] tier2Depths = { 36, 40, 45, 50 };

            bool foundTier2Gear = false;

            for (int d = 0; d < tier2Depths.Length; d++)
            {
                int depth = tier2Depths[d];

                var world = new VirtualWorldState();
                world.RegeneratePit(depth);

                var tiledMapService = new VirtualTiledMapService(world);
                var pitWidthManager = new VirtualPitWidthManager(tiledMapService);
                var generator = new VirtualPitGenerator(world, tiledMapService, pitWidthManager);
                generator.RegenerateForLevel(depth);

                // LastGeneratedEquipmentTypes stores item.Name for each chest.
                // In headless mode a tier-2 Gear has name "Inv_XXX_Name+2".
                var equipmentTypes = world.LastGeneratedEquipmentTypes;
                for (int e = 0; e < equipmentTypes.Count; e++)
                {
                    if (equipmentTypes[e].EndsWith("+2"))
                    {
                        foundTier2Gear = true;
                    }
                }

                // Verify the tier is correct for these depths.
                Assert.AreEqual(2, BiomeProgressionConfig.GetTierForDepth(depth),
                    $"Depth {depth} must be tier 2");
            }

            Assert.IsTrue(foundTier2Gear,
                "Across tier-2 depth samples {36,40,45,50} at least one Gear item with '+2' name " +
                "(= Tier == 2 in headless mode) must appear in chest loot");
        }
    }
}
