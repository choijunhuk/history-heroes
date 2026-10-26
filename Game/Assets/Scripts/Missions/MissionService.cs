using System;
using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;

namespace HistoryHeroes.Missions
{
    /// <summary>
    /// 미션·업적 규칙 (Missions 2·3·7장). 저장 데이터만 다루고(화면·전투와 따로), 시각은 밖에서 받는다 → 테스트가 쉽다.
    /// - 일일: 고정 2 + 그날 4개 (요일별 모드 미션 최대 1개, 4개 합계를 70에 맞춰 하루 합계가 늘 100), 하루 1번 바꾸기, 05:00 초기화
    /// - 주간: 6개 (각 20), 월요일 05:00 초기화, 못 쓰는 미션은 대체
    /// - 업적: 영구, 단계별, 보석 (누르면 받는다)
    /// </summary>
    public static class MissionService
    {
        /// <summary>미션을 만들 때 필요한 바깥 정보 (영웅 해금·이름, 조합 수)</summary>
        public sealed class Context
        {
            public Func<string, bool> HeroUnlocked;
            public Func<string, string> HeroName;
            public int TotalRecipes;
            public Func<int> CountHeroes;
        }

        // ---------------------------------------------------------------- 날짜

        private static DateTimeOffset Shift(DateTimeOffset now, int resetHour) => now.AddHours(-resetHour);

        /// <summary>05:00에 바뀌는 날 ("2026-10-26")</summary>
        public static string DayKey(DateTimeOffset now, int resetHour) => Shift(now, resetHour).ToString("yyyy-MM-dd");

        /// <summary>월요일 05:00에 바뀌는 주 (그 주 월요일 날짜)</summary>
        public static string WeekKey(DateTimeOffset now, int resetHour)
        {
            DateTime d = Shift(now, resetHour).Date;
            int sinceMonday = ((int)d.DayOfWeek + 6) % 7;
            return d.AddDays(-sinceMonday).ToString("yyyy-MM-dd");
        }

        public static MissionDays DayFlag(DateTimeOffset now, int resetHour)
        {
            switch (Shift(now, resetHour).DayOfWeek)
            {
                case DayOfWeek.Monday: return MissionDays.Mon;
                case DayOfWeek.Tuesday: return MissionDays.Tue;
                case DayOfWeek.Wednesday: return MissionDays.Wed;
                case DayOfWeek.Thursday: return MissionDays.Thu;
                case DayOfWeek.Friday: return MissionDays.Fri;
                case DayOfWeek.Saturday: return MissionDays.Sat;
                default: return MissionDays.Sun;
            }
        }

        private static int DayNumber(DateTimeOffset now, int resetHour) => (int)(Shift(now, resetHour).Date - new DateTime(2026, 1, 1)).TotalDays;

        /// <summary>날·주가 바뀌었으면 새로 만든다. 바꿨으면 true</summary>
        public static bool Ensure(SaveData save, MissionTable table, DateTimeOffset now, Context ctx)
        {
            if (table == null) return false;
            bool changed = false;
            string day = DayKey(now, table.resetHour);
            if (save.missions.dayKey != day)
            {
                GenerateDaily(save, table, now, ctx);
                changed = true;
            }

            string week = WeekKey(now, table.resetHour);
            if (save.missions.weekKey != week)
            {
                GenerateWeekly(save, table, now, ctx);
                changed = true;
            }

            return changed;
        }

        public static bool GateOpen(SaveData save, MissionGate gate)
        {
            switch (gate)
            {
                case MissionGate.None: return true;
                case MissionGate.Survival: return save.unlockedChapter >= 3;
                case MissionGate.Equipment: return Equipment.EquipmentInventory.Unlocked(save);
                case MissionGate.BossRecord: return save.records.Count > 0;
                case MissionGate.RiftDaily: return Modes.RiftRules.Unlocked(save);
                default: return false;
            }
        }

        // ---------------------------------------------------------------- 일일

