using System;
using System.Collections.Generic;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Modes
{
    /// <summary>
    /// 일일 도전 규칙 (Modes 5장): 방 7개 (일반 4 · 엘리트 1 · 보상 1 · 중간 보스 1), **날짜 시드 → 모두 같은 판**.
    /// 오늘의 영웅 1명 고정 + 공정한 고정 세트 (Lv15 ★1, 수련·유물·펫·룬 없음, 고급 장비 세트 강화 0). 변형 규칙 매일 2개.
    /// 공식 도전 하루 1회 (첫 시도만 기록, 죽어도 그게 기록) + 연습 무제한 (보상·기록 없음). 보상 🪙1,000 + 🧩1 (오늘의 영웅).
    /// 점수 = 깬 방 × 100 + 처치 × 1 + 남은 체력(%) × 2 + 클리어 500 + 시간 보너스 (600 − 초, 최소 0) × 2. 죽으면 클리어·시간 보너스 없음.
    /// </summary>
    public static class DailyRules
    {
        public const int ResetHour = 5;
        public const int RewardGold = 1000;
        public const int RewardHeroShards = 1;

        public static string DayKey(DateTimeOffset now) => Missions.MissionService.DayKey(now, ResetHour);
        public static int Seed(string dayKey) => Common.RunRandom.Hash("daily/" + dayKey);

        private static int DayNumber(string dayKey) => DateTime.TryParse(dayKey, out DateTime d) ? (int)(d - new DateTime(2026, 1, 1)).TotalDays : 0;

        /// <summary>오늘의 구간 (1~5를 날마다 돌아가며)</summary>
        public static int ChapterFor(string dayKey) => 1 + ((DayNumber(dayKey) % 5) + 5) % 5;

        /// <summary>오늘의 영웅 (영웅 순서를 날마다 돌아가며, 해금 안 했어도 빌려 쓴다)</summary>
        public static string HeroFor(string dayKey, IReadOnlyList<string> heroOrder)
        {
            if (heroOrder == null || heroOrder.Count == 0) return "khan";
            int n = DayNumber(dayKey) + 1; // 미션 "오늘의 영웅"과 하루 어긋나게
            return heroOrder[((n % heroOrder.Count) + heroOrder.Count) % heroOrder.Count];
        }

        public static List<RoomPlan> Plan()
        {
            return new List<RoomPlan>
            {
                new RoomPlan { kind = RoomKind.Normal, budget = 8, minTypes = 2, maxTypes = 2 },
                new RoomPlan { kind = RoomKind.Normal, budget = 11, minTypes = 2, maxTypes = 3 },
                new RoomPlan { kind = RoomKind.Elite, budget = 6, minTypes = 1, maxTypes = 2, elites = 1 },
                new RoomPlan { kind = RoomKind.Reward },
                new RoomPlan { kind = RoomKind.Normal, budget = 14, minTypes = 3, maxTypes = 3 },
                new RoomPlan { kind = RoomKind.Normal, budget = 17, minTypes = 3, maxTypes = 4 },
                new RoomPlan { kind = RoomKind.MidBoss, budget = 4, minTypes = 1, maxTypes = 1 },
            };
        }

        // ---------------------------------------------------------------- 변형 규칙 (매일 2개)

        public enum Modifier
        {
            DoubleEnemiesNoHeal,   // 적 2배 + 회복 없음 (보상 방에 회복이 없다)
            FastSkillsWeakShots,   // 액티브 쿨다운 -30% + 기본 공격 피해 -30%
            ToughEnemiesStronger,  // 적 체력 +40% + 내 공격력 +20%
            FastArrows,            // 적 투사체 빠르기 +25%
            StrongShotsWeakSkills, // 기본 공격 피해 +40% + 액티브 피해 -30%
            FastButFragile,        // 이동속도 +20% + 받는 피해 +20%
        }

        public static List<Modifier> ModifiersFor(string dayKey)
        {
            var random = new System.Random(Seed(dayKey) ^ 0x5bd1e995);
            var all = new List<Modifier>((Modifier[])Enum.GetValues(typeof(Modifier)));
            var picked = new List<Modifier>(2);
            for (int i = 0; i < 2 && all.Count > 0; i++)
            {
                int k = random.Next(all.Count);
                Modifier m = all[k];
                picked.Add(m);
                all.RemoveAt(k);
                all.RemoveAll(other => Conflicts(m, other)); // 서로 뒤집는 규칙은 같은 날 안 나온다
            }

            return picked;
        }

        /// <summary>기본 공격·액티브를 서로 반대로 바꾸는 두 규칙 (같이 나오면 둘 다 의미가 없어진다)</summary>
        public static bool Conflicts(Modifier a, Modifier b)
        {
            bool A(Modifier m) => m == Modifier.FastSkillsWeakShots || m == Modifier.StrongShotsWeakSkills;
            return a != b && A(a) && A(b);
        }

        public static string ModifierText(Modifier m)
        {
            switch (m)
            {
                case Modifier.DoubleEnemiesNoHeal: return "적 2배 + 회복 없음";
                case Modifier.FastSkillsWeakShots: return "액티브 쿨다운 -30% + 기본 공격 피해 -30%";
                case Modifier.ToughEnemiesStronger: return "적 체력 +40% + 내 공격력 +20%";
                case Modifier.FastArrows: return "적 투사체 빠르기 +25%";
                case Modifier.StrongShotsWeakSkills: return "기본 공격 피해 +40% + 액티브 피해 -30%";
                default: return "이동속도 +20% + 받는 피해 +20%";
            }
        }

        // ---------------------------------------------------------------- 점수

        public static int Score(int roomsCleared, int kills, float hpRatio, bool cleared, float seconds)
        {
            int score = roomsCleared * 100 + kills + Mathf.RoundToInt(Mathf.Clamp01(hpRatio) * 100f) * 2;
            if (cleared) score += 500 + Mathf.Max(0, 600 - Mathf.CeilToInt(seconds)) * 2;
            return score;
        }

        public static bool Unlocked(SaveData save) => save.unlockedChapter >= 4; // 3구간 보스 격파 (Systems 3장)

        /// <summary>날이 바뀌었으면 오늘의 공식 도전을 다시 연다 (지난 기록은 목록에 남는다)</summary>
        public static bool EnsureDay(SaveData save, DateTimeOffset now)
        {
            string day = DayKey(now);
            if (save.dailyChallenge.dayKey == day) return false;
            save.dailyChallenge.dayKey = day;
            save.dailyChallenge.officialDone = false;
            save.dailyChallenge.officialScore = 0;
            save.dailyChallenge.bestPractice = 0;
            return true;
        }

        /// <summary>공식 도전 기록 (첫 시도만). 처음이면 보상 줄을 돌려준다</summary>
        public static List<string> RecordOfficial(SaveData save, string heroId, int score, bool cleared)
        {
            var lines = new List<string>();
            if (save.dailyChallenge.officialDone) return lines;
            save.dailyChallenge.officialDone = true;
            save.dailyChallenge.officialScore = score;
            save.dailyChallenge.participations++;
            save.dailyChallenge.history.Insert(0, new DailyResult { dayKey = save.dailyChallenge.dayKey, heroId = heroId, score = score, cleared = cleared });
            if (save.dailyChallenge.history.Count > 14) save.dailyChallenge.history.RemoveRange(14, save.dailyChallenge.history.Count - 14);
            var reward = new Economy.Reward { Gold = RewardGold, HeroShards = RewardHeroShards, HeroId = heroId };
            Economy.Wallet.Grant(save, reward);
            lines.Add(reward.ToString());
            return lines;
        }

        /// <summary>오늘의 공정한 고정 세트 (Modes 5장): Lv15 ★1, 고급 장비 3칸 강화 0, 수련·유물·펫 없음</summary>
        public static MetaSnapshot FixedSet()
        {
            var snap = new MetaSnapshot { heroLevel = 15, heroStars = 1 };
            snap.equipment.Add(new EquippedItem { slot = 0, dataId = "weapon_elite_u", level = 0 });
            snap.equipment.Add(new EquippedItem { slot = 1, dataId = "armor_elite_u", level = 0 });
            snap.equipment.Add(new EquippedItem { slot = 2, dataId = "acc_feather_u", level = 0 });
            return snap;
        }
    }
}
