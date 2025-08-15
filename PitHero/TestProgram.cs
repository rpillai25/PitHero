using System;

namespace PitHero
{
    class TestRunner
    {
        public static void RunTests()
        {
            Console.WriteLine("PitHero ECS Test Console");
            Console.WriteLine("========================");
            
            try
            {
                Tests.ECSTest.RunTests();
                
                Console.WriteLine("\n✓ All tests completed successfully!");
                Console.WriteLine("\nCore ECS structure is working correctly:");
                Console.WriteLine("- Event-driven architecture implemented");
                Console.WriteLine("- WorldState manages entities");
                Console.WriteLine("- EventLog tracks all events");
                Console.WriteLine("- Systems process events deterministically");
                Console.WriteLine("- GameManager coordinates everything");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}