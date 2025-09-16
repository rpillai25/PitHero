using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PitHero.RolePlayingSystem.Items.Consumable
{
    public class HealingHerb : Item
    {
        public HealingHerb()
        {
            Name = "Healing Herb";
            BuyPrice = 40;
            SellPrice = 20;
            Description = "Heals 50 HP to one ally";
        }
    }
}
