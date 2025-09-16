using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PitHero.RolePlayingSystem.Jobs
{
    public class Monk : Vocation
    {
        public override int GetStrength()
        {
            return 50;
        }
        public override int GetAgility()
        {
            return 25;
        }
        public override int GetVitality()
        {
            return 50;
        }
        public override int GetMagic()
        {
            return 1;
        }
        public override string GetDisplayName()
        {
            return "Monk";
        }
    }
}
