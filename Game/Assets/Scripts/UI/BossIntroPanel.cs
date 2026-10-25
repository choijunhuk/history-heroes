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

        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startLabel;
        [SerializeField] private Button backButton;

        [SerializeField] private SkillDatabase database;
        [SerializeField] private RecordSaveScreen reselect;

        private static readonly Color Normal = new Color(0.16f, 0.18f, 0.24f);
        private static readonly Color Selected = new Color(0.25f, 0.42f, 0.7f);

        private BossData _boss;
        private int _chapter;
        private int _selectedSlot = -1;

        private void Awake()
        {
            root.SetActive(false);
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i;
                slotButtons[i].onClick.AddListener(() => Select(slot));
            }

            startButton.onClick.AddListener(StartFight);
            backButton.onClick.AddListener(() => root.SetActive(false));
        }

        public void Open(BossData boss, int chapter)
        {
            _boss = boss;
            _chapter = chapter;
            titleLabel.text = boss.displayName;
            testsLabel.text = "시험하는 것: " + boss.tests;
            recommendLabel.text = "추천: " + boss.recommend;

            // 이 보스를 아직 못 깬 기록 중 가장 최근 것을 먼저 골라 둔다 (★3 "서로 다른 기록 2개" 유도)
            _selectedSlot = -1;
            SaveData data = SaveSystem.Data;
            RunRecord pick = null;
            foreach (RunRecord r in data.records)
            {
                if (pick == null || !r.bossesDefeated.Contains(boss.id)) pick = r;
            }

            if (pick != null) _selectedSlot = pick.slot;
            root.SetActive(true);
            Render();
        }

        private void Select(int slot)
        {
            if (SaveSystem.Data.RecordInSlot(slot) == null) return;
            _selectedSlot = slot;
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
                slotBackgrounds[i].color = i == _selectedSlot ? Selected : Normal;

                if (record == null)
                {
                    slotLabels[i].text = $"{i + 1}번 칸 · 비어 있음";
                    continue;
                }

                string beaten = record.bossesDefeated.Contains(_boss.id) ? "  (이 기록으로 격파함)" : string.Empty;
                slotLabels[i].text = $"{RecordFormat.Title(record)}{beaten}\n{RecordFormat.Details(record, database)}";
            }

            RunRecord selected = data.RecordInSlot(_selectedSlot);
            startButton.interactable = selected != null;
            bool over = selected != null && RecordValidator.Validate(selected, database).OverCapacity;
            startLabel.text = selected == null ? "기록을 고르세요" : over ? "다시 고르고 시작" : "도전";
        }

        private void StartFight()
        {
            RunRecord record = SaveSystem.Data.RecordInSlot(_selectedSlot);
            if (record == null) return;

            RecordValidator.Result build = RecordValidator.Validate(record, database);
            if (build.OverCapacity && reselect != null)
            {
                reselect.OpenReselect(DraftFrom(record, build), d => d.ToRecord(record.heroId, record.chapter, record.roomReached, record.roomCount, record.cleared, database.balanceVersion),
                    record.slot, Render);
                return;
            }

            GameSession.StartBoss(_chapter, record.id);
        }

        private RecordDraft DraftFrom(RunRecord record, RecordValidator.Result build)
        {
            var entries = new List<RecordDraft.Entry>();
            foreach (RecordSkill s in build.Skills)
            {
                SkillData data = database.Find(s.id);
                if (data == null) continue;
                entries.Add(new RecordDraft.Entry(data, s.level, s.fusion, s.fusion ? database.RecipeProducing(data) : null));
            }

            return new RecordDraft(entries, record.capacity);
        }
    }
}
