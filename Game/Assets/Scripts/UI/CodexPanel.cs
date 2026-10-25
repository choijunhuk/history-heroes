using System.Text;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HistoryHeroes.UI
{
    /// <summary>
    /// 조합 도감 (UX.md 1장 "도감" 탭, GameDesign 6-2 레시피 공개).
    /// 한 번 만든 조합은 재료까지 다 보인다. 아직 못 만든 조합은 종류만 보이고 나머지는 ???.
    /// </summary>
    public sealed class CodexPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text listLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private SkillDatabase database;

        public int RecipeCount => database != null ? database.recipes.Count : 0;

        private void Awake()
        {
            root.SetActive(false);
            backButton.onClick.AddListener(() => root.SetActive(false));
        }

        public void Open()
        {
            root.SetActive(true);
            Render();
        }

        private void Render()
        {
            int found = 0;
            var sb = new StringBuilder();
            foreach (FusionRecipe r in database.recipes)
            {
                if (r == null || r.result == null) continue;
                bool known = RecipeBook.IsDiscovered(r);
                if (known) found++;

                string kind = r.type == FusionType.Evolution ? "진화" : "합체";
                if (known)
                {
                    string a = r.materialA != null ? r.materialA.displayName : "?";
                    string b = r.materialB != null ? r.materialB.displayName : "?";
                    string rule = r.type == FusionType.Evolution ? $"{a} Lv{r.requiredLevelA} + {b}" : $"{a} Lv{r.requiredLevelA} + {b} Lv{r.requiredLevelB}";
                    sb.AppendLine($"<b>{r.result.displayName}</b>  ({kind})");
                    sb.AppendLine($"  {rule}");
                }
                else
                {
                    string hint = r.type == FusionType.Evolution ? "액티브 최대 레벨 + 패시브" : "액티브 Lv3 + 액티브 Lv3";
                    sb.AppendLine($"<b>???</b>  ({kind})");
                    sb.AppendLine($"  {hint}");
                }

                sb.AppendLine();
            }

            // 스킬별 조합 수 (조사 A3): 미발견 조합은 결과·짝 대신 개수만
            sb.AppendLine("<b>스킬별 조합</b>");
            foreach (SkillData s in database.skills)
            {
                if (s == null) continue;
                (int total, int known) = FusionHints.CountFor(s, database.recipes, RecipeBook.IsDiscovered);
                if (total == 0) continue;
                string color = known == total ? "#7CE38B" : known > 0 ? "#FFE08A" : "#A9B4C2";
                sb.AppendLine($"  {s.displayName}  <color={color}>{total}개 ({known}개 발견)</color>");
            }

            progressLabel.text = $"발견 {found} / {RecipeCount}";
            listLabel.text = sb.ToString();
        }
    }
}
