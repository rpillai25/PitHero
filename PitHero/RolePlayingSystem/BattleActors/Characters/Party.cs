using System.Collections.Generic;

namespace PitHero.RolePlayingSystem.BattleActors.Characters
{
    public class Party
    {
        public const int MAX_PARTY_SIZE = 5;

        List<Character> partyMembers;

        public Party()
        {
            partyMembers = new List<Character>();
        }

        public bool AddPartyMember(Character character)
        {
            if(partyMembers.Count >= MAX_PARTY_SIZE)
            {
                Nez.Debug.Log("At max size. Can't add any more party members.");
                return false;
            }
            partyMembers.Add(character);
            return true;
        }
    }
}
