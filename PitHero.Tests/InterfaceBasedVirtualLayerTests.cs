using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.VirtualGame;
using Microsoft.Xna.Framework;
using System;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>
    /// Demonstration tests showing that virtual layer now uses actual PitGenerator and PitWidthManager
    /// This verifies that the virtual layer reflects real game behavior
    /// </summary>
    [TestClass]
    public class InterfaceBasedVirtualLayerTests
    {
        [TestMethod]
        public void VirtualLayer_UsesActualPitWidthManagerLogic_ShouldExpandPitCorrectly()
        {
            // Capture console output for validation
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            
            try
            {
                Console.WriteLine("=== DEMONSTRATION: Virtual Layer Using Actual PitWidthManager ===");
                Console.WriteLine();
                
                // STEP 1: Initialize virtual world
                var virtualWorld = new VirtualWorldState();
                var context = new VirtualGoapContext(virtualWorld);
                
                // STEP 2: Initialize the virtual PitWidthManager (which uses actual PitWidthManager logic)
                context.PitWidthManager.Initialize();
                
                Console.WriteLine($"Initial pit bounds: {context.PitWidthManager.CalculateCurrentPitWorldBounds()}");
                Console.WriteLine($"Initial pit right edge: {context.PitWidthManager.CurrentPitRightEdge}");
                Console.WriteLine($"Initial pit center X: {context.PitWidthManager.CurrentPitCenterTileX}");
                Console.WriteLine();
                
                // STEP 3: Set pit level to 20 (should trigger width expansion using actual PitWidthManager logic)
                Console.WriteLine("Setting pit level to 20 (should expand pit width by 2 tiles)...");
                context.PitWidthManager.SetPitLevel(20);
                
                Console.WriteLine($"After level 20 - pit right edge: {context.PitWidthManager.CurrentPitRightEdge}");
                Console.WriteLine($"After level 20 - pit center X: {context.PitWidthManager.CurrentPitCenterTileX}");
                Console.WriteLine($"After level 20 - pit bounds: {context.PitWidthManager.CalculateCurrentPitWorldBounds()}");
                Console.WriteLine();
                
                // STEP 4: Test pit candidates (should use dynamic pit width)
                var candidates = context.PitWidthManager.GetCurrentPitCandidateTargets();
                Console.WriteLine($"Pit candidate targets at level 20:");
                foreach (var candidate in candidates)
                {
                    Console.WriteLine($"  - ({candidate.X},{candidate.Y})");
                }
                Console.WriteLine();
                
                // STEP 5: Use actual PitGenerator to regenerate pit content
                Console.WriteLine("Using actual PitGenerator logic to regenerate pit content...");
                context.PitGenerator.RegenerateForLevel(20);
                
                Console.WriteLine();
                Console.WriteLine("Visual representation after using real PitWidthManager and PitGenerator:");
                Console.WriteLine(context.GetVisualRepresentation());
                
                // Verify that the virtual layer is using real logic
                Assert.IsTrue(candidates.Length > 0, "Pit candidates should be generated");
                Assert.IsTrue(candidates[0].X > GameConfig.PitRectX + GameConfig.PitRectWidth, "Pit should be expanded beyond default width");
                
                var output = stringWriter.ToString();
                Assert.IsTrue(output.Contains("[VirtualPitWidthManager]"), "Should use VirtualPitWidthManager with real logic");
                Assert.IsTrue(output.Contains("[VirtualPitGenerator]"), "Should use VirtualPitGenerator with real logic");
                Assert.IsTrue(output.Contains("extending pit by"), "Should show actual pit expansion logic");
                
                Console.WriteLine();
                Console.WriteLine("SUCCESS: Virtual layer successfully uses actual PitWidthManager and PitGenerator logic!");
            }
            finally
            {
                Console.SetOut(originalOut);
                
                // Also output to test runner for verification
                var output = stringWriter.ToString();
                System.Diagnostics.Debug.WriteLine(output);
            }
        }

        [TestMethod]
        public void VirtualLayer_PitGeneratorWithDynamicWidth_ShouldUseActualEntityGeneration()
        {
            // Capture console output for validation
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            
            try
            {
                Console.WriteLine("=== DEMONSTRATION: Virtual PitGenerator Using Actual Generation Logic ===");
                Console.WriteLine();
                
                // STEP 1: Initialize virtual world with expanded pit
                var virtualWorld = new VirtualWorldState();
                var context = new VirtualGoapContext(virtualWorld);
                
                context.PitWidthManager.Initialize();
                context.PitWidthManager.SetPitLevel(40); // Level 40 should create significant expansion
                
                Console.WriteLine($"Level 40 pit right edge: {context.PitWidthManager.CurrentPitRightEdge}");
                Console.WriteLine($"Level 40 pit width: {context.PitWidthManager.CurrentPitRectWidthTiles}");
                Console.WriteLine();
                
                // STEP 2: Use actual PitGenerator logic for entity generation
                Console.WriteLine("Generating pit content using actual PitGenerator logic...");
                context.PitGenerator.RegenerateForLevel(40);
                
                // STEP 3: Verify entities were generated in the expanded area
                var entities = virtualWorld.GetEntityPositions();
                Console.WriteLine($"Generated entities:");
                foreach (var entityType in entities.Keys)
                {
                    Console.WriteLine($"  {entityType}: {entities[entityType].Count} entities");
                    foreach (var pos in entities[entityType])
                    {
                        Console.WriteLine($"    - ({pos.X},{pos.Y})");
                    }
                }
                Console.WriteLine();
                
                Console.WriteLine("Visual representation with dynamically generated content:");
                Console.WriteLine(context.GetVisualRepresentation());
                
                // Verify actual PitGenerator logic was used
                Assert.IsTrue(entities.ContainsKey("WizardOrb"), "Should generate wizard orb");
                Assert.AreEqual(1, entities["WizardOrb"].Count, "Should generate exactly 1 wizard orb");
                
                var output = stringWriter.ToString();
                Assert.IsTrue(output.Contains("calculated amounts"), "Should show actual PitGenerator calculations");
                Assert.IsTrue(output.Contains("Max Monsters:"), "Should show monster calculations");
                Assert.IsTrue(output.Contains("Max Chests:"), "Should show chest calculations");
                Assert.IsTrue(output.Contains("Max Obstacles:"), "Should show obstacle calculations");
                
                Console.WriteLine();
                Console.WriteLine("SUCCESS: Virtual PitGenerator uses actual entity generation logic!");
            }
            finally
            {
                Console.SetOut(originalOut);
                
                // Also output to test runner for verification
                var output = stringWriter.ToString();
                System.Diagnostics.Debug.WriteLine(output);
            }
        }
    }
}