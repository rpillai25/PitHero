using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PitHero.RolePlayingSystem.Jobs
{
    public class Priest : Vocation
    {
        public override int GetStrength()
        {
            return 17;
        }
        public override int GetAgility()
        {
            return 25;
        }
        public override int GetVitality()
        {
            return 24;
        }
        public override int GetMagic()
        {
            return 49;
        }
        public override string GetDisplayName()
        {
            return "Priest";
        }
    }
}
