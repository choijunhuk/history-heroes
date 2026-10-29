using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HistoryHeroes.Common
{
    public enum BattleMode
    {
        Dungeon,
        Boss,
        Survival,
        Rift,  // 무한의 균열 (Modes 4장)
        Daily, // 일일 도전 (Modes 5장)
    }

    /// <summary>
    /// 장면을 넘어가도 남는 "이번에 무엇을 하러 가는가" (로비 → 전투 장면).
    /// 전투 장면을 에디터에서 바로 Play하면 기본값(1구간 던전)으로 시작한다.
    /// </summary>
    public static class GameSession
    {
        public const string LobbyScene = "Lobby";
        public const string BattleScene = "Battle";

        public static BattleMode Mode { get; private set; } = BattleMode.Dungeon;
        public static int Chapter { get; private set; } = 1;

        /// <summary>보스전에 쓸 기록 ID</summary>
        public static string BossRecordId { get; private set; }

        /// <summary>신의 시련은 기존 보스 모드·해당 구간 경기장을 쓰고 진행과 보상만 나눈다.</summary>
        public static string GodTrialId { get; private set; }
        public static bool IsGodTrial => !string.IsNullOrEmpty(GodTrialId);

        private static bool CanEnterGodTrial(SaveData data, string trialId, string recordId)
        {
            if (data == null || !Modes.GodTrialRules.IsUnlocked(data, trialId)) return false;
            RunRecord record = data.FindRecord(recordId);
            return record != null && (!Modes.GodTrialRules.IsGod(record.heroId) || Modes.GodTrialRules.IsHeroUnlocked(data, record.heroId));
        }

        /// <summary>장면·하트를 건드리지 않고 입장 설정만 만든다. 없는 기록을 다른 기록으로 바꾸지 않는다.</summary>
        public static bool PrepareGodTrial(SaveData data, string trialId, string recordId, bool practice)
        {
            if (!CanEnterGodTrial(data, trialId, recordId)) return false;
            ClearSpecialRun();
            GodTrialId = trialId;
            Mode = BattleMode.Boss;
            Chapter = Modes.GodTrialRules.Find(trialId).ArenaChapter;
            BossRecordId = recordId;
            Practice = practice;
            HeartSpent = false;
            return true;
        }

        public static bool TryStartGodTrial(string trialId, string recordId)
        {
            if (!TryPrepareGodTrialAttempt(SaveSystem.Data, trialId, recordId, Clock.Now())) return false;
            SaveSystem.Data.run = BossSnapshot(Clock.UnixSeconds); // 하트와 이어하기를 한 번에 저장
            SaveSystem.Save();
            Load(BattleScene);
            return true;
        }

        /// <summary>장면을 열기 전에 실전 시련 비용을 확정한다. 첫날 무료 판과 따로 센다.</summary>
        public static bool TryPrepareGodTrialAttempt(SaveData data, string trialId, string recordId, System.DateTimeOffset now)
        {
            if (!CanEnterGodTrial(data, trialId, recordId)) return false;
            if (!Hearts.TrySpend(data, 1, now.ToUnixTimeSeconds(), now.ToString("yyyy-MM-dd"), out _, allowFirstDayFree: false)) return false;
            PrepareGodTrial(data, trialId, recordId, false);
            HeartSpent = true;
            return true;
        }

        public static bool StartGodTrialPractice(string trialId, string recordId)
        {
            if (!PrepareGodTrial(SaveSystem.Data, trialId, recordId, true)) return false;
            Load(BattleScene);
            return true;
        }

        /// <summary>이번 판에 하트를 실제로 썼는가 (첫날 무료 판이면 false → 보스 패배 환급 대상 아님)</summary>
        public static bool HeartSpent { get; private set; }

        /// <summary>보스 연습: 하트를 안 쓰고 보상·별·해금도 없다 (Economy 3-4 "하트가 0이어도 보스맵 연습")</summary>
        public static bool Practice { get; private set; }

        public const float HeroTrialSeconds = 180f;
        public static string TrialHeroId { get; private set; }
        public static bool IsHeroTrial => !string.IsNullOrEmpty(TrialHeroId);
        public static bool HeroTrialExpired(float elapsed) => IsHeroTrial && elapsed >= HeroTrialSeconds;

        /// <summary>장면을 열지 않고 체험을 준비한다. 하트와 저장된 영웅 선택은 건드리지 않는다.</summary>
        public static void PrepareHeroTrial(SaveData data, string heroId)
        {
            if (data == null) throw new System.ArgumentNullException(nameof(data));
            if (string.IsNullOrEmpty(heroId)) throw new System.ArgumentException("체험할 영웅이 필요해요", nameof(heroId));
            PrepareDungeon(Mathf.Clamp(data.unlockedChapter, 1, 5));
            HeartSpent = false;
            Practice = true;
            TrialHeroId = heroId;
        }

        public static void StartHeroTrial(string heroId)
        {
            PrepareHeroTrial(SaveSystem.Data, heroId);
            Load(BattleScene);
        }

        public static void StartBossPractice(int chapter, string recordId)
        {
            HeartSpent = false;
            Practice = true;
            ClearSpecialRun();
            Mode = BattleMode.Boss;
            Chapter = Mathf.Max(1, chapter);
            BossRecordId = recordId;
            Load(BattleScene);
        }

        /// <summary>하트 1개를 쓰고 던전 시작. 모자라면 false (로비가 충전 방법을 보여준다)</summary>
        public static bool TryStartDungeon(int chapter)
        {
            if (!TrySpendHeart()) return false;
            StartDungeon(chapter);
            return true;
        }

        /// <summary>하트 1개를 쓰고 보스 도전 (Modes 8장)</summary>
        public static bool TryStartBoss(int chapter, string recordId)
        {
            if (!TrySpendHeart()) return false;
            StartBoss(chapter, recordId);
            return true;
        }

        /// <summary>판 시드 (0 = 아무거나). 일일 도전은 날짜로 정한다 (Modes 5장)</summary>
        public static int RunSeed { get; set; }

        /// <summary>던전 방 구성을 바꿔 끼울 때 (일일 도전 7방). null = 던전 15방</summary>
        public static System.Collections.Generic.IReadOnlyList<Dungeon.RoomPlan> CustomPlan { get; set; }

        /// <summary>오늘의 변형 규칙 등 (적 체력·공격력 배수)</summary>
        public static float EnemyHpScale { get; set; } = 1f;
        public static float EnemyAttackScale { get; set; } = 1f;

        /// <summary>던전·보스·서바이벌을 새로 시작할 때 특수 판 설정을 지운다</summary>
        /// <summary>이어하는 판의 스냅샷 (전투 장면이 열릴 때 DungeonRun이 가져간다)</summary>
        public static RunSnapshot Resuming { get; private set; }

        public static RunSnapshot ConsumeResume()
        {
            RunSnapshot s = Resuming;
            Resuming = null;
            return s;
        }

        /// <summary>
        /// 저장된 판을 이어한다 (하트는 판을 시작할 때 이미 썼다). 보스는 그 기록으로 처음부터 다시.
        /// </summary>
        public static void StartResume(RunSnapshot s)
        {
            if (s == null || !s.valid) return;
            switch ((BattleMode)s.mode)
            {
                case BattleMode.Boss:
                    if (!string.IsNullOrEmpty(s.godTrialId))
                    {
                        if (!PrepareGodTrial(SaveSystem.Data, s.godTrialId, s.recordId, s.practice))
                        {
                            SaveSystem.Data.run = new RunSnapshot();
                            SaveSystem.Save();
                            return;
                        }

                        HeartSpent = !s.practice && s.heartSpent;
                        Load(BattleScene);
                        return;
                    }

                    if (s.practice)
                    {
                        StartBossPractice(s.chapter, s.recordId);
                    }
                    else
                    {
                        HeartSpent = s.heartSpent; // 하트를 쓰고 시작한 첫 보스전 → 지면 돌려준다
                        StartBoss(s.chapter, s.recordId);
                    }

                    return;
                case BattleMode.Survival:
                    ClearSpecialRun();
                    Practice = false;
                    Mode = BattleMode.Survival;
                    SurvivalMap = Mathf.Clamp(s.survivalMap, 0, Survival.SurvivalProgress.MapIds.Length - 1);
                    break;
                case BattleMode.Daily:
                    ClearSpecialRun();
                    Practice = true;
                    DailyOfficial = false;
                    Mode = BattleMode.Daily;
                    RunSeed = s.seed;
                    CustomPlan = Modes.DailyRules.Plan();
                    Chapter = s.chapter;
                    break;
                default:
                    ClearSpecialRun();
                    Practice = s.practice;
                    Mode = (BattleMode)s.mode;
                    RunSeed = s.seed;
                    Chapter = Mathf.Max(1, s.chapter);
                    RiftContinue = false;
                    break;
            }

            BossRecordId = null;
            HeartSpent = s.heartSpent;
            Resuming = s;
            Load(BattleScene);
        }

        /// <summary>튜토리얼 판 (UX 4장): 지지 않게, 별·해금·하트 없음</summary>
        public static bool Tutorial { get; private set; }

        /// <summary>튜토리얼을 방금 끝냈다 (로비가 "1구간 던전에 도전하세요"를 띄운다)</summary>
        public static bool TutorialJustFinished { get; set; }

        /// <summary>튜토리얼 던전 6방 (replay = 설정의 "다시 보기": 보상·기록 없음)</summary>
        public static void StartTutorial(bool replay)
        {
            ClearSpecialRun();
            Tutorial = true;
            Practice = replay;
            HeartSpent = false;
            Mode = BattleMode.Dungeon;
            Chapter = 1;
            BossRecordId = null;
            RunSeed = Progress.Tutorial.Seed;
            CustomPlan = Progress.Tutorial.Plan();
            EnemyHpScale = Progress.Tutorial.EnemyHp;
            EnemyAttackScale = Progress.Tutorial.EnemyAttack;
            Load(BattleScene);
        }

        /// <summary>튜토리얼 보스: 방금 남긴 기록으로 1구간 수문장 (약하게, 꼭 이기게)</summary>
        public static void StartTutorialBoss(string recordId)
        {
            ClearSpecialRun();
            Tutorial = true;
            Practice = false;
            HeartSpent = false;
            Mode = BattleMode.Boss;
            Chapter = 1;
            BossRecordId = recordId;
            EnemyHpScale = Progress.Tutorial.BossHp;
            EnemyAttackScale = Progress.Tutorial.BossAttack;
            Load(BattleScene);
        }

        public static void ClearSpecialRun()
        {
            Resuming = null;
            TrialHeroId = null;
            GodTrialId = null;
            Tutorial = false;
            RunSeed = 0;
            CustomPlan = null;
            EnemyHpScale = 1f;
            EnemyAttackScale = 1f;
        }

        /// <summary>이번 모드의 하트 (던전·보스 1, 서바이벌 2 — Modes 8장)</summary>
        public static int HeartCost => Mode == BattleMode.Survival ? SurvivalHearts : Mode == BattleMode.Rift || Mode == BattleMode.Daily ? 0 : 1;

        /// <summary>무한의 균열: 체크포인트에서 이어하기</summary>
        public static bool RiftContinue { get; private set; }

        /// <summary>일일 도전: 공식 도전 (첫 시도만 기록). 연습이면 false (Practice = true, 보상·기록 없음)</summary>
        public static bool DailyOfficial { get; private set; }

        /// <summary>무한의 균열 (하트 0, 3구간 보스 격파 뒤)</summary>
        public static bool TryStartRift(bool continueRun)
        {
            SaveData data = SaveSystem.Data;
            if (!Modes.RiftRules.Unlocked(data) && !Debug.isDebugBuild) return false;
            Modes.RiftRules.EnsureWeek(data, Clock.Now());
            if (continueRun && !data.rift.checkpoint.valid) return false;
            StartRift(continueRun);
            return true;
        }

        public static void StartRift(bool continueRun)
        {
            Practice = false;
            ClearSpecialRun();
            Mode = BattleMode.Rift;
            RiftContinue = continueRun;
            Chapter = 1;
            BossRecordId = null;
            RiftCheckpoint cp = SaveSystem.Data.rift.checkpoint;
            RunSeed = continueRun && cp.valid ? cp.seed : 0;
            Load(BattleScene);
        }

        /// <summary>일일 도전 (하트 0). 공식은 하루 1번</summary>
        public static bool TryStartDaily(bool official)
        {
            SaveData data = SaveSystem.Data;
            if (!Modes.DailyRules.Unlocked(data) && !Debug.isDebugBuild) return false;
            Modes.DailyRules.EnsureDay(data, Clock.Now());
            if (official && !Modes.DailyRules.OfficialAvailable(data)) return false;
            StartDaily(official);
            return true;
        }

        public static void StartDaily(bool official)
        {
            ClearSpecialRun();
            Practice = !official;
            DailyOfficial = official;
            if (official)
            {
                Modes.DailyRules.MarkOfficialStarted(SaveSystem.Data);
                SaveSystem.Save();
            }

            Mode = BattleMode.Daily;
            string day = Modes.DailyRules.DayKey(Clock.Now());
            RunSeed = Modes.DailyRules.Seed(day);
            CustomPlan = Modes.DailyRules.Plan();
            Chapter = Modes.DailyRules.ChapterFor(day);
            BossRecordId = null;
            Load(BattleScene);
        }

        /// <summary>서바이벌 하트 (확장 1차 6-1장: 2 → 1)</summary>
        public const int SurvivalHearts = 1;

        /// <summary>이번 서바이벌 맵 (0 균열의 들판 · 1 무너진 성곽 도시 · 2 증기 공장)</summary>
        public static int SurvivalMap { get; set; }

        /// <summary>서바이벌 해금 (확장 1차: 1구간 보스 격파 = 맵 1). 에디터·개발 빌드는 시험용으로 늘 열림</summary>
        public static bool SurvivalUnlocked(SaveData data) => SurvivalUnlockedByProgress(data) || Debug.isDebugBuild;

        public static bool SurvivalUnlockedByProgress(SaveData data) => Survival.SurvivalProgress.Unlocked(data, 0);

        /// <summary>맵 하나가 열렸나 (개발 빌드는 늘)</summary>
        public static bool SurvivalMapUnlocked(SaveData data, int map) => Survival.SurvivalProgress.Unlocked(data, map) || Debug.isDebugBuild;

        /// <summary>서바이벌 (하트 1개). 맵이 잠겨 있거나 하트가 모자라면 false</summary>
        public static bool TryStartSurvival(int map = 0)
        {
            if (!SurvivalMapUnlocked(SaveSystem.Data, map)) return false;
            if (!TrySpendHeart(SurvivalHearts)) return false;
            StartSurvival(map);
            return true;
        }

        public static void StartSurvival(int map = 0)
        {
            Practice = false;
            ClearSpecialRun();
            Mode = BattleMode.Survival;
            SurvivalMap = Mathf.Clamp(map, 0, Survival.SurvivalProgress.MapIds.Length - 1);
            BossRecordId = null;
            Load(BattleScene);
        }

        /// <summary>결과 화면의 "다시 도전": 같은 설정, 그 모드의 하트</summary>
        public static bool TryRetry()
        {
            if (IsGodTrial)
                return Practice ? StartGodTrialPractice(GodTrialId, BossRecordId) : TryStartGodTrial(GodTrialId, BossRecordId);

            if (Tutorial)
            {
                if (Mode == BattleMode.Boss) StartTutorialBoss(BossRecordId);
                else StartTutorial(Practice);
                return true;
            }

            if (Mode == BattleMode.Rift)
            {
                StartRift(false); // 1층부터 새로
                return true;
            }

            if (Mode == BattleMode.Daily)
            {
                StartDaily(false); // 공식은 하루 1번 → 다시 하면 연습
                return true;
            }

            if (Practice)
            {
                Retry(); // 연습은 하트 없이 또
                return true;
            }

            if (!TrySpendHeart(HeartCost)) return false;
            Retry();
            return true;
        }

        private static bool TrySpendHeart(int amount = 1)
        {
            SaveData data = SaveSystem.Data;
            if (!Hearts.TrySpend(data, amount, Clock.UnixSeconds, Clock.Today, out bool free)) return false;
            HeartSpent = !free;
            SaveSystem.Save();
            return true;
        }

        public static void StartDungeon(int chapter)
        {
            PrepareDungeon(chapter);
            Load(BattleScene);
        }

        /// <summary>장면을 열지 않고 일반 던전 설정으로 바꾼다.</summary>
        public static void PrepareDungeon(int chapter)
        {
            Practice = false;
            ClearSpecialRun();
            Mode = BattleMode.Dungeon;
            Chapter = Mathf.Max(1, chapter);
            BossRecordId = null;
        }

        public static void StartBoss(int chapter, string recordId)
        {
            Practice = false;
            ClearSpecialRun();
            Mode = BattleMode.Boss;
            Chapter = Mathf.Max(1, chapter);
            BossRecordId = recordId;
            SaveSystem.Data.run = BossSnapshot(Clock.UnixSeconds);
            SaveSystem.Save();
            Load(BattleScene);
        }

        /// <summary>
        /// 보스 · 시련 이어하기 스냅샷 (지금 입장 설정 그대로). 장면을 열기 전에 저장한다:
        /// 하트를 저장한 뒤 장면 로딩 중에 앱이 꺼지면 하트만 사라지고 이어하기가 없었다 (2026-10-29 Codex 리뷰). BossRun.Start가 같은 값으로 다시 쓴다.
        /// </summary>
        public static RunSnapshot BossSnapshot(long now) => new RunSnapshot
        {
            valid = true, mode = (int)BattleMode.Boss, chapter = Chapter, practice = Practice,
            recordId = BossRecordId, godTrialId = GodTrialId, heartSpent = HeartSpent, savedAt = now,
        };

        /// <summary>같은 설정으로 한 번 더 (결과 화면의 "다시 도전")</summary>
        public static void Retry() => Load(BattleScene);

        public static void ToLobby()
        {
            if (IsHeroTrial) PrepareDungeon(Chapter); // 로비의 미션·성장은 다시 정상 처리한다
            Load(LobbyScene);
        }

        private static void Load(string scene)
        {
            GamePause.ResetAll();
            SceneManager.LoadScene(scene);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Mode = BattleMode.Dungeon;
            Chapter = 1;
            BossRecordId = null;
            HeartSpent = false;
            Practice = false;
            ClearSpecialRun();
        }
    }
}
