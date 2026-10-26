using System.Collections.Generic;
using HistoryHeroes.Meta;
using HistoryHeroes.Records;
using HistoryHeroes.Runes;
using HistoryHeroes.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 룬 화면 (Runes 8장): 영웅마다 룬 1~2칸 → 칸을 고르고 룬을 누르면 "희귀 카드 중 불 스킬 25% → 40%"(그 영웅 스킬 풀로 계산)
    /// → 장착 / 강화 / 빼기. 2·4구간 보스 보상 고르기, 칸이 열린 뒤 상점 구매(골드 8,000)도 여기서.
    /// </summary>
    public sealed class RunePanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private MetaTable metaTable;
        [SerializeField] private SkillDatabase database;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text infoLabel;
        [SerializeField] private Button[] slotButtons;
        [SerializeField] private TMP_Text[] slotLabels;
        [SerializeField] private ButtonList list;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipLabel;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private TMP_Text upgradeLabel;
        [SerializeField] private Button removeButton;
        [SerializeField] private Button backButton;

        private static readonly Color SlotNormal = new Color(0.16f, 0.18f, 0.24f);
        private static readonly Color SlotFocused = new Color(0.25f, 0.42f, 0.7f);
        private static readonly Color RowNormal = new Color(0.13f, 0.15f, 0.2f);
        private static readonly Color RowSelected = new Color(0.22f, 0.36f, 0.6f);
        private static readonly Color RowChoice = new Color(0.38f, 0.3f, 0.12f);
        private static readonly Color RowDisabled = new Color(0.1f, 0.1f, 0.12f);

        private RuneTable Table => metaTable != null ? metaTable.runes : null;

        private string _heroId;
        private string _heroName;
        private int _slot;
        private string _selected;
        private string _message;
        private System.Action _onClosed;

        private void Awake()
        {
            root.SetActive(false);
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i;
                slotButtons[i].onClick.AddListener(() =>
                {
                    _slot = slot;
                    _message = null;
                    Render();
                });
            }

            equipButton.onClick.AddListener(EquipSelected);
            upgradeButton.onClick.AddListener(UpgradeSelected);
            removeButton.onClick.AddListener(RemoveFromSlot);
            backButton.onClick.AddListener(() =>
            {
                root.SetActive(false);
                _onClosed?.Invoke();
            });
        }

        public void Open(string heroId, string heroName, System.Action onClosed = null)
        {
            _heroId = heroId;
            _heroName = heroName;
            _onClosed = onClosed;
            _slot = 0;
            _selected = null;
            _message = null;
            root.SetActive(true);
            Render();
        }

        private void Render()
        {
            SaveData save = SaveSystem.Data;
            RuneTable table = Table;
            titleLabel.text = $"{_heroName} 룬";
            int slots = RuneService.SlotCount(save, table);
            infoLabel.text = slots == 0
                ? "<color=#FFB347>2구간 보스를 깨면 룬 1칸이 열려요</color>"
                : $"룬은 레벨업 카드에서 같은 등급 안의 스킬만 더 잘 나오게 해요 (능력치 없음) · 골드 {save.gold:N0}";

            for (int i = 0; i < slotButtons.Length; i++)
            {
                bool open = i < slots;
                slotButtons[i].interactable = open;
                slotButtons[i].GetComponent<Image>().color = open && i == _slot ? SlotFocused : SlotNormal;
                RuneData rune = table.Find(RuneService.Equipped(save, _heroId, i));
                string lockText = i == 0 ? "2구간 보스 뒤" : "4구간 보스 뒤";
                slotLabels[i].text = !open ? $"칸 {i + 1}  <color=#A9B4C2>잠김 · {lockText}</color>"
                    : rune != null ? $"칸 {i + 1}  <b>{rune.displayName}</b> Lv {RuneService.Level(save, rune.id)}" : $"칸 {i + 1}  <color=#A9B4C2>비어 있음</color>";
            }

            var rows = new List<ButtonList.Row>();
            string pending = RuneService.PendingChoice(save, table);
            if (pending != null)
            {
                rows.Add(new ButtonList.Row($"<color=#FFD966>{(pending == "ch2" ? "2" : "4")}구간 보스 보상: 룬 하나를 골라요</color>", RowDisabled, null, false));
                foreach (RuneData r in RuneService.ChoiceOptions(save, table))
                {
                    string id = r.id;
                    rows.Add(new ButtonList.Row($"<b>{r.displayName}</b> 고르기\n<size=85%>{r.aim}</size>", RowChoice, () => TakeChoice(id)));
                }
            }

            foreach (RuneData r in table.runes)
            {
                if (r == null) continue;
                string id = r.id;
                int level = RuneService.Level(save, id);
                string state;
                if (level > 0) state = $"Lv {level}/{table.MaxLevel} · ×{table.MultiplierAt(level):0.0}";
                else if (RuneService.CanBuy(save, table, r)) state = $"<color=#FFD966>상점 골드 {table.shopPrice:N0}</color>";
                else state = $"<color=#A9B4C2>{Source(r)}</color>";
                rows.Add(new ButtonList.Row($"<b>{r.displayName}</b>  {state}\n<size=85%>{r.aim}</size>", id == _selected ? RowSelected : RowNormal, () => Select(id)));
            }

            list.Show(rows);
            RenderDetail(save, table, slots);
        }

        private static string Source(RuneData r)
        {
            switch (r.source)
            {
                case RuneSource.BossChoice: return "2·4구간 보스 보상 (못 고르면 상점)";
                case RuneSource.Survival: return "서바이벌 첫 클리어";
                default: return "룬 칸이 열리면 상점";
            }
        }

        private void RenderDetail(SaveData save, RuneTable table, int slots)
        {
            RuneData rune = table.Find(_selected);
            if (rune == null)
            {
                detailLabel.text = _message ?? "<color=#A9B4C2>룬을 누르면 카드가 얼마나 잘 나오게 되는지 보여줘요</color>";
                equipButton.gameObject.SetActive(false);
                upgradeButton.gameObject.SetActive(false);
                removeButton.gameObject.SetActive(slots > 0 && !string.IsNullOrEmpty(RuneService.Equipped(save, _heroId, _slot)));
                return;
            }

            int level = RuneService.Level(save, rune.id);
            detailLabel.text = (_message != null ? _message + "\n" : string.Empty) + RuneService.ShareText(table, rune, level, Pool(), _heroId);

            bool owned = level > 0;
            bool buyable = !owned && RuneService.CanBuy(save, table, rune);
            equipButton.gameObject.SetActive(owned || buyable);
            equipButton.interactable = owned ? slots > 0 : save.gold >= table.shopPrice;
            equipLabel.text = owned ? $"칸 {_slot + 1}에 끼기" : $"사기 · 골드 {table.shopPrice:N0}";

            int cost = table.UpgradeCost(level);
            upgradeButton.gameObject.SetActive(owned);
            upgradeButton.interactable = cost > 0 && save.gold >= cost;
            upgradeLabel.text = cost > 0 ? $"강화 Lv {level + 1} · 골드 {cost:N0}" : "최대 레벨";
            removeButton.gameObject.SetActive(slots > 0 && !string.IsNullOrEmpty(RuneService.Equipped(save, _heroId, _slot)));
        }

        /// <summary>그 영웅이 판에서 받을 수 있는 스킬 (조합 결과는 카드로 안 나오므로 뺀다)</summary>
        private List<SkillData> Pool()
        {
            var list = new List<SkillData>();
            if (database == null) return list;
            foreach (SkillData s in database.skills)
            {
                if (s != null && database.RecipeProducing(s) == null) list.Add(s);
            }

            return list;
        }

        private void Select(string runeId)
        {
            _selected = runeId;
            _message = null;
            Render();
        }

        private void TakeChoice(string runeId)
        {
            if (!RuneService.TakeChoice(SaveSystem.Data, Table, runeId)) return;
            SaveSystem.Save();
            _selected = runeId;
            _message = $"<color=#7CE38B>{Table.Find(runeId).displayName}을(를) 얻었어요</color>";
            Render();
        }

        private void EquipSelected()
        {
            SaveData save = SaveSystem.Data;
            RuneTable table = Table;
            if (!RuneService.Owns(save, _selected))
            {
                if (!RuneService.TryBuy(save, table, _selected)) return;
                _message = $"<color=#7CE38B>{table.Find(_selected).displayName}을(를) 샀어요</color>";
            }
            else
            {
                if (!RuneService.Equip(save, table, _heroId, _slot, _selected)) return;
                _message = $"<color=#7CE38B>칸 {_slot + 1}에 꼈어요</color>";
            }

            SaveSystem.Save();
            Render();
        }

        private void UpgradeSelected()
        {
            if (!RuneService.TryUpgrade(SaveSystem.Data, Table, _selected)) return;
            SaveSystem.Save();
            _message = $"<color=#7CE38B>Lv {RuneService.Level(SaveSystem.Data, _selected)}</color>";
            Render();
        }

        private void RemoveFromSlot()
        {
            if (!RuneService.Equip(SaveSystem.Data, Table, _heroId, _slot, string.Empty)) return;
            SaveSystem.Save();
            _message = "칸을 비웠어요";
            Render();
        }
    }
}
