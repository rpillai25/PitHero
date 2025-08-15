using Microsoft.Xna.Framework;

namespace PitHero.Events
{
    /// <summary>
    /// Event fired when a hero is spawned
    /// </summary>
    public class HeroSpawnEvent : BaseEvent
    {
        public int HeroId { get; }
        public Vector2 Position { get; }
        public float Health { get; }
        
        public HeroSpawnEvent(double gameTime, int heroId, Vector2 position, float health = 100f)
            : base(gameTime)
        {
            HeroId = heroId;
            Position = position;
            Health = health;
        }
    }
    
    /// <summary>
    /// Event fired when a hero moves
    /// </summary>
    public class HeroMoveEvent : BaseEvent
    {
        public int HeroId { get; }
        public Vector2 FromPosition { get; }
        public Vector2 ToPosition { get; }
        public Vector2 Velocity { get; }
        
        public HeroMoveEvent(double gameTime, int heroId, Vector2 fromPosition, Vector2 toPosition, Vector2 velocity)
            : base(gameTime)
        {
            HeroId = heroId;
            FromPosition = fromPosition;
            ToPosition = toPosition;
            Velocity = velocity;
        }
    }
    
    /// <summary>
    /// Event fired when a hero dies
    /// </summary>
    public class HeroDeathEvent : BaseEvent
    {
        public int HeroId { get; }
        public Vector2 Position { get; }
        public string DeathCause { get; }
        
        public HeroDeathEvent(double gameTime, int heroId, Vector2 position, string deathCause)
            : base(gameTime)
        {
            HeroId = heroId;
            Position = position;
            DeathCause = deathCause;
        }
    }
    
    /// <summary>
    /// Event fired when a building is placed
    /// </summary>
    public class BuildingPlaceEvent : BaseEvent
    {
        public int BuildingId { get; }
        public Vector2 Position { get; }
        public string BuildingType { get; }
        
        public BuildingPlaceEvent(double gameTime, int buildingId, Vector2 position, string buildingType)
            : base(gameTime)
        {
            BuildingId = buildingId;
            Position = position;
            BuildingType = buildingType;
        }
    }
    
    /// <summary>
    /// Event fired when a pit event occurs
    /// </summary>
    public class PitEvent : BaseEvent
    {
        public int PitId { get; }
        public Vector2 Position { get; }
        public string EventType { get; }
        public float CrystalPower { get; }
        
        public PitEvent(double gameTime, int pitId, Vector2 position, string eventType, float crystalPower = 1f)
            : base(gameTime)
        {
            PitId = pitId;
            Position = position;
            EventType = eventType;
            CrystalPower = crystalPower;
        }
    }
}