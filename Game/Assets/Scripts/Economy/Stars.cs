using System.Collections.Generic;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using UnityEngine;

namespace HistoryHeroes.Economy
{
    /// <summary>
    /// 구간별 별 ★1~3 (Modes.md 3장). 조건은 따로따로 딴다. 한 번 딴 별은 사라지지 않는다.
    /// 던전: ★1 클리어 · ★2 맞은 횟수 N번 이하 · ★3 전설 스킬 없이 클리어
    /// 보스: ★1 격파 · ★2 맞은 횟수 N번 이하 · ★3 서로 다른 기록 2개로 각각 격파
    /// 별 5개마다 상자 (Modes 3-4).
    /// </summary>
    public static class Stars
    {
        public const string Dungeon = "dungeon";
        public const string Boss = "boss";

        /// <summary>던전 ★2: 구간 1~5 맞은 횟수 한도</summary>
        private static readonly int[] DungeonHitLimits = { 15, 13, 11, 10, 8 };

        public static string Key(int chapter, string mode, int star) => $"c{chapter}.{mode}.{star}";

        public static bool Has(SaveData d, int chapter, string mode, int star) => d.stars.Contains(Key(chapter, mode, star));

        public static int Count(SaveData d, int chapter, string mode)
        {
            int n = 0;
            for (int star = 1; star <= 3; star++)
            {
                if (Has(d, chapter, mode, star)) n++;
            }

            return n;
        }

        public static int DungeonHitLimit(int chapter) => DungeonHitLimits[Mathf.Clamp(chapter - 1, 0, DungeonHitLimits.Length - 1)];

        /// <summary>"★★☆" 형태</summary>
        public static string Display(int count) => new string('★', Mathf.Clamp(count, 0, 3)) + new string('☆', 3 - Mathf.Clamp(count, 0, 3));

        /// <summary>던전이 끝났을 때. 새로 딴 별의 설명을 돌려준다.</summary>
        public static List<string> EvaluateDungeon(SaveData d, int chapter, bool cleared, int hits, bool choseLegendary)
        {
            var earned = new List<string>();
            if (!cleared) return earned;

            int limit = DungeonHitLimit(chapter);
            TryEarn(d, chapter, Dungeon, 1, true, "구간 클리어", earned);
            TryEarn(d, chapter, Dungeon, 2, hits <= limit, $"맞은 횟수 {limit}번 이하로 클리어", earned);
            TryEarn(d, chapter, Dungeon, 3, !choseLegendary, "전설 스킬 없이 클리어", earned);
            return earned;
        }

        /// <summary>보스전이 끝났을 때 (record.bossesDefeated는 이미 갱신된 상태로 부른다)</summary>
        public static List<string> EvaluateBoss(SaveData d, int chapter, string bossId, bool victory, int hits, int star2MaxHits,
            RunRecord record, SkillDatabase database)
        {
            var earned = new List<string>();
            if (!victory) return earned;

            TryEarn(d, chapter, Boss, 1, true, "보스 격파", earned);
            TryEarn(d, chapter, Boss, 2, hits <= star2MaxHits, $"맞은 횟수 {star2MaxHits}번 이하로 격파", earned);
            TryEarn(d, chapter, Boss, 3, HasDifferentWinningRecord(d, bossId, record, database), "서로 다른 기록 2개로 격파", earned);
            return earned;
        }

        private static void TryEarn(SaveData d, int chapter, string mode, int star, bool condition, string description, List<string> earned)
        {
            if (!condition || Has(d, chapter, mode, star)) return;
            d.stars.Add(Key(chapter, mode, star));
            earned.Add($"★{star} {description}");
        }

        /// <summary>
        /// 이 보스를 깬 다른 기록이 있고, 액티브(조합 포함) 구성이 2개 이상 다른가 (Modes 3-3).
        /// "2개 이상 다르다" = 한쪽에만 있는 액티브를 합쳐서 2개 이상 (하나를 바꾸면 2개 차이).
        /// 패시브만 바꾼 복사본이나 같은 빌드를 다시 저장한 기록은 같은 기록으로 본다.
        /// </summary>
        public static bool HasDifferentWinningRecord(SaveData d, string bossId, RunRecord record, SkillDatabase database)
        {
            if (record == null) return false;
            HashSet<string> mine = ActiveIds(record, database);
            foreach (RunRecord other in d.records)
            {
                if (other == record || other.id == record.id || !other.bossesDefeated.Contains(bossId)) continue;
                var difference = new HashSet<string>(mine);
                difference.SymmetricExceptWith(ActiveIds(other, database));
                if (difference.Count >= 2) return true;
            }

            return false;
        }

        private static HashSet<string> ActiveIds(RunRecord record, SkillDatabase database)
        {
            var ids = new HashSet<string>();
            foreach (RecordSkill s in record.skills)
            {
                SkillData data = database != null ? database.Find(s.id) : null;
                if (data != null && data.Kind != SkillKind.Passive) ids.Add(s.id);
            }

            return ids;
        }

        // ---------------- 별 상자 (별 5개마다) ----------------

        public static int BoxesEarned(SaveData d) => d.stars.Count / 5;

        /// <summary>받을 상자를 다 받는다. 받은 게 없으면 빈 보상</summary>
        public static Reward ClaimBoxes(SaveData d)
        {
            Reward total = default;
            while (d.starBoxesClaimed < BoxesEarned(d))
            {
                d.starBoxesClaimed++;
                total += Box(d.starBoxesClaimed);
            }

            if (!total.IsEmpty) Wallet.Grant(d, total);
            return total;
        }

        /// <summary>Modes 3-4 표: 5 / 10 / 15 / 20 / 25 / 30개</summary>
        public static Reward Box(int index)
        {
            switch (index)
            {
                case 1: return new Reward { Gold = 5000, Gems = 100 };
                case 2: return new Reward { Tickets = 1, Gems = 100 };
                case 3: return new Reward { GearShards = 30, Gems = 100 };
                case 4: return new Reward { Tickets = 1, Gems = 100 };
                case 5: return new Reward { GearShards = 30, Gems = 100 };
                case 6: return new Reward { Tickets = 1, Gems = 100 }; // + 칭호 "역사의 정복자" (칭호 시스템은 나중에)
                default: return new Reward { Gems = 100 };
            }
        }
    }
}
