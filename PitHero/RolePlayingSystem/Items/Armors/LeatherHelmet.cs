namespace PitHero.RolePlayingSystem.Items.Armors
{
    public class LeatherHelmet : Armor
    {
        public LeatherHelmet()
        {
            Name = "Leather Helmet";
            Defense = 1;
            MagicDefense = 1;
            EvadePercent = 0;
            MagicEvadePercent = 0;
            Weight = 1;
            BuyPrice = 50;
            SellPrice = BuyPrice / 2;
            EquipVocations = GameData.VocationType.All;
        }
    }
}
