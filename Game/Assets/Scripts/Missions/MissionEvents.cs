using System;
using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using UnityEngine;

namespace HistoryHeroes.Missions
{
    /// <summary>
    /// 게임 쪽은 사건만 알린다 (Missions 10장: "이벤트를 듣는 카운터"): MissionEvents.Raise("kill").
    /// 여기서 날·주가 바뀌었는지 보고, 미션 진행·업적 카운터를 올린 뒤 저장할 것이 있다고 표시한다 (저장은 안전한 시점에 게임이).
    /// 보스 연습(보상 없음)은 세지 않는다.
    /// </summary>
    public static class MissionEvents
    {
        public const string Login = "login";
        public const string Run = "run";
        public const string Kill = "kill";
        public const string Elite = "elite";
        public const string BasicKill = "basic_kill";
        public const string NoHitRoom = "nohit_room";
        public const string Fusion = "fusion";
        public const string ActivesLv3 = "actives_lv3";
        public const string PassivesFull = "passives_full";
        public const string HeroDungeon = "hero_dungeon";
        public const string HeroLevel = "hero_level";
        public const string BossTry = "boss_try";
        public const string Survival5 = "survival_5min";
        public const string Record = "record";
        public const string Equip = "equip";
        public const string DailyBox100 = "daily_box100";
        public const string DungeonClear = "dungeon_clear";
        public const string BossWin = "boss_win";
        public const string RecipeNew = "recipe_new";
        public const string SurvivalSeconds = "survival_sec";
        public const string NoLegendClear = "nolegend_clear";
        public const string Legendary = "legendary";
        public const string RiftFloor5 = "rift_floor5";
        public const string DailyChallenge = "daily_challenge";

        /// <summary>미션 표 (로비·전투 장면이 열릴 때 넣는다)</summary>
        public static MissionTable Table { get; set; }
        public static MissionService.Context Context { get; set; }

        /// <summary>미션이 끝났다 ("미션 완료  적 300마리 처치  (+15)") — HUD·로비가 알림으로 띄운다</summary>
        public static event Action<string> Completed;

        private static bool Suppressed => GameSession.Practice;

        /// <summary>장면이 열릴 때: 미션 표 + 영웅 해금·이름 + 조합 수</summary>
        public static void Configure(MissionTable table, Heroes.HeroRoster roster, int totalRecipes)
        {
            Table = table;
            Context = new MissionService.Context
            {
                HeroUnlocked = id => roster != null && Heroes.HeroUnlock.IsUnlocked(SaveSystem.Data, roster.Find(id)),
                HeroName = id => roster != null && roster.Find(id) != null ? roster.Find(id).displayName : id,
                TotalRecipes = totalRecipes,
                CountHeroes = () =>
                {
                    if (roster == null) return 0;
                    int n = 0;
                    foreach (Heroes.HeroData h in roster.heroes)
                    {
                        if (Heroes.HeroUnlock.IsUnlocked(SaveSystem.Data, h)) n++;
                    }

                    return n;
                },
            };
        }

        /// <summary>날·주가 바뀌었으면 새 미션 (로비가 열릴 때도 부른다)</summary>
        public static void EnsureCurrent()
        {
            if (Table == null) return;
            if (MissionService.Ensure(SaveSystem.Data, Table, Clock.Now(), Context)) SaveSystem.MarkDirty();
        }

        public static void Raise(string key, int amount = 1, string param = null)
        {
            if (Suppressed || amount <= 0) return;
            SaveData save = SaveSystem.Data;
            EnsureCurrent();
            Count(save, key, amount);
            Notify(MissionService.Raise(save, key, amount, param));
            SaveSystem.MarkDirty();
        }

        /// <summary>"지금 값" 미션 (액티브 Lv3 개수, 패시브 칸 수)</summary>
        public static void SetAtLeast(string key, int value)
        {
            if (Suppressed) return;
            EnsureCurrent();
            List<string> lines = MissionService.SetAtLeast(SaveSystem.Data, key, value);
            if (lines.Count == 0) return;
            Notify(lines);
            SaveSystem.MarkDirty();
        }

        /// <summary>적이 쓰러졌다 (마지막 한 방이 기본 공격이면 "멈춤의 달인")</summary>
        public static void EnemyKilled(bool elite, bool basicLastHit)
        {
            Raise(Kill);
            if (elite) Raise(Elite);
            if (basicLastHit) Raise(BasicKill);
        }

        /// <summary>던전 한 판이 끝났다 (졌어도 "판"은 센다)</summary>
        public static void DungeonFinished(string heroId, int chapter, bool victory, bool choseLegendary)
        {
            if (Suppressed) return;
            Raise(Run);
            Raise(HeroDungeon, 1, heroId);
            if (!victory) return;
            Raise(DungeonClear, 1, heroId);
            MissionCounters c = SaveSystem.Data.missions.counters;
            if (!choseLegendary)
            {
                Raise(NoLegendClear);
                if (!c.noLegendChapters.Contains(chapter)) c.noLegendChapters.Add(chapter);
            }

            if (chapter >= 3 && !string.IsNullOrEmpty(heroId) && !c.chapter3Heroes.Contains(heroId)) c.chapter3Heroes.Add(heroId);
        }

        /// <summary>보스맵 한 판이 끝났다 (연습 제외)</summary>
        public static void BossFinished(bool victory, string recordId)
        {
            if (Suppressed) return;
            Raise(Run);
            Raise(BossTry);
            if (!victory) return;
            Raise(BossWin);
            MissionCounters c = SaveSystem.Data.missions.counters;
            if (!string.IsNullOrEmpty(recordId) && !c.bossWinRecords.Contains(recordId)) c.bossWinRecords.Add(recordId);
        }

        public static void SurvivalFinished(float seconds)
        {
            if (Suppressed) return;
            Raise(Run);
            Raise(SurvivalSeconds, Mathf.FloorToInt(seconds));
            if (seconds >= 300f) Raise(Survival5);
        }

        private static void Count(SaveData save, string key, int amount)
        {
            MissionCounters c = save.missions.counters;
            switch (key)
            {
                case BasicKill: c.basicKills += amount; break;
                case NoHitRoom: c.noHitRooms += amount; break;
                case Elite: c.elites += amount; break;
                case Legendary: c.legendary = true; break;
            }
        }

        private static void Notify(List<string> lines)
        {
            foreach (string line in lines) Completed?.Invoke(line);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Completed = null;
            Table = null;
            Context = null;
        }
    }
}
