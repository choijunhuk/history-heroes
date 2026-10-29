using System;
using HistoryHeroes.Records;

namespace HistoryHeroes.Economy
{
    /// <summary>
    /// 골드·보석 (Economy.md 2-1). 보석은 무료분과 유료분을 따로 세고, 쓸 때는 무료분부터 쓴다.
    /// 보조 재료(고급 소환권, 장비 조각, 영웅 조각)는 SaveData.items에 쌓아 둔다 (쓰는 화면은 v0.4~).
    /// </summary>
    public static class Wallet
    {
        public const string Ticket = "ticket_premium";     // 🎫 고급 소환권
        public const string GearShard = "gear_shard";      // 🔩 장비 조각
        public static string HeroShard(string heroId) => "hero_shard_" + heroId; // 🧩 영웅 조각

        public static int Gems(SaveData d) => d.gemsFree + d.gemsPaid;

        public static void AddGold(SaveData d, int amount) => d.gold = Math.Max(0, d.gold + Math.Max(0, amount));

        public static void AddFreeGems(SaveData d, int amount) => d.gemsFree = Math.Max(0, d.gemsFree + Math.Max(0, amount));

        public static bool TrySpendGold(SaveData d, int amount)
        {
            if (amount < 0 || d.gold < amount) return false;
            d.gold -= amount;
            return true;
        }

        public static bool TrySpendGems(SaveData d, int amount)
        {
            if (amount < 0 || Gems(d) < amount) return false;
            int fromFree = Math.Min(d.gemsFree, amount);
            d.gemsFree -= fromFree;
            d.gemsPaid -= amount - fromFree;
            return true;
        }

        /// <summary>보상 묶음을 한 번에 넣는다</summary>
        public static void Grant(SaveData d, Reward r)
        {
            AddGold(d, r.Gold);
            AddFreeGems(d, r.Gems);
            if (r.Tickets > 0) d.AddItem(Ticket, r.Tickets);
            if (r.GearShards > 0) d.AddItem(GearShard, r.GearShards);
            if (r.HeroShards > 0 && !string.IsNullOrEmpty(r.HeroId)) d.AddItem(HeroShard(r.HeroId), r.HeroShards);
        }

        /// <summary>"1,234" 형태</summary>
        public static string Format(int amount) => amount.ToString("N0");
    }

    /// <summary>보상 묶음 (골드, 보석, 보조 재료)</summary>
    public struct Reward
    {
        public int Gold;
        public int Gems;
        public int Tickets;
        public int GearShards;
        public int HeroShards;
        public string HeroId;

        public bool IsEmpty => Gold == 0 && Gems == 0 && Tickets == 0 && GearShards == 0 && HeroShards == 0;

        public static Reward operator +(Reward a, Reward b)
        {
            return new Reward
            {
                Gold = a.Gold + b.Gold,
                Gems = a.Gems + b.Gems,
                Tickets = a.Tickets + b.Tickets,
                GearShards = a.GearShards + b.GearShards,
                HeroShards = a.HeroShards + b.HeroShards,
                HeroId = a.HeroId ?? b.HeroId,
            };
        }

        /// <summary>"골드 +700 · 보석 +500 · 고급 소환권 +1" 형태</summary>
        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();
            // 글 앞에 보상 그림 (2026-10-29 개발자: "보상에 그림이 너무 없다" — TMP 기본 스프라이트 묶음 RewardIcons)
            if (Gold > 0) parts.Add($"<sprite name=gold>골드 +{Wallet.Format(Gold)}");
            if (Gems > 0) parts.Add($"<sprite name=gem>보석 +{Wallet.Format(Gems)}");
            if (Tickets > 0) parts.Add($"<sprite name=ticket>고급 소환권 +{Tickets}");
            if (GearShards > 0) parts.Add($"<sprite name=equip_shard>장비 조각 +{GearShards}");
            if (HeroShards > 0) parts.Add($"<sprite name=hero_shard>영웅 조각 +{HeroShards}");
            return string.Join(" · ", parts);
        }
    }
}
