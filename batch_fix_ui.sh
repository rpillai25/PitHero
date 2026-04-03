#!/bin/bash

# List of files to fix (those that have Core.Services.GetService<TextService>() in constructors or init methods)
FILES=(
    "PitHero/UI/HeroCrystalTab.cs"
    "PitHero/UI/HeroCreationUI.cs"
    "PitHero/UI/SaveLoadUI.cs"
    "PitHero/UI/ItemCard.cs"
    "PitHero/UI/MonsterUI.cs"
    "PitHero/UI/MercenaryHireDialog.cs"
    "PitHero/UI/MercenariesTab.cs"
    "PitHero/UI/InventoryContextMenu.cs"
    "PitHero/UI/StencilLibraryPanel.cs"
    "PitHero/UI/GraphicalHUD.cs"
    "PitHero/UI/TitleMenuUI.cs"
    "PitHero/UI/SettingsUI.cs"
    "PitHero/UI/EquipPreviewTooltip.cs"
)

for file in "${FILES[@]}"; do
    echo "Processing $file..."
    # Replace _textService.DisplayText with GetText
    sed -i 's/_textService\.DisplayText(/GetText(/g' "$file"
done

echo "Done replacing DisplayText calls"
