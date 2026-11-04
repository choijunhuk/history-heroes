using System;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 기록 보관함 (UX.md 1장 "기록" 탭): 슬롯마다 기록 요약·스킬·비용, 지우기.
    /// 지우기는 실수로 누르지 않게 3초 안에 한 번 더 눌러야 한다.
    /// </summary>
    public sealed class RecordArchivePanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text[] slotTitles;
        [SerializeField] private TMP_Text[] slotDetails;
        [SerializeField] private Button[] deleteButtons;
        [SerializeField] private TMP_Text[] deleteLabels;
        [SerializeField] private Button[] lockButtons;
        [SerializeField] private TMP_Text[] lockLabels;
        [SerializeField] private TMP_Text tempTitle;
        [SerializeField] private TMP_Text tempDetails;
        [SerializeField] private Button moveTempButton;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private SkillDatabase database;
        [Header("대표 스킬 그림 (슬롯마다 4개)")]
        [SerializeField] private Image[] slotSkillIcons = new Image[0];
        [SerializeField] private TMP_Text[] slotSkillLabels = new TMP_Text[0];
        [SerializeField] private Image[] tempSkillIcons = new Image[0];
        [SerializeField] private TMP_Text[] tempSkillLabels = new TMP_Text[0];
        [SerializeField] private ScrollRect scroll;

        private const float ConfirmSeconds = 3f;
        private const string HelpText = "기록은 보스맵에서 계속 쓸 수 있어요. 잠그면 덮어쓰기와 지우기를 막아요.";

        private Action _onClosed;
        private int _confirmSlot = -1;
        private float _confirmUntil;

        private void Awake()
        {
            root.SetActive(false);
            for (int i = 0; i < deleteButtons.Length; i++)
            {
                int slot = i;
                deleteButtons[i].onClick.AddListener(() => Delete(slot));
                if (lockButtons != null && i < lockButtons.Length) lockButtons[i].onClick.AddListener(() => ToggleLock(slot));
            }

            if (moveTempButton != null) moveTempButton.onClick.AddListener(MoveTemp);

            backButton.onClick.AddListener(Close);
        }

        public void Open(Action onClosed)
        {
            _onClosed = onClosed;
            _confirmSlot = -1;
            if (statusLabel != null) statusLabel.text = HelpText;
            root.SetActive(true);
            Render();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private void Update()
        {
            if (_confirmSlot >= 0 && Time.unscaledTime > _confirmUntil)
            {
                _confirmSlot = -1;
                Render();
            }
        }

        /// <summary>보관함 카드엔 대표 스킬 4개만 (레벨 높은 순), 나머지는 "외 N개" — 전체 UI 검토 9번</summary>
        private const int CardSkills = 4;

        private void Render()
        {
            SaveData data = SaveSystem.Data;
            for (int i = 0; i < slotTitles.Length; i++)
            {
                bool exists = i < data.recordSlots;
                slotTitles[i].transform.parent.gameObject.SetActive(exists);
                if (!exists) continue;

                RunRecord record = data.RecordInSlot(i);
                bool preview = slotSkillIcons.Length >= (i + 1) * CardSkills && slotSkillLabels.Length >= (i + 1) * CardSkills;
                slotTitles[i].gameObject.SetActive(record != null);
                slotTitles[i].text = record != null ? RecordFormat.Title(record, database) : string.Empty;
                slotDetails[i].text = record != null ? RecordFormat.Details(record, database, CardSkills, !preview) : EmptyText($"{i + 1}번 칸", "던전을 끝내면 기록을 남길 수 있어요");
                slotDetails[i].alignment = record != null ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
                deleteButtons[i].gameObject.SetActive(record != null && !record.locked);
                deleteLabels[i].text = _confirmSlot == i ? "한 번 더 누르면 지워요" : "지우기";
                if (lockButtons != null && i < lockButtons.Length) lockButtons[i].gameObject.SetActive(record != null);
                if (lockLabels != null && i < lockLabels.Length && record != null) lockLabels[i].text = record.locked ? "잠금 풀기" : "잠그기";
                if (preview)
                {
                    RenderSkills(record, slotSkillIcons, slotSkillLabels, i * CardSkills);
                    deleteButtons[i].transform.parent.gameObject.SetActive(record != null);
                }
            }

            RunRecord temp = data.TempRecord;
            bool tempPreview = tempSkillIcons.Length >= CardSkills && tempSkillLabels.Length >= CardSkills;
            if (tempTitle != null)
            {
                tempTitle.gameObject.SetActive(temp != null);
                tempTitle.text = temp != null ? "임시 칸 · " + RecordFormat.Title(temp, database) : string.Empty;
            }
            if (tempDetails != null)
            {
                tempDetails.text = temp != null ? RecordFormat.Details(temp, database, CardSkills, !tempPreview) : EmptyText("임시 칸", "보관함이 찼을 때 새 기록 하나를 둘 수 있어요");
                tempDetails.alignment = temp != null ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
            }
            if (moveTempButton != null) moveTempButton.gameObject.SetActive(temp != null && data.FirstFreeSlot() >= 0);
            if (tempPreview)
            {
                RenderSkills(temp, tempSkillIcons, tempSkillLabels, 0);
                if (moveTempButton != null) moveTempButton.transform.parent.gameObject.SetActive(moveTempButton.gameObject.activeSelf);
            }
        }

        private void RenderSkills(RunRecord record, Image[] icons, TMP_Text[] labels, int offset)
        {
            var skills = RecordFormat.RepresentativeSkills(record, database, CardSkills);
            icons[offset].transform.parent.parent.gameObject.SetActive(skills.Count > 0);
            for (int i = 0; i < CardSkills; i++)
            {
                Image icon = icons[offset + i];
                TMP_Text label = labels[offset + i];
                bool shown = i < skills.Count;
                icon.transform.parent.gameObject.SetActive(shown);
                if (!shown)
                {
                    icon.sprite = null;
                    label.text = string.Empty;
                    continue;
                }

                RecordSkill skill = skills[i];
                SkillData data = database.Find(skill.id);
                icon.sprite = Skills.HeroSkillArt.IconFor(data);
                icon.enabled = icon.sprite != null;
                label.text = $"{(data != null ? data.displayName : skill.id)}\nLv{skill.level}";
            }
        }

        /// <summary>빈 칸은 카드 가운데 한 덩어리 (위에 붙은 한 줄 + 빈 상자로 보이지 않게)</summary>
        private static string EmptyText(string name, string hint) => $"<size=125%>{name} · 비어 있음</size>\n<color=#A9B4C2>{hint}</color>";

        private void Delete(int slot)
        {
            RunRecord record = SaveSystem.Data.RecordInSlot(slot);
            if (record == null || record.locked) return;
            if (_confirmSlot != slot)
            {
                _confirmSlot = slot;
                _confirmUntil = Time.unscaledTime + ConfirmSeconds;
                Render();
                return;
            }

            SaveSystem.CommitResult result = SaveSystem.Commit(() => RecordService.Delete(SaveSystem.Data, slot));
            _confirmSlot = -1;
            ShowStatus(result);
            Render();
        }

        private void ToggleLock(int slot)
        {
            SaveSystem.CommitResult result = SaveSystem.Commit(() => RecordService.ToggleLock(SaveSystem.Data, slot));
            _confirmSlot = -1;
            ShowStatus(result);
            Render();
        }

        private void MoveTemp()
        {
            SaveSystem.CommitResult result = SaveSystem.Commit(() => RecordService.MoveTempToArchive(SaveSystem.Data));
            ShowStatus(result);
            Render();
        }

        private void ShowStatus(SaveSystem.CommitResult result)
        {
            if (statusLabel == null) return;
            statusLabel.text = result == SaveSystem.CommitResult.Saved ? HelpText
                : result == SaveSystem.CommitResult.SaveFailed ? SaveSystem.SaveFailedText
                : "기록을 바꿀 수 없어요. 칸 상태를 확인해 주세요.";
        }

        private void Close()
        {
            root.SetActive(false);
            _onClosed?.Invoke();
        }
    }
}
