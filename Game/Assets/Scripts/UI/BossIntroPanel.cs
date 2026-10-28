using System.Collections.Generic;
using HistoryHeroes.Common;
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
        private int _chapter;
        private string _selectedRecordId;

        private void Awake()
        {
            root.SetActive(false);
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
            backButton.onClick.AddListener(() => root.SetActive(false));
        }

        public void Open(BossData boss, int chapter)
        {
            _boss = boss;
            _chapter = chapter;
            titleLabel.text = boss.displayName;
            testsLabel.text = "시험하는 것: " + boss.tests;
            string hint = boss.prefab != null ? Enemy.BossGimmicks.HintFor(Enemy.BossGimmicks.For(boss.prefab)) : string.Empty;
            if (!string.IsNullOrEmpty(hint)) testsLabel.text += $"\n<color=#C9A8FF>기믹: {hint}</color>"; // 확장 1차 5장: 처음 나올 때 한 줄
            recommendLabel.text = boss.recommendedRole != SkillRole.None
                ? $"추천: <color={RecordStrength.RoleColor(boss.recommendedRole)}>{RecordStrength.RoleName(boss.recommendedRole)} 강점</color> · {boss.recommend}"
                : "추천: " + boss.recommend;

            // 이 보스를 아직 못 깬 기록 중 가장 최근 것을 먼저 골라 둔다 (★3 "서로 다른 기록 2개" 유도)
            _selectedRecordId = null;
            SaveData data = SaveSystem.Data;
            RunRecord pick = null;
            foreach (RunRecord r in data.records)
            {
                if (pick == null || !r.bossesDefeated.Contains(boss.id)) pick = r;
            }

            RunRecord temp = data.TempRecord;
            if (temp != null && (pick == null || !temp.bossesDefeated.Contains(boss.id))) pick = temp;

            if (pick != null) _selectedRecordId = pick.id;
            root.SetActive(true);
            Render();
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
            startButton.interactable = selected != null;
            bool over = selected != null && RecordValidator.Validate(selected, database).OverCapacity;
            if (practiceButton != null) practiceButton.interactable = selected != null && !over;
            startLabel.text = selected == null ? "기록을 고르세요" : over ? "다시 고르고 시작" : "도전";
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

            if (!GameSession.TryStartBoss(_chapter, record.id) && hearts != null) hearts.Open(Render);
        }

        /// <summary>하트 없이, 보상 없이 (Economy 3-4)</summary>
        private void StartPractice()
        {
            RunRecord record = SaveSystem.Data.FindRecord(_selectedRecordId);
            if (record == null || RecordValidator.Validate(record, database).OverCapacity) return;
            GameSession.StartBossPractice(_chapter, record.id);
        }

        private string RecordLine(RunRecord record)
        {
            string beaten = record.bossesDefeated.Contains(_boss.id) ? "  (이 기록으로 격파함)" : string.Empty;
            bool recommended = _boss.recommendedRole != SkillRole.None && RecordStrength.Of(record, database).Main == _boss.recommendedRole;
            string badge = recommended ? "<color=#7CE38B>◎ 추천</color>  " : string.Empty;
            return $"{badge}{RecordFormat.Title(record, database)}{beaten}\n{RecordFormat.Details(record, database)}";
        }

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
