using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS;
using PitHero.Events;
using PitHero.Systems;

namespace PitHero
{
    /// <summary>
    /// Central manager for the event-driven game systems
    /// </summary>
    public class GameManager
    {
        private readonly EventLog _eventLog;
        private readonly WorldState _worldState;
        private readonly EventProcessor _eventProcessor;
        private readonly List<ISystem> _systems;
        
        public EventLog EventLog => _eventLog;
        public WorldState WorldState => _worldState;
        public EventProcessor EventProcessor => _eventProcessor;
        
        public GameManager()
        {
            _eventLog = new EventLog();
            _worldState = new WorldState();
            _eventProcessor = new EventProcessor(_eventLog, _worldState);
            _systems = new List<ISystem>();
            
            InitializeSystems();
        }
        
        /// <summary>
        /// Initialize all game systems
        /// </summary>
        private void InitializeSystems()
        {
            var heroSystem = new HeroSystem();
            var pitSystem = new PitSystem();
            var townSystem = new TownSystem();
            
            _systems.Add(heroSystem);
            _systems.Add(pitSystem);
            _systems.Add(townSystem);
            
            // Register systems with the event processor
            foreach (var system in _systems)
            {
                _eventProcessor.RegisterSystem(system);
            }
        }
        
        /// <summary>
        /// Update the game systems
        /// </summary>
        public void Update(float deltaTime)
        {
            _eventProcessor.Update(deltaTime);
        }
        
        /// <summary>
        /// Process a game event
        /// </summary>
        public void ProcessEvent(IEvent gameEvent)
        {
            _eventProcessor.ProcessEvent(gameEvent);
        }
        
        /// <summary>
        /// Spawn a hero at the specified position
        /// </summary>
        public void SpawnHero(Vector2 position, float health = 100f)
        {
            // Create a temporary entity to get its ID for the event
            var tempHero = new Entity("TempHero");
            var heroId = tempHero.Id;
            var spawnEvent = new HeroSpawnEvent(Time.TotalTime, heroId, position, health);
            ProcessEvent(spawnEvent);
        }
        
        /// <summary>
        /// Place a building at the specified position
        /// </summary>
        public void PlaceBuilding(Vector2 position, string buildingType)
        {
            // Create a temporary entity to get its ID for the event
            var tempBuilding = new Entity("TempBuilding");
            var buildingId = tempBuilding.Id;
            var buildingEvent = new BuildingPlaceEvent(Time.TotalTime, buildingId, position, buildingType);
            ProcessEvent(buildingEvent);
        }
        
        /// <summary>
        /// Trigger a pit event
        /// </summary>
        public void TriggerPitEvent(uint pitId, Vector2 position, string eventType, float crystalPower = 1f)
        {
            var pitEvent = new PitEvent(Time.TotalTime, pitId, position, eventType, crystalPower);
            ProcessEvent(pitEvent);
        }
        
        /// <summary>
        /// Get a specific system by type
        /// </summary>
        public T GetSystem<T>() where T : class, ISystem
        {
            foreach (var system in _systems)
            {
                if (system is T targetSystem)
                    return targetSystem;
            }
            return null;
        }
        
        /// <summary>
        /// Start a new game
        /// </summary>
        public void StartNewGame()
        {
            _eventLog.Clear();
            _worldState.Clear();
            // Note: Game time is managed by Nez.Time.TotalTime, no need to reset manually
            // _worldState.GameTime = 0.0; // Removed - use Nez.Time instead
            
            // Spawn a single hero at the center of the map (world coordinates)
            var centerPosition = new Vector2(10 * 64 + 32, 6 * 64 + 32); // Tile (10,6) center
            SpawnHero(centerPosition);
        }
    }
}