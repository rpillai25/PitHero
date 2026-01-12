# Gold Yield System - Quick Reference

## How to Use the Gold System

### 1. Accessing Global Funds

```csharp
using PitHero.Services;
using Nez;

// Get the game state service
var gameState = Core.Services.GetService<GameStateService>();

// Check current funds
int currentGold = gameState.Funds;

// Add gold
gameState.Funds += 100;

// Spend gold
gameState.Funds -= 50;
```

### 2. Monster Gold Yields (Level-Based)

All monsters automatically calculate their GoldYield based on level:

```csharp
var slime = new Slime();      // Level 1 = 8 gold
var goblin = new Goblin();    // Level 3 = 14 gold
var skeleton = new Skeleton(); // Level 6 = 23 gold
var pitLord = new PitLord();   // Level 10 = 35 gold

// Access the gold yield
int goldFromSlime = slime.GoldYield;
```

### 3. Gold is Automatically Awarded on Monster Defeat

No manual code needed - the battle system handles it:

```csharp
// In AttackMonsterAction.cs, when a monster dies:
// 1. Hero gains XP, JP, SP
// 2. Global Funds automatically increased by monster.GoldYield
// 3. Debug log shows gold earned and new total
```

### 4. Creating New Monsters with GoldYield

When creating a new monster, follow this pattern:

```csharp
public sealed class NewMonster : IEnemy
{
    private int _hp;
    
    // Required properties (including GoldYield)
    public string Name => "NewMonster";
    public int Level { get; }
    public StatBlock Stats { get; }
    public DamageKind AttackKind => DamageKind.Physical;
    public ElementType Element => ElementType.Fire;
    public ElementalProperties ElementalProps { get; }
    public int MaxHP { get; }
    public int CurrentHP => _hp;
    public int ExperienceYield { get; }
    public int JPYield { get; }
    public int SPYield { get; }
    public int GoldYield { get; }  // <-- Don't forget this!
    
    public NewMonster(int level = 25)
    {
        Level = level;
        var archetype = BalanceConfig.MonsterArchetype.Balanced;
        
        // ... calculate stats ...
        
        // Calculate all yields using BalanceConfig
        ExperienceYield = BalanceConfig.CalculateMonsterExperience(Level);
        JPYield = BalanceConfig.CalculateMonsterJPYield(Level);
        SPYield = BalanceConfig.CalculateMonsterSPYield(Level);
        GoldYield = BalanceConfig.CalculateMonsterGoldYield(Level);  // <-- Add this line!
    }
    
    public bool TakeDamage(int amount) { /* ... */ }
}
```

### 5. Gold Formula Reference

```
Formula: 5 + (level × 3)
```

| Level | Gold | Context |
|-------|------|---------|
| 1 | 8 | Starter enemies (Slime, Bat, Rat) |
| 3 | 14 | Early enemies (Goblin, Spider, Snake) |
| 6 | 23 | Mid enemies (Skeleton, Orc, Wraith) |
| 10 | 35 | Boss (Pit Lord) |
| 25 | 80 | Mid-game |
| 50 | 155 | Late-game |
| 75 | 230 | Advanced |
| 99 | 302 | Max level |

### 6. Future Shop Integration Example

```csharp
// Example shop purchase code (not yet implemented)
public bool TryPurchaseItem(IItem item, int cost)
{
    var gameState = Core.Services.GetService<GameStateService>();
    
    if (gameState.Funds >= cost)
    {
        gameState.Funds -= cost;
        hero.Inventory.AddItem(item);
        Debug.Log($"Purchased {item.Name} for {cost} gold. Remaining: {gameState.Funds}");
        return true;
    }
    else
    {
        Debug.Log($"Not enough gold. Need {cost}, have {gameState.Funds}");
        return false;
    }
}
```

### 7. Display Gold in UI (Example)

```csharp
// Example UI display code (not yet implemented)
public void UpdateGoldDisplay()
{
    var gameState = Core.Services.GetService<GameStateService>();
    goldText.SetText($"Gold: {gameState.Funds}");
}
```

## Important Notes

1. **Global Persistence**: Funds persist across hero deaths/changes
2. **Service Access**: Always use `Core.Services.GetService<GameStateService>()`
3. **Automatic**: No manual code needed to award gold on monster defeat
4. **Thread-Safe**: Service-based access is thread-safe
5. **Save/Load**: Remember to persist Funds when implementing save system

## Testing

Run gold system tests:
```bash
dotnet test PitHero.Tests/ --filter "FullyQualifiedName~GoldYieldSystemTests | FullyQualifiedName~GameStateFundsTests"
```

All 15 tests should pass.
