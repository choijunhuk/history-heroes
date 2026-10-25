using System;
using HistoryHeroes.Records;

namespace HistoryHeroes.Economy
{
    /// <summary>
    /// 계정 레벨 (Stats.md 7장).
    /// - 경험치: 던전 = 깬 방 × 8 (최대 120), 보스맵 = 50
    /// - 필요 경험치: Lv n → n+1 = 100 + 40 × n, 최대 Lv 40
    /// - 레벨업마다 골드·보석 조금, 5레벨마다 고급 소환권 1, 하트를 최대치까지 채움 (Economy 3-3)
    /// </summary>
    public static class AccountLevel
    {
        public const int MaxLevel = 40;
        public const int BossExp = 50;

        public static int ExpToNext(int level) => 100 + 40 * level;

        public static int DungeonExp(int roomsCleared) => Math.Min(120, Math.Max(0, roomsCleared) * 8);

        /// <summary>레벨업 한 번의 보상 (임시값: 골드 300 + 레벨 × 50, 보석 20, 5레벨마다 고급 소환권 1)</summary>
        public static Reward LevelUpReward(int newLevel)
        {
            return new Reward { Gold = 300 + newLevel * 50, Gems = 20, Tickets = newLevel % 5 == 0 ? 1 : 0 };
        }

        public struct Result
        {
            public int Exp;
            public int FromLevel;
            public int ToLevel;
            public Reward Reward;
            public bool LeveledUp => ToLevel > FromLevel;
        }

        /// <summary>경험치를 넣고, 오른 레벨만큼 보상을 주고 하트를 채운다</summary>
        public static Result AddExp(SaveData d, int exp, long now)
        {
            var result = new Result { Exp = Math.Max(0, exp), FromLevel = d.accountLevel };
            if (d.accountLevel >= MaxLevel)
            {
                result.ToLevel = d.accountLevel;
                return result;
            }

            d.accountExp += result.Exp;
            while (d.accountLevel < MaxLevel && d.accountExp >= ExpToNext(d.accountLevel))
            {
                d.accountExp -= ExpToNext(d.accountLevel);
                d.accountLevel++;
                result.Reward += LevelUpReward(d.accountLevel);
            }

            if (d.accountLevel >= MaxLevel) d.accountExp = 0;
            result.ToLevel = d.accountLevel;

            if (result.LeveledUp)
            {
                Wallet.Grant(d, result.Reward);
                Hearts.Refill(d, now);
            }

            return result;
        }
    }
}
