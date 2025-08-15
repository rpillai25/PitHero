using System;
using System.Collections.Generic;
using System.Linq;
using PitHero.Events;

namespace PitHero.ECS
{
    /// <summary>
    /// Contains all entities and global game state
    /// </summary>
    public class WorldState
    {
        private readonly Dictionary<int, Entity> _entities;
        private readonly object _lock = new object();
        
        public WorldState()
        {
            _entities = new Dictionary<int, Entity>();
            GameTime = 0.0;
        }
        
        /// <summary>
        /// Current game time in seconds
        /// </summary>
        public double GameTime { get; set; }
        
        /// <summary>
        /// Add an entity to the world
        /// </summary>
        public void AddEntity(Entity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
                
            lock (_lock)
            {
                if (_entities.ContainsKey(entity.Id))
                    throw new InvalidOperationException($"Entity with ID {entity.Id} already exists in world");
                    
                _entities[entity.Id] = entity;
            }
        }
        
        /// <summary>
        /// Remove an entity from the world
        /// </summary>
        public bool RemoveEntity(int entityId)
        {
            lock (_lock)
            {
                return _entities.Remove(entityId);
            }
        }
        
        /// <summary>
        /// Remove an entity from the world
        /// </summary>
        public bool RemoveEntity(Entity entity)
        {
            if (entity == null)
                return false;
                
            return RemoveEntity(entity.Id);
        }
        
        /// <summary>
        /// Get an entity by ID
        /// </summary>
        public Entity GetEntity(int entityId)
        {
            lock (_lock)
            {
                return _entities.TryGetValue(entityId, out var entity) ? entity : null;
            }
        }
        
        /// <summary>
        /// Get all entities in the world
        /// </summary>
        public IReadOnlyList<Entity> GetAllEntities()
        {
            lock (_lock)
            {
                return _entities.Values.ToList().AsReadOnly();
            }
        }
        
        /// <summary>
        /// Get all entities that have a specific component
        /// </summary>
        public IReadOnlyList<Entity> GetEntitiesWithComponent<T>() where T : Component
        {
            lock (_lock)
            {
                return _entities.Values
                    .Where(entity => entity.HasComponent<T>())
                    .ToList()
                    .AsReadOnly();
            }
        }
        
        /// <summary>
        /// Get all entities with a specific name
        /// </summary>
        public IReadOnlyList<Entity> GetEntitiesByName(string name)
        {
            lock (_lock)
            {
                return _entities.Values
                    .Where(entity => entity.Name == name)
                    .ToList()
                    .AsReadOnly();
            }
        }
        
        /// <summary>
        /// Clear all entities from the world
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _entities.Clear();
            }
        }
        
        /// <summary>
        /// Get the number of entities in the world
        /// </summary>
        public int EntityCount
        {
            get
            {
                lock (_lock)
                {
                    return _entities.Count;
                }
            }
        }
    }
}