        /// <summary>그날의 미션 후보 (열린 것, 모드 미션은 오늘 요일에 나올 수 있는 것)</summary>
        public static List<MissionDef> DailyCandidates(SaveData save, MissionTable table, MissionDays today)
        {
            var list = new List<MissionDef>();
            foreach (MissionDef m in table.dailyPool)
            {
                if (m == null || !GateOpen(save, m.gate)) continue;
                if (m.modeMission && (m.days & today) == 0) continue;
                list.Add(m);
            }

            return list;
        }

        public static void GenerateDaily(SaveData save, MissionTable table, DateTimeOffset now, Context ctx)
        {
            MissionSave ms = save.missions;
            ms.dayKey = DayKey(now, table.resetHour);
            ms.daily.Clear();
            ms.dailyPoints = 0;
            ms.dailyChests.Clear();
            ms.rerollUsed = false;

            foreach (MissionDef m in table.dailyFixed) ms.daily.Add(Entry(m, m.points, null, ctx));

            MissionDays today = DayFlag(now, table.resetHour);
            List<MissionDef> candidates = DailyCandidates(save, table, today);
            var random = new Random(StableHash(ms.dayKey));
            Shuffle(candidates, random);

            var picked = new List<MissionDef>();
            bool mode = false;
            foreach (MissionDef m in candidates)
            {
                if (picked.Count >= table.dailyRandomCount) break;
                if (m.modeMission && mode) continue; // 모드 미션은 하루 최대 1개
                mode |= m.modeMission;
                picked.Add(m);
            }

            var basePoints = new int[picked.Count];
            for (int i = 0; i < picked.Count; i++) basePoints[i] = picked[i].points;
            int[] points = NormalizePoints(basePoints, table.dailyRandomTotal);

            string hero = HeroOfDay(table, now, ctx);
            for (int i = 0; i < picked.Count; i++) ms.daily.Add(Entry(picked[i], points[i], picked[i].heroOfDay ? hero : null, ctx));
        }

        /// <summary>오늘의 영웅: 날마다 차례로. 아직 안 연 영웅이면 null (= 아무 영웅, 대체 규칙 a)</summary>
        public static string HeroOfDay(MissionTable table, DateTimeOffset now, Context ctx)
        {
            if (table.heroOrder.Count == 0) return null;
            int n = DayNumber(now, table.resetHour);
            string hero = table.heroOrder[((n % table.heroOrder.Count) + table.heroOrder.Count) % table.heroOrder.Count];
            return ctx?.HeroUnlocked != null && ctx.HeroUnlocked(hero) ? hero : null;
        }

        private static MissionProgress Entry(MissionDef m, int points, string param, Context ctx)
        {
            string title = m.Title(m.target);
            if (m.heroOfDay)
            {
                string name = param != null && ctx?.HeroName != null ? ctx.HeroName(param) : null;
                title = name != null ? $"오늘의 영웅 {name}(으)로 던전 1판" : "아무 영웅으로 던전 1판";
            }

            return new MissionProgress
            {
                id = m.id,
                eventKey = m.eventKey,
                title = title,
                target = Math.Max(1, m.target),
                points = points,
                param = param,
                distinct = m.distinct,
            };
        }

