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

                OfferLegendaryChoice();
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

        private void OpenBoss()
        {
            BossData boss = BossFor(_chapter);
            if (boss == null) return;
            bossIntro.Open(boss, _chapter);
        }
    }
}
