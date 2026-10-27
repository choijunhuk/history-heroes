using System;
using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Missions
{
    /// <summary>무엇으로 끝났는지 보나 (대부분은 저장 상태, 몇 개는 사건 카운터)</summary>
    public enum NewcomerCheck
    {
        Tutorial,      // 튜토리얼 끝내기
        DungeonClear,  // value 구간 던전 클리어 (★1)
        BossBeaten,    // value 구간 보스 격파
        BossTried,     // value 구간 보스 격파 또는 3번 도전 (전체 검토 11번: 느린 플레이어도 받게)
        RecordSaved,   // 첫 기록 저장
        EquipSlots,    // 한 영웅의 장비 value칸 채우기
        HeroDungeon,   // param 영웅으로 던전 1판 (사건)
        Fusion,        // 조합 1번 (사건)
        HeroLevel,     // 한 영웅 Lv value
        GearUpgrade,   // 장비 칸 강화 1회
        RecordsOwned,  // 기록 value개 보유
        DailyBox100,   // 일일 미션 상자 100 열기 (사건)
        RuneEquipped,  // 룬 장착
        Survival,      // 서바이벌 value초 생존
        LowHitClear,   // 피격 10회 이하로 던전 클리어 (사건)
        Training,      // 수련 1회
        Recipes,       // 서로 다른 조합 value개 발견
        DailyJoined,   // 일일 도전 참여
        Stars,         // 별 value개
    }

    public sealed class NewcomerMission
    {
        public string Id;
        public int Day;
        public string Title;
        public NewcomerCheck Check;
        public int Value = 1;
        public string Param;
        /// <summary>이 미션의 모드가 열리는 구간 (안 열렸으면 "던전 1판 클리어"로 대신, 보상 그대로). 0 = 없음</summary>
        public int GateChapter;
    }

    /// <summary>
    /// 신규 유저 7일 미션 (Missions 6장): 튜토리얼 보스를 깬 날부터 **들어온 날마다** 한 칸씩 열린다 (05:00 기준).
    /// 지난 칸은 계속 할 수 있고, 기한은 없다 (전체 검토 11번: 14일 기한은 느린 플레이어의 보상만 없앴다).
    /// 칸의 미션을 다 하면 그 칸 보상, 7칸을 다 받으면 완료 보너스.
    /// </summary>
    public static class Newcomer
    {
        public const int Days = 7;
        public const int ResetHour = 5;
        /// <summary>보스 "도전" 미션: 이만큼 도전하면 못 깨도 된다</summary>
        public const int BossTries = 3;

        public const string LowHitEvent = "clear_hits10";
        public const string FallbackTitle = "던전 1판 클리어";

        public static readonly NewcomerMission[] Missions =
        {
            M("d1_tutorial", 1, "튜토리얼 끝내기", NewcomerCheck.Tutorial),
            M("d1_dungeon1", 1, "1구간 던전 클리어", NewcomerCheck.DungeonClear, 1),
            M("d1_record", 1, "첫 기록 저장", NewcomerCheck.RecordSaved),
            M("d1_boss1", 1, "1구간 보스 격파", NewcomerCheck.BossBeaten, 1),
            M("d2_level3", 2, "영웅 Lv3 달성", NewcomerCheck.HeroLevel, 3), // 전체 검토 11번: 장비 채우기는 첫날 목표로 어색
            M("d2_guan", 2, "관우로 던전 1판", NewcomerCheck.HeroDungeon, 1, "guan"),
            M("d2_fusion", 2, "조합 1번 만들기", NewcomerCheck.Fusion),
            M("d3_dungeon2", 3, "2구간 던전 클리어", NewcomerCheck.DungeonClear, 2),
            M("d3_level5", 3, "영웅 Lv5 달성", NewcomerCheck.HeroLevel, 5),
            M("d3_upgrade", 3, "장비 강화 1회", NewcomerCheck.GearUpgrade),
            M("d4_boss2", 4, "2구간 보스 격파 (또는 3번 도전)", NewcomerCheck.BossTried, 2),
            M("d4_records", 4, "기록 2개 보유", NewcomerCheck.RecordsOwned, 2),
            M("d4_box", 4, "일일 미션 상자(100) 열기", NewcomerCheck.DailyBox100),
            M("d5_napoleon", 5, "나폴레옹으로 던전 1판", NewcomerCheck.HeroDungeon, 1, "napoleon"),
            M("d5_rune", 5, "룬 장착", NewcomerCheck.RuneEquipped),
            M("d5_survival", 5, "서바이벌 3분 생존", NewcomerCheck.Survival, 180, gate: 2), // 1구간 보스 격파 뒤 열림
            M("d5_lowhit", 5, "피격 10회 이하로 던전 클리어", NewcomerCheck.LowHitClear),
            M("d6_dungeon3", 6, "3구간 던전 클리어", NewcomerCheck.DungeonClear, 3),
            M("d6_training", 6, "수련 1회", NewcomerCheck.Training),
            M("d6_recipes", 6, "서로 다른 조합 2개 발견", NewcomerCheck.Recipes, 2),
            M("d7_boss3", 7, "3구간 보스 격파 (또는 3번 도전)", NewcomerCheck.BossTried, 3),
            M("d7_daily", 7, "일일 도전 참여", NewcomerCheck.DailyJoined, gate: 4),
            M("d7_level10", 7, "영웅 Lv10 달성", NewcomerCheck.HeroLevel, 10),
            M("d7_stars", 7, "별 6개 모으기", NewcomerCheck.Stars, 6),
        };

        public static readonly Reward[] DayRewards =
        {
            new Reward { Gems = 100, Gold = 3000 },
            new Reward { Gems = 80, Gold = 3000 },
            new Reward { Gems = 80, Tickets = 1 },
            new Reward { Gems = 80, Gold = 4000 },
            new Reward { Gems = 80, GearShards = 20 },
            new Reward { Gems = 80, Gold = 5000 },
            new Reward { Gems = 100, Tickets = 1 },
        };

        public static readonly Reward Bonus = new Reward { Gems = 100, Tickets = 3, GearShards = 40 };

        private static NewcomerMission M(string id, int day, string title, NewcomerCheck check, int value = 1, string param = null, int gate = 0)
        {
            return new NewcomerMission { Id = id, Day = day, Title = title, Check = check, Value = value, Param = param, GateChapter = gate };
        }

        public static bool Unlocked(SaveData s) => s.tutorialDone;

        /// <summary>
        /// 열렸으면 시작한 날을 적고, 들어온 날 수를 센다 (로비가 열릴 때마다 부른다). 바뀌었으면 true.
        /// 옛 저장(시작 날만 있고 센 날이 없음)은 달력으로 지난 날 수 + 1에서 시작한다 (최대 7)
        /// </summary>
        public static bool EnsureStarted(SaveData s, DateTimeOffset now)
        {
            if (!Unlocked(s)) return false;
            string today = MissionService.DayKey(now, ResetHour);
            if (string.IsNullOrEmpty(s.newcomer.startDay))
            {
                s.newcomer.startDay = today;
                s.newcomer.activeDays = 1;
                s.newcomer.lastActiveDay = today;
                return true;
            }

            if (s.newcomer.activeDays <= 0 && DateTime.TryParse(s.newcomer.startDay, out DateTime start))
            {
                int calendar = (int)(DateTime.Parse(today) - start).TotalDays + 1;
                s.newcomer.activeDays = Mathf.Clamp(calendar, 1, Days);
                s.newcomer.lastActiveDay = today;
                return true;
            }

            if (s.newcomer.lastActiveDay == today) return false;
            s.newcomer.lastActiveDay = today;
            s.newcomer.activeDays++;
            return true;
        }

        /// <summary>몇째 날까지 열렸나 (들어온 날 수 − 1, 첫날 = 0). 시작 안 했으면 -1</summary>
        public static int DayIndex(SaveData s, DateTimeOffset now)
        {
            if (string.IsNullOrEmpty(s.newcomer.startDay)) return -1;
            return Mathf.Max(0, s.newcomer.activeDays - 1);
        }

        /// <summary>기한 없음 (전체 검토 11번). 예전 호출 자리를 위해 남긴다</summary>
        public static bool Expired(SaveData s, DateTimeOffset now) => false;

        public static bool AllClaimed(SaveData s) => s.newcomer.bonusClaimed;

        /// <summary>로비에 보일까 (시작했고, 기한 안이고, 다 받지 않았다)</summary>
        public static bool Visible(SaveData s, DateTimeOffset now) => DayIndex(s, now) >= 0 && !Expired(s, now) && !AllClaimed(s);

        public static bool DayOpen(SaveData s, int day, DateTimeOffset now)
        {
            int index = DayIndex(s, now);
            return index >= 0 && index >= day - 1 && !Expired(s, now);
        }

        public static IEnumerable<NewcomerMission> OfDay(int day)
        {
            foreach (NewcomerMission m in Missions)
            {
                if (m.Day == day) yield return m;
            }
        }

        /// <summary>모드가 아직 안 열려서 "던전 1판 클리어"로 대신하나</summary>
        public static bool UsesFallback(SaveData s, NewcomerMission m) => m.GateChapter > 0 && s.unlockedChapter < m.GateChapter;

        public static string TitleOf(SaveData s, NewcomerMission m) => UsesFallback(s, m) ? FallbackTitle : m.Title;

        public static int Count(SaveData s, string id)
        {
            NamedCount c = s.newcomer.counts.Find(x => x.id == id);
            return c != null ? c.value : 0;
        }

        public static bool Done(SaveData s, NewcomerMission m)
        {
            if (UsesFallback(s, m)) return Count(s, MissionEvents.DungeonClear) >= 1;
            switch (m.Check)
            {
                case NewcomerCheck.Tutorial: return s.tutorialDone;
                case NewcomerCheck.DungeonClear: return Stars.Has(s, m.Value, Stars.Dungeon, 1);
                case NewcomerCheck.BossBeaten: return s.unlockedChapter > m.Value;
                case NewcomerCheck.BossTried: return s.unlockedChapter > m.Value || Count(s, $"{MissionEvents.BossTry}:{m.Value}") >= BossTries;
                case NewcomerCheck.RecordSaved: return s.records.Count > 0 || s.relics.recordsSavedTotal > 0;
                case NewcomerCheck.EquipSlots:
                    int best = 0;
                    foreach (HeroLoadout l in s.equipment.loadouts)
                    {
                        int n = 0;
                        foreach (string uid in l.uids)
                        {
                            if (!string.IsNullOrEmpty(uid)) n++;
                        }

                        best = Mathf.Max(best, n);
                    }

                    return best >= m.Value;
                case NewcomerCheck.HeroDungeon: return Count(s, $"{MissionEvents.HeroDungeon}:{m.Param}") >= m.Value;
                case NewcomerCheck.Fusion: return Count(s, MissionEvents.Fusion) >= 1;
                case NewcomerCheck.HeroLevel:
                    int level = 0;
                    foreach (HeroProgress h in s.heroProgress) level = Mathf.Max(level, h.level);
                    return level >= m.Value;
                case NewcomerCheck.GearUpgrade: return s.equipment.slotLevels.Exists(v => v > 0);
                case NewcomerCheck.RecordsOwned: return s.records.Count >= m.Value;
                case NewcomerCheck.DailyBox100: return Count(s, MissionEvents.DailyBox100) >= 1;
                case NewcomerCheck.RuneEquipped: return s.runes.loadouts.Exists(l => l.ids.Exists(id => !string.IsNullOrEmpty(id)));
                case NewcomerCheck.Survival: return s.survival.longestSeconds >= m.Value;
                case NewcomerCheck.LowHitClear: return Count(s, LowHitEvent) >= 1;
                case NewcomerCheck.Training:
                    int sum = 0;
                    foreach (int t in s.training) sum += t;
                    return sum >= 1;
                case NewcomerCheck.Recipes: return s.discoveredRecipes.Count >= m.Value;
                case NewcomerCheck.DailyJoined: return s.dailyChallenge.participations >= 1;
                case NewcomerCheck.Stars: return s.stars.Count >= m.Value;
                default: return false;
            }
        }

        public static bool DayComplete(SaveData s, int day)
        {
            foreach (NewcomerMission m in OfDay(day))
            {
                if (!Done(s, m)) return false;
            }

            return true;
        }

        public static bool CanClaim(SaveData s, int day, DateTimeOffset now) => DayOpen(s, day, now) && !s.newcomer.claimedDays.Contains(day) && DayComplete(s, day);

        /// <summary>받을 수 있는 칸 수 (로비 빨간 점)</summary>
        public static int Claimable(SaveData s, DateTimeOffset now)
        {
            int n = 0;
            for (int d = 1; d <= Days; d++)
            {
                if (CanClaim(s, d, now)) n++;
            }

            if (CanClaimBonus(s, now)) n++;
            return n;
        }

        /// <summary>칸 보상 받기. 받은 줄 (못 받으면 null)</summary>
        public static string ClaimDay(SaveData s, int day, DateTimeOffset now)
        {
            if (day < 1 || day > Days || !CanClaim(s, day, now)) return null;
            s.newcomer.claimedDays.Add(day);
            Reward r = DayRewards[day - 1];
            Wallet.Grant(s, r);
            return $"7일 미션 {day}일째  {r}";
        }

        public static bool CanClaimBonus(SaveData s, DateTimeOffset now) => !s.newcomer.bonusClaimed && !Expired(s, now) && s.newcomer.claimedDays.Count >= Days;

        public static string ClaimBonus(SaveData s, DateTimeOffset now)
        {
            if (!CanClaimBonus(s, now)) return null;
            s.newcomer.bonusClaimed = true;
            Wallet.Grant(s, Bonus);
            return $"7일 미션 전부 완료  {Bonus}";
        }

        /// <summary>사건 카운터 (MissionEvents.Raise가 부른다). 시작 전·기한 뒤에는 세지 않는다</summary>
        public static void OnEvent(SaveData s, string key, int amount, string param, DateTimeOffset now)
        {
            if (!Unlocked(s) || DayIndex(s, now) < 0 || Expired(s, now) || amount <= 0) return;
            switch (key)
            {
                case MissionEvents.HeroDungeon:
                    if (!string.IsNullOrEmpty(param)) Add(s, $"{MissionEvents.HeroDungeon}:{param}", amount);
                    break;
                case MissionEvents.BossTry:
                    if (!string.IsNullOrEmpty(param)) Add(s, $"{MissionEvents.BossTry}:{param}", amount); // 구간마다 도전 수
                    break;
                case MissionEvents.Fusion:
                case MissionEvents.DailyBox100:
                case MissionEvents.DungeonClear:
                case LowHitEvent:
                    Add(s, key, amount);
                    break;
            }
        }

        private static void Add(SaveData s, string id, int amount)
        {
            NamedCount c = s.newcomer.counts.Find(x => x.id == id);
            if (c == null) s.newcomer.counts.Add(new NamedCount { id = id, value = amount });
            else c.value += amount;
        }
    }
}