        /// <summary>
        /// 뽑힌 미션의 기준 포인트를 합계 total에 맞춘다: 비율대로 늘리거나 줄여 5 단위로 맞추고, 남는 차이는 가장 큰 미션에 (Missions 2-1).
        /// </summary>
        public static int[] NormalizePoints(int[] basePoints, int total)
        {
            var result = new int[basePoints.Length];
            if (basePoints.Length == 0) return result;
            int sum = 0;
            foreach (int p in basePoints) sum += Math.Max(1, p);

            // 비율대로 나눈 값을 5 단위로 내리고, 모자란 5점짜리는 소수 부분이 큰 미션부터 하나씩 준다
            var fraction = new double[basePoints.Length];
            int assigned = 0;
            for (int i = 0; i < basePoints.Length; i++)
            {
                double scaled = (double)Math.Max(1, basePoints[i]) * total / sum;
                result[i] = Math.Max(5, (int)Math.Floor(scaled / 5.0) * 5);
                fraction[i] = scaled - result[i];
                assigned += result[i];
            }

            while (total - assigned >= 5)
            {
                int best = 0;
                for (int i = 1; i < fraction.Length; i++)
                {
                    if (fraction[i] > fraction[best]) best = i;
                }

                result[best] += 5;
                fraction[best] -= 5.0;
                assigned += 5;
            }

            // 5로 안 떨어지는 나머지(또는 넘친 만큼)는 가장 큰 미션에
            int largest = 0;
            for (int i = 1; i < result.Length; i++)
            {
                if (result[i] > result[largest]) largest = i;
            }

            result[largest] += total - assigned;
            return result;
        }

        /// <summary>하루 1번, 아직 안 끝난 그날의 미션 하나를 같은 포인트의 다른 미션으로 (같은 요일 규칙 안에서)</summary>
        public static bool Reroll(SaveData save, MissionTable table, int index, DateTimeOffset now, Context ctx)
        {
            MissionSave ms = save.missions;
            int fixedCount = table.dailyFixed.Count;
            if (ms.rerollUsed || index < fixedCount || index >= ms.daily.Count || ms.daily[index].done) return false;

            MissionDays today = DayFlag(now, table.resetHour);
            bool otherMode = false;
            for (int i = fixedCount; i < ms.daily.Count; i++)
            {
                if (i == index) continue;
                MissionDef d = table.FindDaily(ms.daily[i].id);
                if (d != null && d.modeMission) otherMode = true;
            }

            var options = new List<MissionDef>();
            foreach (MissionDef m in DailyCandidates(save, table, today))
            {
                if (ms.daily.Exists(e => e.id == m.id)) continue;
                if (m.modeMission && otherMode) continue;
                options.Add(m);
            }

            if (options.Count == 0) return false;
            var random = new Random(StableHash(ms.dayKey + "/reroll"));
            MissionDef pick = options[random.Next(options.Count)];
            ms.daily[index] = Entry(pick, ms.daily[index].points, pick.heroOfDay ? HeroOfDay(table, now, ctx) : null, ctx);
            ms.rerollUsed = true;
            return true;
        }

        // ---------------------------------------------------------------- 주간

        public static void GenerateWeekly(SaveData save, MissionTable table, DateTimeOffset now, Context ctx)
        {
            MissionSave ms = save.missions;
            ms.weekKey = WeekKey(now, table.resetHour);
            ms.weekly.Clear();
            ms.weeklyPoints = 0;
            ms.weeklyChests.Clear();
            foreach (MissionDef m in table.weekly)
            {
                MissionDef use = m;
                bool allRecipes = m.eventKey == MissionEvents.RecipeNew && ctx != null && ctx.TotalRecipes > 0 && save.discoveredRecipes.Count >= ctx.TotalRecipes;
                if ((!GateOpen(save, m.gate) || allRecipes) && !string.IsNullOrEmpty(m.fallbackId)) use = table.FindWeekly(m.fallbackId) ?? m;
                ms.weekly.Add(Entry(use, table.weeklyPointsEach, null, ctx));
            }
        }

        // ---------------------------------------------------------------- 진행

        /// <summary>사건 하나를 센다. 이번에 끝난 미션의 알림 줄을 돌려준다</summary>
        public static List<string> Raise(SaveData save, string key, int amount, string param)
        {
            var done = new List<string>();
            MissionSave ms = save.missions;
            foreach (MissionProgress m in ms.daily)
            {
                if (Advance(m, key, amount, param))
                {
                    ms.dailyPoints += m.points;
                    done.Add($"미션 완료  {m.title}  (+{m.points})");
                }
            }

            foreach (MissionProgress m in ms.weekly)
            {
                if (Advance(m, key, amount, param))
                {
                    ms.weeklyPoints += m.points;
                    done.Add($"주간 미션 완료  {m.title}");
                }
            }

            return done;
        }

