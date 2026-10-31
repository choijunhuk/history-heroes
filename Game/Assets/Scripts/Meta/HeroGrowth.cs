using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Meta
{
    /// <summary>
    /// 영웅 성장 규칙 (Stats 4~7장, Economy 5장): 영웅 레벨(골드), 각성 ★(🧩 영웅 조각), 수련(계정 공용, 골드),
    /// 골드 상점의 영웅 조각(하루 3개). 저장 데이터만 바꾸고 저장은 부르는 쪽이 한다.
    /// 영웅 조각은 뽑기로도, 보석으로도 팔지 않는다 (Stats 6장).
    /// </summary>
    public static class HeroGrowth
    {
        public const int ShardPrice = 500;
        public const int ShardsPerDay = 5; // 2026-10-31 개발자 결정 (추천안): 영웅 21명이라 3개면 전원 ★5까지 약 788일 → 5개 약 470일 (Economy 9장)
        public const int TrainingCount = 6;

        public static int Shards(SaveData save, string heroId) => save.ItemCount(Wallet.HeroShard(heroId));

        // ---------------------------------------------------------------- 영웅 레벨

        public static bool CanLevelUp(SaveData save, MetaTable table, string heroId, out int cost)
        {
            if (!Modes.GodTrialRules.IsHeroUnlocked(save, heroId))
            {
                cost = 0;
                return false;
            }
            HeroProgress h = save.Hero(heroId);
            cost = table.HeroLevelCost(h.level);
            return h.level < table.maxHeroLevel;
        }

        public static bool TryLevelUp(SaveData save, MetaTable table, string heroId)
        {
            if (!Modes.GodTrialRules.IsHeroUnlocked(save, heroId)) return false;
            if (!CanLevelUp(save, table, heroId, out int cost) || !Wallet.TrySpendGold(save, cost)) return false;
            save.Hero(heroId).level++;
            return true;
        }

        // ---------------------------------------------------------------- 각성

        /// <summary>다음 별에 필요한 조각 (★5면 -1)</summary>
        public static int AwakenCost(SaveData save, MetaTable table, string heroId) => table.AwakenCost(save.Hero(heroId).stars);

        public static bool TryAwaken(SaveData save, MetaTable table, string heroId)
        {
            if (!Modes.GodTrialRules.IsHeroUnlocked(save, heroId)) return false;
            int cost = AwakenCost(save, table, heroId);
            if (cost < 0 || Shards(save, heroId) < cost) return false;
            save.AddItem(Wallet.HeroShard(heroId), -cost);
            save.Hero(heroId).stars++;
            return true;
        }

        // ---------------------------------------------------------------- 수련 (계정 공용)

        /// <summary>2구간 보스 격파 후 (Systems 3장)</summary>
        public static bool TrainingUnlocked(SaveData save) => save.unlockedChapter >= 3;

        public static int TrainingLevel(SaveData save, TrainingType type)
        {
            int i = (int)type;
            return i < save.training.Count ? save.training[i] : 0;
        }

        /// <summary>수련 레벨 ≤ 계정 레벨 (Stats 5장). 막히면 이유를 돌려준다</summary>
        public static bool CanTrain(SaveData save, MetaTable table, TrainingType type, out int cost, out string reason)
        {
            int level = TrainingLevel(save, type);
            TrainingRow row = table.training[(int)type];
            cost = table.TrainingCost(level);
            reason = null;
            if (!TrainingUnlocked(save)) reason = "2구간 보스를 깨면 열려요";
            else if (level >= row.maxLevel) reason = "최대 레벨";
            else if (level + 1 > save.accountLevel) reason = $"계정 Lv {level + 1} 필요";
            return reason == null;
        }

        public static bool TryTrain(SaveData save, MetaTable table, TrainingType type)
        {
            if (!CanTrain(save, table, type, out int cost, out _) || !Wallet.TrySpendGold(save, cost)) return false;
            while (save.training.Count < TrainingCount) save.training.Add(0);
            save.training[(int)type]++;
            return true;
        }

        // ---------------------------------------------------------------- 골드 상점: 영웅 조각

        public static int ShardsLeftToday(SaveData save, string today)
        {
            if (save.shop.date != today) return ShardsPerDay;
            return Mathf.Max(0, ShardsPerDay - save.shop.heroShardsBought);
        }

        public static bool TryBuyHeroShard(SaveData save, string heroId, string today)
        {
            if (string.IsNullOrEmpty(heroId) || !Modes.GodTrialRules.CanReceiveNormalShards(save, heroId)
                || ShardsLeftToday(save, today) <= 0) return false;
            if (!Wallet.TrySpendGold(save, ShardPrice)) return false;
            if (save.shop.date != today)
            {
                save.shop.date = today;
                save.shop.heroShardsBought = 0;
            }

            save.shop.heroShardsBought++;
            save.AddItem(Wallet.HeroShard(heroId), 1);
            return true;
        }
    }
}
