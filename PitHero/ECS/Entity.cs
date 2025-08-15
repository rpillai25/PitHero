using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PitHero.ECS
{
    /// <summary>
    /// Base class for all components in the ECS system
    /// </summary>
    public abstract class Component
    {
        /// <summary>
        /// The entity this component belongs to
        /// </summary>
        public Entity Entity { get; internal set; }
        
        /// <summary>
        /// Whether this component is enabled
        /// </summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// Called when the component is added to an entity
        /// </summary>
        public virtual void OnAddedToEntity() { }
        
        /// <summary>
        /// Called when the component is removed from an entity
        /// </summary>
        public virtual void OnRemovedFromEntity() { }
    }
    
    /// <summary>
    /// Represents an entity in the ECS system
    /// </summary>
    public class Entity
    {
        private readonly Dictionary<Type, Component> _components;
        private static int _nextId = 1;
        
        public int Id { get; }
        public string Name { get; set; }
        public Vector2 Position { get; set; }
        public bool Enabled { get; set; } = true;
        
        public Entity(string name = null)
        {
            Id = _nextId++;
            Name = name ?? $"Entity_{Id}";
            _components = new Dictionary<Type, Component>();
        }
        
        /// <summary>
        /// Add a component to this entity
        /// </summary>
        public T AddComponent<T>(T component) where T : Component
        {
            var type = typeof(T);
            if (_components.ContainsKey(type))
            {
                throw new InvalidOperationException($"Entity {Name} already has a component of type {type.Name}");
            }
            
            component.Entity = this;
            _components[type] = component;
            component.OnAddedToEntity();
            
            return component;
        }
        
        /// <summary>
        /// Get a component of the specified type
        /// </summary>
        public T GetComponent<T>() where T : Component
        {
            var type = typeof(T);
            return _components.TryGetValue(type, out var component) ? (T)component : null;
        }
        
        /// <summary>
        /// Check if this entity has a component of the specified type
        /// </summary>
        public bool HasComponent<T>() where T : Component
        {
            return _components.ContainsKey(typeof(T));
        }
        
        /// <summary>
        /// Remove a component from this entity
        /// </summary>
        public bool RemoveComponent<T>() where T : Component
        {
            var type = typeof(T);
            if (_components.TryGetValue(type, out var component))
            {
                component.OnRemovedFromEntity();
                component.Entity = null;
                _components.Remove(type);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Get all components on this entity
        /// </summary>
        public IEnumerable<Component> GetAllComponents()
        {
            return _components.Values;
        }
    }
}