        /// <summary>"지금 값"으로 맞추는 미션 (액티브 3개 Lv3, 패시브 칸 다 채우기): 판 중 가장 높았던 값</summary>
        public static List<string> SetAtLeast(SaveData save, string key, int value)
        {
            var done = new List<string>();
            foreach (MissionProgress m in save.missions.daily)
            {
                if (m.done || m.eventKey != key || value <= m.progress) continue;
                m.progress = Math.Min(m.target, value);
                if (m.progress >= m.target)
                {
                    m.done = true;
                    save.missions.dailyPoints += m.points;
                    done.Add($"미션 완료  {m.title}  (+{m.points})");
                }
            }

            return done;
        }

        private static bool Advance(MissionProgress m, string key, int amount, string param)
        {
            if (m.done || m.eventKey != key || amount <= 0) return false;
            if (!string.IsNullOrEmpty(m.param) && m.param != param) return false; // 오늘의 영웅 등
            if (m.distinct)
            {
                if (string.IsNullOrEmpty(param) || m.seen.Contains(param)) return false;
                m.seen.Add(param);
                m.progress = m.seen.Count;
            }
            else
            {
                m.progress = Math.Min(m.target, m.progress + amount);
            }

            if (m.progress < m.target) return false;
            m.progress = m.target;
            m.done = true;
            return true;
        }

        // ---------------------------------------------------------------- 상자

        public static bool CanClaimDaily(SaveData save, ChestDef chest) => chest != null && save.missions.dailyPoints >= chest.points && !save.missions.dailyChests.Contains(chest.points);
        public static bool CanClaimWeekly(SaveData save, ChestDef chest) => chest != null && save.missions.weeklyPoints >= chest.points && !save.missions.weeklyChests.Contains(chest.points);

        /// <summary>일일 상자 받기. 100 상자를 열면 주간 "상자 4일" 진행 (Missions 3-1)</summary>
        public static bool ClaimDaily(SaveData save, MissionTable table, ChestDef chest, string heroId, List<string> notices = null)
        {
            if (!CanClaimDaily(save, chest)) return false;
            save.missions.dailyChests.Add(chest.points);
            Grant(save, chest, heroId);
            if (chest.points >= LastPoints(table.dailyChests))
            {
                List<string> lines = Raise(save, MissionEvents.DailyBox100, 1, null);
                notices?.AddRange(lines);
            }

            return true;
        }

        public static bool ClaimWeekly(SaveData save, ChestDef chest, string heroId)
        {
            if (!CanClaimWeekly(save, chest)) return false;
            save.missions.weeklyChests.Add(chest.points);
            Grant(save, chest, heroId);
            return true;
        }

        private static int LastPoints(List<ChestDef> chests)
        {
            int max = 0;
            foreach (ChestDef c in chests) max = Math.Max(max, c.points);
            return max;
        }

        public static Reward RewardOf(ChestDef chest, string heroId)
        {
            return new Reward { Gold = chest.gold, Gems = chest.gems, GearShards = chest.gearShards, Tickets = chest.tickets, HeroShards = chest.heroShards, HeroId = heroId };
        }

        public static string RewardText(ChestDef chest, string heroId)
        {
            string text = RewardOf(chest, heroId).ToString();
            if (chest.ancientShards > 0) text += (text.Length > 0 ? " · " : "") + $"고대 파편 +{chest.ancientShards}";
            return text;
        }

        private static void Grant(SaveData save, ChestDef chest, string heroId)
        {
            Wallet.Grant(save, RewardOf(chest, heroId));
            if (chest.ancientShards > 0) save.AddItem(Relics.RelicTable.AncientShard, chest.ancientShards);
        }

        // ---------------------------------------------------------------- 업적

