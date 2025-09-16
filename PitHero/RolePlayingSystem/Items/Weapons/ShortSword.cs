using PitHero.RolePlayingSystem.GameData;

namespace PitHero.RolePlayingSystem.Items.Weapons
{
    public class ShortSword : Weapon
    {
        public ShortSword()
        {
            Name = "Short Sword";
            Attack = 10;
            HitPercent = 100;
            BuyPrice = 180;
            SellPrice = BuyPrice / 2;
            Throwable = true;
            AttackCategory = AttackCategory.Physical;
            WeaponSpecial = WeaponSpecial.DoubleGripEnabled | WeaponSpecial.MagicSwordEnabled;
            EquipVocations = VocationType.Knight;
        }
    }
}
