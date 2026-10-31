using System;
using System.Collections.Generic;
using HistoryHeroes.Economy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Missions;
using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 로비 "미션" (Missions 2·3·7장): 일일 · 주간 · 업적 탭.
    /// 일일: 미션 6개 (끝나면 포인트가 바로 쌓인다) + 포인트 상자 5개 + 하루 1번 바꾸기. 주간: 6개 + 상자 3개. 업적: 단계별, 누르면 보석.
    /// 남은 시간은 조용히 표시만 한다 (재촉하지 않는다, 1장).
    /// </summary>
    public sealed class MissionPanel : MonoBehaviour
    {
        private enum Tab { Daily, Weekly, Achievements }

        [SerializeField] private GameObject root;
        [SerializeField] private Meta.MetaTable metaTable;
        [SerializeField] private HeroRoster roster;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text infoLabel;
        [SerializeField] private Button[] tabButtons;
        [SerializeField] private TMP_Text[] tabLabels;
        [SerializeField] private Button[] chestButtons;
        [SerializeField] private TMP_Text[] chestLabels;
        [SerializeField] private Image[] chestGlows;
        [SerializeField] private ButtonList list;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button rerollButton;
        [SerializeField] private TMP_Text rerollLabel;
        [SerializeField] private Button backButton;

        private static readonly Color TabNormal = new Color(0.16f, 0.18f, 0.24f);
        private static readonly Color TabFocused = new Color(0.25f, 0.42f, 0.7f);
        private static readonly Color RowNormal = new Color(0.13f, 0.15f, 0.2f);
        private static readonly Color RowDone = new Color(0.14f, 0.3f, 0.2f);
        private static readonly Color RowSelected = new Color(0.22f, 0.36f, 0.6f);
        private static readonly Color RowClaim = new Color(0.42f, 0.33f, 0.1f);
        private static readonly Color ChestReady = new Color(0.55f, 0.42f, 0.12f);
        private static readonly Color ChestLocked = new Color(0.16f, 0.18f, 0.24f);
        private static readonly Color ChestTaken = new Color(0.1f, 0.22f, 0.15f);

        private MissionTable Table => metaTable != null ? metaTable.missions : null;

        private Tab _tab;
        private int _selected = -1;
        private string _message;
        private Action _onClosed;

        private void Awake()
        {
            root.SetActive(false);
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() =>
                {
                    _tab = (Tab)index;
                    _selected = -1;
                    _message = null;
                    Render();
                });
            }

            for (int i = 0; i < chestButtons.Length; i++)
            {
                int index = i;
                chestButtons[i].onClick.AddListener(() => ClaimChest(index));
            }

            rerollButton.onClick.AddListener(Reroll);
            backButton.onClick.AddListener(() =>
            {
                root.SetActive(false);
                _onClosed?.Invoke();
            });
        }

        public void Open(Action onClosed = null)
        {
            _onClosed = onClosed;
            _tab = Tab.Daily;
            _selected = -1;
            _message = null;
            MissionEvents.EnsureCurrent();
            SaveSystem.SaveIfDirty();
            root.SetActive(true);
            Render();
        }

        private void Update()
        {
            if (!root.activeSelf) return;
            // 05:00이 지나면 열려 있는 동안에도 새 미션으로
            MissionTable table = Table;
            if (table != null && SaveSystem.Data.missions.dayKey != MissionService.DayKey(Clock.Now(), table.resetHour))
            {
                MissionEvents.EnsureCurrent();
                SaveSystem.SaveIfDirty();
                Render();
            }
        }

        private void Render()
        {
            MissionTable table = Table;
            if (table == null) return;
            SaveData save = SaveSystem.Data;
            MissionService.Context ctx = MissionEvents.Context;
            titleLabel.text = "미션";

            int dailyReady = 0, weeklyReady = 0, achReady = 0;
            foreach (ChestDef c in table.dailyChests) if (MissionService.CanClaimDaily(save, c)) dailyReady++;
            foreach (ChestDef c in table.weeklyChests) if (MissionService.CanClaimWeekly(save, c)) weeklyReady++;
            foreach (AchievementDef a in table.achievements) if (MissionService.CanClaim(save, a, ctx)) achReady++;
            string[] names = { "일일", "주간", "업적" };
            int[] ready = { dailyReady, weeklyReady, achReady };
            for (int i = 0; i < tabButtons.Length; i++)
            {
                tabButtons[i].GetComponent<Image>().color = (int)_tab == i ? TabFocused : TabNormal;
                tabLabels[i].text = ready[i] > 0 ? $"{names[i]} <color=#FFD966>●{ready[i]}</color>" : names[i];
            }

            switch (_tab)
            {
                case Tab.Daily: RenderDaily(save, table); break;
                case Tab.Weekly: RenderWeekly(save, table); break;
                default: RenderAchievements(save, table, ctx); break;
            }

            messageLabel.text = _message ?? string.Empty;
        }

        private void RenderDaily(SaveData save, MissionTable table)
        {
            MissionSave ms = save.missions;
            TimeSpan left = NextReset(table, weekly: false);
            infoLabel.text = $"오늘 활동 포인트 <b>{ms.dailyPoints}</b>/100 · 새 미션까지 {(int)left.TotalHours}시간 {left.Minutes}분";
            RenderChests(table.dailyChests, c => MissionService.CanClaimDaily(save, c), c => ms.dailyChests.Contains(c.points), ms.dailyPoints);

            var rows = new List<ButtonList.Row>();
            for (int i = 0; i < ms.daily.Count; i++)
            {
                int index = i;
                MissionProgress m = ms.daily[i];
                bool canPick = i >= table.dailyFixed.Count && !m.done && !ms.rerollUsed;
                Color bg = m.done ? RowDone : i == _selected ? RowSelected : RowNormal;
                rows.Add(new ButtonList.Row(MissionText(m), bg, canPick ? () => Select(index) : (Action)null, canPick, ProgressAmount(m)));
            }

            list.Show(rows);
            rerollButton.gameObject.SetActive(true);
            rerollButton.interactable = !ms.rerollUsed && _selected >= table.dailyFixed.Count && _selected < ms.daily.Count && !ms.daily[_selected].done;
            rerollLabel.text = ms.rerollUsed ? "오늘 교체 완료" : _selected < 0 ? "미션 교체: 미완료 줄 선택" : "선택한 미션 교체 (하루 1회)";
        }

        private void RenderWeekly(SaveData save, MissionTable table)
        {
            MissionSave ms = save.missions;
            TimeSpan left = NextReset(table, weekly: true);
            infoLabel.text = $"이번 주 포인트 <b>{ms.weeklyPoints}</b>/{table.weeklyPointsEach * ms.weekly.Count} · 월요일 05:00까지 {(int)left.TotalDays}일 {left.Hours}시간";
            RenderChests(table.weeklyChests, c => MissionService.CanClaimWeekly(save, c), c => ms.weeklyChests.Contains(c.points), ms.weeklyPoints);

            var rows = new List<ButtonList.Row>();
            foreach (MissionProgress m in ms.weekly) rows.Add(new ButtonList.Row(MissionText(m), m.done ? RowDone : RowNormal, null, false, ProgressAmount(m)));
            list.Show(rows);
            rerollButton.gameObject.SetActive(false);
        }

        private void RenderAchievements(SaveData save, MissionTable table, MissionService.Context ctx)
        {
            int total = 0, got = 0;
            var rows = new List<ButtonList.Row>();
            var claimable = new List<ButtonList.Row>();
            foreach (AchievementDef a in table.achievements)
            {
                int tiers = a.targets.Length;
                int claimed = MissionService.ClaimedTiers(save, a.id);
                total += tiers;
                got += claimed;
                int progress = MissionService.AchievementProgress(save, a, ctx);
                bool can = MissionService.CanClaim(save, a, ctx);
                string text;
                if (claimed >= tiers)
                {
                    text = $"<color=#7CE38B>[완료]</color> <b>{a.title}</b>  <size=85%>{a.category} · 다 받음</size>";
                    rows.Add(new ButtonList.Row(text, RowDone, null, false));
                    continue;
                }

                int target = MissionService.Target(a, claimed, ctx);
                int gems = a.gems[Mathf.Min(claimed, a.gems.Length - 1)];
                string goal = string.Format(a.goal, target.ToString("N0"));
                string stage = tiers > 1 ? $" {claimed + 1}/{tiers}단계" : string.Empty;
                text = $"<b>{a.title}</b>{stage}  <size=85%>{a.category}</size>\n<size=88%>{goal}  {Mathf.Min(progress, target):N0}/{target:N0}  ·  보석 {gems}</size>";
                if (can)
                {
                    AchievementDef def = a;
                    claimable.Add(new ButtonList.Row(text + "  <color=#FFD966>[받기]</color>", RowClaim, () => ClaimAchievement(def), true, target > 0 ? Mathf.Clamp01((float)progress / target) : 1f));
                }
                else
                {
                    rows.Add(new ButtonList.Row(text, RowNormal, null, false, target > 0 ? Mathf.Clamp01((float)progress / target) : 0f));
                }
            }

            claimable.AddRange(rows); // 받을 수 있는 것을 맨 위에
            infoLabel.text = $"업적 {got}/{total}단계 · 업적은 영구 (한 번만 받아요)";
            RenderChests(new List<ChestDef>(), _ => false, _ => false, 0);
            list.Show(claimable);
            rerollButton.gameObject.SetActive(false);
        }

        /// <summary>진행도 글 (서바이벌 시간은 분으로). 로비 홈 미션 띠도 쓴다</summary>
        public static string Progress(MissionProgress m) => m.eventKey == MissionEvents.SurvivalSeconds
            ? $"{m.progress / 60}분/{m.target / 60}분"
            : $"{m.progress}/{m.target}";

        public static float ProgressAmount(MissionProgress m) => m.done ? 1f : m.target > 0 ? Mathf.Clamp01((float)m.progress / m.target) : 0f;

        private static string MissionText(MissionProgress m)
        {
            string progress = Progress(m);
            string mark = m.done ? "<color=#7CE38B>[완료]</color> " : string.Empty;
            return $"{mark}<b>{m.title}</b>\n<size=88%>{progress}  ·  활동 +{m.points}{(m.done ? " (받음)" : string.Empty)}</size>";
        }

        private void RenderChests(List<ChestDef> chests, Func<ChestDef, bool> canClaim, Func<ChestDef, bool> taken, int points)
        {
            string hero = SaveSystem.Data.selectedHero;
            for (int i = 0; i < chestButtons.Length; i++)
            {
                bool exists = i < chests.Count;
                chestButtons[i].gameObject.SetActive(exists);
                if (!exists) continue;
                ChestDef c = chests[i];
                bool ready = canClaim(c);
                bool done = taken(c);
                chestButtons[i].interactable = ready;
                chestButtons[i].GetComponent<Image>().color = done ? ChestTaken : ready ? ChestReady : ChestLocked;
                if (chestGlows != null && i < chestGlows.Length && chestGlows[i] != null) chestGlows[i].gameObject.SetActive(ready);
                string state = done ? " <color=#7CE38B>[받음]</color>" : ready ? " <color=#FFD966>[받기]</color>" : " <color=#A9B4C2>[잠김]</color>";
                chestLabels[i].text = $"<b>{c.points}</b>{state}\n<size=70%>{MissionService.RewardText(c, hero)}</size>";
            }
        }

        private void Select(int index)
        {
            _selected = index;
            _message = null;
            Render();
        }

        private void ClaimChest(int index)
        {
            MissionTable table = Table;
            SaveData save = SaveSystem.Data;
            string hero = save.selectedHero;
            List<ChestDef> chests = _tab == Tab.Daily ? table.dailyChests : table.weeklyChests;
            if (index >= chests.Count) return;
            ChestDef chest = chests[index];
            var notices = new List<string>();
            SaveSystem.CommitResult r = SaveSystem.Commit(() => _tab == Tab.Daily
                ? MissionService.ClaimDaily(save, table, chest, hero, notices)
                : MissionService.ClaimWeekly(save, chest, hero));
            if (r == SaveSystem.CommitResult.Rejected) return;
            _message = r == SaveSystem.CommitResult.Saved
                ? $"<color=#FFD966>{chest.points} 상자: {MissionService.RewardText(chest, hero)}</color>" + (notices.Count > 0 ? "\n" + string.Join(" · ", notices) : string.Empty)
                : SaveSystem.SaveFailedText;
            Render();
        }

        private void ClaimAchievement(AchievementDef def)
        {
            SaveData save = SaveSystem.Data;
            int tier = MissionService.ClaimedTiers(save, def.id);
            int gems = def.gems[Mathf.Min(tier, def.gems.Length - 1)];
            SaveSystem.CommitResult r = SaveSystem.Commit(() => MissionService.ClaimAchievement(save, def, MissionEvents.Context));
            if (r == SaveSystem.CommitResult.Rejected) return;
            _message = r == SaveSystem.CommitResult.Saved ? $"<color=#FFD966>업적 {def.title}: 보석 +{gems}</color>" : SaveSystem.SaveFailedText;
            Render();
        }

        private void Reroll()
        {
            MissionTable table = Table;
            SaveData save = SaveSystem.Data;
            int index = _selected;
            SaveSystem.CommitResult r = SaveSystem.Commit(() => MissionService.Reroll(save, table, index, Clock.Now(), MissionEvents.Context));
            if (r == SaveSystem.CommitResult.Rejected)
            {
                _message = "<color=#A9B4C2>바꿀 수 있는 다른 미션이 없어요</color>";
                Render();
                return;
            }

            _selected = -1;
            _message = r == SaveSystem.CommitResult.Saved ? $"<color=#7CE38B>새 미션: {save.missions.daily[index].title}</color>" : SaveSystem.SaveFailedText;
            Render();
        }

        private static TimeSpan NextReset(MissionTable table, bool weekly)
        {
            DateTimeOffset now = Clock.Now();
            DateTimeOffset shifted = now.AddHours(-table.resetHour);
            DateTimeOffset next = new DateTimeOffset(shifted.Date, shifted.Offset).AddDays(1).AddHours(table.resetHour);
            if (weekly)
            {
                int toMonday = ((int)DayOfWeek.Monday - (int)shifted.DayOfWeek + 7) % 7;
                if (toMonday == 0) toMonday = 7;
                next = new DateTimeOffset(shifted.Date, shifted.Offset).AddDays(toMonday).AddHours(table.resetHour);
            }

            TimeSpan left = next - now;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }
    }
}
