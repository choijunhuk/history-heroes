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
        /// 1: v0.2 기록·도감·구간 / 2: v0.2b 재화·하트·계정 레벨·별 / 3: v0.4 장비·영웅 성장·수련·상자·상점
        /// <summary>4 (v0.5): 서바이벌·펫·미션 — 빈 상태로 시작 (Normalize가 채운다). 옛 빌드는 더 새 파일을 읽지 않고 멈춘다</summary>
        /// <summary>6: 카드 빼기 이력·남은 횟수와 목표 레시피 (옛 저장은 빼기 2회부터)</summary>
        /// <summary>7: 유물 뽑기 횟수·무료 날짜·중복 진행. 옛 저장의 빈 값은 모두 0/없음이 맞다.</summary>
        /// <summary>8: 장비 6칸. 앞의 무기·갑옷·장신구 번호는 그대로 두고 새 칸을 뒤에 붙인다.</summary>
        public const int CurrentSchemaVersion = 8;

        public int schemaVersion = CurrentSchemaVersion;

        /// <summary>마지막으로 저장한 게임의 밸런스 버전 (SkillDatabase.balanceVersion). 패치 감지용.</summary>
        public int balanceVersion;

        public long savedAtUnixSeconds;
        /// <summary>v1.0 저장 파일 해시 (SaveStore가 쓰고 확인한다. 비어 있으면 옛 파일)</summary>
        public string integrity;
        /// <summary>해시가 틀린 파일을 쓸 수밖에 없었던 횟수 (나중에 서버가 본다)</summary>
        public int integrityFailures;

        /// <summary>기록 슬롯 수. 처음 3개 (GameDesign 7-3, 임시값)</summary>
        public int recordSlots = 3;

        /// <summary>기록들. 슬롯 번호는 RunRecord.slot (0부터)</summary>
        public List<RunRecord> records = new List<RunRecord>();

        /// <summary>번호 칸과 따로 두는 임시 기록 (없거나 하나). 보관함 개수에는 세지 않는다.</summary>
        public List<RunRecord> tempRecords = new List<RunRecord>();

        public RunRecord TempRecord => tempRecords != null && tempRecords.Count > 0 ? tempRecords[0] : null;

        /// <summary>조합 도감: 한 번이라도 만든 레시피 ID</summary>
        public List<string> discoveredRecipes = new List<string>();

        /// <summary>발견한 레시피 중 목표로 고른 ID (최대 2개, 뽑기 확률은 그대로)</summary>
        public List<string> targetRecipes = new List<string>();

        /// <summary>열린 구간 (1부터). 구간 보스를 처음 깨면 +1 (GameDesign 7-4)</summary>
        public int unlockedChapter = 1;

        /// <summary>한 번이라도 깬 보스 ID</summary>
        public List<string> defeatedBosses = new List<string>();

        /// <summary>판 수 (통계·테스트용)</summary>
        public int dungeonRuns;
        /// <summary>10판 기념으로 보석 영웅 한 명을 무료로 열었나 (전체 검토 8번, 한 번만)</summary>
        public bool freeHeroClaimed;
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

        // 설정 (판 밖, 조사 A9 · UX 5장 접근성)
        public GameSettings settings = new GameSettings();

        // ----- v0.4 판 밖 성장 (Stats 4~7, Equipment, Gacha) -----
        public EquipmentSave equipment = new EquipmentSave();
        /// <summary>영웅별 레벨·별(각성). 없으면 Lv1 ★1. 🧩 영웅 조각은 items (Wallet.HeroShard)</summary>
        public List<HeroProgress> heroProgress = new List<HeroProgress>();
        /// <summary>수련 6종 레벨 (순서 = Meta.TrainingType). 계정 공용</summary>
        public List<int> training = new List<int>();
        public GachaSave gacha = new GachaSave();
        public ShopSave shop = new ShopSave();
        public RuneSave runes = new RuneSave();
        public RelicSave relics = new RelicSave();
        public RelicGachaSave relicGacha = new RelicGachaSave();

        // ----- v0.5 서바이벌 (GameDesign 8-3: 생존 시간 기록, 영웅별 최고 기록) -----
        public SurvivalSave survival = new SurvivalSave();

        // ----- v0.5 펫 (Pets 4·5장) -----
        public PetSave pets = new PetSave();

        // ----- v0.5 미션·업적 (Missions 2·3·7장) -----
        public MissionSave missions = new MissionSave();

        // ----- v0.6 무한의 균열 (Modes 4장) · 일일 도전 (Modes 5장) -----
        public RiftSave rift = new RiftSave();
        public DailySave dailyChallenge = new DailySave();
        /// <summary>v1.0 판 이어하기 (방 시작마다)</summary>
        public RunSnapshot run = new RunSnapshot();

        // ---------------------------------------------------------------- v1.0 (구조 5)
        /// <summary>튜토리얼 보스를 깼다 → 출석부·신규 7일 미션·기록 보관함이 열린다 (Systems 3장)</summary>
        public bool tutorialDone;
        /// <summary>튜토리얼 던전에서 남긴 기록 (보스 전에 앱이 꺼져도 튜토리얼 보스로 바로 간다)</summary>
        public string tutorialRecordId;
        public AttendanceSave attendance = new AttendanceSave();
        public NewcomerSave newcomer = new NewcomerSave();
        public PatrolSave patrol = new PatrolSave();
        /// <summary>사용자 ID (문의용 무작위 번호, 이름·연락처와 연결되지 않음)</summary>
        public string userId;
        /// <summary>얻은 칭호 (꾸미기는 v1.0 상점과 함께 — 지금은 모으기만)</summary>
        public List<string> titles = new List<string>();
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
            if (slot < 0 || slot >= recordSlots) return null;
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

        /// <summary>영웅 성장 칸 (없으면 만든다)</summary>
        public HeroProgress Hero(string heroId)
        {
            foreach (HeroProgress h in heroProgress)
            {
                if (h.heroId == heroId) return h;
            }

            var created = new HeroProgress { heroId = heroId };
            heroProgress.Add(created);
            return created;
        }

        /// <summary>
        /// 튜토리얼 없이 진행한 흔적 (v1.0 전부터 하던 사람). 튜토리얼 판은 판 수·구간·별·계정 레벨을 바꾸지 않으므로
        /// 이 중 하나라도 있으면 튜토리얼을 끝낸 것으로 친다 (튜토리얼도 안 뜨고 출석부도 안 열리는 상태가 없게).
        /// </summary>
        public bool ProgressedWithoutTutorial() =>
            dungeonRuns > 0 || unlockedChapter > 1 || stars.Count > 0 || accountLevel > 1
            || (records.Count > 0 && string.IsNullOrEmpty(tutorialRecordId));

        public RunRecord FindRecord(string recordId)
        {
            if (string.IsNullOrEmpty(recordId)) return null;
            foreach (RunRecord r in records)
            {
                if (r.id == recordId) return r;
            }

            return TempRecord != null && TempRecord.id == recordId ? TempRecord : null;
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
        public bool locked;
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

        /// <summary>
        /// 만들 당시의 판 밖 성장 (장비 ID + 강화값, 영웅 레벨·별, 수련). 보스전은 이걸로 싸운다 (GameDesign 7-3·7-5).
        /// v0.4 전에 만든 기록은 비어 있다 = 판 밖 성장 없음.
        /// </summary>
        public MetaSnapshot meta = new MetaSnapshot();
    }

    /// <summary>판 밖 성장 상태 한 장 (기록에 고정 저장, 또는 지금 계정에서 만든다). 수치는 언제나 최신 데이터로 계산</summary>
    [Serializable]
    public sealed class MetaSnapshot
    {
        public int heroLevel = 1;
        public int heroStars = 1;
        /// <summary>장착 장비 (칸마다 하나, 비면 빠짐). level = 그 칸의 강화값을 장비 등급 상한으로 자른 값</summary>
        public List<EquippedItem> equipment = new List<EquippedItem>();
        public List<int> training = new List<int>();
        /// <summary>효과가 있던 유물과 별 (유물 탭이 열린 뒤에만, 결정 53 "기록 당시 값")</summary>
        public List<RelicStar> relics = new List<RelicStar>();
        /// <summary>장착한 펫 (비면 없음). 레벨·인연도 기록 당시 값 (Pets 5장: 보스맵은 기록의 펫)</summary>
        public string petId;
        public int petLevel;
        public bool petBond;
    }

    [Serializable]
    public sealed class EquippedItem
    {
        public int slot;
        public string dataId;
        public int level;
    }

    // ---------------------------------------------------------------- v0.4 장비 (Equipment.md 12장)

    [Serializable]
    public sealed class EquipmentSave
    {
        public List<EquipmentItem> items = new List<EquipmentItem>();
        /// <summary>칸 강화 (무기·갑옷·목걸이·장갑·허리띠·신발, 계정 공용). 새 칸은 Lv1, 옛 Lv0도 보존한다.</summary>
        public List<int> slotLevels = new List<int> { 1, 1, 1, 1, 1, 1 };
        public List<HeroLoadout> loadouts = new List<HeroLoadout>();
        public int nextUid = 1;
        /// <summary>희귀·전설 중복을 얻자마자 조각으로 (Equipment 9장 기본값 켜짐)</summary>
        public bool autoDismantle = true;
        /// <summary>장비 보상을 받은 구간 보스 ("ch1" …). 장비가 생기기 전에 보스를 깬 저장도 로비에서 받게</summary>
        public List<string> bossRewardsGiven = new List<string>();
        /// <summary>5구간 보스 최초 격파 보상 "전설 1개 선택"을 받았나 (Equipment 8장)</summary>
        public bool legendaryChoiceTaken;
    }

    [Serializable]
    public sealed class EquipmentItem
    {
        public string uid;
        public string dataId;
        public bool locked;
    }

    /// <summary>영웅마다 6칸 (장비 uid, 빈 칸은 빈 문자열). 같은 장비를 여러 영웅이 동시에 낄 수 있다</summary>
    [Serializable]
    public sealed class HeroLoadout
    {
        public string heroId;
        public List<string> uids = new List<string> { "", "", "", "", "", "" };
    }

    // ---------------------------------------------------------------- v0.4 영웅 성장 (Stats 4·6)

    [Serializable]
    public sealed class HeroProgress
    {
        public string heroId;
        public int level = 1;
        public int stars = 1;
        /// <summary>이 영웅으로 끝낸 던전 판 수 (첫 판이면 첫 카드에 전용 액티브 확정, 전체 검토 3번)</summary>
        public int runs;
    }

    // ---------------------------------------------------------------- v0.6 무한의 균열 · 일일 도전

    [Serializable]
    public sealed class RiftSave
    {
        /// <summary>이번 주 (월요일 05:00 기준 그 주 월요일)</summary>
        public string weekKey;
        public int weeklyBest;
        public int bestEver;
        /// <summary>이번 주 받은 층 보상 (10·20·30·40·50)</summary>
        public List<int> rewardsClaimed = new List<int>();
        public RiftCheckpoint checkpoint = new RiftCheckpoint();
    }

    /// <summary>10층 보스를 깰 때마다 남기는 이어하기 (빌드·체력·시드·레벨, 주간 시즌 안에서)</summary>
    [Serializable]
    public sealed class RiftCheckpoint
    {
        public bool valid;
        public int floor;
        public int seed;
        public string heroId;
        public float hpRatio = 1f;
        public int level = 1;
        public float experience;
        public List<RecordSkill> skills = new List<RecordSkill>();
        /// <summary>균열 강화를 고른 횟수 (공격력 / 체력 / 공격속도 순서)</summary>
        public List<int> boosts = new List<int>();
        public List<string> bannedSkillIds = new List<string>();
        public int bansLeft = 2;
    }

    /// <summary>
    /// v1.0 판 이어하기: 방에 들어갈 때마다 남긴다. 앱이 꺼졌다 켜지면 로비에서 "○번 방부터 이어하기".
    /// 판이 끝나면 지운다 (valid = false). 일일 도전 공식 판·서바이벌은 남기지 않는다.
    /// </summary>
    [Serializable]
    public sealed class RunSnapshot
    {
        public bool valid;
        /// <summary>Common.BattleMode (0 던전 · 1 보스 · 3 균열 · 4 일일 도전 연습)</summary>
        public int mode;
        public int chapter = 1;
        public int seed;
        public bool practice;
        /// <summary>일일 도전 연습: 그날이 지나면 이어하지 않는다 · 균열: 주가 바뀌면 이어하지 않는다</summary>
        public string dayKey;
        public string weekKey;
        /// <summary>보스: 쓴 기록</summary>
        public string recordId;
        public int room = 1;
        public string heroId;
        public List<RecordSkill> skills = new List<RecordSkill>();
        public List<int> boosts = new List<int>();
        public List<string> fusions = new List<string>();
        public int level = 1;
        public float experience;
        public float hpRatio = 1f;
        public int kills;
        public int roomsCleared;
        public int hits;
        public float elapsed;
        public int offersMade;
        public bool choseLegendary;
        public int rerollsLeft = -1;
        public List<string> bannedSkillIds = new List<string>();
        public int bansLeft = 2;
        /// <summary>확장 1차 3장: 갈림길에서 고른 문 (갈림길마다 -1 = 아직, 0 = 왼쪽(평범), 1 = 오른쪽(특별))</summary>
        public List<int> forkChoices = new List<int>();
        /// <summary>기억의 석비에서 고른 축복 id (판 안 효과, 없으면 빈 글)</summary>
        public List<string> blessings = new List<string>();
        /// <summary>이 방(room)의 "맞지 않고 깨기"를 이미 실패했나 (껐다 켜서 다시 도전하지 못하게, 검토 9장 2번)</summary>
        public bool challengeFailed;
        /// <summary>이 방의 보상(보상 방·균열 엘리트 층)을 이미 받았나 (껐다 켜서 다시 받기 방지)</summary>
        public bool rewardTaken;
        /// <summary>판을 시작할 때 하트를 썼나 (보스 첫 패배 하트 돌려주기)</summary>
        public bool heartSpent;
        /// <summary>판을 시작할 때의 판 밖 성장 (이어할 때 그대로 — 로비에서 바꾼 강화가 끼어들지 않게)</summary>
        public bool hasMeta;
        public MetaSnapshot meta;

        // ── 서바이벌 이어하기 (전체 검토 6번: 10분짜리 판이 앱이 꺼지면 통째로 사라지던 것) ──
        public int survivalMap;
        public int nextElite;
        public bool midBossDone;
        public int nextSwarm;
        public int cratesBroken;
        public int elitesEarly;
        public int bagGold;
        /// <summary>조합 이력: 재료로 쓴 스킬 · 끝낸 조합 · 이번 판에 숨긴 조합</summary>
        public List<string> consumed = new List<string>();
        public List<string> completedFusions = new List<string>();
        public List<string> hiddenFusions = new List<string>();
        /// <summary>난수 3갈래 상태 (방 · 적 · 카드)</summary>
        public List<string> rng = new List<string>();
        public long savedAt;
    }

    [Serializable]
    public sealed class DailySave
    {
        public string dayKey;
        public bool officialDone;
        /// <summary>공식 도전을 시작했다 (끝나기 전에 앱을 꺼도 다시 못 한다 → 로비에서 0점으로 정리)</summary>
        public bool officialStarted;
        public int officialScore;
        public int bestPractice;
        /// <summary>공식 도전한 날 수 (업적 "매일의 도전자")</summary>
        public int participations;
        /// <summary>최근 공식 기록 (14일)</summary>
        public List<DailyResult> history = new List<DailyResult>();
    }

    [Serializable]
    public sealed class DailyResult
    {
        public string dayKey;
        public string heroId;
        public int score;
        public bool cleared;
    }

    // ---------------------------------------------------------------- v0.5 미션·업적

    [Serializable]
    public sealed class MissionSave
    {
        /// <summary>오늘 (05:00 기준 날짜). 다르면 새 일일 미션</summary>
        public string dayKey;
        public List<MissionProgress> daily = new List<MissionProgress>();
        public int dailyPoints;
        /// <summary>받은 일일 상자 (포인트 구간)</summary>
        public List<int> dailyChests = new List<int>();
        public bool rerollUsed;

        /// <summary>이번 주 (월요일 05:00 기준 그 주 월요일)</summary>
        public string weekKey;
        public List<MissionProgress> weekly = new List<MissionProgress>();
        public int weeklyPoints;
        public List<int> weeklyChests = new List<int>();

        /// <summary>업적용 누적 카운터 (영구)</summary>
        public MissionCounters counters = new MissionCounters();
        public List<AchievementClaim> achievements = new List<AchievementClaim>();
    }

    [Serializable]
    public sealed class MissionProgress
    {
        public string id;
        public string eventKey;
        public string title;
        public int target;
        public int progress;
        public int points;
        /// <summary>이 값일 때만 센다 (오늘의 영웅 ID). 비면 아무거나</summary>
        public string param;
        /// <summary>서로 다른 값만 센다 ("서로 다른 영웅 2명")</summary>
        public bool distinct;
        public List<string> seen = new List<string>();
        public bool done;
    }

    [Serializable]
    public sealed class MissionCounters
    {
        public long basicKills;
        public int noHitRooms;
        public int elites;
        public bool legendary;
        /// <summary>전설 없이 클리어한 구간</summary>
        public List<int> noLegendChapters = new List<int>();
        /// <summary>3구간 이상을 클리어한 영웅 ("만능")</summary>
        public List<string> chapter3Heroes = new List<string>();
        /// <summary>보스를 이긴 기록 ID ("맞춤 전략")</summary>
        public List<string> bossWinRecords = new List<string>();
    }

    [Serializable]
    public sealed class AchievementClaim
    {
        public string id;
        /// <summary>받은 단계 수</summary>
        public int tiers;
    }

    // ---------------------------------------------------------------- v0.5 펫

    [Serializable]
    public sealed class PetSave
    {
        /// <summary>가진 펫과 레벨 (Pets 7장 PetCollection)</summary>
        public List<PetLevel> owned = new List<PetLevel>();
        /// <summary>영웅마다 장착한 펫 (Pets 7장 HeroPetLoadout, 1칸)</summary>
        public List<HeroPet> loadouts = new List<HeroPet>();
    }

    [Serializable]
    public sealed class PetLevel
    {
        public string id;
        public int level;
    }

    [Serializable]
    public sealed class HeroPet
    {
        public string heroId;
        public string petId;
    }

    // ---------------------------------------------------------------- v0.5 서바이벌

    [Serializable]
    public sealed class SurvivalSave
    {
        /// <summary>영웅별 최고 생존 시간 = 맵 1(균열의 들판) 기록 (확장 1차: 맵이 3개가 되며 옛 기록은 맵 1로)</summary>
        public List<HeroBest> best = new List<HeroBest>();
        /// <summary>맵마다 (확장 1차 6장 + 검토 9장 5번): 최고 시간 · 별 · 첫 클리어 · 클리어 수</summary>
        public List<SurvivalMapSave> maps = new List<SurvivalMapSave>();
        /// <summary>받은 서바이벌 별 상자 수 (별 5개마다 1개)</summary>
        public int starChestsTaken;
        /// <summary>서바이벌을 클리어한 영웅 (유물 "영웅 2명으로 클리어")</summary>
        public List<string> clearHeroes = new List<string>();
        public int clears;
        public float longestSeconds;

        /// <summary>영웅의 최고 생존 시간 (없으면 0, 목록을 바꾸지 않는다 — 화면 표시용)</summary>
        public float SecondsOf(string heroId)
        {
            foreach (HeroBest b in best)
            {
                if (b.heroId == heroId) return b.seconds;
            }

            return 0f;
        }

        public HeroBest BestOf(string heroId)
        {
            foreach (HeroBest b in best)
            {
                if (b.heroId == heroId) return b;
            }

            var created = new HeroBest { heroId = heroId };
            best.Add(created);
            return created;
        }

        /// <summary>맵 기록 (없으면 null — 화면 표시용)</summary>
        public SurvivalMapSave FindMap(string id)
        {
            if (maps != null)
            {
                foreach (SurvivalMapSave m in maps)
                {
                    if (m.id == id) return m;
                }
            }

            // 옛 저장 (맵이 하나이던 때): 맵 1 기록은 읽을 때 옮긴다 → 로비에서 바로 맵 2가 열린다 (코드 검토 2026-10-28 2번)
            if (id == "field" && (clears > 0 || longestSeconds > 0f)) return MapOf(id);
            return null;
        }

        /// <summary>
        /// 맵 기록 (없으면 만든다). 맵 1을 처음 만들 때 옛 저장의 기록을 옮긴다:
        /// 최고 시간 = 영웅 최고 중 가장 긴 것, 옛 클리어가 있으면 클리어 1번 (다음 맵이 열린다)
        /// </summary>
        public SurvivalMapSave MapOf(string id)
        {
            if (maps != null)
            {
                foreach (SurvivalMapSave m in maps)
                {
                    if (m.id == id) return m;
                }
            }

            if (maps == null) maps = new List<SurvivalMapSave>();
            var created = new SurvivalMapSave { id = id };
            if (id == "field")
            {
                created.bestSeconds = longestSeconds;
                if (clears > 0)
                {
                    created.clears = 1;
                    created.stars = 1; // ★1 클리어 (옛 판에서 이미 깼다)
                    created.firstClearTaken = true; // 첫 클리어 보상은 옛 규칙으로 이미 받았다
                }
            }

            maps.Add(created);
            return created;
        }

        public int TotalStars()
        {
            int total = 0;
            if (maps == null) return 0;
            foreach (SurvivalMapSave m in maps) total += Survival.SurvivalProgress.StarCount(m.stars);
            return total;
        }
    }

    [Serializable]
    public sealed class SurvivalMapSave
    {
        public string id;
        public float bestSeconds;
        /// <summary>별 칸 (1 클리어 · 2 맵 임무 · 4 전설 없이)</summary>
        public int stars;
        public bool firstClearTaken;
        public int clears;
    }

    [Serializable]
    public sealed class HeroBest
    {
        public string heroId;
        public float seconds;
    }

    // ---------------------------------------------------------------- v0.4 유물 (Relics 8장)

    [Serializable]
    public sealed class RelicSave
    {
        public List<RelicStar> owned = new List<RelicStar>();
        /// <summary>영웅 첫 클리어 또는 종전 고정 유물의 파편 보상을 이미 받았는가</summary>
        public List<string> sourcesClaimed = new List<string>();
        /// <summary>던전을 클리어해 본 영웅 (영웅 유물)</summary>
        public List<string> heroDungeonClears = new List<string>();
        /// <summary>새로 남긴 기록 수 (업적 "기록 10개 저장")</summary>
        public int recordsSavedTotal;
    }

    [Serializable]
    public sealed class RelicStar
    {
        public string id;
        public int stars;
        /// <summary>다음 ★까지 모인 중복 (★2에서 0~1개)</summary>
        public int duplicates;
    }

    [Serializable]
    public sealed class RelicGachaSave
    {
        /// <summary>새 유물을 못 얻은 연속 횟수. 전부 얻으면 0으로 둔다.</summary>
        public int newRelicPity;
        public int totalPulls;
        public string freeDate;
        public List<GachaLogEntry> log = new List<GachaLogEntry>();
    }

    // ---------------------------------------------------------------- v0.4 룬 (Runes 9장)

    [Serializable]
    public sealed class RuneSave
    {
        public List<RuneLevel> owned = new List<RuneLevel>();
        public List<HeroRunes> loadouts = new List<HeroRunes>();
        /// <summary>받은 보스 보상 고르기 ("ch2", "ch4")</summary>
        public List<string> choicesTaken = new List<string>();
        /// <summary>보스 보상으로 고른 룬 (상점에서 산 것과 구분, Codex v0.4 5번)</summary>
        public List<string> choiceRunes = new List<string>();
    }

    [Serializable]
    public sealed class RuneLevel
    {
        public string id;
        public int level;
    }

    /// <summary>영웅마다 룬 2칸 (빈 칸은 빈 문자열)</summary>
    [Serializable]
    public sealed class HeroRunes
    {
        public string heroId;
        public List<string> ids = new List<string> { "", "" };
    }

    // ---------------------------------------------------------------- v0.4 상자·상점 (Gacha, Economy 5)

    [Serializable]
    public sealed class GachaSave
    {
        /// <summary>고급 상자 천장 카운터 (전설이 나오면 0). 보석·소환권으로 연 고급 상자 전부가 같이 쓴다</summary>
        public int premiumPity;
        /// <summary>일반 상자 광고 무료를 받은 날 (하루 1번)</summary>
        public string freeNormalDate;
        /// <summary>뽑기 기록 (고객 문의·검증용, 최근 100개)</summary>
        public List<GachaLogEntry> log = new List<GachaLogEntry>();
    }

    [Serializable]
    public sealed class GachaLogEntry
    {
        public long at;
        public string box;
        public string dataId;
        public int pityBefore;
    }

    [Serializable]
    public sealed class ShopSave
    {
        public string date;
        /// <summary>오늘 골드 상점에서 산 영웅 조각 수 (하루 3개)</summary>
        public int heroShardsBought;
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

    /// <summary>설정 (UX 5장 접근성, 조사 A9)</summary>
    [Serializable]
    public sealed class GameSettings
    {
        /// <summary>화면 흔들림: 0 끄기, 1 약하게(50%), 2 보통</summary>
        public int shake = 2;
        public bool damageNumbers = true;
        /// <summary>번쩍임 줄이기: 화면을 크게 덮는 섬광을 옅게, 1초에 3번 이하로 (WCAG 2.3.1)</summary>
        public bool reduceFlashes;
        /// <summary>로컬 알림 (LiveOps 9장): 하트 가득 · 순찰 가득. 종류별로 끈다</summary>
        public bool notifyHearts = true;
        public bool notifyPatrol = true;
        /// <summary>안드로이드 13 알림 권한을 물어봤나 (첫 판이 끝난 뒤 한 번)</summary>
        public bool notifyAsked;
        /// <summary>프레임: true 60 / false 30 (배터리 절약)</summary>
        public bool frame60 = true;
        /// <summary>그래픽 품질 (v1.0 17번 · 3D): 0 낮음(그림자 끔 · 해상도 75%) / 1 보통(그림자 · 90%) / 2 높음(그림자 · 100% · 계단 줄임). 옛 저장은 보통</summary>
        public int graphics = 1;
        /// <summary>조이스틱: false = 누른 자리에 생김 (기본, UX 2장) / true = 아래 가운데 고정</summary>
        public bool joystickFixed;
        /// <summary>조이스틱 크기: 0 작게 · 1 보통 · 2 크게</summary>
        public int joystickSize = 1;
        /// <summary>조이스틱 진하기: 0 연하게 · 1 보통 · 2 진하게</summary>
        public int joystickOpacity = 1;
        /// <summary>배경음 · 효과음 음량 (0~4 = 0 · 25 · 50 · 75 · 100%)</summary>
        public int musicVolume = 3;
        public int sfxVolume = 4;
        /// <summary>진동 (Audio 4장, 기본 켜짐)</summary>
        public bool vibration = true;
    }

    // ---------------------------------------------------------------- v1.0 출석부 · 신규 7일 미션 · 순찰

    /// <summary>출석부 (Missions 5장): 출석한 날 수로 센다 (연속 아님)</summary>
    [Serializable]
    public sealed class AttendanceSave
    {
        /// <summary>마지막으로 출석한 날 (05:00 기준)</summary>
        public string lastDay;
        /// <summary>지금까지 출석한 날 수</summary>
        public int days;
    }

    /// <summary>신규 7일 미션 (Missions 6장)</summary>
    [Serializable]
    public sealed class NewcomerSave
    {
        /// <summary>시작한 날 (튜토리얼 보스 격파한 날, 05:00 기준). 비어 있으면 아직</summary>
        public string startDay;
        /// <summary>시작한 뒤 들어온 날 수 (칸이 이 수만큼 열린다, 전체 검토 11번) · 마지막으로 센 날</summary>
        public int activeDays;
        public string lastActiveDay;
        /// <summary>사건으로 세는 미션의 카운터 ("fusion", "hero_dungeon:guan", "boss_try:2" …)</summary>
        public List<NamedCount> counts = new List<NamedCount>();
        public List<int> claimedDays = new List<int>();
        public bool bonusClaimed;
    }

    [Serializable]
    public sealed class NamedCount
    {
        public string id;
        public int value;
    }

    /// <summary>순찰 (LiveOps 8장): 마지막으로 받은 때부터 쌓인다 (최대 12시간)</summary>
    [Serializable]
    public sealed class PatrolSave
    {
        public long lastClaimAt;
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
