# Virtual Game Layer — AI Testing

GOAP actions support dual execution: live game (`Execute(HeroComponent)`) and virtual testing (`Execute(IGoapContext)`).

## IGoapContext Interface

```csharp
public interface IGoapContext
{
    IWorldState        WorldState        { get; }
    IHeroController    HeroController    { get; }
    IPathfinder        Pathfinder        { get; }
    IPitLevelManager   PitLevelManager   { get; }
    ITiledMapService   TiledMapService   { get; }
    IPitGenerator      PitGenerator      { get; }
    IPitWidthManager   PitWidthManager   { get; }

    Dictionary<string, bool> GetGoapWorldState();
    void UpdateHeroPositionStates();
    void LogDebug(string message);
    void LogWarning(string message);
}
```

## VirtualGoapContext

Used by `VirtualHeroStateMachine` to run GOAP plans without Nez rendering:

```csharp
var context = new VirtualGoapContext(virtualWorldState, virtualHero);
action.Execute(context);     // Virtual execution
context.SyncBackToHero();    // Sync state changes back
```

## Adding Virtual Support to a New Action

```csharp
public override bool Execute(IGoapContext context)
{
    context.LogDebug("[MyAction] Virtual execution");
    context.HeroController.SomeFlag = true;
    return true;
}
```

## GOAP Interfaces

| File | Purpose |
|---|---|
| `IGoapContext.cs` | Unified GOAP execution interface |
| `IWorldState.cs` | World state provider |
| `IHeroController.cs` | Hero control abstraction |
| `IPathfinder.cs` | A* pathfinding abstraction |

## When Coverage is Missing

If a feature isn't testable on the virtual layer, the `virtual-game-layer` skill covers gap analysis and adding missing virtual components.
