using System;
using System.Collections.Generic;
using System.Text;
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
    /// 영웅 장비 화면 (Equipment 11장): 장비 3칸 → 칸을 누르면 그 칸 장비 목록 → 장비를 누르면 비교("공격력 +11% → +17.5%")
    /// → 장착·잠금·분해. 칸 강화(+최대까지), 합성(같은 칸·등급 3개 → 다음 등급 중 고르기), 중복 자동 분해 켜고 끄기.
    /// 바꿀 때마다 바로 저장한다. 분해는 3초 안에 한 번 더 눌러야 한다 (실수 방지).
    /// </summary>
    public sealed class EquipmentPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private MetaTable metaTable;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text summaryLabel;
        [SerializeField] private Button[] slotButtons;
        [SerializeField] private TMP_Text[] slotLabels;
        [SerializeField] private Button enhanceButton;
        [SerializeField] private TMP_Text enhanceLabel;
        [SerializeField] private Button enhanceMaxButton;
        [SerializeField] private TMP_Text enhanceMaxLabel;
        [SerializeField] private Button bagTab;
        [SerializeField] private Button synthTab;
        [SerializeField] private Button autoButton;
        [SerializeField] private TMP_Text autoLabel;
        [SerializeField] private ButtonList list;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipLabel;
        [SerializeField] private Button lockButton;
        [SerializeField] private TMP_Text lockLabel;
        [SerializeField] private Button dismantleButton;
        [SerializeField] private TMP_Text dismantleLabel;
        [SerializeField] private Button backButton;

        private static readonly Color SlotNormal = new Color(0.16f, 0.18f, 0.24f);
        private static readonly Color SlotFocused = new Color(0.25f, 0.42f, 0.7f);
        private static readonly Color RowNormal = new Color(0.13f, 0.15f, 0.2f);
        private static readonly Color RowSelected = new Color(0.22f, 0.36f, 0.6f);
        private static readonly Color RowDisabled = new Color(0.1f, 0.1f, 0.12f);
        private const float ConfirmSeconds = 3f;

        private EquipmentTable Table => metaTable != null ? metaTable.equipment : null;

        private string _heroId;
        private string _heroName;
        private EquipSlot _slot;
        private string _selected;
        private bool _synthMode;
        private string _message;
        private string _confirmUid;
        private float _confirmUntil;
        private Action _onClosed;

        public bool IsOpen => root.activeSelf;

        private void Awake()
        {
            root.SetActive(false);
            for (int i = 0; i < slotButtons.Length; i++)
            {
                var slot = (EquipSlot)i;
                slotButtons[i].onClick.AddListener(() => FocusSlot(slot));
            }

            enhanceButton.onClick.AddListener(Enhance);
            enhanceMaxButton.onClick.AddListener(EnhanceMax);
            bagTab.onClick.AddListener(() => SetMode(false));
            synthTab.onClick.AddListener(() => SetMode(true));
            autoButton.onClick.AddListener(ToggleAuto);
            equipButton.onClick.AddListener(EquipSelected);
            lockButton.onClick.AddListener(ToggleLock);
            dismantleButton.onClick.AddListener(DismantleSelected);
            backButton.onClick.AddListener(Close);
        }

        public void Open(string heroId, string heroName, Action onClosed = null)
        {
            _heroId = heroId;
            _heroName = heroName;
            _onClosed = onClosed;
            _slot = EquipSlot.Weapon;
            _selected = null;
            _synthMode = false;
            _message = null;
            _confirmUid = null;
            root.SetActive(true);
            Render(true);
        }

        private void Close()
        {
            root.SetActive(false);
            _onClosed?.Invoke();
        }

        private void Update()
        {
            if (_confirmUid != null && Time.unscaledTime > _confirmUntil)
            {
                _confirmUid = null;
                Render(false);
            }
        }

        private void FocusSlot(EquipSlot slot)
        {
            _slot = slot;
            _selected = null;
            _message = null;
            Render(true);
        }

        private void SetMode(bool synth)
        {
            _synthMode = synth;
            _selected = null;
            _message = null;
            Render(true);
        }

        // ---------------------------------------------------------------- 그리기

        private void Render(bool scrollToTop)
        {
            SaveData save = SaveSystem.Data;
            EquipmentTable table = Table;
            titleLabel.text = $"{_heroName} 장비";

            bool unlocked = table != null && EquipmentInventory.Unlocked(save);
            if (!unlocked)
            {
                summaryLabel.text = "<color=#FFB347>1구간 보스를 깨면 장비 3칸과 강화가 열려요</color>";
                for (int i = 0; i < slotLabels.Length; i++)
                {
                    slotLabels[i].text = $"{EquipmentText.SlotName((EquipSlot)i)}  <color=#A9B4C2>잠김</color>";
                    slotButtons[i].interactable = false;
                }

                SetActionButtons(false);
                list.Show(new List<ButtonList.Row>());
                detailLabel.text = string.Empty;
                return;
            }

            summaryLabel.text = Summary(save, table);
            for (int i = 0; i < slotLabels.Length; i++)
            {
                var slot = (EquipSlot)i;
                slotButtons[i].interactable = true;
                slotButtons[i].GetComponent<Image>().color = slot == _slot ? SlotFocused : SlotNormal;
                slotLabels[i].text = SlotLine(save, table, slot);
            }

            // 강화
            int level = EquipmentInventory.SlotLevel(save, _slot);
            bool can = EquipmentInventory.CanEnhance(save, table, _heroId, _slot, out int cost);
            enhanceButton.interactable = can && save.gold >= cost;
            enhanceLabel.text = can ? $"강화 Lv {level} → {level + 1} · 골드 {cost:N0}" : EquipmentInventory.Equipped(save, _heroId, _slot) == null ? "장비를 끼우면 강화" : $"Lv {level} · 등급 상한";
            (int levels, int total) = EquipmentInventory.PreviewMax(save, table, _heroId, _slot);
            enhanceMaxButton.interactable = levels > 0;
            enhanceMaxLabel.text = levels > 0 ? $"최대까지 +{levels} · 골드 {total:N0}" : "최대까지";

            bagTab.GetComponent<Image>().color = _synthMode ? SlotNormal : SlotFocused;
            synthTab.GetComponent<Image>().color = _synthMode ? SlotFocused : SlotNormal;
            autoLabel.text = $"중복 자동 분해: {(save.equipment.autoDismantle ? "켜기" : "끄기")}";

            if (_synthMode) RenderSynthesis(save, table, scrollToTop);
            else RenderBag(save, table, scrollToTop);
        }

        private string Summary(SaveData save, EquipmentTable table)
        {
            var snap = new MetaSnapshot { equipment = EquipmentInventory.Snapshot(save, table, _heroId) };
            List<StatModifier> mods = MetaStats.Build(snap, metaTable, this); // 레벨·별·수련을 비운 스냅샷 → 장비 몫만
            var parts = new List<string>();
            AddPart(parts, mods, StatType.Attack, ModifierKind.Percent);
            AddPart(parts, mods, StatType.MaxHp, ModifierKind.Percent);
            AddPart(parts, mods, StatType.AttackSpeed, ModifierKind.Percent);
            AddPart(parts, mods, StatType.CritRate, ModifierKind.Flat);
            AddPart(parts, mods, StatType.CritDamage, ModifierKind.Flat);
            AddPart(parts, mods, StatType.CooldownReduction, ModifierKind.Flat);
            AddPart(parts, mods, StatType.MoveSpeed, ModifierKind.Percent);
            AddPart(parts, mods, StatType.DamageReduction, ModifierKind.Flat);
            string shards = $"장비 조각 {save.ItemCount(Economy.Wallet.GearShard)} · 골드 {save.gold:N0}";
            return (parts.Count > 0 ? "장비 합계  " + string.Join(" · ", parts) : "장비 합계  없음") + $"\n<size=85%><color=#A9B4C2>{shards} · 칸 강화는 모든 영웅이 같이 써요</color></size>";
        }

        private static void AddPart(List<string> parts, List<StatModifier> mods, StatType stat, ModifierKind kind)
        {
            float v = MetaStats.Sum(mods, stat, kind);
            if (v > 0f) parts.Add($"{EquipmentText.StatName(stat)} +{v * 100f:0.#}%");
        }

        private string SlotLine(SaveData save, EquipmentTable table, EquipSlot slot)
        {
            int level = EquipmentInventory.SlotLevel(save, slot);
            EquipmentItem item = EquipmentInventory.Equipped(save, _heroId, slot);
            EquipmentData data = item != null ? table.Find(item.dataId) : null;
            if (data == null) return $"{EquipmentText.SlotName(slot)}  <color=#A9B4C2>비어 있음</color>  <size=85%>칸 Lv {level}</size>";
            int cap = table.CapOf(data.rarity);
            string over = level > cap ? $" <color=#FFB347>(상한 {cap})</color>" : string.Empty;
            string second = data.hasSecondary ? $" · {data.SecondaryText}" : string.Empty;
            return $"{EquipmentText.SlotName(slot)}  {Name(data)}\n<size=85%>칸 Lv {Mathf.Min(level, cap)}/{cap}{over} · {MainText(table, data, level)}{second}</size>";
        }

        private static string Name(EquipmentData data) => $"<color={EquipmentText.RarityColor(data.rarity)}>{data.displayName} ({EquipmentText.RarityName(data.rarity)})</color>";

        private static string MainText(EquipmentTable table, EquipmentData data, int slotLevel) =>
            $"{EquipmentText.StatName(EquipmentTable.MainStat(data.slot))} +{table.MainPercent(data, slotLevel) * 100f:0.##}%";

        private void RenderBag(SaveData save, EquipmentTable table, bool scrollToTop)
        {
            int level = EquipmentInventory.SlotLevel(save, _slot);
            var items = new List<(EquipmentItem item, EquipmentData data)>();
            foreach (EquipmentItem item in save.equipment.items)
            {
                EquipmentData data = table.Find(item.dataId);
                if (data != null && data.slot == _slot) items.Add((item, data));
            }

            items.Sort((a, b) => b.data.rarity != a.data.rarity ? b.data.rarity.CompareTo(a.data.rarity) : string.CompareOrdinal(a.data.id, b.data.id));
            string mine = EquipmentInventory.Equipped(save, _heroId, _slot)?.uid;

            var rows = new List<ButtonList.Row>();
            foreach ((EquipmentItem item, EquipmentData data) in items)
            {
                string uid = item.uid;
                string tags = uid == mine ? "  <color=#7CE38B>[끼는 중]</color>" : EquipmentInventory.IsEquipped(save, uid) ? "  <color=#A9B4C2>[다른 영웅]</color>" : string.Empty;
                string second = data.hasSecondary ? $" · {data.SecondaryText}" : string.Empty;
                string locked = item.locked ? "<color=#FFD966>[잠금]</color> " : string.Empty;
                string text = $"{locked}{Name(data)}{tags}\n<size=85%>{MainText(table, data, level)}{second}</size>";
                rows.Add(new ButtonList.Row(text, uid == _selected ? RowSelected : RowNormal, () => Select(uid)));
            }

            if (rows.Count == 0) rows.Add(new ButtonList.Row("<color=#A9B4C2>이 칸 장비가 없어요. 던전 클리어·상자·교환소에서 얻어요</color>", RowDisabled, null, false));
            list.Show(rows, scrollToTop);
            RenderDetail(save, table, mine);
        }

        private void RenderDetail(SaveData save, EquipmentTable table, string mine)
        {
            EquipmentItem item = EquipmentInventory.Find(save, _selected);
            EquipmentData data = item != null ? table.Find(item.dataId) : null;
            if (data == null)
            {
                SetActionButtons(false);
                detailLabel.text = _message ?? "<color=#A9B4C2>장비를 누르면 끼웠을 때 바뀌는 값을 보여줘요</color>";
                return;
            }

            int level = EquipmentInventory.SlotLevel(save, _slot);
            EquipmentItem current = EquipmentInventory.Find(save, mine);
            EquipmentData currentData = current != null ? table.Find(current.dataId) : null;
            StatType main = EquipmentTable.MainStat(_slot);
            float before = currentData != null ? table.MainPercent(currentData, level) : 0f;
            float after = table.MainPercent(data, level);
            string compare = item.uid == mine
                ? $"지금 끼는 장비 · {EquipmentText.StatName(main)} +{after * 100f:0.##}%"
                : $"끼우면: {EquipmentText.StatName(main)} +{before * 100f:0.##}% → <color={(after >= before ? "#7CE38B" : "#FF7A6B")}>+{after * 100f:0.##}%</color>";
            string second = currentData != null && currentData.hasSecondary ? currentData.SecondaryText : "없음";
            string secondAfter = data.hasSecondary ? data.SecondaryText : "없음";
            if (item.uid != mine) compare += $"\n<size=85%>부가: {second} → {secondAfter}</size>";
            detailLabel.text = (_message != null ? _message + "\n" : string.Empty) + compare;

            equipButton.gameObject.SetActive(true);
            lockButton.gameObject.SetActive(true);
            dismantleButton.gameObject.SetActive(true);
            equipButton.interactable = item.uid != mine;
            equipLabel.text = item.uid == mine ? "끼는 중" : "장착";
            lockButton.interactable = true;
            lockLabel.text = item.locked ? "잠금 풀기" : "잠그기";
            bool canDismantle = !item.locked && !EquipmentInventory.IsEquipped(save, item.uid);
            dismantleButton.interactable = canDismantle;
            dismantleLabel.text = _confirmUid == item.uid ? "한 번 더 누르면 분해" : $"분해 · 조각 +{table.DismantleShards(data.rarity)}";
        }

        private void RenderSynthesis(SaveData save, EquipmentTable table, bool scrollToTop)
        {
            SetActionButtons(false);
            var rows = new List<ButtonList.Row>();
            foreach (EquipRarity from in new[] { EquipRarity.Common, EquipRarity.Uncommon })
            {
                int have = CountFree(save, table, _slot, from);
                int cost = table.SynthesisCost(from);
                List<string> materials = EquipmentInventory.SuggestMaterials(save, table, _slot, from);
                foreach (EquipmentData result in table.Of(_slot, from + 1))
                {
                    string resultId = result.id;
                    bool ok = materials != null && save.gold >= cost;
                    string second = result.hasSecondary ? $" · {result.SecondaryText}" : string.Empty;
                    string text = $"{EquipmentText.RarityName(from)} {table.synthesisCount}개 → {Name(result)}\n<size=85%>골드 {cost:N0} · 재료 {have}/{table.synthesisCount}개{second}</size>";
                    rows.Add(new ButtonList.Row(text, ok ? RowNormal : RowDisabled, () => Synthesize(from, resultId), ok));
                }
            }

            rows.Add(new ButtonList.Row("<size=85%><color=#A9B4C2>잠근 장비·끼고 있는 장비는 재료로 안 써요. 희귀 → 전설 합성은 없어요 (전설은 상자·교환소·보스 보상)</color></size>", RowDisabled, null, false));
            list.Show(rows, scrollToTop);
            detailLabel.text = _message ?? "<color=#A9B4C2>같은 칸·같은 등급 3개로 다음 등급 장비 중 하나를 골라 만들어요</color>";
        }

        private static int CountFree(SaveData save, EquipmentTable table, EquipSlot slot, EquipRarity rarity)
        {
            int n = 0;
            foreach (EquipmentItem item in save.equipment.items)
            {
                EquipmentData d = table.Find(item.dataId);
                if (d != null && d.slot == slot && d.rarity == rarity && !item.locked && !EquipmentInventory.IsEquipped(save, item.uid)) n++;
            }

            return n;
        }

        private void SetActionButtons(bool visible)
        {
            equipButton.gameObject.SetActive(visible);
            lockButton.gameObject.SetActive(visible);
            dismantleButton.gameObject.SetActive(visible);
        }

        // ---------------------------------------------------------------- 누르기

        private void Select(string uid)
        {
            _selected = uid;
            _message = null;
            _confirmUid = null;
            Render(false);
        }

        /// <summary>바꾸고 저장 (저장을 못 하면 되돌리고 알림, Codex v0.4 2번)</summary>
        private bool Apply(System.Func<bool> change, string okMessage)
        {
            SaveSystem.CommitResult r = SaveSystem.Commit(change);
            if (r == SaveSystem.CommitResult.Rejected) return false;
            _message = r == SaveSystem.CommitResult.Saved ? okMessage : SaveSystem.SaveFailedText;
            Render(false);
            return r == SaveSystem.CommitResult.Saved;
        }

        private void EquipSelected() => Apply(() => EquipmentInventory.Equip(SaveSystem.Data, Table, _heroId, _selected), "<color=#7CE38B>장착했어요</color>");

        private void ToggleLock()
        {
            EquipmentItem item = EquipmentInventory.Find(SaveSystem.Data, _selected);
            if (item == null) return;
            bool locked = !item.locked;
            Apply(() =>
            {
                EquipmentInventory.SetLocked(SaveSystem.Data, _selected, locked);
                return true;
            }, locked ? "잠갔어요 (합성·분해에 안 써요)" : "잠금을 풀었어요");
        }

        private void DismantleSelected()
        {
            if (_confirmUid != _selected)
            {
                _confirmUid = _selected;
                _confirmUntil = Time.unscaledTime + ConfirmSeconds;
                Render(false);
                return;
            }

            _confirmUid = null;
            int shards = 0;
            string uid = _selected;
            if (Apply(() => (shards = EquipmentInventory.TryDismantle(SaveSystem.Data, Table, uid)) > 0, string.Empty))
            {
                _selected = null;
                _message = $"<color=#7CE38B>분해했어요 · 장비 조각 +{shards}</color>";
                Render(false);
            }
        }

        private void Enhance()
        {
            if (Apply(() => EquipmentInventory.TryEnhance(SaveSystem.Data, Table, _heroId, _slot), string.Empty))
            {
                _message = $"<color=#7CE38B>{EquipmentText.SlotName(_slot)} 칸 Lv {EquipmentInventory.SlotLevel(SaveSystem.Data, _slot)}</color>";
                Render(false);
            }
        }

        private void EnhanceMax()
        {
            int levels = 0;
            if (Apply(() => (levels = EquipmentInventory.EnhanceMax(SaveSystem.Data, Table, _heroId, _slot)) > 0, string.Empty))
            {
                _message = $"<color=#7CE38B>{EquipmentText.SlotName(_slot)} 칸 +{levels} → Lv {EquipmentInventory.SlotLevel(SaveSystem.Data, _slot)}</color>";
                Render(false);
            }
        }

        private void Synthesize(EquipRarity from, string resultId)
        {
            EquipmentItem made = null;
            bool ok = Apply(() =>
            {
                SaveData save = SaveSystem.Data;
                List<string> materials = EquipmentInventory.SuggestMaterials(save, Table, _slot, from);
                made = materials != null ? EquipmentInventory.TrySynthesize(save, Table, materials, resultId) : null;
                return made != null; // 재료 제거 + 결과 + 골드가 한 저장에
            }, string.Empty);

            if (ok)
            {
                _message = $"<color=#7CE38B>합성: {Table.Find(made.dataId).displayName}</color>";
                Render(false);
            }
        }

        private void ToggleAuto()
        {
            Apply(() =>
            {
                SaveSystem.Data.equipment.autoDismantle = !SaveSystem.Data.equipment.autoDismantle;
                return true;
            }, null);
        }
    }
}
