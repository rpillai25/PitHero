using Microsoft.Xna.Framework;
using System.Collections.Generic;
using PitHero.AI.Interfaces;
using PitHero.AI;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual GOAP context that provides all interfaces needed for GOAP actions
    /// without Nez dependencies
    /// </summary>
    public class VirtualGoapContext : IGoapContext
    {
        public IWorldState WorldState { get; }
        public IHeroController HeroController { get; }
        public IPathfinder Pathfinder { get; }
        public IPitLevelManager PitLevelManager { get; }
        public ITiledMapService TiledMapService { get; }
        public IPitGenerator PitGenerator { get; }
        public IPitWidthManager PitWidthManager { get; }

        private readonly VirtualWorldState _virtualWorld;
        private readonly VirtualHero _virtualHero;
        private readonly VirtualHeroController _virtualHeroController;
        private readonly VirtualTiledMapService _virtualTiledMapService;
        private readonly VirtualPitGenerator _virtualPitGenerator;
        private readonly VirtualPitWidthManager _virtualPitWidthManager;

        public VirtualGoapContext(VirtualWorldState worldState) : this(worldState, new VirtualHero(worldState))
        {
        }

        public VirtualGoapContext(VirtualWorldState worldState, VirtualHero hero)
        {
            _virtualWorld = worldState;
            _virtualHero = hero;
            WorldState = worldState;
            
            _virtualHeroController = new VirtualHeroController(worldState);
            HeroController = _virtualHeroController;
            
            // Copy state from VirtualHero to VirtualHeroController
            SyncHeroStates();
            
            Pathfinder = new VirtualPathfinder(worldState);
            PitLevelManager = new VirtualPitLevelManager(worldState);
            
            _virtualTiledMapService = new VirtualTiledMapService(worldState);
            TiledMapService = _virtualTiledMapService;
            
            _virtualPitWidthManager = new VirtualPitWidthManager(_virtualTiledMapService);
            PitWidthManager = _virtualPitWidthManager;
            
            _virtualPitGenerator = new VirtualPitGenerator(worldState, _virtualTiledMapService, _virtualPitWidthManager);
            PitGenerator = _virtualPitGenerator;
        }

        public Dictionary<string, bool> GetGoapWorldState()
        {
            var ws = new Dictionary<string, bool>();

            ws[GoapConstants.HeroInitialized] = true;
            ws[GoapConstants.PitInitialized] = HeroController.PitInitialized;
            
            if (HeroController.IsMoving && !HeroController.AdjacentToPitBoundaryFromOutside)
                ws[GoapConstants.MovingToPit] = true;

            if (HeroController.AdjacentToPitBoundaryFromOutside)
                ws[GoapConstants.AdjacentToPitBoundaryFromOutside] = true;
            if (HeroController.AdjacentToPitBoundaryFromInside)
                ws[GoapConstants.AdjacentToPitBoundaryFromInside] = true;
            if (HeroController.InsidePit)
                ws[GoapConstants.InsidePit] = true;

            // Wizard orb workflow states
            if (HeroController.ActivatedWizardOrb)
                ws[GoapConstants.ActivatedWizardOrb] = true;
            if (HeroController.MovingToInsidePitEdge)
                ws[GoapConstants.MovingToInsidePitEdge] = true;
            if (HeroController.ReadyToJumpOutOfPit)
                ws[GoapConstants.ReadyToJumpOutOfPit] = true;
            if (HeroController.MovingToPitGenPoint)
                ws[GoapConstants.MovingToPitGenPoint] = true;

            // Check exploration status
            if (WorldState.IsMapExplored)
                ws[GoapConstants.MapExplored] = true;

            // Check wizard orb status
            if (WorldState.IsWizardOrbFound)
                ws[GoapConstants.FoundWizardOrb] = true;

            // Check positional states
            if (CheckAtWizardOrb())
                ws[GoapConstants.AtWizardOrb] = true;

            if (HeroController.CurrentTilePosition.X == 34 && HeroController.CurrentTilePosition.Y == 6) // Pit gen point
                ws[GoapConstants.AtPitGenPoint] = true;

            if (!WorldState.PitBounds.Contains(HeroController.CurrentTilePosition))
                ws[GoapConstants.OutsidePit] = true;

            return ws;
        }

        public void UpdateHeroPositionStates()
        {
            if (_virtualHeroController != null)
            {
                // Update position states based on current location
                // This is automatically handled in VirtualHeroController when position changes
                SyncHeroStates();
            }
        }
        
        /// <summary>
        /// Sync state from VirtualHero to VirtualHeroController
        /// </summary>
        private void SyncHeroStates()
        {
            if (_virtualHero != null && _virtualHeroController != null)
            {
                _virtualHeroController.PitInitialized = _virtualHero.PitInitialized;
                _virtualHeroController.AdjacentToPitBoundaryFromOutside = _virtualHero.AdjacentToPitBoundaryFromOutside;
                _virtualHeroController.AdjacentToPitBoundaryFromInside = _virtualHero.AdjacentToPitBoundaryFromInside;
                _virtualHeroController.InsidePit = _virtualHero.InsidePit;
                _virtualHeroController.ActivatedWizardOrb = _virtualHero.ActivatedWizardOrb;
                _virtualHeroController.MovingToInsidePitEdge = _virtualHero.MovingToInsidePitEdge;
                _virtualHeroController.ReadyToJumpOutOfPit = _virtualHero.ReadyToJumpOutOfPit;
                _virtualHeroController.MovingToPitGenPoint = _virtualHero.MovingToPitGenPoint;
            }
        }
        
        /// <summary>
        /// Sync state from VirtualHeroController back to VirtualHero (after action execution)
        /// </summary>
        public void SyncBackToHero()
        {
            if (_virtualHero != null && _virtualHeroController != null)
            {
                _virtualHero.PitInitialized = _virtualHeroController.PitInitialized;
                _virtualHero.AdjacentToPitBoundaryFromOutside = _virtualHeroController.AdjacentToPitBoundaryFromOutside;
                _virtualHero.AdjacentToPitBoundaryFromInside = _virtualHeroController.AdjacentToPitBoundaryFromInside;
                _virtualHero.InsidePit = _virtualHeroController.InsidePit;
                _virtualHero.ActivatedWizardOrb = _virtualHeroController.ActivatedWizardOrb;
                _virtualHero.MovingToInsidePitEdge = _virtualHeroController.MovingToInsidePitEdge;
                _virtualHero.ReadyToJumpOutOfPit = _virtualHeroController.ReadyToJumpOutOfPit;
                _virtualHero.MovingToPitGenPoint = _virtualHeroController.MovingToPitGenPoint;
            }
        }

        public void LogDebug(string message)
        {
            System.Console.WriteLine($"[VirtualGoapContext] {message}");
        }

        public void LogWarning(string message)
        {
            System.Console.WriteLine($"[VirtualGoapContext] WARNING: {message}");
        }

        /// <summary>
        /// Check if hero is at wizard orb position
        /// </summary>
        private bool CheckAtWizardOrb()
        {
            var orbPos = WorldState.WizardOrbPosition;
            if (!orbPos.HasValue)
                return false;
            
            return HeroController.CurrentTilePosition == orbPos.Value;
        }

        /// <summary>
        /// Execute movement step for virtual simulation
        /// </summary>
        public bool ExecuteMovementStep()
        {
            return _virtualHero?.ExecuteMovementStep() ?? true;
        }

        /// <summary>
        /// Get visual representation of the current world state
        /// </summary>
        public string GetVisualRepresentation()
        {
            return _virtualWorld.GetVisualRepresentation();
        }
    }
}