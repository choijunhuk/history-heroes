using System.Collections.Generic;
using HistoryHeroes.Equipment;
using HistoryHeroes.Meta;
using HistoryHeroes.Records;
using HistoryHeroes.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 수련 (Stats 5장): 계정 공용 기본 능력치 6종. 직접 골라 골드로 올린다 (무작위 재능 아님).
    /// 수련 레벨 ≤ 계정 레벨. 2구간 보스를 깨면 열린다.
    /// </summary>
    public sealed class TrainingPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private MetaTable metaTable;
        [SerializeField] private TMP_Text infoLabel;
        [SerializeField] private ButtonList list;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private Button backButton;

        private static readonly Color RowNormal = new Color(0.13f, 0.15f, 0.2f);
        private static readonly Color RowDisabled = new Color(0.1f, 0.1f, 0.12f);

        private System.Action _onClosed;

        private void Awake()
        {
            root.SetActive(false);
            backButton.onClick.AddListener(() =>
            {
                root.SetActive(false);
                _onClosed?.Invoke();
            });
        }

        public void Open(System.Action onClosed = null)
        {
            _onClosed = onClosed;
            messageLabel.text = string.Empty;
            root.SetActive(true);
            Render();
        }

        private void Render()
        {
            SaveData save = SaveSystem.Data;
            bool unlocked = HeroGrowth.TrainingUnlocked(save);
            infoLabel.text = unlocked
                ? $"모든 영웅에게 같이 적용돼요 · 수련 레벨은 계정 레벨(지금 Lv {save.accountLevel})까지 · 골드 {save.gold:N0}"
                : "<color=#FFB347>2구간 보스를 깨면 열려요</color>";

            var rows = new List<ButtonList.Row>();
            for (int i = 0; i < metaTable.training.Length; i++)
            {
                var type = (TrainingType)i;
                TrainingRow row = metaTable.training[i];
                int level = HeroGrowth.TrainingLevel(save, type);
                bool can = HeroGrowth.CanTrain(save, metaTable, type, out int cost, out string reason);
                string now = Effect(row, level);
                string next = level < row.maxLevel ? $" → {Effect(row, level + 1)}" : string.Empty;
                string tail = can ? $"골드 {cost:N0}" : $"<color=#FFB347>{reason}</color>";
                bool afford = can && save.gold >= cost;
                if (can && !afford) tail = $"<color=#FF7A6B>골드 {cost:N0} (모자라요)</color>";
                string text = $"<b>{row.name}</b>  Lv {level}/{row.maxLevel}\n<size=85%>{now}{next} · {tail}</size>";
                rows.Add(new ButtonList.Row(text, afford ? RowNormal : RowDisabled, () => Train(type), afford));
            }

            list.Show(rows);
        }

        private static string Effect(TrainingRow row, int level)
        {
            float v = level * row.perLevel;
            if (row.stat == StatType.HpRegenFlat) return $"초당 체력 +{v:0} 회복";
            return $"{EquipmentText.StatName(row.stat)} +{v * 100f:0.##}%";
        }

        private void Train(TrainingType type)
        {
            SaveSystem.CommitResult r = SaveSystem.Commit(() => HeroGrowth.TryTrain(SaveSystem.Data, metaTable, type));
            if (r == SaveSystem.CommitResult.Rejected) return;
            messageLabel.text = r == SaveSystem.CommitResult.Saved
                ? $"<color=#7CE38B>{metaTable.training[(int)type].name} Lv {HeroGrowth.TrainingLevel(SaveSystem.Data, type)}</color>"
                : SaveSystem.SaveFailedText;
            Render();
        }
    }
}