        public static int AchievementProgress(SaveData save, AchievementDef def, Context ctx)
        {
            MissionCounters c = save.missions.counters;
            switch (def.kind)
            {
                case AchievementKind.BossChapters: return Math.Max(0, save.unlockedChapter - 1);
                case AchievementKind.Stars: return save.stars.Count;
                case AchievementKind.BasicKills: return (int)Math.Min(int.MaxValue, c.basicKills);
                case AchievementKind.NoHitRooms: return c.noHitRooms;
                case AchievementKind.EliteKills: return c.elites;
                case AchievementKind.Recipes: return save.discoveredRecipes.Count;
                case AchievementKind.Legendary: return c.legendary ? 1 : 0;
                case AchievementKind.NoLegendChapters: return c.noLegendChapters.Count;
                case AchievementKind.HeroesUnlocked: return ctx?.CountHeroes != null ? ctx.CountHeroes() : 0;
                case AchievementKind.HeroLevel:
                    int best = 0;
                    foreach (HeroProgress h in save.heroProgress) best = Math.Max(best, h.level);
                    return best;
                case AchievementKind.AllHeroesChapter3: return c.chapter3Heroes.Count;
                case AchievementKind.RecordsSaved: return save.relics.recordsSavedTotal;
                case AchievementKind.BossRecords: return Math.Min(c.bossWinRecords.Count, save.defeatedBosses.Count);
                case AchievementKind.SurvivalClears: return save.survival.clears;
                case AchievementKind.RiftFloor: return save.rift.bestEver;
                case AchievementKind.DailyDays: return save.dailyChallenge.participations;
                default: return 0;
            }
        }

        public static int ClaimedTiers(SaveData save, string id)
        {
            AchievementClaim c = save.missions.achievements.Find(a => a.id == id);
            return c != null ? c.tiers : 0;
        }

        /// <summary>단계 목표 (-1 = 전부 → 조합 수)</summary>
        public static int Target(AchievementDef def, int tier, Context ctx)
        {
            int t = def.targets[Math.Min(tier, def.targets.Length - 1)];
            return t < 0 ? Math.Max(1, ctx != null ? ctx.TotalRecipes : 1) : t;
        }

        public static bool CanClaim(SaveData save, AchievementDef def, Context ctx)
        {
            int tier = ClaimedTiers(save, def.id);
            return tier < def.targets.Length && AchievementProgress(save, def, ctx) >= Target(def, tier, ctx);
        }

        public static bool ClaimAchievement(SaveData save, AchievementDef def, Context ctx)
        {
            if (!CanClaim(save, def, ctx)) return false;
            int tier = ClaimedTiers(save, def.id);
            AchievementClaim c = save.missions.achievements.Find(a => a.id == def.id);
            if (c == null)
            {
                c = new AchievementClaim { id = def.id };
                save.missions.achievements.Add(c);
            }

            c.tiers = tier + 1;
            int gems = def.gems != null && def.gems.Length > 0 ? def.gems[Math.Min(tier, def.gems.Length - 1)] : 0;
            Wallet.Grant(save, new Reward { Gems = gems });
            return true;
        }

        /// <summary>받을 수 있는 것 수 (로비 버튼 표시: 상자 + 업적 단계)</summary>
        public static int ClaimableCount(SaveData save, MissionTable table, Context ctx)
        {
            if (table == null) return 0;
            int n = 0;
            foreach (ChestDef c in table.dailyChests) if (CanClaimDaily(save, c)) n++;
            foreach (ChestDef c in table.weeklyChests) if (CanClaimWeekly(save, c)) n++;
            foreach (AchievementDef a in table.achievements) if (CanClaim(save, a, ctx)) n++;
            return n;
        }

        // ---------------------------------------------------------------- 도우미

        /// <summary>문자열 → 같은 씨앗 (string.GetHashCode는 실행마다 달라질 수 있어서)</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char ch in s ?? string.Empty) h = h * 31 + ch;
                return h;
            }
        }

        private static void Shuffle<T>(List<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
