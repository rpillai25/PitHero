# Hero Crystal Tab UI Mockup

This is a text representation of the Hero Crystal tab UI as implemented.

```
┌─────────────────── Hero Menu Window ───────────────────┐
│ [Inventory] [Pit Priorities] [Hero Crystal] ← NEW TAB │
├────────────────────────────────────────────────────────┤
│                                                        │
│  ┌─ Crystal Information ─────────────────────────┐   │
│  │                                                 │   │
│  │  Job: Knight           Current JP: 500         │   │
│  │  Level: 10             Total JP: 1000          │   │
│  │  Job Level: 5          STR:20 AGI:15 VIT:18... │   │
│  │                                                 │   │
│  └─────────────────────────────────────────────────┘   │
│                                                        │
│  ┌─ Available Skills (Scrollable) ───────────────┐   │
│  │                                                 │   │
│  │   [🟥] [🟩] [🟦] [🟨]  ← Colored skill icons  │   │
│  │    ✓     ✓    🔒   🔒   (✓=learned, 🔒=locked)│   │
│  │                                                 │   │
│  │   [🟪] [🟦] [🟧] [🟩]                         │   │
│  │   🔒   🔒   🔒   🔒                            │   │
│  │                                                 │   │
│  │   [🟨] [🟥] [🟩] ...                          │   │
│  │   🔒   🔒   🔒                                 │   │
│  │                                                 │   │
│  └─────────────────────────────────────────────────┘   │
│                                                        │
└────────────────────────────────────────────────────────┘

When hovering over a skill:
┌─────────────────────┐
│ Heavy Strike        │  ← Green if learned, white if not
│ Active (AP: 5)      │  ← Skill type and AP cost
│ Learn Lv: 3 | Cost: 180 JP │  ← Requirements
│ (Insufficient JP)   │  ← Status in red if can't afford
└─────────────────────┘

When clicking an unlearned skill:
┌─────── Confirm Purchase ────────┐
│                                  │
│  Learn Heavy Strike for 180 JP? │
│                                  │
│  Active Skill                    │
│  Learn Level: 3                  │
│  AP Cost: 5                      │
│                                  │
│     [  Yes  ]    [  No  ]       │
│                                  │
└──────────────────────────────────┘
```

## Color Legend for Skill Icons

The skill icons use 10 different colors (deterministically assigned based on skill ID):

1. 🟥 Red (255, 100, 100)
2. 🟩 Green (100, 255, 100)
3. 🟦 Blue (100, 100, 255)
4. 🟨 Yellow (255, 255, 100)
5. 🟪 Magenta (255, 100, 255)
6. 🟦 Cyan (100, 255, 255)
7. 🟧 Orange (255, 150, 100)
8. 🟪 Purple (150, 100, 255)
9. 🟩 Light Green (100, 255, 150)
10. 💗 Pink (255, 100, 150)

## Visual States

### Learned Skills
- Full color icon (100% opacity)
- Green tooltip title
- "(Learned)" status in tooltip

### Unlearned Skills (Affordable)
- Grayed icon (50% opacity, grayscale tint)
- White tooltip title
- Shows JP cost in yellow
- Clickable for purchase

### Unlearned Skills (Can't Afford)
- Grayed icon (50% opacity, grayscale tint)
- White tooltip title
- "(Insufficient JP)" status in red
- Not purchasable

### Unlearned Skills (Level Too Low)
- Grayed icon (50% opacity, grayscale tint)
- White tooltip title
- "(Level too low)" status in red
- Not purchasable

## Interaction Flow

1. **Open Hero Menu** → Click Hero button in main UI
2. **Navigate to Crystal Tab** → Click "Hero Crystal" tab
3. **View Crystal Info** → See job, levels, JP, and stats at top
4. **Browse Skills** → Scroll through skill grid, hover for details
5. **Purchase Skill** → Click unlearned skill (if affordable and level met)
6. **Confirm** → Click "Yes" in dialog
7. **Update** → UI refreshes showing:
   - Reduced Current JP
   - Skill now shown as learned (full color, green tooltip)
   - Possibly increased Job Level
