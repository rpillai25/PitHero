namespace PitHero.RolePlayingSystem.BattleActors.Characters
{
    public static class ExperienceTable
    {
        private static LevelUpUnit[] LevelUpUnits;
        public static int MaxExp = 9999999;

        static ExperienceTable()
        {
            LevelUpUnits = new LevelUpUnit[99];
            LevelUpUnits[0] = new LevelUpUnit(0, 20, 2);
            LevelUpUnits[1] = new LevelUpUnit(10, 25, 5);
            LevelUpUnits[2] = new LevelUpUnit(33, 30, 8);
            LevelUpUnits[3] = new LevelUpUnit(74, 40, 11);
            LevelUpUnits[4] = new LevelUpUnit(140, 50, 14);
            LevelUpUnits[5] = new LevelUpUnit(241, 60, 17);
            LevelUpUnits[6] = new LevelUpUnit(389, 70, 20);
            LevelUpUnits[7] = new LevelUpUnit(599, 80, 23);
            LevelUpUnits[8] = new LevelUpUnit(888, 90, 26);
            LevelUpUnits[9] = new LevelUpUnit(1276, 100, 29);
            LevelUpUnits[10] = new LevelUpUnit(1786, 120, 32);
            LevelUpUnits[11] = new LevelUpUnit(2441, 140, 35);
            LevelUpUnits[12] = new LevelUpUnit(3269, 160, 38);
            LevelUpUnits[13] = new LevelUpUnit(4299, 180, 41);
            LevelUpUnits[14] = new LevelUpUnit(5564, 200, 44);
            LevelUpUnits[15] = new LevelUpUnit(7097, 220, 47);
            LevelUpUnits[16] = new LevelUpUnit(8936, 240, 50);
            LevelUpUnits[17] = new LevelUpUnit(11120, 260, 53);
            LevelUpUnits[18] = new LevelUpUnit(13691, 280, 56);
            LevelUpUnits[19] = new LevelUpUnit(16693, 300, 59);
            LevelUpUnits[20] = new LevelUpUnit(20173, 320, 62);
            LevelUpUnits[21] = new LevelUpUnit(24180, 340, 65);
            LevelUpUnits[22] = new LevelUpUnit(28765, 360, 68);
            LevelUpUnits[23] = new LevelUpUnit(33983, 380, 71);
            LevelUpUnits[24] = new LevelUpUnit(39890, 400, 74);
            LevelUpUnits[25] = new LevelUpUnit(46546, 420, 77);
            LevelUpUnits[26] = new LevelUpUnit(54012, 440, 80);
            LevelUpUnits[27] = new LevelUpUnit(62352, 460, 83);
            LevelUpUnits[28] = new LevelUpUnit(71632, 480, 86);
            LevelUpUnits[29] = new LevelUpUnit(81921, 500, 89);
            LevelUpUnits[30] = new LevelUpUnit(93291, 530, 92);
            LevelUpUnits[31] = new LevelUpUnit(105815, 560, 95);
            LevelUpUnits[32] = new LevelUpUnit(119569, 590, 98);
            LevelUpUnits[33] = new LevelUpUnit(134633, 620, 101);
            LevelUpUnits[34] = new LevelUpUnit(151087, 650, 104);
            LevelUpUnits[35] = new LevelUpUnit(169015, 690, 107);
            LevelUpUnits[36] = new LevelUpUnit(188503, 730, 110);
            LevelUpUnits[37] = new LevelUpUnit(209640, 770, 113);
            LevelUpUnits[38] = new LevelUpUnit(232517, 810, 116);
            LevelUpUnits[39] = new LevelUpUnit(257227, 850, 119);
            LevelUpUnits[40] = new LevelUpUnit(283867, 900, 122);
            LevelUpUnits[41] = new LevelUpUnit(312534, 950, 125);
            LevelUpUnits[42] = new LevelUpUnit(343330, 1000, 128);
            LevelUpUnits[43] = new LevelUpUnit(376357, 1050, 131);
            LevelUpUnits[44] = new LevelUpUnit(411722, 1100, 134);
            LevelUpUnits[45] = new LevelUpUnit(449533, 1160, 137);
            LevelUpUnits[46] = new LevelUpUnit(489900, 1220, 140);
            LevelUpUnits[47] = new LevelUpUnit(532937, 1280, 143);
            LevelUpUnits[48] = new LevelUpUnit(578759, 1340, 146);
            LevelUpUnits[49] = new LevelUpUnit(627485, 1400, 149);
            LevelUpUnits[50] = new LevelUpUnit(679235, 1460, 152);
            LevelUpUnits[51] = new LevelUpUnit(734131, 1520, 155);
            LevelUpUnits[52] = new LevelUpUnit(792300, 1580, 158);
            LevelUpUnits[53] = new LevelUpUnit(853869, 1640, 161);
            LevelUpUnits[54] = new LevelUpUnit(918969, 1700, 164);
            LevelUpUnits[55] = new LevelUpUnit(987732, 1760, 167);
            LevelUpUnits[56] = new LevelUpUnit(1060294, 1820, 170);
            LevelUpUnits[57] = new LevelUpUnit(1136793, 1880, 173);
            LevelUpUnits[58] = new LevelUpUnit(1217368, 1940, 176);
            LevelUpUnits[59] = new LevelUpUnit(1302163, 2000, 179);
            LevelUpUnits[60] = new LevelUpUnit(1391323, 2050, 182);
            LevelUpUnits[61] = new LevelUpUnit(1484995, 2100, 185);
            LevelUpUnits[62] = new LevelUpUnit(1583329, 2150, 188);
            LevelUpUnits[63] = new LevelUpUnit(1686478, 2200, 191);
            LevelUpUnits[64] = new LevelUpUnit(1794597, 2250, 194);
            LevelUpUnits[65] = new LevelUpUnit(1907843, 2300, 197);
            LevelUpUnits[66] = new LevelUpUnit(2026376, 2350, 200);
            LevelUpUnits[67] = new LevelUpUnit(2150358, 2400, 203);
            LevelUpUnits[68] = new LevelUpUnit(2279955, 2450, 206);
            LevelUpUnits[69] = new LevelUpUnit(2415333, 2500, 209);
            LevelUpUnits[70] = new LevelUpUnit(2556663, 2550, 212);
            LevelUpUnits[71] = new LevelUpUnit(2704116, 2600, 215);
            LevelUpUnits[72] = new LevelUpUnit(2857867, 2650, 218);
            LevelUpUnits[73] = new LevelUpUnit(3018093, 2700, 221);
            LevelUpUnits[74] = new LevelUpUnit(3184974, 2750, 224);
            LevelUpUnits[75] = new LevelUpUnit(3358692, 2800, 227);
            LevelUpUnits[76] = new LevelUpUnit(3539432, 2850, 230);
            LevelUpUnits[77] = new LevelUpUnit(3727380, 2900, 233);
            LevelUpUnits[78] = new LevelUpUnit(3922726, 2950, 236);
            LevelUpUnits[79] = new LevelUpUnit(4125661, 3000, 239);
            LevelUpUnits[80] = new LevelUpUnit(4336381, 3050, 242);
            LevelUpUnits[81] = new LevelUpUnit(4555081, 3100, 245);
            LevelUpUnits[82] = new LevelUpUnit(4781961, 3150, 248);
            LevelUpUnits[83] = new LevelUpUnit(5017223, 3200, 251);
            LevelUpUnits[84] = new LevelUpUnit(5261071, 3250, 254);
            LevelUpUnits[85] = new LevelUpUnit(5513712, 3300, 257);
            LevelUpUnits[86] = new LevelUpUnit(5775354, 3350, 260);
            LevelUpUnits[87] = new LevelUpUnit(6046210, 3400, 263);
            LevelUpUnits[88] = new LevelUpUnit(6326493, 3450, 266);
            LevelUpUnits[89] = new LevelUpUnit(6616420, 3500, 269);
            LevelUpUnits[90] = new LevelUpUnit(6916210, 3550, 272);
            LevelUpUnits[91] = new LevelUpUnit(7226084, 3600, 275);
            LevelUpUnits[92] = new LevelUpUnit(7546266, 3650, 278);
            LevelUpUnits[93] = new LevelUpUnit(7876982, 3700, 281);
            LevelUpUnits[94] = new LevelUpUnit(8218461, 3750, 284);
            LevelUpUnits[95] = new LevelUpUnit(8570934, 3800, 287);
            LevelUpUnits[96] = new LevelUpUnit(8934635, 3850, 290);
            LevelUpUnits[97] = new LevelUpUnit(9309800, 3900, 293);
            LevelUpUnits[98] = new LevelUpUnit(9696668, 3950, 296);
        }

        public static LevelUpUnit GetLevelUpUnit(int level)
        {
            if (level < 0 || level >= 99)
            {
                //Return an experience value that is impossible to reach
                return new LevelUpUnit(int.MaxValue, int.MaxValue, int.MaxValue);
            }
            return LevelUpUnits[level];
        }
    }
}
