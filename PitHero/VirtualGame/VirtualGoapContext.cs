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

        /// <summary>
        /// Access to the virtual hero for configuration properties like UncoverRadius
        /// </summary>
        public VirtualHero VirtualHero => _virtualHero;

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

            // Use the simplified 7-state GOAP model
            ws[GoapConstants.HeroInitialized] = HeroController.HeroInitialized;
            ws[GoapConstants.PitInitialized] = HeroController.PitInitialized;
            ws[GoapConstants.InsidePit] = HeroController.InsidePit;
            ws[GoapConstants.OutsidePit] = !HeroController.InsidePit;
            ws[GoapConstants.ExploredPit] = HeroController.ExploredPit;
            ws[GoapConstants.FoundWizardOrb] = HeroController.FoundWizardOrb;
            ws[GoapConstants.ActivatedWizardOrb] = HeroController.ActivatedWizardOrb;

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
        /// Sync state from VirtualHero to VirtualHeroController - simplified GOAP model
        /// </summary>
        private void SyncHeroStates()
        {
            if (_virtualHero != null && _virtualHeroController != null)
            {
                _virtualHeroController.HeroInitialized = _virtualHero.HeroInitialized;
                _virtualHeroController.PitInitialized = _virtualHero.PitInitialized;
                _virtualHeroController.InsidePit = _virtualHero.InsidePit;
                _virtualHeroController.ExploredPit = _virtualHero.ExploredPit;
                _virtualHeroController.FoundWizardOrb = _virtualHero.FoundWizardOrb;
                _virtualHeroController.ActivatedWizardOrb = _virtualHero.ActivatedWizardOrb;
            }
        }
        
        /// <summary>
        /// Sync state from VirtualHeroController back to VirtualHero (after action execution) - simplified GOAP model
        /// </summary>
        public void SyncBackToHero()
        {
            if (_virtualHero != null && _virtualHeroController != null)
            {
                _virtualHero.HeroInitialized = _virtualHeroController.HeroInitialized;
                _virtualHero.PitInitialized = _virtualHeroController.PitInitialized;
                _virtualHero.InsidePit = _virtualHeroController.InsidePit;
                _virtualHero.ExploredPit = _virtualHeroController.ExploredPit;
                _virtualHero.FoundWizardOrb = _virtualHeroController.FoundWizardOrb;
                _virtualHero.ActivatedWizardOrb = _virtualHeroController.ActivatedWizardOrb;
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