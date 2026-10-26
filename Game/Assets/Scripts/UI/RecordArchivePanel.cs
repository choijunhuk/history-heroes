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
        [SerializeField] private Button backButton;
        [SerializeField] private SkillDatabase database;

        private const float ConfirmSeconds = 3f;

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
            }

            backButton.onClick.AddListener(Close);
        }

        public void Open(Action onClosed)
        {
            _onClosed = onClosed;
            _confirmSlot = -1;
            root.SetActive(true);
            Render();
        }

        private void Update()
        {
            if (_confirmSlot >= 0 && Time.unscaledTime > _confirmUntil)
            {
                _confirmSlot = -1;
                Render();
            }
        }

        private void Render()
        {
            SaveData data = SaveSystem.Data;
            for (int i = 0; i < slotTitles.Length; i++)
            {
                bool exists = i < data.recordSlots;
                slotTitles[i].transform.parent.gameObject.SetActive(exists);
                if (!exists) continue;

                RunRecord record = data.RecordInSlot(i);
                slotTitles[i].text = record != null ? RecordFormat.Title(record, database) : $"{i + 1}번 칸";
                slotDetails[i].text = record != null ? RecordFormat.Details(record, database) : "비어 있어요. 던전을 끝내면 기록을 남길 수 있어요.";
                deleteButtons[i].gameObject.SetActive(record != null);
                deleteLabels[i].text = _confirmSlot == i ? "한 번 더 누르면 지워요" : "지우기";
            }
        }

        private void Delete(int slot)
        {
            if (_confirmSlot != slot)
            {
                _confirmSlot = slot;
                _confirmUntil = Time.unscaledTime + ConfirmSeconds;
                Render();
                return;
            }

            SaveSystem.Data.records.RemoveAll(r => r.slot == slot);
            SaveSystem.Save();
            _confirmSlot = -1;
            Render();
        }

        private void Close()
        {
            root.SetActive(false);
            _onClosed?.Invoke();
        }
    }
}
