using System;
using System.Collections.Generic;
using Nez;
using PitHero.ECS.Components;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Mercenaries;

namespace PitHero.AI
{
    /// <summary>
    /// The one place item automation runs before the party drops into the pit (issue #411), invoked
    /// by <see cref="JumpIntoPitAction"/> once the landing tile is validated. Order matters and is the
    /// anti-churn argument: (1) an auto-equip sweep over the bag so every upgrade is worn before
    /// anything is judged excess; (2) the auto-sell sweep down to the Inventory Sell % threshold,
    /// weakest first, with gear that still upgrades someone as the very last tier; (3) auto-purchase,
    /// which only buys upgrades over what is equipped or bagged, never buys back a name sold in step 2,
    /// and tops consumables up to the same Keep Stacks value the sweep floors on.
    /// </summary>
    public static class PrePitAutomationPass
    {
        // Reused across passes so the pre-jump frame allocates as little as possible
        private static readonly List<Mercenary> _mercenaries = new List<Mercenary>(4);
        private static readonly List<string> _soldNames = new List<string>(16);

        /// <summary>Runs equip sweep → sell sweep → purchase pass for the hero's party.</summary>
        public static void Run(HeroComponent heroComp)
        {
            if (heroComp?.LinkedHero == null || heroComp.Bag == null)
                return;

            var sellService = Core.Services?.GetService<AutoSellExcessItemsService>();
            var purchaseService = Core.Services?.GetService<AutoItemPurchaseService>();

            _soldNames.Clear();
            if (sellService != null && sellService.Enabled)
            {
                EquipUpgradesFromBag(heroComp);
                CollectHiredMercenaries(_mercenaries);
                var hero = heroComp.LinkedHero;
                var mercs = _mercenaries;
                sellService.TrySellDownToThreshold(heroComp.Bag, g => PartyGearUpgradeCheck.IsUpgradeForAnyone(hero, mercs, g), _soldNames);
            }

            purchaseService?.TryPurchasePass(heroComp, _soldNames);
            _mercenaries.Clear();
        }

        /// <summary>
        /// The upgrade test the in-pit chest safety net hands to <see cref="AutoSellExcessItemsService.TryMakeRoom"/>:
        /// true for gear the hero or any hired mercenary would wear over what they have on.
        /// </summary>
        public static Func<IGear, bool> CreateGearUpgradeCheck(HeroComponent heroComp)
        {
            if (heroComp?.LinkedHero == null)
                return null;
            var hero = heroComp.LinkedHero;
            var mercs = new List<Mercenary>(4);
            CollectHiredMercenaries(mercs);
            return g => PartyGearUpgradeCheck.IsUpgradeForAnyone(hero, mercs, g);
        }

        /// <summary>
        /// Offers every piece of gear in the bag to the party (hero first, then mercenaries, with
        /// hand-me-downs) honoring the auto-equip flags. Equipping removes the item from its slot and
        /// may drop displaced gear elsewhere in the bag, so each slot is re-read on its turn.
        /// </summary>
        private static void EquipUpgradesFromBag(HeroComponent heroComp)
        {
            if (!heroComp.AutoEquipHero && !heroComp.AutoEquipMercenaries)
                return;
            var bag = heroComp.Bag;
            for (int i = 0; i < bag.Capacity; i++)
            {
                if (bag.GetSlotItem(i) is IGear gear)
                    PartyAutoEquipHelper.TryAutoEquipForParty(heroComp, gear);
            }
        }

        private static void CollectHiredMercenaries(List<Mercenary> result)
        {
            result.Clear();
            var hired = Core.Services?.GetService<MercenaryManager>()?.GetHiredMercenaries();
            if (hired == null)
                return;
            for (int i = 0; i < hired.Count; i++)
            {
                var mercComp = hired[i].GetComponent<MercenaryComponent>();
                if (mercComp?.LinkedMercenary != null)
                    result.Add(mercComp.LinkedMercenary);
            }
        }
    }
}
