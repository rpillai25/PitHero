using System;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Console application to run virtual game simulation
    /// This allows running the simulation outside of tests for verification
    /// </summary>
    public class VirtualGameConsole
    {
        public static void RunSimulation(string[] args)
        {
            Console.WriteLine("PitHero Virtual Game Logic Simulation");
            Console.WriteLine("=====================================");
            Console.WriteLine();

            if (args.Length > 0 && args[0] == "--help")
            {
                ShowHelp();
                return;
            }

            try
            {
                Console.WriteLine("Starting complete GOAP workflow simulation...");
                Console.WriteLine();

                var simulation = new VirtualGameSimulation();
                simulation.RunCompleteSimulation();

                Console.WriteLine();
                Console.WriteLine("Simulation completed successfully!");
                Console.WriteLine("Press any key to exit...");

                if (args.Length == 0) // Only wait for key if not running in automated mode
                {
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Simulation failed with exception:");
                Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Stack trace:");
                Console.WriteLine(ex.StackTrace);

                if (args.Length == 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                }

                Environment.Exit(1);
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Usage: VirtualGameConsole [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --help    Show this help message");
            Console.WriteLine();
            Console.WriteLine("This console application runs a complete simulation of the PitHero GOAP workflow:");
            Console.WriteLine("1. Generate pit at level 40");
            Console.WriteLine("2. Hero executes MoveToPitAction");
            Console.WriteLine("3. Hero executes JumpIntoPitAction");
            Console.WriteLine("4. Hero explores entire pit via WanderPitAction");
            Console.WriteLine("5. Hero executes complete wizard orb workflow:");
            Console.WriteLine("   - MoveToWizardOrbAction");
            Console.WriteLine("   - ActivateWizardOrbAction");
            Console.WriteLine("   - MovingToInsidePitEdgeAction");
            Console.WriteLine("   - JumpOutOfPitAction");
            Console.WriteLine("   - MoveToPitGenPointAction");
            Console.WriteLine("6. Pit regenerates at higher level");
            Console.WriteLine();
            Console.WriteLine("The simulation provides detailed logging of all actions and state changes,");
            Console.WriteLine("allowing verification of the complete GOAP workflow without graphics.");
        }
    }
}