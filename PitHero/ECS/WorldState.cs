using System;
using System.Collections.Generic;
using System.Linq;
using Nez;
using PitHero.Events;

namespace PitHero.ECS
{
    /// <summary>
    /// Contains all entities and global game state
    /// Wraps Nez Scene for entity management
    /// </summary>
    public class WorldState
    {
        private readonly Scene _scene;
        private readonly object _lock = new object();
        
        public WorldState()
        {
            _scene = new Scene();
            GameTime = 0.0;
        }
        
        /// <summary>
        /// The underlying Nez scene
        /// </summary>
        public Scene Scene => _scene;
        
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
                _scene.AddEntity(entity);
            }
        }
        
        /// <summary>
        /// Remove an entity from the world
        /// </summary>
        public bool RemoveEntity(uint entityId)
        {
            lock (_lock)
            {
                // Find entity by ID without using GetEntity to avoid double-locking
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    var entity = _scene.Entities[i];
                    if (entity != null && entity.Id == entityId)
                    {
                        entity.Destroy();
                        return true;
                    }
                }
                return false;
            }
        }
        
        /// <summary>
        /// Remove an entity from the world
        /// </summary>
        public bool RemoveEntity(Entity entity)
        {
            if (entity == null)
                return false;
                
            lock (_lock)
            {
                entity.Destroy();
                return true;
            }
        }
        
        /// <summary>
        /// Get an entity by ID
        /// </summary>
        public Entity GetEntity(uint entityId)
        {
            lock (_lock)
            {
                // Since Nez doesn't provide FindEntity by ID, we need to iterate
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    var entity = _scene.Entities[i];
                    if (entity != null && entity.Id == entityId)
                        return entity;
                }
                return null;
            }
        }
        
        /// <summary>
        /// Get all entities in the world
        /// </summary>
        public IReadOnlyList<Entity> GetAllEntities()
        {
            lock (_lock)
            {
                var entities = new List<Entity>();
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    var entity = _scene.Entities[i];
                    if (entity != null)
                        entities.Add(entity);
                }
                return entities.AsReadOnly();
            }
        }
        
        /// <summary>
        /// Get all entities that have a specific component
        /// </summary>
        public IReadOnlyList<Entity> GetEntitiesWithComponent<T>() where T : Component
        {
            lock (_lock)
            {
                var entities = new List<Entity>();
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    var entity = _scene.Entities[i];
                    if (entity != null && entity.GetComponent<T>() != null)
                        entities.Add(entity);
                }
                return entities.AsReadOnly();
            }
        }
        
        /// <summary>
        /// Get all entities with a specific name
        /// </summary>
        public IReadOnlyList<Entity> GetEntitiesByName(string name)
        {
            lock (_lock)
            {
                var entities = new List<Entity>();
                for (int i = 0; i < _scene.Entities.Count; i++)
                {
                    var entity = _scene.Entities[i];
                    if (entity != null && entity.Name == name)
                        entities.Add(entity);
                }
                return entities.AsReadOnly();
            }
        }
        
        /// <summary>
        /// Clear all entities from the world
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _scene.DestroyAllEntities();
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
                    return _scene.Entities.Count;
                }
            }
        }
    }
}