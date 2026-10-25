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
        public const int CurrentSchemaVersion = 1;

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

        /// <summary>보스맵에서 이 기록으로 깬 보스 ID (별 ★3 "서로 다른 기록 2개" 판정용, Modes 3-3)</summary>
        public List<string> bossesDefeated = new List<string>();
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
