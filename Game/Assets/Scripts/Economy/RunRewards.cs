using System;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Economy
{
    /// <summary>
    /// 판 보상 (Economy.md 4장 획득처, 임시값).
    /// - 던전 클리어: 골드 400 + 구간 × 100. 사망: 클리어 보상 × 깬 방 비율
    /// - 보스 최초 격파: 골드 2,000 + 보석 500 + 고급 소환권 1 + 쓴 영웅 조각 5
    /// - 보스 반복 격파: 골드 300 (하루 3회까지)
    /// </summary>
    public static class RunRewards
    {
        public const int BossRepeatPerDay = 3;

        public static Reward Dungeon(int chapter, int roomsCleared, int roomCount, bool cleared)
        {
            int full = 400 + Math.Max(1, chapter) * 100;
            float ratio = cleared ? 1f : roomCount <= 0 ? 0f : Mathf.Clamp01((float)roomsCleared / roomCount);
            return new Reward { Gold = Mathf.FloorToInt(full * ratio) + (cleared ? ClearBonus(chapter) : 0) };
        }

        /// <summary>
        /// 던전 완주 보너스 (2026-10-29 전체 기획 검토 9번): 순찰 골드를 줄인 만큼(하루 수입의 40% → 25%) 직접 끝낸 판으로 옮긴다.
        /// 30분 유저(2구간 · 하루 약 1.6판) 기준 하루 총수입은 그대로 (Economy 4-2)
        /// </summary>
        public static int ClearBonus(int chapter) => 900 + Math.Max(1, chapter) * 100;

        public static Reward BossFirstClear(string heroId)
        {
            return new Reward { Gold = 2000, Gems = 500, Tickets = 1, HeroShards = 5, HeroId = heroId };
        }

        public static Reward BossRepeat(SaveData d, string today)
        {
            DailyCounters daily = d.Today(today);
            if (daily.bossRepeatRewards >= BossRepeatPerDay) return default;
            daily.bossRepeatRewards++;
            return new Reward { Gold = 300 };
        }
    }
}
