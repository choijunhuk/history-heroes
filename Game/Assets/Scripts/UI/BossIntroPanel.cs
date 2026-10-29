using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Modes;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 보스 시작 전 화면 (Modes.md 2-9): 보스 소개 + 이 보스가 시험하는 것 + 추천 + 기록 고르기.
    /// 패치로 용량을 넘은 기록은 바로 시작하지 않고 다시 고르게 한다 (GameDesign 7-5).
    /// </summary>
    public sealed class BossIntroPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text testsLabel;
        [SerializeField] private TMP_Text recommendLabel;
        [SerializeField] private Button normalTab;
        [SerializeField] private Button trialTab;
        [SerializeField] private Button[] trialButtons;
        [SerializeField] private TMP_Text[] trialLabels;
        [SerializeField] private Button weeklyGoalButton;
        [SerializeField] private TMP_Text weeklyGoalLabel;
        [SerializeField] private BossData thorTrial;
        [SerializeField] private BossData[] godTrials;
        [SerializeField] private OpeningPanel opening;

        [Header("기록 칸")]
        [SerializeField] private Button[] slotButtons;
        [SerializeField] private Image[] slotBackgrounds;
        [SerializeField] private TMP_Text[] slotLabels;
        [SerializeField] private Button tempButton;
        [SerializeField] private Image tempBackground;
        [SerializeField] private TMP_Text tempLabel;

        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private Button practiceButton;

        [SerializeField] private SkillDatabase database;
        [SerializeField] private RecordSaveScreen reselect;
        [SerializeField] private HeartPanel hearts;

        private static readonly Color Normal = new Color(0.16f, 0.18f, 0.24f);
        private static readonly Color Selected = new Color(0.25f, 0.42f, 0.7f);

        private BossData _boss;
        private BossData _normalBoss;
        private bool _trial;
        private int _chapter;
        private string _selectedRecordId;
        private float _nextWeekCheck;

        private Color _startColor;

        private void Awake()
        {
            root.SetActive(false);
            _startColor = startButton.image.color;
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i;
                slotButtons[i].onClick.AddListener(() => Select(slot));
            }

            if (tempButton != null) tempButton.onClick.AddListener(() =>
            {
                RunRecord record = SaveSystem.Data.TempRecord;
                if (record != null) SelectRecord(record);
            });

            startButton.onClick.AddListener(StartFight);
            if (practiceButton != null) practiceButton.onClick.AddListener(StartPractice);
            if (normalTab != null) normalTab.onClick.AddListener(() => SwitchTab(false));
            if (trialTab != null) trialTab.onClick.AddListener(() => SwitchTab(true));
            if (trialButtons != null)
                for (int i = 0; i < trialButtons.Length; i++)
                {
                    int index = i;
                    trialButtons[i].onClick.AddListener(() => SelectTrial(index));
                }
            if (weeklyGoalButton != null) weeklyGoalButton.onClick.AddListener(ChooseWeeklyGoal);
            backButton.onClick.AddListener(() => root.SetActive(false));
        }

        private void Update()
        {
            if (!root.activeSelf || !_trial || Time.unscaledTime < _nextWeekCheck) return;
            _nextWeekCheck = Time.unscaledTime + 1f;
            if (!GodTrialRules.EnsureWeek(SaveSystem.Data, Clock.Now())) return;
            SaveSystem.Save();
            Render();
        }

        public void Open(BossData boss, int chapter)
        {
            _normalBoss = boss;
            _chapter = chapter;
            _trial = false;
            ShowBoss(boss);
            root.SetActive(true);
        }

        private void SwitchTab(bool trial)
        {
            if (trial && TrialBoss(GodTrialRules.ThorTrialId) == null) return;
            _trial = trial;
            if (trial && GodTrialRules.EnsureWeek(SaveSystem.Data, Clock.Now())) SaveSystem.Save();
            ShowBoss(trial ? TrialBoss(GodTrialRules.ThorTrialId) : _normalBoss);
        }

        private BossData TrialBoss(string trialId)
        {
            if (godTrials != null)
                foreach (BossData trial in godTrials)
                    if (trial != null && trial.id == trialId) return trial;
            return thorTrial != null && thorTrial.id == trialId ? thorTrial : null;
        }

        private void SelectTrial(int index)
        {
            if (!_trial || index < 0 || index >= GodTrialRules.Trials.Count) return;
            BossData trial = TrialBoss(GodTrialRules.Trials[index].Id);
            if (trial != null) ShowBoss(trial);
        }

        private void ShowBoss(BossData boss)
        {
            if (boss == null) return;
            _boss = boss;
            titleLabel.text = boss.displayName + (_trial && SaveSystem.Data.godTrials.cleared.Contains(boss.id) ? "  <color=#7CE38B><size=60%>첫 격파 완료</size></color>" : string.Empty);
            bool trialLocked = _trial && !GodTrialRules.IsUnlocked(SaveSystem.Data, boss.id);
            GodTrialDefinition definition = _trial ? GodTrialRules.Find(boss.id) : null;
            string condition = definition == null || definition.Order == 1 ? "망각의 왕"
                : GodTrialRules.Trials[definition.Order - 2].DisplayName + " 시련";
            testsLabel.text = trialLocked ? $"{condition}을 본인 기록으로 처음 격파하면 열려요"
                : "시험하는 것: " + boss.tests;
            string hint = boss.id == GodTrialRules.AthenaTrialId ? string.Empty
                : boss.id == GodTrialRules.SusanooTrialId ? "그림자 표식은 열린 형상을 사거리에 두고 멈춰서 풀어요"
                : boss.prefab != null ? Enemy.BossGimmicks.HintFor(Enemy.BossGimmicks.For(boss.prefab)) : string.Empty;
            if (!trialLocked && !string.IsNullOrEmpty(hint)) testsLabel.text += $"\n<color=#C9A8FF>기믹: {hint}</color>";
            recommendLabel.text = trialLocked ? "기록을 모아 마지막 보스에 도전해 보세요"
                : boss.recommendedRole != SkillRole.None
                ? $"추천: <color={RecordStrength.RoleColor(boss.recommendedRole)}>{RecordStrength.RoleName(boss.recommendedRole)} 강점</color> · {boss.recommend}"
                : "추천: " + boss.recommend;

            // 이 보스를 아직 못 깬 기록 중 가장 최근 것을 먼저 골라 둔다 (★3 "서로 다른 기록 2개" 유도)
            _selectedRecordId = null;
            SaveData data = SaveSystem.Data;
            RunRecord pick = null;
            foreach (RunRecord r in data.records)
            {
                if (pick == null || !RecordHasWon(r, data)) pick = r;
            }

            RunRecord temp = data.TempRecord;
            if (temp != null && (pick == null || !RecordHasWon(temp, data))) pick = temp;

            if (pick != null) _selectedRecordId = pick.id;
            Render();
        }

        private void ChooseWeeklyGoal()
        {
            SaveData data = SaveSystem.Data;
            var candidates = new List<GodTrialDefinition>();
            foreach (GodTrialDefinition trial in GodTrialRules.Trials)
                if (trial.HeroId != null && GodTrialRules.IsUnlocked(data, trial.Id)
                    && !GodTrialRules.IsHeroUnlocked(data, trial.HeroId)) candidates.Add(trial);
            if (candidates.Count == 0) return;
            int current = candidates.FindIndex(t => t.Id == data.godTrials.goalTrialId);
            string nextId = candidates[(current + 1) % candidates.Count].Id;
            SaveSystem.CommitResult result = SaveSystem.Commit(() =>
                GodTrialRules.TryChooseGoal(SaveSystem.Data, nextId, Clock.Now()));
            if (result == SaveSystem.CommitResult.SaveFailed) weeklyGoalLabel.text = SaveSystem.SaveFailedText;
            else Render();
        }

        private void Select(int slot)
        {
            SelectRecord(SaveSystem.Data.RecordInSlot(slot));
        }

        private void SelectRecord(RunRecord record)
        {
            if (record == null) return;
            _selectedRecordId = record.id;
            Render();
        }

        private void Render()
        {
            SaveData data = SaveSystem.Data;
            bool trialUnlocked = !_trial || GodTrialRules.IsUnlocked(data, _boss.id);
            if (normalTab != null) normalTab.GetComponent<Image>().color = _trial ? Normal : Selected;
            if (trialTab != null) trialTab.GetComponent<Image>().color = _trial ? Selected : Normal;
            if (trialButtons != null)
                for (int i = 0; i < trialButtons.Length && i < GodTrialRules.Trials.Count; i++)
                {
                    GodTrialDefinition definition = GodTrialRules.Trials[i];
                    trialButtons[i].gameObject.SetActive(_trial);
                    trialButtons[i].interactable = TrialBoss(definition.Id) != null;
                    trialButtons[i].image.color = _boss.id == definition.Id ? Selected : Normal;
                    if (trialLabels != null && i < trialLabels.Length && trialLabels[i] != null)
                    {
                        string clearText = data.godTrials.cleared.Contains(definition.Id) ? " <color=#7CE38B>완료</color>" : string.Empty; // ✔ 글자는 글꼴에 없다
                        if (GodTrialRules.IsUnlocked(data, definition.Id))
                            trialLabels[i].text = definition.DisplayName + clearText;
                        else
                        {
                            string predecessor = definition.Order == 1 ? "망각의 왕"
                                : GodTrialRules.Trials[definition.Order - 2].DisplayName + " 시련";
                            trialLabels[i].text = $"{definition.DisplayName} <color=#FFB347>잠김</color>\n<size=65%>{predecessor} 첫 승리</size>"; // 자물쇠 이모지는 글꼴에 없어 네모로 보였다
                        }
                    }
                }
            if (weeklyGoalButton != null)
            {
                var candidates = new List<GodTrialDefinition>();
                foreach (GodTrialDefinition definition in GodTrialRules.Trials)
                    if (definition.HeroId != null && GodTrialRules.IsUnlocked(data, definition.Id)
                        && !GodTrialRules.IsHeroUnlocked(data, definition.HeroId)) candidates.Add(definition);
                bool showGoal = _trial && candidates.Count > 0;
                weeklyGoalButton.gameObject.SetActive(showGoal);
                if (showGoal)
                {
                    var goal = data.godTrials;
                    GodTrialDefinition selectedGoal = GodTrialRules.Find(goal.goalTrialId);
                    weeklyGoalButton.interactable = !goal.goalLocked && goal.weeklyShards < GodTrialRules.WeeklyShards
                        && (selectedGoal == null || candidates.Count > 1);
                    weeklyGoalLabel.text = goal.weeklyShards >= GodTrialRules.WeeklyShards ? "이번 주 신 조각 5개 받음"
                        : selectedGoal == null ? "주간 목표 선택 · 열린 신의 시련"
                        : $"주간 목표: {selectedGoal.DisplayName} {(string.IsNullOrEmpty(goal.firstRecordId) ? 0 : 1)}/2 기록 · {(goal.goalLocked ? "이번 주 고정" : candidates.Count > 1 ? "눌러서 목표 변경" : "선택 완료")}";
                }
            }
            for (int i = 0; i < slotButtons.Length; i++)
            {
                bool exists = i < data.recordSlots;
                slotButtons[i].gameObject.SetActive(exists);
                if (!exists) continue;

                RunRecord record = data.RecordInSlot(i);
                slotButtons[i].interactable = record != null;
                slotBackgrounds[i].color = record != null && record.id == _selectedRecordId ? Selected : Normal;

                if (record == null)
                {
                    slotLabels[i].text = $"{i + 1}번 칸 · 비어 있음";
                    continue;
                }

                slotLabels[i].text = RecordLine(record);
            }

            RunRecord temp = data.TempRecord;
            if (tempButton != null) tempButton.gameObject.SetActive(temp != null);
            if (temp != null && tempLabel != null) tempLabel.text = "임시 칸 · " + RecordLine(temp);
            if (temp != null && tempBackground != null) tempBackground.color = temp.id == _selectedRecordId ? Selected : Normal;

            RunRecord selected = data.FindRecord(_selectedRecordId);
            startButton.interactable = trialUnlocked && selected != null;
            // 못 누르는 주 단추는 초록 대신 회색 (어두운 초록은 눌러도 되는 것처럼 보였다)
            startButton.image.color = startButton.interactable ? _startColor : new Color(0.3f, 0.32f, 0.38f);
            bool over = selected != null && RecordValidator.Validate(selected, database).OverCapacity;
            if (practiceButton != null) practiceButton.interactable = trialUnlocked && selected != null && !over;
            startLabel.text = !trialUnlocked ? "잠긴 시련" : selected == null ? "기록을 고르세요" : over ? "다시 고르고 시작" : "도전";
        }

        private void StartFight()
        {
            RunRecord record = SaveSystem.Data.FindRecord(_selectedRecordId);
            if (record == null) return;

            RecordValidator.Result build = RecordValidator.Validate(record, database);
            if (build.OverCapacity && reselect != null)
            {
                reselect.OpenReselect(DraftFrom(record, build), d => d.ToRecord(record.heroId, record.chapter, record.roomReached, record.roomCount, record.cleared, database.balanceVersion),
                    record.id, Render);
                return;
            }

            if (_trial)
            {
                if (opening != null) opening.Open(Story.GodTrialIntro.For(_boss.id), Story.GodTrialIntro.TintsFor(_boss.id), "도전", StartTrialNow);
                else StartTrialNow();
                return;
            }
            if (!GameSession.TryStartBoss(_chapter, record.id) && hearts != null) hearts.Open(Render);
        }

        private void StartTrialNow()
        {
            if (!GameSession.TryStartGodTrial(_boss.id, _selectedRecordId) && hearts != null) hearts.Open(Render);
        }

        /// <summary>하트 없이, 보상 없이 (Economy 3-4)</summary>
        private void StartPractice()
        {
            RunRecord record = SaveSystem.Data.FindRecord(_selectedRecordId);
            if (record == null || RecordValidator.Validate(record, database).OverCapacity) return;
            if (_trial) GameSession.StartGodTrialPractice(_boss.id, record.id);
            else GameSession.StartBossPractice(_chapter, record.id);
        }

        private string RecordLine(RunRecord record)
        {
            string beaten = RecordHasWon(record, SaveSystem.Data) ? _trial ? "  (이번 주 승리)" : "  (이 기록으로 격파함)" : string.Empty;
            bool recommended = _boss.recommendedRole != SkillRole.None && RecordStrength.Of(record, database).Main == _boss.recommendedRole;
            string badge = recommended ? "<color=#7CE38B>◎ 추천</color>  " : string.Empty;
            return $"{badge}{RecordFormat.Title(record, database)}{beaten}\n{RecordFormat.Details(record, database)}";
        }

        private bool RecordHasWon(RunRecord record, SaveData save) => _trial
            ? save.godTrials.goalTrialId == _boss.id && save.godTrials.weeklyWins.Exists(win => win.recordId == record.id)
            : record.bossesDefeated.Contains(_boss.id);

        private RecordDraft DraftFrom(RunRecord record, RecordValidator.Result build)
        {
            var entries = new List<RecordDraft.Entry>();
            foreach (RecordSkill s in build.Skills)
            {
                SkillData data = database.Find(s.id);
                if (data == null) continue;
                entries.Add(new RecordDraft.Entry(data, s.level, s.fusion, s.fusion ? database.RecipeProducing(data) : null) { FromSavedRecord = true });
            }

            return new RecordDraft(entries, record.capacity);
        }
    }
}
