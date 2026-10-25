using System;
using System.Collections.Generic;

namespace HistoryHeroes.Records
{
    /// <summary>
    /// 저장 파일 전체 (JSON 한 개). GameDesign 12-2 "ID와 버전": 저장 구조 버전과 밸런스 버전을 따로 둔다.
    /// JsonUtility로 저장하므로 필드는 public, 빈 칸(null)은 쓰지 않는다.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>저장 구조가 바뀌면 올린다. 읽을 때 옛 구조면 옮겨 담고(Migrate), 더 새 구조면 읽지 않고 원본을 보존한다.</summary>
        /// 1: v0.2 기록·도감·구간 / 2: v0.2b 재화·하트·계정 레벨·별
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;

        /// <summary>마지막으로 저장한 게임의 밸런스 버전 (SkillDatabase.balanceVersion). 패치 감지용.</summary>
        public int balanceVersion;

        public long savedAtUnixSeconds;

        /// <summary>기록 슬롯 수. 처음 3개 (GameDesign 7-3, 임시값)</summary>
        public int recordSlots = 3;

        /// <summary>기록들. 슬롯 번호는 RunRecord.slot (0부터)</summary>
        public List<RunRecord> records = new List<RunRecord>();

        /// <summary>조합 도감: 한 번이라도 만든 레시피 ID</summary>
        public List<string> discoveredRecipes = new List<string>();

        /// <summary>열린 구간 (1부터). 구간 보스를 처음 깨면 +1 (GameDesign 7-4)</summary>
        public int unlockedChapter = 1;

        /// <summary>한 번이라도 깬 보스 ID</summary>
        public List<string> defeatedBosses = new List<string>();

        /// <summary>판 수 (통계·테스트용)</summary>
        public int dungeonRuns;
        public int bossRuns;

        // ---------------- v0.2b 경제 (Economy.md, Stats.md 7장, Modes.md 3장) ----------------
        public int gold;
        /// <summary>보석은 무료분과 유료분을 따로 센다 (Economy 2-1). 쓸 때는 무료분부터</summary>
        public int gemsFree;
        public int gemsPaid;

        public int hearts = 5;
        /// <summary>하트 충전을 마지막으로 계산한 시각 (유닉스 초). 가득 차 있으면 의미 없음</summary>
        public long heartsUpdatedAt;
        /// <summary>첫날 처음 3판은 하트를 쓰지 않는다 (Economy 3-2)</summary>
        public string firstPlayDate;
        public int freeRunsUsed;

        public int accountLevel = 1;
        public int accountExp;

        /// <summary>딴 별. "c1.dungeon.2" 형태 (구간·모드·별 번호). 한 번 딴 별은 사라지지 않는다 (Modes 3-1)</summary>
        public List<string> stars = new List<string>();
        /// <summary>받은 별 상자 수 (별 5개마다 1개, Modes 3-4)</summary>
        public int starBoxesClaimed;

        /// <summary>아직 쓰는 화면이 없는 보조 재료 (고급 소환권, 장비 조각, 영웅 조각…). 나중 시스템이 쓴다</summary>
        public List<ItemCount> items = new List<ItemCount>();

        public DailyCounters daily = new DailyCounters();

        // ---------------- v0.3 영웅 (Heroes.md) ----------------
        /// <summary>로비에서 고른 영웅 (던전에 들고 들어감)</summary>
        public string selectedHero = "khan";
        /// <summary>보석으로 바로 해금한 영웅 (보스 격파로 열린 영웅은 unlockedChapter로 판단)</summary>
        public List<string> unlockedHeroes = new List<string>();

        public int ItemCount(string itemId)
        {
            foreach (ItemCount i in items)
            {
                if (i.id == itemId) return i.count;
            }

            return 0;
        }

        public void AddItem(string itemId, int amount)
        {
            if (amount == 0 || string.IsNullOrEmpty(itemId)) return;
            foreach (ItemCount i in items)
            {
                if (i.id != itemId) continue;
                i.count += amount;
                return;
            }

            items.Add(new ItemCount { id = itemId, count = amount });
        }

        /// <summary>오늘 날짜가 아니면 하루 한도를 새로 시작한다</summary>
        public DailyCounters Today(string today)
        {
            daily ??= new DailyCounters();
            if (daily.date != today) daily = new DailyCounters { date = today };
            return daily;
        }

        public RunRecord RecordInSlot(int slot)
        {
            foreach (RunRecord r in records)
            {
                if (r.slot == slot) return r;
            }

            return null;
        }

        public int FirstFreeSlot()
        {
            for (int i = 0; i < recordSlots; i++)
            {
                if (RecordInSlot(i) == null) return i;
            }

            return -1;
        }

        public RunRecord FindRecord(string recordId)
        {
            foreach (RunRecord r in records)
            {
                if (r.id == recordId) return r;
            }

            return null;
        }
    }

    /// <summary>
    /// 기록 하나 (GameDesign 7-1). 스킬은 **ID + 레벨**만 저장한다 → 수치는 언제나 최신 데이터에서 가져온다 (7-5 패치 정책).
    /// </summary>
    [Serializable]
    public sealed class RunRecord
    {
        public string id;
        public int slot;
        public string heroId;
        /// <summary>화면 표시용 (영웅 이름이 바뀌어도 ID로 다시 찾는다)</summary>
        public string heroName;
        public List<RecordSkill> skills = new List<RecordSkill>();

        /// <summary>저장할 때의 용량. 패치로 비용이 올라 이걸 넘으면 다음 사용 때 다시 고르게 한다 (7-5).</summary>
        public int capacity;

        public int chapter = 1;
        public int roomReached;
        public int roomCount;
        public bool cleared;

        /// <summary>만든 시각 (UTC, ISO 8601)</summary>
        public string createdAt;
        public int balanceVersion;

        /// <summary>보스맵에서 이 기록으로 깬 보스 ID (화면 표시용)</summary>
        public List<string> bossesDefeated = new List<string>();

        /// <summary>
        /// 이 기록으로 이긴 보스와 그때 **실제로 싸운** 액티브 구성 (별 ★3 "서로 다른 기록 2개" 판정, Modes 3-3).
        /// 패치·다시 고르기로 기록이 바뀌어도 그때 싸운 구성으로 비교한다.
        /// </summary>
        public List<BossWin> bossWins = new List<BossWin>();
    }

    [Serializable]
    public sealed class BossWin
    {
        public string bossId;
        public List<string> actives = new List<string>();
    }

    [Serializable]
    public sealed class ItemCount
    {
        public string id;
        public int count;
    }

    /// <summary>하루 한도 (자정에 새로)</summary>
    [Serializable]
    public sealed class DailyCounters
    {
        public string date;
        /// <summary>보스 반복 격파 보상 (하루 3회, Economy 4장)</summary>
        public int bossRepeatRewards;
        /// <summary>광고로 하트 받기 (하루 3회)</summary>
        public int adHearts;
        /// <summary>보석으로 하트 채우기 (하루 5회)</summary>
        public int gemHearts;
        /// <summary>보스 최초 도전 패배 환급: 보스마다 하루 5회 (Economy 3-3)</summary>
        public List<ItemCount> heartRefunds = new List<ItemCount>();
    }

    [Serializable]
    public sealed class RecordSkill
    {
        public string id;
        public int level;

        /// <summary>조합 결과(진화·합체)인가</summary>
        public bool fusion;

        public RecordSkill() { }

        public RecordSkill(string id, int level, bool fusion)
        {
            this.id = id;
            this.level = level;
            this.fusion = fusion;
        }
    }
}
