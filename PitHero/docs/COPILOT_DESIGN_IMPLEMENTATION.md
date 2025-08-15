# PitHero Event-Driven ECS Design

## Overview
This document describes the event-driven Entity-Component-System (ECS) architecture implemented for PitHero.

## Core Architecture

### Event-Driven System
- **IEvent**: Base interface for all events with Id, Timestamp, and GameTime
- **BaseEvent**: Base implementation with automatic ID generation and timing
- **EventLog**: Thread-safe storage for all game events with querying capabilities
- **EventProcessor**: Coordinates event processing through all systems

### ECS Components
- **Entity**: Core game object with position, components, and ID
- **Component**: Base class for all entity components with lifecycle hooks
- **WorldState**: Central state manager containing all entities and game time

### Systems
- **ISystem**: Base interface for all systems with Update and ProcessEvent methods
- **HeroSystem**: Manages hero spawning, movement, AI, and interactions
- **PitSystem**: Handles pit spawning, crystal logic, and area effects
- **TownSystem**: Manages building placement and area-of-effect behaviors
- **ReplaySystem**: Processes events from EventLog for deterministic replay

### Game Components
- **HeroComponent**: Health, movement speed, velocity, and alive state
- **PitComponent**: Crystal power, effect radius, and active state
- **TownBuildingComponent**: Building type, effect radius, and construction state
- **RenderComponent**: Visual properties (color, size, bounds)

### Events
- **HeroSpawnEvent**: Hero creation with position and health
- **HeroMoveEvent**: Hero movement with from/to positions and velocity
- **HeroDeathEvent**: Hero death with position and cause
- **BuildingPlaceEvent**: Building placement with position and type
- **PitEvent**: Pit-related events (spawn, activate, crystal changes)

## Key Features

### Deterministic Replay
- All game actions are logged as events with precise timing
- Replay system can recreate any point in game history
- Events can be filtered by time range or type

### Event-Driven Updates
- Systems respond to events rather than polling state
- World state changes only through event processing
- Easy to add new behaviors by creating new events and handlers

### Thread-Safe Design
- EventLog and WorldState use locks for thread safety
- Events can be processed from multiple threads safely

### Configuration Management
- GameConfig class centralizes all constants and settings
- Easy to tune game balance and visual properties

## Usage Example

```csharp
// Initialize the game
var gameManager = new GameManager();
gameManager.StartNewGame();

// Spawn a hero
gameManager.SpawnHero(new Vector2(100, 300));

// Place a building
gameManager.PlaceBuilding(new Vector2(200, 200), "healing_tower");

// Update game systems
gameManager.Update(deltaTime);

// Start replay
gameManager.StartFullReplay();
```

## Testing
The implementation includes comprehensive tests that verify:
- Entity creation and component management
- Event logging and processing
- Game manager coordination
- System interactions

Run tests with: `dotnet run test`