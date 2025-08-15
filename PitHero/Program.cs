using System;

namespace PitHero
{
    class Program
    {
        public static void Main(string[] args)
        {
            // Check if we're in test mode
            if (args.Length > 0 && args[0].ToLower() == "test")
            {
                TestRunner.RunTests();
                return;
            }
            
            // Regular game mode
            try
            {
                using (Game1 game = new Game1())
                {
                    game.Run();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Game failed to start: {ex.Message}");
                Console.WriteLine("This is expected in headless environments without graphics support.");
                Console.WriteLine("Run with 'test' argument to test the ECS structure instead.");
            }
        }
    }
}
