using System;
using HistoryHeroes.Common;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 기록 남기기 화면 (GameDesign 7-2 "저장 화면 규칙", UX: 용량 막대 + 스킬마다 −/+).
    /// - 처음 열 때는 용량에 맞게 자동으로 줄인 추천값. 플레이어가 −/+로 바꾼다
    /// - 진화를 남겼는데 필요한 패시브를 빼면 "○○로 돌아가요"를 미리 보여준다
    /// - 빈 칸이 없으면 바꿀 칸을 고른다 (저장 안 하고 나갈 수도 있다)
    /// 던전 결과 뒤(새 기록)와 로비(패치 뒤 다시 고르기) 둘 다 쓴다.
    /// </summary>
    public sealed class RecordSaveScreen : MonoBehaviour
    {
        [Serializable]
        public sealed class Row
        {
            public GameObject root;
            public Image band;
            public TMP_Text nameLabel;
            public TMP_Text levelLabel;
            public TMP_Text costLabel;
            public TMP_Text noteLabel;
            public Button minus;
            public Button plus;
        }

        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private TMP_Text capacityLabel;
        [SerializeField] private Image capacityFill;
        [SerializeField] private Row[] rows;
        [SerializeField] private Button autoFitButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private TMP_Text saveLabel;
        [SerializeField] private Button skipButton;
        [SerializeField] private TMP_Text skipLabel;

        [Header("칸 고르기 (빈 칸이 없을 때)")]
        [SerializeField] private GameObject slotPanel;
        [SerializeField] private Button[] slotButtons;
        [SerializeField] private TMP_Text[] slotLabels;
        [SerializeField] private Button slotCancel;
        [SerializeField] private TMP_Text slotTitleLabel;
        [SerializeField] private Button tempButton;
        [SerializeField] private TMP_Text tempLabel;

        [SerializeField] private SkillDatabase database;

        private static readonly Color Fits = new Color(0.3f, 0.75f, 0.45f);
        private static readonly Color Over = new Color(0.9f, 0.3f, 0.3f);

        private RecordDraft _draft;
        private Func<RecordDraft, RunRecord> _makeRecord;
        private Action _onDone;

        /// <summary>"저장 안 함"을 보일까 (튜토리얼은 꼭 저장해야 보스로 간다). 닫으면 다시 true</summary>
        public bool AllowSkip { get; set; } = true;
        private string _fixedRecordId;
        private bool _confirmTemp;

        public bool IsOpen => root.activeSelf;

        /// <summary>스킬 ID → 이번 판 피해 비율 (조사 A5: 무엇을 남길지 고르는 근거). 없으면 표시 안 함</summary>
        public Func<string, float> DamageShare { get; set; }
        public RecordDraft Draft => _draft;

        private void Awake()
        {
            root.SetActive(false);
            slotPanel.SetActive(false);

            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                rows[i].minus.onClick.AddListener(() => Step(index, -1));
                rows[i].plus.onClick.AddListener(() => Step(index, +1));
                // 이름을 누르면 고정 (자동 맞추기가 깎지 않는다, 2개까지 — 전체 검토 2번)
                if (rows[i].nameLabel != null)
                {
                    rows[i].nameLabel.raycastTarget = true;
                    Button pin = rows[i].nameLabel.GetComponent<Button>() ?? rows[i].nameLabel.gameObject.AddComponent<Button>();
                    pin.transition = Selectable.Transition.None;
                    pin.onClick.AddListener(() => TogglePin(index));
                }
            }

            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i;
                slotButtons[i].onClick.AddListener(() => Commit(slot));
            }

            autoFitButton.onClick.AddListener(AutoFit);
            saveButton.onClick.AddListener(Save);
            skipButton.onClick.AddListener(Skip);
            slotCancel.onClick.AddListener(() =>
            {
                if (_confirmTemp) ShowSlotChoices();
                else slotPanel.SetActive(false);
            });
            if (tempButton != null) tempButton.onClick.AddListener(SaveToTemp);
        }

        /// <summary>새 기록 남기기 (던전이 끝난 뒤)</summary>
        public void Open(RecordDraft draft, Func<RecordDraft, RunRecord> makeRecord, string subtitle, Action onDone)
        {
            _fixedRecordId = null;
            OpenInternal(draft, makeRecord, "기록 남기기", subtitle, "저장 안 함", onDone);
        }

        /// <summary>패치로 비용이 올라 용량을 넘은 기록을 다시 고르기 (같은 칸에 덮어씀, GameDesign 7-5)</summary>
        public void OpenReselect(RecordDraft draft, Func<RecordDraft, RunRecord> makeRecord, string recordId, Action onDone)
        {
            DamageShare = null; // 다시 고르기에는 판 피해 기록이 없다
            _fixedRecordId = recordId;
            OpenInternal(draft, makeRecord, "기록 다시 고르기", "패치로 비용이 올라 용량을 넘었어요. 남길 스킬을 다시 골라 주세요.", "취소", onDone);
        }

        private void OpenInternal(RecordDraft draft, Func<RecordDraft, RunRecord> makeRecord, string title, string subtitle, string skip, Action onDone)
        {
            _draft = draft;
            _makeRecord = makeRecord;
            _onDone = onDone;
            titleLabel.text = title;
            subtitleLabel.text = subtitle + "\n<size=80%><color=#A9B4C2>이름을 누르면 고정 (자동 맞추기가 안 깎아요, 2개까지) · 장비·룬은 지금 상태로 남아요</color></size>"; // 전체 검토 2번
            subtitleLabel.color = new Color(1f, 1f, 1f, 0.75f);
            skipLabel.text = skip;
            skipButton.gameObject.SetActive(AllowSkip);
            _draft.AutoFit(DamageShare); // 처음엔 추천값 (피해를 많이 낸 스킬을 남긴다)
            slotPanel.SetActive(false);
            _confirmTemp = false;
            root.SetActive(true);
            GamePause.Pause(this);
            Render();
        }

        private void Step(int index, int delta)
        {
            if (_draft == null || index >= _draft.Entries.Count) return;
            _draft.Step(_draft.Entries[index], delta);
            Render();
        }

        private void AutoFit()
        {
            foreach (RecordDraft.Entry e in _draft.Entries) _draft.SetLevel(e, e.OwnedLevel);
            _draft.AutoFit(DamageShare);
            Render();
        }

        private void Render()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                bool visible = i < _draft.Entries.Count;
                row.root.SetActive(visible);
                if (!visible) continue;

                RecordDraft.Entry e = _draft.Entries[i];
                row.band.color = e.IsFusion ? SkillCardView.FusionBand : e.Skill.Kind == SkillKind.Passive ? SkillCardView.PassiveBand : SkillCardView.ActiveBand;
                row.nameLabel.text = e.IsEvolution ? $"{e.Skill.displayName} (진화)" : e.IsMerge ? $"{e.Skill.displayName} (합체)" : e.Skill.displayName;
                if (e.Pinned) row.nameLabel.text = "<color=#FFD966>[고정]</color> " + row.nameLabel.text; // 이름을 누르면 고정 · 풀기

                if (e.IsEvolution) row.levelLabel.text = e.Kept ? "남김" : "뺌";
                else row.levelLabel.text = e.Kept ? $"Lv {e.Level} / {e.OwnedLevel}" : "뺌";
                row.levelLabel.color = e.Kept ? Color.white : new Color(1f, 1f, 1f, 0.4f);

                row.costLabel.text = $"{_draft.CostOf(e)}pt";
                row.noteLabel.text = Note(e);
                // 누를 수 없는 버튼은 아예 숨긴다 (흐린 버튼은 폰에서 구분이 잘 안 된다)
                row.minus.gameObject.SetActive(e.Kept);
                row.plus.gameObject.SetActive(e.Level < e.OwnedLevel);
            }

            int cost = _draft.TotalCost;
            bool fits = _draft.Fits;
            capacityLabel.text = $"용량 {cost} / {_draft.Capacity}pt";
            capacityLabel.color = fits ? Color.white : Over;
            capacityFill.fillAmount = _draft.Capacity > 0 ? Mathf.Clamp01((float)cost / _draft.Capacity) : (cost > 0 ? 1f : 0f);
            capacityFill.color = fits ? Fits : Over;

            bool canSave = fits && _draft.HasAnything;
            saveButton.interactable = canSave;
            saveLabel.text = !fits ? "용량 초과" : !_draft.HasAnything ? "남길 스킬 없음" : "저장";
        }

        private string Note(RecordDraft.Entry e)
        {
            if (_draft.Reverts(e))
            {
                SkillData baseActive = e.Recipe.materialA;
                string passive = e.Recipe.materialB != null ? e.Recipe.materialB.displayName : "필요한 패시브";
                return $"{KoreanText.Josa(passive, "이", "가")} 없어서 {baseActive.displayName} Lv{RecordDraft.RevertLevel(baseActive)}로 돌아가요";
            }

            foreach (RecordDraft.Entry other in _draft.Entries)
            {
                if (other.IsEvolution && other.Kept && !other.FromSavedRecord && other.Recipe.materialB == e.Skill)
                {
                    return $"{other.Skill.displayName}에 필요해요 (빼면 진화가 풀려요)";
                }
            }

            if (DamageShare != null && e.Skill.Kind != SkillKind.Passive)
            {
                return $"<color=#9FD8FF>이번 판 피해 {Mathf.RoundToInt(DamageShare(e.Skill.id) * 100f)}%</color>";
            }

            return string.Empty;
        }

        private void Save()
        {
            if (!_draft.Fits || !_draft.HasAnything) return;
            if (_fixedRecordId != null)
            {
                Commit(-1);
                return;
            }

            int free = SaveSystem.Data.FirstFreeSlot();
            if (free >= 0)
            {
                Commit(free);
                return;
            }

            ShowSlotChoices();
        }

        private void ShowSlotChoices()
        {
            // 빈 칸이 없다 → 바꿀 칸 고르기. 새 기록 요약을 위에 보여 주어 바꾸기 전후를 견준다 (전체 검토 2번)
            _confirmTemp = false;
            SaveData data = SaveSystem.Data;
            RunRecord preview = _makeRecord(_draft);
            subtitleLabel.text = $"바꿀 칸을 고르세요\n<size=85%><color=#7CFC9A>새 기록</color>  {RecordFormat.Details(preview, database)}</size>";
            subtitleLabel.color = Color.white;
            if (slotTitleLabel != null) slotTitleLabel.text = $"바꿀 칸을 고르세요\n새 기록: {RecordFormat.Title(preview, database)}\n잠긴 기록은 바꿀 수 없어요";
            for (int i = 0; i < slotButtons.Length; i++)
            {
                bool exists = i < data.recordSlots;
                slotButtons[i].gameObject.SetActive(exists);
                if (!exists) continue;
                RunRecord old = data.RecordInSlot(i);
                slotButtons[i].interactable = old == null || !old.locked;
                slotLabels[i].text = old != null ? $"{(old.locked ? "<color=#FFD966>[잠김]</color> " : "")}{RecordFormat.Title(old, database)}\n{RecordFormat.Details(old, database)}" : $"{i + 1}번 칸 (비어 있음)";
            }

            if (tempButton != null) tempButton.gameObject.SetActive(true);
            if (tempLabel != null) tempLabel.text = "임시 칸에 두기";
            slotPanel.SetActive(true);
        }

        private void Commit(int slot)
        {
            RunRecord record = _makeRecord(_draft);
            bool newRecord = _fixedRecordId == null;
            SaveSystem.CommitResult result = SaveSystem.Commit(() => newRecord
                ? RecordService.SaveToSlot(SaveSystem.Data, record, slot)
                : RecordService.Reselect(SaveSystem.Data, _fixedRecordId, record));
            if (result != SaveSystem.CommitResult.Saved)
            {
                slotPanel.SetActive(false);
                subtitleLabel.text = result == SaveSystem.CommitResult.SaveFailed ? SaveSystem.SaveFailedText : "이 기록은 바꿀 수 없어요. 다른 칸을 골라 주세요.";
                subtitleLabel.color = Over;
                return;
            }

            if (newRecord) RecordSaved();
            Close();
        }

        private void SaveToTemp()
        {
            if (_fixedRecordId != null || !_draft.Fits || !_draft.HasAnything) return;
            SaveData data = SaveSystem.Data;
            if (data.FirstFreeSlot() >= 0) return;
            if (data.TempRecord != null && !_confirmTemp)
            {
                _confirmTemp = true;
                if (slotTitleLabel != null) slotTitleLabel.text = $"임시 기록을 바꿀까요?\n기존: {RecordFormat.Title(data.TempRecord, database)}\n새 기록: {RecordFormat.Title(_makeRecord(_draft), database)}";
                subtitleLabel.text = $"기존: {RecordFormat.Title(data.TempRecord, database)}\n새 기록: {RecordFormat.Details(_makeRecord(_draft), database)}";
                for (int i = 0; i < slotButtons.Length; i++) slotButtons[i].gameObject.SetActive(false);
                if (tempLabel != null) tempLabel.text = "기존 기록 지우고 두기";
                return;
            }

            RunRecord record = _makeRecord(_draft);
            SaveSystem.CommitResult result = SaveSystem.Commit(() => RecordService.SaveToTemp(SaveSystem.Data, record));
            if (result != SaveSystem.CommitResult.Saved)
            {
                ShowSlotChoices();
                if (slotTitleLabel != null) slotTitleLabel.text = result == SaveSystem.CommitResult.SaveFailed
                    ? "저장하지 못했어요. 저장 공간을 확인하고 다시 눌러 주세요." : "임시 칸에 둘 수 없어요.";
                return;
            }

            RecordSaved();
            Close();
        }

        private static void RecordSaved()
        {
            Missions.MissionEvents.Raise(Missions.MissionEvents.Record);
            SaveSystem.SaveIfDirty();
        }

        private void TogglePin(int index)
        {
            if (_draft == null || index < 0 || index >= _draft.Entries.Count) return;
            if (!_draft.TogglePin(_draft.Entries[index]))
            {
                subtitleLabel.text = $"고정은 {RecordDraft.MaxPins}개까지예요 (이름을 다시 누르면 풀려요)";
                return;
            }

            Render();
        }

        private void Skip() => Close();

        private void Close()
        {
            AllowSkip = true;
            skipButton.gameObject.SetActive(true);
            slotPanel.SetActive(false);
            _confirmTemp = false;
            root.SetActive(false);
            GamePause.Resume(this);
            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
        }
    }
}
