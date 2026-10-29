using System.Collections.Generic;
using HistoryHeroes.Relics;
using HistoryHeroes.Records;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 유물 도감 (Relics 6장): 세트 6 × 유물 4. 가진 유물은 ★·사실 글, 없는 유물은 ??? + 얻는 방법.
    /// 유물을 누르면 사실과 별 진행을 보여 준다. 뽑기 버튼은 상점의 같은 유물 화면으로 간다.
    /// </summary>
    public sealed class RelicPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RelicTable table;
        [SerializeField] private TMP_Text summaryLabel;
        [SerializeField] private ButtonList list;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private Image detailIcon;
        [SerializeField] private Button drawButton;
        [SerializeField] private ShopPanel shop;
        [SerializeField] private Button backButton;

        private static readonly Color RowOwned = new Color(0.16f, 0.2f, 0.26f);
        private static readonly Color RowMissing = new Color(0.1f, 0.1f, 0.12f);
        private static readonly Color RowSet = new Color(0.3f, 0.24f, 0.1f);
        private static readonly Color RowSelected = new Color(0.22f, 0.36f, 0.6f);

        private string _selected;
        private System.Action _onClosed;

        private void Awake()
        {
            root.SetActive(false);
            drawButton.onClick.AddListener(() => shop.OpenRelics(() => { if (root.activeSelf) Render(); }));
            backButton.onClick.AddListener(() =>
            {
                root.SetActive(false);
                System.Action callback = _onClosed;
                _onClosed = null;
                callback?.Invoke();
            });
        }

        public void Open(System.Action onClosed = null)
        {
            _onClosed = onClosed;
            _selected = null;
            transform.SetAsLastSibling();
            root.SetActive(true);
            Render();
        }

        private void Render()
        {
            SaveData save = SaveSystem.Data;
            drawButton.interactable = RelicService.Unlocked(save);
            RelicService.Bonus bonus = RelicService.Calculate(save.relics.owned, table);
            int count = save.relics.owned.Count;
            string active = RelicService.Unlocked(save) ? string.Empty : "\n<color=#FFB347>2구간 보스를 깨면 효과가 켜져요 (지금은 보관만)</color>";
            summaryLabel.text = $"유물 {count}/{table.relics.Count} · 공격력·체력 +{bonus.AttackHp * 100f:0.#}% · 피해 감소 +{bonus.DamageReduction * 100f:0.#}% · 고대 파편 {RelicService.Shards(save)}{active}";

            var rows = new List<ButtonList.Row>();
            for (int set = 0; set < table.setNames.Length; set++)
            {
                int have = 0;
                foreach (RelicData r in table.relics)
                {
                    if (r != null && r.set == set && RelicService.Stars(save, r.id) > 0) have++;
                }

                string done = have >= 4 ? $"  <color=#FFD966>세트 완성 · 공격력·체력 +{table.setBonus * 100f:0.###}% · 피해 감소 +{table.setDamageReduction * 100f:0.###}%</color>" : $"  {have}/4";
                rows.Add(new ButtonList.Row($"<b>{table.setNames[set]}</b>{done}", RowSet, null, false));
                foreach (RelicData r in table.relics)
                {
                    if (r == null || r.set != set) continue;
                    string id = r.id;
                    int stars = RelicService.Stars(save, id);
                    string text = stars > 0
                        ? $"{r.displayName}  <color=#FFD966>{new string('★', stars)}{new string('☆', RelicTable.MaxStars - stars)}</color>\n<size=82%>{r.fact}</size>"
                        : $"<color=#A9B4C2>???</color>\n<size=82%><color=#A9B4C2>얻는 방법: {r.howToGet}</color></size>";
                    rows.Add(new ButtonList.Row(text, id == _selected ? RowSelected : stars > 0 ? RowOwned : RowMissing, () => Select(id)));
                }
            }

            list.Show(rows);
            RenderDetail(save);
        }

        private void RenderDetail(SaveData save)
        {
            RelicData relic = table.Find(_selected);
            // 아직 얻지 않은 유물의 그림·이야기는 계속 숨긴다.
            if (detailIcon != null)
            {
                detailIcon.sprite = relic != null && RelicService.Stars(save, relic.id) > 0 ? relic.icon : null;
                detailIcon.gameObject.SetActive(detailIcon.sprite != null);
            }
            if (relic == null)
            {
                detailLabel.text = "<color=#A9B4C2>유물을 누르면 이야기와 별을 볼 수 있어요. 같은 유물이 나오면 별이 올라요.</color>";
                return;
            }

            int stars = RelicService.Stars(save, relic.id);
            if (stars > 0)
            {
                int duplicates = 0;
                foreach (RelicStar owned in save.relics.owned)
                    if (owned.id == relic.id) { duplicates = owned.duplicates; break; }
                string next = stars < RelicTable.MaxStars ? $" · 다음 별까지 중복 {duplicates}/{table.duplicateCosts[stars - 1]}" : " · 완성";
                detailLabel.text = $"{relic.displayName} ★{stars} · 공격력·체력 +{stars * table.perStar * 100f:0.###}%{next}"
                    + (string.IsNullOrEmpty(relic.story) ? string.Empty : $"\n<size=85%>게임 각색: {relic.story}</size>");
            }
            else
            {
                detailLabel.text = $"얻는 방법: {relic.howToGet}";
            }
        }

        private void Select(string id)
        {
            _selected = id;
            Render();
        }
    }
}
