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
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private TMP_Text[] recipeLabels;
        [SerializeField] private Button[] recipeButtons;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private Button backButton;
        [SerializeField] private SkillDatabase database;
        [Tooltip("유물 도감 (v0.4, 조합 도감 옆)")]
        [SerializeField] private Button relicButton;
        [SerializeField] private RelicPanel relics;

        public int RecipeCount => database != null ? database.recipes.Count : 0;

        private void Awake()
        {
            root.SetActive(false);
            backButton.onClick.AddListener(() => root.SetActive(false));
            if (relicButton != null) relicButton.onClick.AddListener(() => relics.Open());
#if UNITY_EDITOR
            if (recipeLabels == null || recipeButtons == null || recipeLabels.Length != database.recipes.Count || recipeButtons.Length != recipeLabels.Length)
                Debug.LogWarning("[CodexPanel] 목표 레시피 단추 연결이 없습니다. SceneBuilder.BuildAll로 장면을 다시 만들어 주세요.", this);
#endif
            for (int i = 0; recipeButtons != null && i < recipeButtons.Length; i++)
            {
                int index = i;
                recipeButtons[i].onClick.AddListener(() => ToggleTarget(index));
            }
        }

        public void Open()
        {
            root.SetActive(true);
            if (messageLabel != null) messageLabel.text = "";
            Render();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
            else listLabel.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void ToggleTarget(int index)
        {
            if (index >= database.recipes.Count) return;
            FusionRecipe recipe = database.recipes[index];
            if (!RecipeBook.IsDiscovered(recipe)) return;
            bool selected = RecipeBook.IsTargeted(recipe);
            SaveSystem.CommitResult result = RecipeBook.TrySetTarget(recipe, !selected);
            if (messageLabel != null)
            {
                if (result == SaveSystem.CommitResult.SaveFailed) messageLabel.text = SaveSystem.SaveFailedText;
                else if (result == SaveSystem.CommitResult.Rejected && !selected) messageLabel.text = "목표는 최대 2개까지 고를 수 있어요";
                else messageLabel.text = "";
            }
            Render();
        }

        private void Render()
        {
            int found = 0;
            var sb = new StringBuilder();
            bool hasRows = recipeLabels != null && recipeButtons != null && recipeLabels.Length == database.recipes.Count && recipeButtons.Length == recipeLabels.Length;
            for (int i = 0; i < database.recipes.Count; i++)
            {
                FusionRecipe r = database.recipes[i];
                if (r == null || r.result == null)
                {
                    if (hasRows) recipeLabels[i].transform.parent.gameObject.SetActive(false);
                    continue;
                }
                if (hasRows) recipeLabels[i].transform.parent.gameObject.SetActive(true);
                bool known = RecipeBook.IsDiscovered(r);
                if (known) found++;

                string kind = r.type == FusionType.Evolution ? "진화" : "합체";
                if (known)
                {
                    string a = r.materialA != null ? r.materialA.displayName : "?";
                    string b = r.materialB != null ? r.materialB.displayName : "?";
                    string rule = r.type == FusionType.Evolution ? $"{a} Lv{r.requiredLevelA} + {b}" : $"{a} Lv{r.requiredLevelA} + {b} Lv{r.requiredLevelB}";
                    if (hasRows)
                    {
                        recipeLabels[i].text = $"<b>{r.result.displayName}</b>  ({kind})\n{rule}";
                        recipeButtons[i].gameObject.SetActive(true);
                        recipeButtons[i].GetComponentInChildren<TMP_Text>().text = RecipeBook.IsTargeted(r) ? "★ 목표" : "목표";
                    }
                    else sb.AppendLine($"<b>{r.result.displayName}</b>  ({kind})\n  {rule}\n");
                }
                else
                {
                    string hint = r.type == FusionType.Evolution ? "액티브 최대 레벨 + 패시브" : "액티브 Lv3 + 액티브 Lv3";
                    if (hasRows)
                    {
                        recipeLabels[i].text = $"<b>???</b>  ({kind})\n{hint}";
                        recipeButtons[i].gameObject.SetActive(false);
                    }
                    else sb.AppendLine($"<b>???</b>  ({kind})\n  {hint}\n");
                }
            }

            sb.AppendLine("<b>토르 · 북유럽 신화</b>  <color=#9FD8FF>신화 · 각색</color>");
            sb.AppendLine("천둥의 무게 · 귀환 망치 · 천둥의 귀환은 게임 각색 기술입니다.");
            sb.AppendLine("<color=#C8D6E5>‘실패를 지우지 않고 다시 지키려는 인간의 선택을 보았기에, 나도 기록을 지키겠다.’</color>\n");

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
