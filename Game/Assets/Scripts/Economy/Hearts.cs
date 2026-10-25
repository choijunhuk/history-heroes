using System;
using HistoryHeroes.Records;

namespace HistoryHeroes.Economy
{
    /// <summary>
    /// 하트 = 판에 들어갈 때 쓰는 횟수 (Economy.md 3장, 개발자 결정 B: 5개 · 20분 충전).
    /// - 자연 충전은 최대치(5)까지만. 선물·광고·보상으로 받은 하트는 99개까지 보관
    /// - 계정 레벨업 때 최대치까지 채움 (초과분은 그대로)
    /// - 첫날 처음 3판은 하트를 쓰지 않음
    /// - 기기 시계를 되돌리면 그 자리에서 충전 시계를 다시 시작 (하트를 빼앗지도, 더 주지도 않음)
    /// </summary>
    public static class Hearts
    {
        public const int Max = 5;
        public const int Cap = 99;
        public const int RegenSeconds = 20 * 60;
        public const int FreeRunsFirstDay = 3;

        public const int AdReward = 2;
        public const int AdPerDay = 3;
        public const int GemRefillCost = 60;
        public const int GemRefillAmount = 5;
        public const int GemRefillPerDay = 5;
        public const int RefundsPerDay = 5;

        /// <summary>지난 시간만큼 자연 충전을 반영한다</summary>
        public static void Tick(SaveData d, long now)
        {
            if (d.hearts >= Max || d.heartsUpdatedAt <= 0 || now < d.heartsUpdatedAt)
            {
                // 가득 찼으면 시계가 멈춰 있다. 처음이거나 시계가 거꾸로 갔으면 지금부터 센다
                d.heartsUpdatedAt = now;
                return;
            }

            long gained = (now - d.heartsUpdatedAt) / RegenSeconds;
            if (gained <= 0) return;

            d.hearts = (int)Math.Min(Max, d.hearts + gained);
            d.heartsUpdatedAt = d.hearts >= Max ? now : d.heartsUpdatedAt + gained * RegenSeconds;
        }

        /// <summary>다음 하트까지 남은 초 (가득 찼으면 0)</summary>
        public static int SecondsToNext(SaveData d, long now)
        {
            Tick(d, now);
            if (d.hearts >= Max) return 0;
            return (int)Math.Max(0, RegenSeconds - (now - d.heartsUpdatedAt));
        }

        /// <summary>판 시작에 하트를 쓴다. 첫날 처음 3판은 무료. 모자라면 false.</summary>
        public static bool TrySpend(SaveData d, int amount, long now, string today, out bool wasFree)
        {
            Tick(d, now);
            wasFree = false;
            if (amount <= 0) return true;

            if (string.IsNullOrEmpty(d.firstPlayDate)) d.firstPlayDate = today;
            if (d.firstPlayDate == today && d.freeRunsUsed < FreeRunsFirstDay)
            {
                d.freeRunsUsed++;
                wasFree = true;
                return true;
            }

            if (d.hearts < amount) return false;
            bool wasFull = d.hearts >= Max;
            d.hearts -= amount;
            if (wasFull && d.hearts < Max) d.heartsUpdatedAt = now; // 가득 찬 동안 멈춰 있던 충전 시계를 지금부터
            return true;
        }

        /// <summary>선물·광고·보상·환급 (최대 99개)</summary>
        public static void Add(SaveData d, int amount, long now)
        {
            Tick(d, now);
            d.hearts = Math.Min(Cap, d.hearts + Math.Max(0, amount));
            if (d.hearts >= Max) d.heartsUpdatedAt = now;
        }

        /// <summary>계정 레벨업: 최대치까지 채운다. 이미 넘쳐 있으면 그대로</summary>
        public static void Refill(SaveData d, long now)
        {
            Tick(d, now);
            if (d.hearts < Max) d.hearts = Max;
            d.heartsUpdatedAt = now;
        }

        /// <summary>광고 보고 +2 (하루 3회)</summary>
        public static bool CanWatchAd(SaveData d, string today) => d.Today(today).adHearts < AdPerDay;

        public static void GrantAd(SaveData d, long now, string today)
        {
            d.Today(today).adHearts++;
            Add(d, AdReward, now);
        }

        /// <summary>보석 60 → 하트 5 (하루 5회)</summary>
        public static bool TryBuyWithGems(SaveData d, long now, string today)
        {
            DailyCounters daily = d.Today(today);
            if (daily.gemHearts >= GemRefillPerDay || !Wallet.TrySpendGems(d, GemRefillCost)) return false;
            daily.gemHearts++;
            Add(d, GemRefillAmount, now);
            return true;
        }

        /// <summary>보스 최초 도전에서 졌을 때 돌려주기 (아직 못 깬 보스만, 하루 5회)</summary>
        public static bool TryRefundBossLoss(SaveData d, long now, string today)
        {
            DailyCounters daily = d.Today(today);
            if (daily.heartRefunds >= RefundsPerDay) return false;
            daily.heartRefunds++;
            Add(d, 1, now);
            return true;
        }

        /// <summary>"12:34" 형태</summary>
        public static string FormatTime(int seconds)
        {
            seconds = Math.Max(0, seconds);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }
    }
}
