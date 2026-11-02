using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Modes;
using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 로비 (UX.md 1장의 v0.2 축소판): 구간 고르기 → 던전 / 보스 도전 / 기록 보관함 / 조합 도감.
    /// 열리지 않은 구간은 고를 수 없고 "○구간 보스를 깨면 열려요"를 보여준다 (Modes 2-11).
    /// </summary>
    public sealed class LobbyScreen : MonoBehaviour
    {
        /// <summary>Content.md 3장 구간 이름</summary>
        public static readonly string[] ChapterNames = { "균열의 초원", "잊힌 성채", "녹슨 공방", "그림자 협곡", "망각의 왕좌" };

        [SerializeField] private List<BossData> bosses = new List<BossData>();

        [Header("위쪽 바")]
        [SerializeField] private TMP_Text walletLabel;
        [Header("그래픽 평가 3번: 위쪽 재화 칸 (있으면 walletLabel 대신 따로)")]
        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text gemLabel;
        [SerializeField] private TMP_Text heartLabel;
        [SerializeField] private TMP_Text levelLabel;
        [Tooltip("가운데 무대: 구간 섬 + 지금 영웅")]
        [SerializeField] private LobbyDiorama diorama;
        [SerializeField] private TMP_Text noticeLabel;

        [Header("구간")]
        [SerializeField] private TMP_Text chapterLabel;
        [SerializeField] private TMP_Text chapterInfo;
        [SerializeField] private Button previousChapter;
        [SerializeField] private Button nextChapter;

        [Header("버튼")]
        [SerializeField] private Button dungeonButton;
        [SerializeField] private Button bossButton;
        [SerializeField] private TMP_Text bossButtonLabel;
        [SerializeField] private Button survivalButton;
        [SerializeField] private TMP_Text survivalButtonLabel;
        [Tooltip("확장 1차 6-6장: 서바이벌 맵 고르기")]
        [SerializeField] private SurvivalMapPanel survivalMaps;
        [SerializeField] private Button archiveButton;
        [SerializeField] private TMP_Text archiveButtonLabel;
        [SerializeField] private Button codexButton;
        [SerializeField] private TMP_Text codexButtonLabel;
        [SerializeField] private Button heroButton;
        [SerializeField] private TMP_Text heroButtonLabel;
        [Tooltip("영웅 버튼 안의 3D 초상 (지금 영웅만 보인다, Art3D-Plan 7번)")]
        [SerializeField] private HeroPortraits heroButtonPortraits;
        [SerializeField] private HeroRoster roster;
        [SerializeField] private TMP_Text heroUpgradeBadge;

        [Header("패널")]
        [SerializeField] private BossIntroPanel bossIntro;
        [SerializeField] private RecordArchivePanel archive;
        [SerializeField] private CodexPanel codex;
        [SerializeField] private HeartPanel hearts;
        [SerializeField] private HeroPanel heroPanel;
        [Tooltip("장비 보상 (v0.4)")]
        [SerializeField] private Meta.MetaTable metaTable;
        [SerializeField] private Button shopButton;
        [SerializeField] private ShopPanel shop;
        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsPanel settings;
        [Header("v0.6 고르기 (5구간 전설 장비)")]
        [SerializeField] private ChoicePanel choice;
        [Header("v0.5 미션")]
        [SerializeField] private Button missionButton;
        [SerializeField] private TMP_Text missionLabel;
        [Header("홈 미션 띠 (2026-10-29)")]
        [SerializeField] private Button homeMissionButton;
        [SerializeField] private TMP_Text homeMissionLabel;
        [SerializeField] private Image homeMissionFill;
        [SerializeField] private MissionPanel missions;
        [Header("v0.6 무한의 균열 · 일일 도전")]
        [SerializeField] private Button riftButton;
        [SerializeField] private TMP_Text riftButtonLabel;
        [SerializeField] private Button dailyButton;
        [SerializeField] private TMP_Text dailyButtonLabel;
        [SerializeField] private ModeInfoPanel modeInfo;
        [Header("v1.0 신규 7일 미션 · 출석 · 순찰")]
        [SerializeField] private Button newcomerButton;
        [SerializeField] private TMP_Text newcomerLabel;
        [SerializeField] private TMP_Text newcomerBadge;
        [SerializeField] private NewcomerPanel newcomerPanel;
        [SerializeField] private Button patrolButton;
        [SerializeField] private TMP_Text patrolLabel;
        [Header("v1.0 튜토리얼")]
        [SerializeField] private OpeningPanel opening;
        [SerializeField] private RecordSaveScreen reselect;
        [SerializeField] private GameObject unsupportedSaveNotice;
        [SerializeField] private LobbyGuide firstUseGuide;
        [Tooltip("첫 실행 동의 (LiveOps 19장). 동의 전에는 로비의 나머지 시작 절차를 미룬다")]
        [SerializeField] private ConsentPanel consent;

        private int _chapter = 1;
        private float _nextWallet;
        private float _noticeUntil;
        private bool _readyForGuide;
        private bool _begun;

        private void Start()
        {
            GamePause.ResetAll();
            SaveData data = SaveSystem.Data;
            if (SaveSystem.UnsupportedNewerSave)
            {
                Block(null, SaveSystem.UnsupportedNewerSaveText);
                return;
            }

            // 지운 계정의 진행을 아직 다 못 지웠다: 새 진행을 쌓지 않게 로비를 막는다 (다시 열면 이어서 지운다)
            if (SaveSystem.WipePending)
            {
                Block("이 기기 진행을 지우는 중이에요\n\n<size=70%>" + SaveSystem.WipeFailedText + "</size>", SaveSystem.WipeFailedText);
                return;
            }
            // 첫 실행 동의가 먼저다: 튜토리얼 자동 시작 · 출석 · 안내보다 앞. 동의하면 그때 나머지를 시작한다.
            if (consent != null && Progress.Consent.NeedsConsent(data)) consent.Open(Begin);
            else Begin();
        }

        /// <summary>로비를 못 쓰게 막고 화면 전체 안내를 띄운다. text가 null이면 안내의 원래 글 (더 새 저장)</summary>
        private void Block(string text, string fallback)
        {
            // ??는 에디터의 "가짜 null"을 못 거른다 → == null
            CanvasGroup controls = GetComponent<CanvasGroup>();
            if (controls == null) controls = gameObject.AddComponent<CanvasGroup>();
            controls.interactable = false;
            controls.blocksRaycasts = false;
            if (unsupportedSaveNotice != null)
            {
                if (text != null) unsupportedSaveNotice.GetComponentInChildren<TMP_Text>(true).text = text;
                unsupportedSaveNotice.SetActive(true);
                unsupportedSaveNotice.transform.SetAsLastSibling();
            }
            else ShowNotice(fallback);
            enabled = false;
        }

        /// <summary>동의가 끝난 뒤의 로비 시작 절차 (예전 Start 본문)</summary>
        private void Begin()
        {
            SaveData data = SaveSystem.Data;
            _begun = true;
            AccountSection.Bind(choice, roster); // 계정 진행 고르기 · 확인 창
            Account.AccountService.Start(); // 동의 뒤에만 서버에 닿는다. 실패해도 게임은 그대로 (오프라인)
            // 결제 상점도 동의 뒤에 붙는다. 지난번에 지급 못 한 결제가 있으면 여기서 받는다 (상점을 안 열어도 알린다)
            IapService.Granted += OnIapGranted;
            IapService.Notice += OnIapNotice;
            IapService.Start();
            GameSession.ConfigureMetaTable(metaTable);
            string lobbyHero = GameSession.ConsumeLobbyHero();
            SettingsApplier.Apply(data.settings);
            Screen.sleepTimeout = SleepTimeout.SystemSetting; // 전투에서 막아 둔 화면 꺼짐을 폰 설정대로
            _chapter =Mathf.Clamp(GameSession.Chapter, 1, MaxChapter(data));

            previousChapter.onClick.AddListener(() => ChangeChapter(-1));
            nextChapter.onClick.AddListener(() => ChangeChapter(+1));
            dungeonButton.onClick.AddListener(StartDungeon);
            bossButton.onClick.AddListener(OpenBoss);
            if (survivalButton != null) survivalButton.onClick.AddListener(StartSurvival);
            archiveButton.onClick.AddListener(() => archive.Open(Render));
            codexButton.onClick.AddListener(() => codex.Open());
            heroButton.onClick.AddListener(() => heroPanel.Open(Render));
            if (settingsButton != null) settingsButton.onClick.AddListener(() => settings.Open());
            if (shopButton != null) shopButton.onClick.AddListener(() => shop.Open(Render));
            if (missionButton != null) missionButton.onClick.AddListener(() => missions.Open(Render));
            if (homeMissionButton != null) homeMissionButton.onClick.AddListener(() => missions.Open(Render));
            if (newcomerButton != null) newcomerButton.onClick.AddListener(() => newcomerPanel.Open(Render, ShowNotice));
            if (patrolButton != null) patrolButton.onClick.AddListener(ClaimPatrol);
            if (riftButton != null) riftButton.onClick.AddListener(OpenRift);
            if (dailyButton != null) dailyButton.onClick.AddListener(OpenDaily);

            // v1.0: 신규 7일 미션 시작 · 오늘 출석 · 순찰 시계
            if (Missions.Newcomer.EnsureStarted(data, Clock.Now())) SaveSystem.MarkDirty();
            // 입장 보상은 같은 프레임에 알림이 서로 덮어쓰지 않게 모아 두었다가 저장 뒤에 보상 연출로 하나씩 보여 준다
            var popups = new List<KeyValuePair<string, string>>(); // 제목, 보상 글
            List<string> attended = Missions.Attendance.CheckIn(data, Clock.Now());
            if (attended.Count > 0)
            {
                SaveSystem.MarkDirty();
                popups.Add(new KeyValuePair<string, string>("출석 보상!", RewardTail(attended)));
            }

            if (Patrol.Ensure(data, Clock.UnixSeconds)) SaveSystem.MarkDirty();

            // 균열 주간 시즌 · 일일 도전 날짜가 바뀌었으면 새로
            if (RiftRules.EnsureWeek(data, Clock.Now())) SaveSystem.MarkDirty();
            List<string> interrupted = DailyRules.SettleInterrupted(data, roster.heroes.ConvertAll(h => h.id));
            if (interrupted.Count > 0)
            {
                SaveSystem.MarkDirty();
                ShowNotice("공식 도전이 중간에 끝나 0점으로 기록했어요 · " + string.Join(" · ", interrupted));
            }

            if (DailyRules.EnsureDay(data, Clock.Now())) SaveSystem.MarkDirty();

            // 미션 (v0.5): 날·주가 바뀌었으면 새로, "게임 접속" 미션
            if (metaTable != null && metaTable.missions != null)
            {
                Missions.MissionEvents.Configure(metaTable.missions, roster, codex.RecipeCount);
                Missions.MissionEvents.Completed += OnMissionCompleted;
                Missions.MissionEvents.EnsureCurrent();
                Missions.MissionEvents.Raise(Missions.MissionEvents.Login);
                SaveSystem.SaveIfDirty();
            }

            // 놓친 별 상자가 있으면 여기서 받는다
            Reward box = Stars.ClaimBoxes(data);
            if (!box.IsEmpty)
            {
                SaveSystem.Save();
                popups.Add(new KeyValuePair<string, string>("별 상자!", box.ToString()));
            }

            // 유물 (업적·별·보스 — 늦게 채운 조건도 여기서 확인, v0.4)
            if (metaTable != null && metaTable.relics != null)
            {
                int heroes = 0;
                foreach (HeroData h in roster.heroes)
                {
                    if (HeroUnlock.IsUnlocked(data, h)) heroes++;
                }

                List<string> relics = Relics.RelicService.CheckAll(data, metaTable.relics, heroes);
                if (relics.Count > 0)
                {
                    SaveSystem.Save();
                    popups.Add(new KeyValuePair<string, string>("새 유물!", RewardTail(relics)));
                }
            }

            // 펫 (v0.5): 펫이 생기기 전에 채운 조건도 여기서
            if (metaTable != null && metaTable.pets != null)
            {
                List<string> pets = Pets.PetService.CheckAll(data, metaTable.pets);
                if (pets.Count > 0)
                {
                    SaveSystem.Save();
                    popups.Add(new KeyValuePair<string, string>("새 펫!", RewardTail(pets)));
                }
            }

            // 튜토리얼 (UX 4장): 처음이면 오프닝 → 튜토리얼 던전. 방금 끝냈으면 다음 목표
            bool pendingRecord = data.pendingRecord != null;
            bool tutorialBoss = !pendingRecord && Progress.Tutorial.BossPending(data);
            bool tutorial = tutorialBoss || (!pendingRecord && Progress.Tutorial.Needed(data) && opening != null);
            Audio.AudioManager.PlayMusic(tutorial ? "opening" : "lobby");
            if (tutorialBoss) GameSession.StartTutorialBoss(data.tutorialRecordId); // 기록은 남겼고 보스 전에 꺼졌다
            else if (tutorial) opening.Open(() => GameSession.StartTutorial(false));
            else if (!pendingRecord && opening != null && Story.GodsTeaser.Pending(data))
            {
                // 망각의 왕 첫 격파 뒤 한 번: 신의 심판 예고 (GodsVsHumanity 2장). 본 순간 저장 (건너뛰어도 다시 안 뜬다)
                data.godsTeaserSeen = true;
                SaveSystem.Save();
                opening.Open(Story.GodsTeaser.Scenes, Story.GodsTeaser.Tints, "확인", null);
            }
            // 튜토리얼 직후 안내는 7일 미션 띠 하나로 모은다. 알림 묻기는 다음 로비에서 한다.
            bool justFinished = GameSession.TutorialJustFinished;
            if (justFinished)
            {
                GameSession.TutorialJustFinished = false;
                if (firstUseGuide == null) ShowNotice("튜토리얼 끝! 이제 <b>1구간 던전</b>에 도전하세요");
            }

            // 장비가 생기기 전에 깬 보스의 장비 보상 (v0.4)
            if (metaTable != null)
            {
                List<string> gear = Equipment.EquipmentInventory.GrantBossRewards(data, metaTable.equipment);
                if (gear.Count > 0)
                {
                    SaveSystem.Save();
                    popups.Add(new KeyValuePair<string, string>("보스 격파 보상!", RewardTail(gear)));
                }

                if (!tutorial && !justFinished && string.IsNullOrEmpty(lobbyHero) && !OfferPendingRecord() && !OfferResume() && !OfferLegendaryChoice()) OfferNotifications();
            }
            else if (!tutorial && !justFinished && string.IsNullOrEmpty(lobbyHero) && !OfferPendingRecord() && !OfferResume())
            {
                OfferNotifications();
            }

            bool persisted = SaveSystem.SaveIfDirty(); // 지난 저장이 실패했으면 다시 시도
            // 저장된 뒤에만 보상 연출 (저장 실패면 예전처럼 알림 글로)
            foreach (KeyValuePair<string, string> p in popups)
            {
                if (persisted && !string.IsNullOrWhiteSpace(p.Value)) RewardPopup.Show(p.Value, p.Key);
                else ShowNotice(p.Key.TrimEnd('!') + ": " + p.Value);
            }

            if (!tutorial) ShowHeroChat(data);
            Render();
            if (!string.IsNullOrEmpty(lobbyHero) && heroPanel != null) heroPanel.Open(Render, lobbyHero);
            _readyForGuide = !tutorial;
        }

        /// <summary>결과 화면에서 꺼진 판은 새 판보다 먼저 기록을 저장하거나 명시적으로 포기한다.</summary>
        private bool OfferPendingRecord()
        {
            SaveData data = SaveSystem.Data;
            if (data.pendingRecord == null) return false;
            if (reselect != null && reselect.OpenPending(data.pendingRecord, data.pendingTutorialRecord, Render)) return true;
            const string unreadable = "남길 기록의 스킬을 읽지 못했어요. 게임을 업데이트한 뒤 다시 열거나 이 기록을 포기해 주세요";
            ShowNotice(unreadable);
            if (choice != null) choice.Open("남기지 않은 기록이 있어요", unreadable, new[]
            {
                new ChoicePanel.Option
                {
                    Text = "이 기록 포기하기",
                    Danger = true,
                    OnChosen = () =>
                    {
                        if (RecordSaveScreen.DiscardPending() == SaveSystem.CommitResult.Saved) GameSession.ToLobby();
                        else { OfferPendingRecord(); ShowNotice(SaveSystem.SaveFailedText); }
                    },
                },
            });
            return true;
        }

        /// <summary>영웅끼리 짧은 대화 (Story 3장): 둘 다 해금됐고 다른 알림이 없을 때 가끔</summary>
        private void ShowHeroChat(SaveData data)
        {
            if (noticeLabel == null || noticeLabel.gameObject.activeSelf || Random.value > 0.4f) return;
            var chats = new List<(string a, string lineA, string b, string lineB)>();
            foreach (var c in Story.StoryLines.Chats)
            {
                HeroData a = roster.Find(c.a), b = roster.Find(c.b);
                if (a != null && b != null && HeroUnlock.IsUnlocked(data, a) && HeroUnlock.IsUnlocked(data, b)) chats.Add(c);
            }

            if (chats.Count == 0) return;
            var chat = chats[Random.Range(0, chats.Count)];
            ShowNotice($"{roster.Find(chat.a).displayName}: \"{chat.lineA}\"   {roster.Find(chat.b).displayName}: \"{chat.lineB}\"");
        }

        private void OnDestroy()
        {
            Missions.MissionEvents.Completed -= OnMissionCompleted;
            IapService.Granted -= OnIapGranted;
            IapService.Notice -= OnIapNotice;
            AccountSection.Unbind();
        }

        /// <summary>5구간 보스 최초 격파: 전설 장비 1개 고르기 (Equipment 8장). "나중에"를 누르면 다음에 로비에서 또</summary>
        private bool OfferLegendaryChoice()
        {
            SaveData data = SaveSystem.Data;
            if (choice == null || metaTable == null || metaTable.equipment == null || !Equipment.EquipmentInventory.LegendaryChoicePending(data)) return false;
            var options = new List<ChoicePanel.Option>();
            foreach (Equipment.EquipmentData e in Equipment.EquipmentInventory.LegendaryOptions(metaTable.equipment))
            {
                string id = e.id;
                string second = e.hasSecondary ? " · " + e.SecondaryText : string.Empty;
                options.Add(new ChoicePanel.Option
                {
                    Text = $"<b>{e.displayName}</b>  <size=85%>{Equipment.EquipmentText.SlotName(e.slot)}{second}</size>",
                    OnChosen = () =>
                    {
                        string line = null;
                        SaveSystem.CommitResult r = SaveSystem.Commit(() => (line = Equipment.EquipmentInventory.TakeLegendaryChoice(SaveSystem.Data, metaTable.equipment, id)) != null);
                        ShowNotice(r == SaveSystem.CommitResult.Saved ? "5구간 보스 보상: " + line.Replace("장비  ", "") : SaveSystem.SaveFailedText);
                        Render();
                    },
                });
            }

            if (options.Count == 0) return false;
            choice.Open("5구간 보스 보상", "전설 장비 1개를 골라요 (한 번만)", options);
            return true;
        }

        /// <summary>
        /// 앱이 꺼져서 끝나지 못한 판 (v1.0 A4): "○번 방부터 이어하기" / "그만두기". 나중에(닫기)를 누르면 다음 로비에서 또.
        /// 일일 도전 연습은 그날이 지났으면, 균열은 주가 바뀌었으면 조용히 버린다.
        /// </summary>
        private bool OfferResume()
        {
            SaveData data = SaveSystem.Data;
            RunSnapshot run = data.run;
            if (run == null || !run.valid || choice == null) return false;
            var mode = (BattleMode)run.mode;
            bool stale = (mode == BattleMode.Daily && run.dayKey != DailyRules.DayKey(Clock.Now()))
                         || (mode == BattleMode.Rift && run.weekKey != RiftRules.WeekKey(Clock.Now()));
            if (stale)
            {
                data.run = new RunSnapshot();
                SaveSystem.Save();
                return false;
            }

            HeroData hero = roster.Find(run.heroId);
            string who = hero != null ? $" ({hero.displayName})" : string.Empty;
            string where = mode == BattleMode.Boss ? $"{ChapterTitle(run.chapter)} 보스 — 처음부터 다시 (하트 없이)"
                : mode == BattleMode.Rift ? $"무한의 균열 {run.room}층부터"
                : mode == BattleMode.Daily ? $"일일 도전 연습 방 {run.room}부터"
                : mode == BattleMode.Survival ? $"서바이벌 {Mathf.FloorToInt(run.elapsed / 60f):0}:{Mathf.FloorToInt(run.elapsed % 60f):00}부터"
                : $"{ChapterTitle(run.chapter)} 던전 방 {run.room}부터";
            var options = new List<ChoicePanel.Option>
            {
                new ChoicePanel.Option { Text = "<color=#FFD27A>그만두면 이번 판은 사라져요 (보상 없음)</color>" },
                new ChoicePanel.Option
                {
                    Text = "그만두기",
                    OnChosen = () =>
                    {
                        SaveSystem.Data.run = new RunSnapshot();
                        SaveSystem.Save();
                        ShowNotice("이번 판을 그만뒀어요");
                    },
                },
                new ChoicePanel.Option { Text = "이어하기", Primary = true, OnChosen = () => GameSession.StartResume(run) },
            };
            choice.Open("끝나지 않은 판이 있어요", $"앱이 꺼져서 멈춘 판이 있어요\n<b>{where}</b>{who}", options);
            return true;
        }

        /// <summary>
        /// 알림 받을지 묻기 (LiveOps 9장): 첫 판이 끝난 뒤 한 번, 이유를 먼저 보여 주고 OS 권한 창(안드로이드 13+)을 띄운다.
        /// </summary>
        private bool OfferNotifications()
        {
            SaveData data = SaveSystem.Data;
            bool playedOnce = data.records.Count > 0 || data.accountExp > 0 || data.accountLevel > 1;
            if (choice == null || data.settings.notifyAsked || !data.tutorialDone || !playedOnce) return false;
            var options = new List<ChoicePanel.Option>
            {
                new ChoicePanel.Option { Text = "밤 9시~아침 8시에는 보내지 않아요 · 하루 2번까지\n설정에서 언제든 바꿀 수 있어요" },
                new ChoicePanel.Option
                {
                    Text = "괜찮아요",
                    OnChosen = () =>
                    {
                        GameSettings s = SaveSystem.Data.settings;
                        s.notifyAsked = true;
                        s.notifyHearts = false;
                        s.notifyPatrol = false;
                        SaveSystem.Save();
                    },
                },
                new ChoicePanel.Option
                {
                    Text = "받을게요",
                    Primary = true,
                    OnChosen = () =>
                    {
                        SaveSystem.Data.settings.notifyAsked = true;
                        SaveSystem.Save();
                        Common.LocalNotices.RequestPermission();
                    },
                },
            };
            choice.Open("알림을 받을까요?", "하트가 다 차거나 순찰 보상이 가득 차면 알려 드려요", options);
            return true;
        }

        private void OnMissionCompleted(string line)
        {
            Audio.AudioManager.Play("reward", 0.8f);
            ShowNotice(line);
        }

        /// <summary>"출석 3일째  보상글" 같은 줄들에서 앞 이름표를 떼고 " · "로 이어 붙인다 (보상 카드 한 장씩)</summary>
        private static string RewardTail(List<string> lines)
        {
            var parts = new List<string>();
            foreach (string line in lines)
            {
                string t = RewardPopup.Tail(line).Replace("조건 달성 보상 · ", string.Empty).Replace("장비  ", string.Empty).Trim();
                if (t.Length > 0) parts.Add(t);
            }

            return string.Join(" · ", parts);
        }

        // 상점이 열려 있으면 상점 화면이 알린다
        private void OnIapGranted(PurchaseEntry entry)
        {
            if (shop != null && shop.IsOpen) return;
            string granted = IapLedger.Text(entry);
            ShowNotice("결제한 상품을 받았어요: " + granted);
            RewardPopup.Show(granted);
        }

        private void OnIapNotice(string text)
        {
            if (shop == null || !shop.IsOpen) ShowNotice(text);
        }

        private void Update()
        {
            if (!_begun) return; // 동의 대기 중
            // 클라우드가 다른 기기에서 바뀌었거나 저장을 못 읽었다: 다른 창이 없을 때 한 번 고르게 한다 (LiveOps 3-2)
            if (Account.AccountService.ConflictPending && choice != null && !choice.IsOpen && (opening == null || !opening.IsOpen))
                _ = Account.AccountService.ResolveConflictAsync();
            if (Time.unscaledTime >= _nextWallet)
            {
                RenderWallet();
                RenderNewcomer(SaveSystem.Data);
                TryFirstUseHint();
            }
            UpdateNotice();
        }

        private void TryFirstUseHint()
        {
            if (!_readyForGuide || firstUseGuide == null || firstUseGuide.Used || firstUseGuide.Blocked) return;
            SaveData save = SaveSystem.Data;
            int ready = metaTable != null && metaTable.missions != null
                ? Missions.MissionService.ClaimableCount(save, metaTable.missions, Missions.MissionEvents.Context) : 0;
            bool inCombat = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != GameSession.LobbyScene;
            string id = Progress.FirstUseHints.Next(save, inCombat, false, Clock.Now(),
                Progress.FirstUseHints.CanLevelUp(save, metaTable, roster),
                Progress.FirstUseHints.HasGear(save, metaTable != null ? metaTable.equipment : null), ready);
            Button target = id == Progress.FirstUseHints.Newcomer ? newcomerButton
                : id == Progress.FirstUseHints.Missions ? missionButton
                : id == Progress.FirstUseHints.Shop ? shopButton : heroButton;
            if (id == null || target == null || !target.gameObject.activeInHierarchy) return;
            // 본 이력도 먼저 확정한다. 저장 실패 때 안내만 사라지고 이력만 남는 일을 피한다.
            firstUseGuide.Show(Progress.FirstUseHints.Text(id), target,
                () => SaveSystem.Commit(() => Progress.FirstUseHints.MarkShown(save, id)) == SaveSystem.CommitResult.Saved);
        }

        // 알림: 0.15초 튀어나옴 → 유지 → 0.2초 사라짐. 연달아 오면 줄을 세운다 (최대 3개, 같은 글은 한 번만)
        private const float NoticePop = 0.15f, NoticeFade = 0.2f, NoticeMinShow = 0.8f, NoticeSeconds = 5f;
        private const int NoticeQueueMax = 3;
        private readonly List<string> _noticeQueue = new List<string>();
        private bool _noticeShowing;
        private string _noticeText;
        private float _noticeStart;

        private void ShowNotice(string text)
        {
            if (noticeLabel == null || string.IsNullOrEmpty(text)) return;
            if (!_noticeShowing || !noticeLabel.gameObject.activeInHierarchy)
            {
                StartNotice(text);
                return;
            }

            if (text == _noticeText)
            {
                _noticeUntil = Time.unscaledTime + NoticeSeconds;
                return;
            }

            if (_noticeQueue.Contains(text)) return;
            if (_noticeQueue.Count >= NoticeQueueMax) _noticeQueue.RemoveAt(0);
            _noticeQueue.Add(text);
            // 뒤에 기다리는 알림이 있으면 지금 것은 최소 시간만 보이고 넘긴다
            _noticeUntil = Mathf.Min(_noticeUntil, Mathf.Max(Time.unscaledTime, _noticeStart + NoticeMinShow));
        }

        private void StartNotice(string text)
        {
            _noticeShowing = true;
            _noticeText = text;
            _noticeStart = Time.unscaledTime;
            _noticeUntil = _noticeStart + NoticeSeconds;
            noticeLabel.text = text;
            noticeLabel.alpha = 1f;
            noticeLabel.transform.localScale = Vector3.one * 0.7f;
            noticeLabel.gameObject.SetActive(true);
        }

        private void UpdateNotice()
        {
            if (!_noticeShowing || noticeLabel == null) return;
            float now = Time.unscaledTime;
            if (now >= _noticeUntil)
            {
                if (_noticeQueue.Count > 0)
                {
                    string next = _noticeQueue[0];
                    _noticeQueue.RemoveAt(0);
                    StartNotice(next);
                }
                else
                {
                    _noticeShowing = false;
                    noticeLabel.transform.localScale = Vector3.one;
                    noticeLabel.alpha = 1f;
                    noticeLabel.gameObject.SetActive(false);
                }

                return;
            }

            float age = now - _noticeStart;
            noticeLabel.transform.localScale = Vector3.one * (age < NoticePop ? Mathf.LerpUnclamped(0.7f, 1f, PanelIntro.OutBack(age / NoticePop)) : 1f);
            noticeLabel.alpha = Mathf.Clamp01((_noticeUntil - now) / NoticeFade);
        }

        /// <summary>골드 · 보석 · 하트(다음 충전까지) · 계정 레벨. 하트 시계 때문에 0.5초마다</summary>
        private void RenderWallet()
        {
            _nextWallet = Time.unscaledTime + 0.5f;
            RenderPatrol(SaveSystem.Data);
            SaveData d = SaveSystem.Data;
            int next = Hearts.SecondsToNext(d, Clock.UnixSeconds);
            if (goldLabel != null)
            {
                // 칸마다 아이콘 + 숫자 (하트는 다음 충전까지 시간을 작게)
                goldLabel.text = Wallet.Format(d.gold);
                if (gemLabel != null) gemLabel.text = Wallet.Format(Wallet.Gems(d));
                if (heartLabel != null) heartLabel.text = d.hearts >= Hearts.Max ? $"{d.hearts}/{Hearts.Max}" : $"{d.hearts}/{Hearts.Max} <size=70%>{Hearts.FormatTime(next)}</size>";
                if (levelLabel != null) levelLabel.text = $"{d.accountLevel}";
            }

            if (walletLabel == null) return;
            string heart = d.hearts >= Hearts.Max ? $"하트 {d.hearts}/{Hearts.Max}" : $"하트 {d.hearts}/{Hearts.Max} ({Hearts.FormatTime(next)})";
            string level = d.accountLevel >= AccountLevel.MaxLevel ? $"Lv {d.accountLevel}" : $"Lv {d.accountLevel} ({d.accountExp}/{AccountLevel.ExpToNext(d.accountLevel)})";
            walletLabel.text = $"골드 {Wallet.Format(d.gold)}   보석 {Wallet.Format(Wallet.Gems(d))}   {heart}   {level}";
        }

        private void StartDungeon()
        {
            if (!GameSession.TryStartDungeon(_chapter)) hearts.Open(Render);
        }

        private void StartSurvival()
        {
            if (!GameSession.SurvivalUnlocked(SaveSystem.Data))
            {
                ShowNotice("1구간 보스를 깨면 서바이벌이 열려요");
                return;
            }

            if (survivalMaps == null)
            {
                if (!GameSession.TryStartSurvival()) hearts.Open(Render);
                return;
            }

            survivalMaps.Open(map =>
            {
                if (!GameSession.TryStartSurvival(map)) hearts.Open(Render);
            });
        }

        private static int MaxChapter(SaveData data) => Mathf.Clamp(data.unlockedChapter, 1, ChapterNames.Length);

        private void ChangeChapter(int delta)
        {
            _chapter = Mathf.Clamp(_chapter + delta, 1, MaxChapter(SaveSystem.Data));
            Render();
        }

        public static string ChapterTitle(int chapter)
        {
            int index = Mathf.Clamp(chapter, 1, ChapterNames.Length) - 1;
            return $"{chapter}구간 · {ChapterNames[index]}";
        }

        private BossData BossFor(int chapter) => bosses.Find(b => b != null && b.chapter == chapter);

        /// <summary>홈 미션 띠: 오늘 활동 점수 · 받을 보상 · 아직 안 끝난 첫 미션 (2026-10-29 개발자: 미션이 홈에 없어서 불편)</summary>
        private void RenderHomeMission(SaveData data, int ready)
        {
            if (homeMissionLabel == null) return;
            MissionProgress next = data.missions.daily.Find(m => !m.done);
            string line1 = $"<b>오늘 미션</b>  {data.missions.dailyPoints}/100" + (ready > 0 ? $"   <color=#FFD966>● 받을 보상 {ready}</color>" : string.Empty);
            string line2 = next != null ? $"{next.title}  {MissionPanel.Progress(next)}" : data.missions.daily.Count > 0 ? "오늘 미션을 다 끝냈어요!" : "미션을 열어 보세요";
            homeMissionLabel.text = $"{line1}\n<size=85%><color=#C9D3E0>{line2}</color></size>";
            if (homeMissionFill != null) homeMissionFill.fillAmount = Mathf.Clamp01(data.missions.dailyPoints / 100f);
        }

        private void Render()
        {
            SaveData data = SaveSystem.Data;
            int max = MaxChapter(data);

            chapterLabel.text = ChapterTitle(_chapter);
            previousChapter.interactable = _chapter > 1;
            nextChapter.interactable = _chapter < max;

            BossData boss = BossFor(_chapter);
            string bossState = boss == null ? "보스 준비 중" : data.defeatedBosses.Contains(boss.id) ? $"보스 {boss.displayName} 격파함" : $"보스 {boss.displayName}";
            string next = _chapter == max && max < ChapterNames.Length ? $"\n{_chapter}구간 보스를 깨면 {_chapter + 1}구간이 열려요" : string.Empty;
            string stars = $"던전 {Stars.Display(Stars.Count(data, _chapter, Stars.Dungeon))}   보스 {Stars.Display(Stars.Count(data, _chapter, Stars.Boss))}   (별 {data.stars.Count}개)";
            chapterInfo.text = stars + "\n" + bossState + next;

            int records = data.records.Count;
            bool hasRecord = records > 0 || data.TempRecord != null;
            bossButton.interactable = boss != null && hasRecord;
            bossButtonLabel.text = boss == null ? "보스 준비 중" : hasRecord ? "보스 도전" : "보스 도전 (기록이 있어야 해요)";
            archiveButtonLabel.text = $"기록 {records}/{data.recordSlots}";
            RenderSurvival(data);
            RenderRiftAndDaily(data);
            RenderNewcomer(data);
            if (missionLabel != null && metaTable != null && metaTable.missions != null)
            {
                int ready = Missions.MissionService.ClaimableCount(data, metaTable.missions, Missions.MissionEvents.Context);
                missionLabel.text = ready > 0 ? $"미션 <color=#FFD966>●{ready}</color>" : "미션";
                RenderHomeMission(data, ready);
            }

            int found = data.discoveredRecipes.Count;
            codexButtonLabel.text = $"조합 도감\n{found}/{codex.RecipeCount}";
            HeroData hero = HeroUnlock.Selected(data, roster);
            if (heroUpgradeBadge != null) heroUpgradeBadge.text = UpgradeNotice.HasHeroUpgrade(data, metaTable, roster) ? "●" : string.Empty;
            heroButtonLabel.text = hero != null ? $"{hero.displayName} · {hero.ClassLabel}" : "영웅"; // 가운데 무대 영웅 이름표
            if (heroButtonPortraits != null && hero != null) heroButtonPortraits.ShowOnly(roster.heroes.IndexOf(hero));
            if (diorama != null) diorama.Show(_chapter, hero); // 고른 구간의 섬 + 지금 영웅
            RenderWallet();
        }

        /// <summary>서바이벌 버튼: 잠김 / 하트 2 + 고른 영웅의 최고 기록</summary>
        private void RenderSurvival(SaveData data)
        {
            if (survivalButton == null || survivalButtonLabel == null) return;
            if (!GameSession.SurvivalUnlocked(data))
            {
                survivalButtonLabel.text = "서바이벌\n<size=85%>1구간 보스를 깨면 열려요</size>";
                return;
            }

            // 확장 1차: 맵 3개 · 서바이벌 별 (던전 별과 따로)
            string dev = GameSession.SurvivalUnlockedByProgress(data) ? "" : " · 개발용 해금";
            int stars = data.survival.TotalStars();
            int maxStars = Survival.SurvivalProgress.MapIds.Length * 3;
            survivalButtonLabel.text = $"서바이벌\n<size=85%>★{stars}/{maxStars} · 하트 {GameSession.SurvivalHearts}{dev}</size>";
        }

        // ---------------------------------------------------------------- v1.0 신규 7일 미션 · 순찰

        private void RenderNewcomer(SaveData data)
        {
            if (newcomerButton == null) return;
            bool attendance = Missions.Attendance.Unlocked(data);
            newcomerButton.gameObject.SetActive(attendance);
            if (!attendance) return;
            if (newcomerBadge != null) newcomerBadge.text = UpgradeNotice.HasNewcomerReward(data, Clock.Now()) ? "●" : string.Empty;
            newcomerLabel.text = Missions.Newcomer.HomeLabel(data, Clock.Now());
        }

        private void RenderPatrol(SaveData data)
        {
            if (patrolButton == null) return;
            bool open = Patrol.Unlocked(data);
            patrolButton.gameObject.SetActive(open);
            if (!open) return;
            long now = Clock.UnixSeconds;
            int gold = Patrol.Pending(data, now);
            string full = Patrol.Full(data, now) ? "<color=#FFD966>가득</color>" : Wallet.Format(gold);
            patrolLabel.text = $"순찰\n<size=80%>{full}</size>";
        }

        private void ClaimPatrol()
        {
            int gold = 0;
            SaveSystem.CommitResult r = SaveSystem.Commit(() => (gold = Patrol.Claim(SaveSystem.Data, Clock.UnixSeconds)) > 0);
            if (r == SaveSystem.CommitResult.Saved) Audio.AudioManager.Play("coin");
            if (r == SaveSystem.CommitResult.Saved)
            {
                ShowNotice($"순찰 보상  골드 +{Wallet.Format(gold)} (최대 {Patrol.MaxHours}시간까지 쌓여요)");
                RewardPopup.Show($"<sprite name=gold>골드 +{Wallet.Format(gold)}");
            }
            else if (r == SaveSystem.CommitResult.SaveFailed) ShowNotice(SaveSystem.SaveFailedText);
            else ShowNotice($"순찰 중이에요 · 시간당 골드 {Patrol.GoldPerHour(SaveSystem.Data)} (최대 {Patrol.MaxHours}시간)");
            RenderWallet();
        }

        // ---------------------------------------------------------------- v0.6 무한의 균열 · 일일 도전

        /// <summary>3구간 보스를 깨야 열린다 (개발 빌드는 늘 열림)</summary>
        private static bool ModeOpen(SaveData data) => RiftRules.Unlocked(data) || Debug.isDebugBuild;

        private void RenderRiftAndDaily(SaveData data)
        {
            bool progress = RiftRules.Unlocked(data);
            string dev = progress ? string.Empty : " · 개발용 해금";
            if (riftButtonLabel != null)
            {
                riftButtonLabel.text = !ModeOpen(data) ? "무한의 균열\n<size=88%>3구간 보스를 깨면 열려요</size>"
                    : data.rift.weeklyBest > 0 ? $"무한의 균열\n<size=88%>이번 주 {data.rift.weeklyBest}층{dev}</size>"
                    : $"무한의 균열\n<size=88%>하트 없음 · 끝없는 층{dev}</size>";
            }

            if (dailyButtonLabel != null)
            {
                dailyButtonLabel.text = !ModeOpen(data) ? "일일 도전\n<size=88%>3구간 보스를 깨면 열려요</size>"
                    : data.dailyChallenge.officialDone ? $"일일 도전\n<size=88%>오늘 {data.dailyChallenge.officialScore}점{dev}</size>"
                    : $"일일 도전\n<size=88%><color=#FFD966>오늘 도전 가능</color>{dev}</size>";
            }
        }

        private void OpenRift()
        {
            SaveData data = SaveSystem.Data;
            if (!ModeOpen(data))
            {
                ShowNotice("3구간 보스를 깨면 무한의 균열이 열려요");
                return;
            }

            RiftRules.EnsureWeek(data, Clock.Now());
            RiftSave rift = data.rift;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<size=85%>층마다 방 1개 · 5층마다 엘리트 (깨면 보상 고르기) · 10층마다 보스 (체크포인트)");
            sb.AppendLine("적은 층마다 강해져요 · 하트 없음 · 기록은 남지 않아요");
            sb.AppendLine("스킬을 다 키우면 \"균열 강화\" 카드가 끝없이 나와요</size>");
            sb.AppendLine();
            sb.AppendLine($"이번 주 최고  <b>{rift.weeklyBest}층</b>   ·   역대 최고  {rift.bestEver}층");
            sb.AppendLine($"<color=#FFD966>{RiftRules.VariantText(RiftRules.VariantFor(rift.weekKey))}</color>");
            sb.AppendLine();
            sb.AppendLine("이번 주 층 보상 (주 1회씩)");
            foreach (int m in RiftRules.Milestones)
            {
                Reward r = RiftRules.RewardFor(m, out int shards);
                string text = r.ToString();
                if (shards > 0) text += (text.Length > 0 ? " · " : string.Empty) + $"고대 파편 {shards}";
                string state = rift.rewardsClaimed.Contains(m) ? "<color=#7FD17F>받음</color>" : "<color=#A9B4C2>아직</color>";
                sb.AppendLine($"  {m}층  {text}   {state}");
            }

            sb.AppendLine();
            RiftCheckpoint cp = rift.checkpoint;
            HeroData cpHero = cp.valid ? roster.Find(cp.heroId) : null;
            sb.AppendLine(cp.valid ? $"체크포인트  {cp.floor}층 보스 격파 ({(cpHero != null ? cpHero.displayName : cp.heroId)}, Lv {cp.level})" : "체크포인트  없음 (10층 보스를 깨면 생겨요)");
            sb.AppendLine("<size=85%><color=#A9B4C2>매주 월요일 05:00에 이번 주 기록·체크포인트가 초기화돼요</color></size>");

            HeroData hero = HeroUnlock.Selected(data, roster);
            string start = hero != null ? $"1층부터 ({hero.displayName})" : "1층부터";
            modeInfo.Open("무한의 균열", sb.ToString(), start, true, () => GameSession.TryStartRift(false),
                cp.valid ? $"{cp.floor + 1}층부터 이어하기" : "이어하기 (없음)", cp.valid, () => GameSession.TryStartRift(true), Render);
        }

        private void OpenDaily()
        {
            SaveData data = SaveSystem.Data;
            if (!ModeOpen(data))
            {
                ShowNotice("3구간 보스를 깨면 일일 도전이 열려요");
                return;
            }

            DailyRules.EnsureDay(data, Clock.Now());
            DailySave daily = data.dailyChallenge;
            string day = daily.dayKey;
            HeroData hero = roster.Find(DailyRules.HeroFor(day, roster.heroes.ConvertAll(h => h.id)));
            int chapter = DailyRules.ChapterFor(day);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"오늘의 영웅  <b>{(hero != null ? $"{hero.displayName} ({hero.ClassLabel})" : "?")}</b>");
            sb.AppendLine($"{ChapterTitle(chapter)}  ·  방 7개");
            sb.AppendLine("<size=85%>일반 4 · 엘리트 · 보상 · 중간 보스 — 모두 같은 판이에요</size>");
            sb.AppendLine("<size=85%>고정 세트: 영웅 Lv15 ★1 · 고급 장비 (강화 0) · 수련·유물·펫·룬 없음</size>");
            sb.AppendLine();
            sb.AppendLine("<color=#FFD966>오늘의 변형</color>");
            foreach (DailyRules.Modifier m in DailyRules.ModifiersFor(day)) sb.AppendLine("  · " + DailyRules.ModifierText(m));
            sb.AppendLine();
            sb.AppendLine($"공식 도전 하루 1번 (첫 시도가 기록) → 골드 {Wallet.Format(DailyRules.RewardGold)} + 영웅 조각 {DailyRules.RewardHeroShards}");
            sb.AppendLine(daily.officialDone ? $"오늘 공식 기록  <b>{daily.officialScore}점</b>" : "오늘 공식 기록  아직");
            if (daily.bestPractice > 0) sb.AppendLine($"<size=85%>연습 최고  {daily.bestPractice}점</size>");
            sb.AppendLine($"<size=85%>점수 = 깬 방 ×100 + 처치 + 남은 체력% ×2 + 클리어 500 + 빨리 깬 보너스</size>");
            if (daily.history.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"지난 기록 (참여 {daily.participations}일)");
                for (int i = 0; i < daily.history.Count && i < 5; i++)
                {
                    DailyResult r = daily.history[i];
                    HeroData h = roster.Find(r.heroId);
                    string date = r.dayKey != null && r.dayKey.Length >= 10 ? r.dayKey.Substring(5) : r.dayKey;
                    sb.AppendLine($"<size=85%>  {date}  {(h != null ? h.displayName : r.heroId)}  {r.score}점{(r.cleared ? "  클리어" : string.Empty)}</size>");
                }
            }

            bool available = DailyRules.OfficialAvailable(data);
            modeInfo.Open("일일 도전", sb.ToString(), available ? "공식 도전" : "공식 도전 (오늘 완료)", available, () => GameSession.TryStartDaily(true),
                "연습\n<size=70%>기록·보상 없음</size>", true, () => GameSession.TryStartDaily(false), Render);
        }

        private void OpenBoss()
        {
            BossData boss = BossFor(_chapter);
            if (boss == null) return;
            bossIntro.Open(boss, _chapter);
        }
    }
}
