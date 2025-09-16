using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PitHero.RolePlayingSystem.Items.Consumable
{
    public class MaxHealingHerb : Item
    {
        public MaxHealingHerb()
        {
            Name = "Max Healing Herb";
            BuyPrice = 750;
            SellPrice = 375;
            Description = "Heals all HP of one ally";
        }
    }
}
