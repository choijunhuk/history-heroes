using System;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Economy
{
    /// <summary>
    /// 순찰 (LiveOps 8장, 오프라인 보상): 시간당 🪙150 + 깬 구간 × 20, 최대 12시간. 골드만 (보석·티켓·장비 없음).
    /// 1구간 보스를 깨야 열린다. 기기 시계를 되돌리면 되돌린 구간은 0 (마지막으로 받은 때를 지금으로 당긴다).
    /// </summary>
    public static class Patrol
    {
        public const int MaxHours = 12;
        public const long MaxSeconds = MaxHours * 3600L;

        public static bool Unlocked(SaveData s) => s.unlockedChapter >= 2;

        /// <summary>깬 구간 = 열린 구간 - 1 (보스를 깨면 다음 구간이 열린다)</summary>
        // 2026-10-29 전체 기획 검토 9번: 하루 수입의 40% → 25% (×0.6). 줄인 몫은 던전 완주 보너스로 (RunRewards.ClearBonus)
        public static int GoldPerHour(SaveData s) => 90 + 12 * Mathf.Clamp(s.unlockedChapter - 1, 0, 5);

        /// <summary>열렸으면 시계를 건다. 시계가 거꾸로 갔으면 지금으로 (그 구간은 0)</summary>
        public static bool Ensure(SaveData s, long now)
        {
            if (!Unlocked(s)) return false;
            if (s.patrol.lastClaimAt <= 0 || now < s.patrol.lastClaimAt)
            {
                s.patrol.lastClaimAt = now;
                return true;
            }

            return false;
        }

        public static long Seconds(SaveData s, long now)
        {
            if (!Unlocked(s) || s.patrol.lastClaimAt <= 0 || now <= s.patrol.lastClaimAt) return 0;
            return Math.Min(now - s.patrol.lastClaimAt, MaxSeconds);
        }

        public static int Pending(SaveData s, long now) => (int)(Seconds(s, now) * GoldPerHour(s) / 3600L);

        public static bool Full(SaveData s, long now) => Seconds(s, now) >= MaxSeconds;

        /// <summary>가득 차는 시각 (알림 예약용). 잠겼으면 0</summary>
        public static long FullAt(SaveData s) => Unlocked(s) && s.patrol.lastClaimAt > 0 ? s.patrol.lastClaimAt + MaxSeconds : 0;

        /// <summary>쌓인 골드를 받는다 (받은 양)</summary>
        public static int Claim(SaveData s, long now)
        {
            Ensure(s, now);
            int gold = Pending(s, now);
            if (gold <= 0) return 0;
            Wallet.Grant(s, new Reward { Gold = gold });
            s.patrol.lastClaimAt = now;
            return gold;
        }
    }
}
