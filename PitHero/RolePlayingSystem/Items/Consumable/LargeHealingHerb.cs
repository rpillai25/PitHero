using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PitHero.RolePlayingSystem.Items.Consumable
{
    public class LargeHealingHerb : Item
    {
        public LargeHealingHerb()
        {
            Name = "Large Healing Herb";
            BuyPrice = 360;
            SellPrice = 180;
            Description = "Heals 500 HP to one ally";
        }
    }
}
