using System.Collections.Generic;
using Nez;
using PitHero.ECS.Components;
using PitHero.Services;
using RolePlayingFramework.Equipment;

namespace PitHero.AI
{
    /// <summary>
    /// Shared party auto-equip cascade: offers a piece of gear to the hero first, then to each hired
    /// mercenary in hire order, and recursively hands any displaced gear down the remaining
    /// mercenaries. Honors <see cref="HeroComponent.AutoEquipHero"/> and
    /// <see cref="HeroComponent.AutoEquipMercenaries"/>.
    /// Used by <see cref="OpenChestAction"/> for chest loot and by
    /// <see cref="AutoItemPurchaseService"/> for auto-purchased gear (issue #345).
    /// </summary>
    public static class PartyAutoEquipHelper
    {
        /// <summary>
        /// Attempts to auto-equip the item on the party. Non-gear items and a null hero are ignored.
        /// Returns true when the item was equipped by someone.
        /// </summary>
        public static bool TryAutoEquipForParty(HeroComponent heroComp, IItem item)
        {
            if (!(item is IGear gear))
                return false;

            if (heroComp?.LinkedHero == null)
                return false;

            var mercenaryManager = Core.Services?.GetService<MercenaryManager>();
            List<Entity> hiredMercenaries = heroComp.AutoEquipMercenaries ? mercenaryManager?.GetHiredMercenaries() : null;

            if (heroComp.AutoEquipHero)
            {
                if (GearAutoEquipService.TryAutoEquipOnHero(heroComp.LinkedHero, heroComp.Bag, gear, out IGear heroDisplaced))
                {
                    Debug.Log($"[PartyAutoEquip] Auto-equipped {gear.Name} on hero");
                    EmitAutoEquipEvent(heroComp.LinkedHero.Name, gear);
                    if (heroDisplaced != null && hiredMercenaries != null)
                        TryHandMeDownToMercs(heroComp, hiredMercenaries, heroDisplaced, 0);
                    return true;
                }
            }

            if (hiredMercenaries == null)
                return false;

            for (int i = 0; i < hiredMercenaries.Count; i++)
            {
                var mercComp = hiredMercenaries[i].GetComponent<MercenaryComponent>();
                if (mercComp?.LinkedMercenary == null) continue;

                if (GearAutoEquipService.TryAutoEquipOnMercenary(mercComp.LinkedMercenary, heroComp.Bag, gear, out IGear mercDisplaced))
                {
                    Debug.Log($"[PartyAutoEquip] Auto-equipped {gear.Name} on mercenary {mercComp.LinkedMercenary.Name}");
                    EmitAutoEquipEvent(mercComp.LinkedMercenary.Name, gear);
                    if (mercDisplaced != null)
                        TryHandMeDownToMercs(heroComp, hiredMercenaries, mercDisplaced, i + 1);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Offers a displaced piece of gear to mercenaries starting at the given index.
        /// If a mercenary equips it and displaces their own gear, that gear is recursively offered to subsequent mercenaries.
        /// </summary>
        private static void TryHandMeDownToMercs(HeroComponent heroComp, List<Entity> hiredMercenaries, IGear displacedGear, int startIndex)
        {
            for (int i = startIndex; i < hiredMercenaries.Count; i++)
            {
                var mercComp = hiredMercenaries[i].GetComponent<MercenaryComponent>();
                if (mercComp?.LinkedMercenary == null) continue;

                if (GearAutoEquipService.TryAutoEquipOnMercenary(mercComp.LinkedMercenary, heroComp.Bag, displacedGear, out IGear chainDisplaced))
                {
                    Debug.Log($"[PartyAutoEquip] Hand-me-down: {displacedGear.Name} auto-equipped on {mercComp.LinkedMercenary.Name}");
                    EmitAutoEquipEvent(mercComp.LinkedMercenary.Name, displacedGear);
                    if (chainDisplaced != null)
                        TryHandMeDownToMercs(heroComp, hiredMercenaries, chainDisplaced, i + 1);
                    return;
                }
            }
        }

        /// <summary>Emits a console event for a successful auto-equip.</summary>
        private static void EmitAutoEquipEvent(string characterName, IGear gear)
        {
            Core.Services?.GetService<GameEventService>()?.EmitLocalized(UITextKey.ConsoleAutoEquip,
                (characterName, GameConfig.ConsoleColorHeroName),
                (gear.Name, RarityUtils.GetRarityColor(gear.Rarity)));
        }
    }
}
