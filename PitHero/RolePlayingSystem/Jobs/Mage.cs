using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PitHero.RolePlayingSystem.Jobs
{
    public class Mage : Vocation
    {
        public override int GetStrength()
        {
            return 15;
        }
        public override int GetAgility()
        {
            return 24;
        }
        public override int GetVitality()
        {
            return 22;
        }
        public override int GetMagic()
        {
            return 55;
        }
        public override string GetDisplayName()
        {
            return "Mage";
        }
    }
}
