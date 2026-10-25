using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
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
        [SerializeField] private Button archiveButton;
        [SerializeField] private TMP_Text archiveButtonLabel;
        [SerializeField] private Button codexButton;
        [SerializeField] private TMP_Text codexButtonLabel;

        [Header("패널")]
        [SerializeField] private BossIntroPanel bossIntro;
        [SerializeField] private RecordArchivePanel archive;
        [SerializeField] private CodexPanel codex;
        [SerializeField] private HeartPanel hearts;

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
            archiveButton.onClick.AddListener(() => archive.Open(Render));
            codexButton.onClick.AddListener(() => codex.Open());

            // 놓친 별 상자가 있으면 여기서 받는다
            Reward box = Stars.ClaimBoxes(data);
            if (!box.IsEmpty)
            {
                SaveSystem.Save();
                ShowNotice("별 상자를 열었어요: " + box);
            }

            SaveSystem.SaveIfDirty(); // 지난 저장이 실패했으면 다시 시도
            Render();
        }

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

            int found = data.discoveredRecipes.Count;
            codexButtonLabel.text = $"조합 도감  {found}/{codex.RecipeCount}";
            RenderWallet();
        }

        private void OpenBoss()
        {
            BossData boss = BossFor(_chapter);
            if (boss == null) return;
            bossIntro.Open(boss, _chapter);
        }
    }
}
