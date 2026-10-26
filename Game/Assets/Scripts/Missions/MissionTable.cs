using System;
using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Missions
{
    /// <summary>미션이 나오려면 열려 있어야 하는 것 (Missions 2-2: 안 열린 모드의 미션은 풀에서 빠진다)</summary>
    public enum MissionGate
    {
        None,
        Survival,       // 2구간 보스 격파
        Equipment,      // 1구간 보스 격파
        BossRecord,     // 기록이 하나라도 있어야 보스맵에 간다
        Never,          // 아직 없는 모드 (무한의 균열·일일 도전 — v0.6)
    }

    [Flags]
    public enum MissionDays
    {
        None = 0,
        Mon = 1,
        Tue = 2,
        Wed = 4,
        Thu = 8,
        Fri = 16,
        Sat = 32,
        Sun = 64,
        Weekdays = Mon | Tue | Wed | Thu | Fri,
        All = Weekdays | Sat | Sun,
    }

    /// <summary>미션 하나의 정의 (Missions 10장: 사건 이름 + 목표 수 + 포인트)</summary>
    [Serializable]
    public sealed class MissionDef
    {
        public string id;
        [Tooltip("{0} = 목표 수")]
        public string title;
        public string eventKey;
        public int target = 1;
        public int points = 15;
        public MissionGate gate;
        [Tooltip("모드 전용 미션은 요일에 묶는다 (하루 최대 1개, Missions 2-1)")]
        public bool modeMission;
        public MissionDays days = MissionDays.All;
        [Tooltip("서로 다른 값(영웅 ID 등)만 센다 (\"서로 다른 영웅 2명\")")]
        public bool distinct;
        [Tooltip("오늘의 영웅 (매일 바뀜)")]
        public bool heroOfDay;
        [Tooltip("이 미션을 못 쓰면 대신 나오는 미션 ID (주간 대체 규칙)")]
        public string fallbackId;

        public string Title(int value) => string.Format(title, value);
    }

    /// <summary>활동 포인트 상자 (Missions 2-3·3-2)</summary>
    [Serializable]
    public sealed class ChestDef
    {
        public int points;
        public int gold;
        public int gems;
        public int gearShards;
        public int tickets;
        public int ancientShards;
        [Tooltip("고른 영웅의 영웅 조각")]
        public int heroShards;
    }

    public enum AchievementKind
    {
        BossChapters,      // 구간 정복
        Stars,             // 별 수집가
        BasicKills,        // 멈춤의 달인
        NoHitRooms,        // 그림자처럼
        EliteKills,        // 엘리트 사냥꾼
        Recipes,           // 조합 연구가 (목표 -1 = 전부)
        Legendary,         // 전설의 순간
        NoLegendChapters,  // 전설 없이도
        HeroesUnlocked,    // 드림팀
        HeroLevel,         // 노련한 영웅
        AllHeroesChapter3, // 만능
        RecordsSaved,      // 기록 보관가
        BossRecords,       // 맞춤 전략
        SurvivalClears,    // 생존자
    }

    /// <summary>업적 하나 (영구, 단계별 — Missions 7장)</summary>
    [Serializable]
    public sealed class AchievementDef
    {
        public string id;
        public string title;
        public string category;
        [Tooltip("{0} = 목표 수")]
        public string goal;
        public AchievementKind kind;
        [Tooltip("단계별 목표 (-1 = 전부)")]
        public int[] targets;
        [Tooltip("단계별 보석")]
        public int[] gems;
    }

    /// <summary>
    /// 미션·업적 표 (Missions 2·3·7장). 미션은 "사건을 세는 카운터"라서, 미션을 바꿀 때 전투 코드를 고치지 않는다 (10장).
    /// 초기화: 매일 05:00, 매주 월요일 05:00 (기기 시계 — 오프라인 게임, 서버 시간은 v1.0 결정 대기).
    /// </summary>
    [CreateAssetMenu(menuName = "History Heroes/Mission Table", fileName = "MissionTable")]
    public sealed class MissionTable : ScriptableObject
    {
        public int resetHour = 5;
        [Tooltip("그날의 미션 4개 합계 (고정 30 + 70 = 100)")]
        public int dailyRandomTotal = 70;
        public int dailyRandomCount = 4;
        public int weeklyPointsEach = 20;

        public List<MissionDef> dailyFixed = new List<MissionDef>();
        public List<MissionDef> dailyPool = new List<MissionDef>();
        public List<MissionDef> weekly = new List<MissionDef>();
        [Tooltip("주간 대체 미션 (fallbackId로 찾는다)")]
        public List<MissionDef> weeklyFallbacks = new List<MissionDef>();
        public List<ChestDef> dailyChests = new List<ChestDef>();
        public List<ChestDef> weeklyChests = new List<ChestDef>();
        public List<AchievementDef> achievements = new List<AchievementDef>();
        [Tooltip("오늘의 영웅 순서")]
        public List<string> heroOrder = new List<string> { "khan", "guan", "napoleon", "tesla" };

        public MissionDef FindDaily(string id) => dailyPool.Find(m => m.id == id) ?? dailyFixed.Find(m => m.id == id);
        public MissionDef FindWeekly(string id) => weekly.Find(m => m.id == id) ?? weeklyFallbacks.Find(m => m.id == id);
        public AchievementDef FindAchievement(string id) => achievements.Find(a => a.id == id);
    }
}
