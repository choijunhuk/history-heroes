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
        [SerializeField] private Button archiveButton;
        [SerializeField] private TMP_Text archiveButtonLabel;
        [SerializeField] private Button codexButton;
        [SerializeField] private TMP_Text codexButtonLabel;
        [SerializeField] private Button heroButton;
        [SerializeField] private TMP_Text heroButtonLabel;
        [SerializeField] private HeroRoster roster;

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
        [SerializeField] private MissionPanel missions;
        [Header("v0.6 무한의 균열 · 일일 도전")]
        [SerializeField] private Button riftButton;
        [SerializeField] private TMP_Text riftButtonLabel;
        [SerializeField] private Button dailyButton;
        [SerializeField] private TMP_Text dailyButtonLabel;
        [SerializeField] private ModeInfoPanel modeInfo;

        private int _chapter = 1;
        private float _nextWallet;
        private float _noticeUntil;

        private void Start()
        {
            GamePause.ResetAll();
            SaveData data = SaveSystem.Data;
            _chapter = Mathf.Clamp(GameSession.Chapter, 1, MaxChapter(data));

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
            if (riftButton != null) riftButton.onClick.AddListener(OpenRift);
            if (dailyButton != null) dailyButton.onClick.AddListener(OpenDaily);

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
                ShowNotice("별 상자를 열었어요: " + box);
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
                    ShowNotice(string.Join(" · ", relics));
                }
            }

            // 펫 (v0.5): 펫이 생기기 전에 채운 조건도 여기서
            if (metaTable != null && metaTable.pets != null)
            {
                List<string> pets = Pets.PetService.CheckAll(data, metaTable.pets);
                if (pets.Count > 0)
                {
                    SaveSystem.Save();
                    ShowNotice(string.Join(" · ", pets));
                }
            }

            // 장비가 생기기 전에 깬 보스의 장비 보상 (v0.4)
            if (metaTable != null)
            {
                List<string> gear = Equipment.EquipmentInventory.GrantBossRewards(data, metaTable.equipment);
                if (gear.Count > 0)
                {
                    SaveSystem.Save();
                    ShowNotice("보스 격파 보상: " + string.Join(", ", gear).Replace("장비  ", ""));
                }

                if (!OfferResume()) OfferLegendaryChoice();
            }
            else
            {
                OfferResume();
            }

            SaveSystem.SaveIfDirty(); // 지난 저장이 실패했으면 다시 시도
            Render();
        }

        private void OnDestroy() => Missions.MissionEvents.Completed -= OnMissionCompleted;

        /// <summary>5구간 보스 최초 격파: 전설 장비 1개 고르기 (Equipment 8장). "나중에"를 누르면 다음에 로비에서 또</summary>
        private void OfferLegendaryChoice()
        {
            SaveData data = SaveSystem.Data;
            if (choice == null || metaTable == null || metaTable.equipment == null || !Equipment.EquipmentInventory.LegendaryChoicePending(data)) return;
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

            if (options.Count > 0) choice.Open("5구간 보스 보상", "전설 장비 1개를 골라요 (한 번만)", options);
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
            bool stale = mode == BattleMode.Survival
                         || (mode == BattleMode.Daily && run.dayKey != DailyRules.DayKey(Clock.Now()))
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
                : $"{ChapterTitle(run.chapter)} 던전 방 {run.room}부터";
            var options = new List<ChoicePanel.Option>
            {
                new ChoicePanel.Option { Text = $"<b>이어하기</b>  <size=80%>{where}{who}</size>", OnChosen = () => GameSession.StartResume(run) },
                new ChoicePanel.Option
                {
                    Text = "<b>그만두기</b>  <size=80%>이번 판은 사라져요 (보상 없음)</size>",
                    OnChosen = () =>
                    {
                        SaveSystem.Data.run = new RunSnapshot();
                        SaveSystem.Save();
                        ShowNotice("이번 판을 그만뒀어요");
                    },
                },
            };
            choice.Open("끝나지 않은 판이 있어요", "앱이 꺼져서 멈춘 판을 이어할 수 있어요", options);
            return true;
        }

        private void OnMissionCompleted(string line) => ShowNotice(line);

        private void Update()
        {
            if (Time.unscaledTime >= _nextWallet) RenderWallet();
            if (noticeLabel != null && noticeLabel.gameObject.activeSelf && Time.unscaledTime > _noticeUntil) noticeLabel.gameObject.SetActive(false);
        }

        private void ShowNotice(string text)
        {
            if (noticeLabel == null) return;
            noticeLabel.text = text;
            noticeLabel.gameObject.SetActive(true);
            _noticeUntil = Time.unscaledTime + 5f;
        }

        /// <summary>골드 · 보석 · 하트(다음 충전까지) · 계정 레벨. 하트 시계 때문에 0.5초마다</summary>
        private void RenderWallet()
        {
            _nextWallet = Time.unscaledTime + 0.5f;
            if (walletLabel == null) return;
            SaveData d = SaveSystem.Data;
            int next = Hearts.SecondsToNext(d, Clock.UnixSeconds);
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
                ShowNotice("2구간 보스를 깨면 서바이벌이 열려요");
                return;
            }

            if (!GameSession.TryStartSurvival()) hearts.Open(Render);
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
            bool hasRecord = records > 0;
            bossButton.interactable = boss != null && hasRecord;
            bossButtonLabel.text = boss == null ? "보스 준비 중" : hasRecord ? "보스 도전" : "보스 도전 (기록이 있어야 해요)";
            archiveButtonLabel.text = $"기록 보관함  {records}/{data.recordSlots}";
            RenderSurvival(data);
            RenderRiftAndDaily(data);
            if (missionLabel != null && metaTable != null && metaTable.missions != null)
            {
                int ready = Missions.MissionService.ClaimableCount(data, metaTable.missions, Missions.MissionEvents.Context);
                missionLabel.text = ready > 0 ? $"미션 <color=#FFD966>●{ready}</color>" : "미션";
            }

            int found = data.discoveredRecipes.Count;
            codexButtonLabel.text = $"조합 도감  {found}/{codex.RecipeCount}";
            HeroData hero = HeroUnlock.Selected(data, roster);
            heroButtonLabel.text = hero != null ? $"영웅  {hero.displayName} ({hero.ClassLabel})" : "영웅";
            RenderWallet();
        }

        /// <summary>서바이벌 버튼: 잠김 / 하트 2 + 고른 영웅의 최고 기록</summary>
        private void RenderSurvival(SaveData data)
        {
            if (survivalButton == null || survivalButtonLabel == null) return;
            if (!GameSession.SurvivalUnlocked(data))
            {
                survivalButtonLabel.text = "서바이벌\n<size=60%>2구간 보스를 깨면 열려요</size>";
                return;
            }

            HeroData hero = HeroUnlock.Selected(data, roster);
            float best = hero != null ? data.survival.SecondsOf(hero.id) : 0f;
            string dev = GameSession.SurvivalUnlockedByProgress(data) ? "" : " · 개발용 해금";
            string record = best > 0f ? $"최고 {Survival.SurvivalRun.Format(best)}" : "10분 버티기";
            survivalButtonLabel.text = $"서바이벌\n<size=60%>{record} · 하트 {GameSession.SurvivalHearts}{dev}</size>";
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
                riftButtonLabel.text = !ModeOpen(data) ? "무한의 균열\n<size=60%>3구간 보스를 깨면 열려요</size>"
                    : data.rift.weeklyBest > 0 ? $"무한의 균열\n<size=60%>이번 주 {data.rift.weeklyBest}층{dev}</size>"
                    : $"무한의 균열\n<size=60%>하트 없음 · 끝없는 층{dev}</size>";
            }

            if (dailyButtonLabel != null)
            {
                dailyButtonLabel.text = !ModeOpen(data) ? "일일 도전\n<size=60%>3구간 보스를 깨면 열려요</size>"
                    : data.dailyChallenge.officialDone ? $"일일 도전\n<size=60%>오늘 {data.dailyChallenge.officialScore}점{dev}</size>"
                    : $"일일 도전\n<size=60%><color=#FFD966>오늘 도전 가능</color>{dev}</size>";
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
                "연습\n<size=60%>기록·보상 없음</size>", true, () => GameSession.TryStartDaily(false), Render);
        }

        private void OpenBoss()
        {
            BossData boss = BossFor(_chapter);
            if (boss == null) return;
            bossIntro.Open(boss, _chapter);
        }
    }
}
