using System.Collections.Generic;
using System.Text;
using HistoryHeroes.Meta;
using HistoryHeroes.Pets;
using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 펫 화면 (Pets 6장): 영웅 창 → 펫. 4마리 목록 (없는 펫은 얻는 방법), 펫을 누르면 이야기(📜 사실 / 📖 전승)·효과·각성,
    /// [장착] [레벨업 골드]. 인연 영웅이면 ♥ +2%. 펫은 영웅마다 1칸, 같은 펫을 여러 영웅이 같이 써도 된다.
    /// </summary>
    public sealed class PetPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private MetaTable metaTable;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text infoLabel;
        [SerializeField] private ButtonList list;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipLabel;
        [SerializeField] private Button levelButton;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private Button backButton;

        private static readonly Color RowNormal = new Color(0.13f, 0.15f, 0.2f);
        private static readonly Color RowSelected = new Color(0.22f, 0.36f, 0.6f);
        private static readonly Color RowEquipped = new Color(0.16f, 0.32f, 0.22f);
        private static readonly Color RowLocked = new Color(0.1f, 0.1f, 0.12f);

        private PetTable Table => metaTable != null ? metaTable.pets : null;

        private string _heroId;
        private string _heroName;
        private string _selected;
        private string _message;
        private System.Action _onClosed;

        private void Awake()
        {
            root.SetActive(false);
            equipButton.onClick.AddListener(ToggleEquip);
            levelButton.onClick.AddListener(LevelUpSelected);
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
            _message = null;
            _selected = PetService.Equipped(SaveSystem.Data, heroId);
            root.SetActive(true);
            Render();
        }

        private void Render()
        {
            SaveData save = SaveSystem.Data;
            PetTable table = Table;
            titleLabel.text = $"{_heroName}의 펫";
            if (table == null)
            {
                infoLabel.text = string.Empty;
                list.Show(new List<ButtonList.Row>());
                return;
            }

            infoLabel.text = !PetService.Unlocked(save)
                ? "<color=#FFB347>던전을 처음 깨면 첫 펫(사냥매)과 펫 칸이 열려요</color>"
                : $"영웅마다 1마리 · 레벨마다 공격력·체력 +{table.perLevel * 100f:0.0}% · 인연 영웅이면 +{table.bondBonus * 100f:0}% · 골드 {save.gold:N0}";

            string equipped = PetService.Equipped(save, _heroId);
            var rows = new List<ButtonList.Row>();
            foreach (PetData p in table.pets)
            {
                if (p == null) continue;
                string id = p.id;
                int level = PetService.Level(save, id);
                bool bond = PetService.IsBond(p, _heroId);
                string heart = bond ? " <color=#FF7A8A>♥ 인연</color>" : string.Empty;
                if (level <= 0)
                {
                    rows.Add(new ButtonList.Row($"<color=#A9B4C2><b>{p.displayName}</b>  잠김 · {p.SourceText}</color>\n<size=85%><color=#7F8A99>{p.trigger}</color></size>",
                        id == _selected ? RowSelected : RowLocked, () => Select(id)));
                    continue;
                }

                string state = id == equipped ? "  <color=#7CE38B>[장착 중]</color>" : string.Empty;
                rows.Add(new ButtonList.Row($"<b>{p.displayName}</b>  Lv {level}/{table.maxLevel}{heart}{state}\n<size=85%>{p.trigger}</size>",
                    id == _selected ? RowSelected : id == equipped ? RowEquipped : RowNormal, () => Select(id)));
            }

            list.Show(rows);
            RenderDetail(save, table, equipped);
        }

        private void Select(string id)
        {
            _selected = id;
            _message = null;
            Render();
        }

        private void RenderDetail(SaveData save, PetTable table, string equipped)
        {
            PetData pet = table.Find(_selected);
            if (pet == null)
            {
                detailLabel.text = _message ?? "<color=#A9B4C2>펫을 눌러 보세요. 펫마다 반응하는 행동이 달라요</color>";
                equipButton.gameObject.SetActive(false);
                levelButton.gameObject.SetActive(false);
                return;
            }

            int level = PetService.Level(save, pet.id);
            bool bond = PetService.IsBond(pet, _heroId);
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_message)) sb.AppendLine(_message);
            sb.AppendLine($"<b>{pet.displayName}</b> — {pet.trigger}");
            sb.AppendLine(pet.effect);
            sb.AppendLine($"<size=92%><color={(level >= table.awakenFirst ? "#FFD966" : "#7F8A99")}>Lv{table.awakenFirst} 각성: {pet.awaken5}</color></size>");
            sb.AppendLine($"<size=92%><color={(level >= table.awakenSecond ? "#FFD966" : "#7F8A99")}>Lv{table.awakenSecond} 각성: {pet.awaken10}</color></size>");
            if (!string.IsNullOrEmpty(pet.fact)) sb.AppendLine($"<size=88%><color=#A9B4C2>[사실] {pet.fact}</color></size>");
            if (!string.IsNullOrEmpty(pet.legend)) sb.AppendLine($"<size=88%><color=#A9B4C2>[전승] {pet.legend}</color></size>");
            string bondText = bond ? $" <color=#FF7A8A>(인연 +{table.bondBonus * 100f:0}% 포함)</color>" : $" <size=85%>(인연: {pet.bondHeroName})</size>";
            if (level > 0) sb.Append($"<size=95%>지금 공격력·체력 +{table.Bonus(level, bond) * 100f:0.0#}%{bondText}</size>");
            else sb.Append($"<size=95%><color=#FFB347>{pet.SourceText} 얻어요</color></size>");
            detailLabel.text = sb.ToString();

            bool owned = level > 0;
            equipButton.gameObject.SetActive(owned);
            equipLabel.text = pet.id == equipped ? "빼기" : "장착";

            int cost = PetService.LevelCost(save, table, pet.id);
            levelButton.gameObject.SetActive(owned);
            levelButton.interactable = cost > 0 && save.gold >= cost;
            levelLabel.text = cost < 0 ? "최대 레벨" : $"레벨업 Lv {level} → {level + 1}\n<size=80%>골드 {cost:N0}</size>";
        }

        private void ToggleEquip()
        {
            PetTable table = Table;
            PetData pet = table != null ? table.Find(_selected) : null;
            if (pet == null) return;
            bool equipped = PetService.Equipped(SaveSystem.Data, _heroId) == pet.id;
            string target = equipped ? null : pet.id;
            SaveSystem.CommitResult r = SaveSystem.Commit(() => PetService.Equip(SaveSystem.Data, table, _heroId, target));
            if (r == SaveSystem.CommitResult.Rejected) return;
            _message = r == SaveSystem.CommitResult.Saved
                ? (equipped ? $"<color=#A9B4C2>{pet.displayName}를 뺐어요</color>" : $"<color=#7CE38B>{_heroName} 곁에 {pet.displayName}</color>")
                : SaveSystem.SaveFailedText;
            Render();
        }

        private void LevelUpSelected()
        {
            PetTable table = Table;
            PetData pet = table != null ? table.Find(_selected) : null;
            if (pet == null) return;
            SaveSystem.CommitResult r = SaveSystem.Commit(() => PetService.TryLevelUp(SaveSystem.Data, table, pet.id));
            if (r == SaveSystem.CommitResult.Rejected) return;
            int level = PetService.Level(SaveSystem.Data, pet.id);
            string awaken = level == table.awakenFirst || level == table.awakenSecond ? " — 각성! 새 행동이 생겼어요" : string.Empty;
            _message = r == SaveSystem.CommitResult.Saved ? $"<color=#FFD966>{pet.displayName} Lv {level}{awaken}</color>" : SaveSystem.SaveFailedText;
            Render();
        }
    }
}
