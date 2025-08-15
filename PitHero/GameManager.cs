using System;
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
            var replaySystem = new ReplaySystem();
            
            _systems.Add(heroSystem);
            _systems.Add(pitSystem);
            _systems.Add(townSystem);
            _systems.Add(replaySystem);
            
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
            var spawnEvent = new HeroSpawnEvent(_worldState.GameTime, heroId, position, health);
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
            var buildingEvent = new BuildingPlaceEvent(_worldState.GameTime, buildingId, position, buildingType);
            ProcessEvent(buildingEvent);
        }
        
        /// <summary>
        /// Trigger a pit event
        /// </summary>
        public void TriggerPitEvent(uint pitId, Vector2 position, string eventType, float crystalPower = 1f)
        {
            var pitEvent = new PitEvent(_worldState.GameTime, pitId, position, eventType, crystalPower);
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
            _worldState.GameTime = 0.0;
            
            // Spawn a single hero
            SpawnHero(new Vector2(100, GameConfig.InternalWorldHeight / 2));
        }
        
        /// <summary>
        /// Start replay mode
        /// </summary>
        public void StartReplay(double startTime, double endTime)
        {
            var replaySystem = GetSystem<ReplaySystem>();
            replaySystem?.StartReplay(_eventLog, startTime, endTime);
        }
        
        /// <summary>
        /// Start full replay from the beginning
        /// </summary>
        public void StartFullReplay()
        {
            var replaySystem = GetSystem<ReplaySystem>();
            replaySystem?.StartFullReplay(_eventLog);
        }
        
        /// <summary>
        /// Stop replay mode
        /// </summary>
        public void StopReplay()
        {
            var replaySystem = GetSystem<ReplaySystem>();
            replaySystem?.StopReplay();
        }
        
        /// <summary>
        /// Get replay world state for rendering
        /// </summary>
        public WorldState GetReplayWorldState()
        {
            var replaySystem = GetSystem<ReplaySystem>();
            return replaySystem?.GetReplayWorldState();
        }
        
        /// <summary>
        /// Check if currently in replay mode
        /// </summary>
        public bool IsReplaying()
        {
            var replaySystem = GetSystem<ReplaySystem>();
            return replaySystem?.IsReplaying ?? false;
        }
    }
}