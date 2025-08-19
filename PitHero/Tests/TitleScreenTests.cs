using System;
using Microsoft.Xna.Framework;
using PitHero.ECS.Scenes;
using PitHero.UI;

namespace PitHero.Tests
{
    /// <summary>
    /// Simple test to validate scene creation and map path handling
    /// </summary>
    public static class TitleScreenTests
    {
        public static void RunTests()
        {
            Console.WriteLine("Running Title Screen Tests...");
            
            // Test 1: TitleScreenScene can be created
            try
            {
                var titleScene = new TitleScreenScene();
                Console.WriteLine("✓ TitleScreenScene creation: PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ TitleScreenScene creation: FAIL - {ex.Message}");
            }
            
            // Test 2: MainGameScene with default constructor
            try
            {
                var mainScene = new MainGameScene();
                Console.WriteLine("✓ MainGameScene default constructor: PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ MainGameScene default constructor: FAIL - {ex.Message}");
            }
            
            // Test 3: MainGameScene with map path
            try
            {
                var mainScene = new MainGameScene("Content/Tilemaps/PitHero.tmx");
                Console.WriteLine("✓ MainGameScene with map path: PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ MainGameScene with map path: FAIL - {ex.Message}");
            }
            
            // Test 4: MainGameScene with large map path
            try
            {
                var mainScene = new MainGameScene("Content/Tilemaps/PitHeroLarge.tmx");
                Console.WriteLine("✓ MainGameScene with large map path: PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ MainGameScene with large map path: FAIL - {ex.Message}");
            }
            
            // Test 5: TitleMenuUI can be created
            try
            {
                var titleUI = new TitleMenuUI();
                Console.WriteLine("✓ TitleMenuUI creation: PASS");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ TitleMenuUI creation: FAIL - {ex.Message}");
            }
            
            Console.WriteLine("All tests completed!");
        }
    }
}