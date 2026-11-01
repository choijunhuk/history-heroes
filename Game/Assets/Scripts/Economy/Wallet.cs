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

        /// <summary>무료 보석을 더한다. 환불 회수로 빚(음수)이 있으면 그 빚부터 메운다 (Settle)</summary>
        public static void AddFreeGems(SaveData d, int amount)
        {
            d.gemsFree += Math.Max(0, amount);
            Settle(d);
        }

        /// <summary>결제로 산 기본 보석 (유료분). 청약철회·환불은 이 몫을 기준으로 계산한다 (Monetization 3-1)</summary>
        public static void AddPaidGems(SaveData d, int amount)
        {
            d.gemsPaid += Math.Max(0, amount);
            Settle(d);
        }

        /// <summary>환불 회수로 보석이 음수다 (LiveOps 3-4). 0 이상이 될 때까지 상점 · 뽑기를 막는다 (보석 쓰기는 잔액 검사로 이미 막힌다)</summary>
        public static bool InDebt(SaveData d) => Gems(d) < 0;

        public const string DebtText = "환불된 결제를 회수해서 보석이 모자라요. 보석이 0 이상이 될 때까지 상점 · 뽑기를 쓸 수 없어요 (새로 얻는 보석으로 먼저 채워요).";

        /// <summary>
        /// 환불 회수(유료 · 무료에서 그 결제로 준 만큼 뺀다) 뒤 한쪽만 음수면 다른 쪽으로 메운다 — 그래서 전체가 0 이상이면 둘 다 0 이상이다.
        /// LiveOps 3-4 "회수할 보석이 모자라면 음수, 이후 얻는 보석으로 먼저 채운다"
        /// </summary>
        public static void Settle(SaveData d)
        {
            if (d.gemsPaid < 0 && d.gemsFree > 0)
            {
                int move = Math.Min(d.gemsFree, -d.gemsPaid);
                d.gemsFree -= move;
                d.gemsPaid += move;
            }
            else if (d.gemsFree < 0 && d.gemsPaid > 0)
            {
                int move = Math.Min(d.gemsPaid, -d.gemsFree);
                d.gemsPaid -= move;
                d.gemsFree += move;
            }
        }

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

        /// <summary>저장 목록에 든 재료를 잔액 확인 후 한 번에 쓴다</summary>
        public static bool TrySpendItem(SaveData d, string itemId, int amount)
        {
            if (amount < 0 || string.IsNullOrEmpty(itemId) || d.ItemCount(itemId) < amount) return false;
            d.AddItem(itemId, -amount);
            return true;
        }

        /// <summary>보상 묶음을 한 번에 넣는다</summary>
        public static void Grant(SaveData d, Reward r)
        {
            AddGold(d, r.Gold);
            AddFreeGems(d, r.Gems);
            if (r.Tickets > 0) d.AddItem(Ticket, r.Tickets);
            if (r.GearShards > 0) d.AddItem(GearShard, r.GearShards);
            if (r.HeroShards > 0 && !string.IsNullOrEmpty(r.HeroId)
                && Modes.GodTrialRules.CanReceiveNormalShards(d, r.HeroId))
                d.AddItem(HeroShard(r.HeroId), r.HeroShards);
        }

        /// <summary>"1,234" 형태</summary>
        public static string Format(int amount) => amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); // 기기 언어와 상관없이 "1,234" (보상 카드가 이 모양을 읽는다)
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
