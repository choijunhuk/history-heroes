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

            sb.AppendLine("<b>아누비스 · 고대 이집트 신화</b>  <color=#9FD8FF>신화 · 각색</color>");
            sb.AppendLine("장례와 죽은 자를 지키는 신으로 전해집니다. 저울의 표식·모래 수호상·모래의 문과 기억의 수호병은 게임 각색입니다.");
            sb.AppendLine("<color=#C8D6E5>실패를 지우지 않고 서로를 돌본 기록을 보고, 봉인 대신 인류와 기억을 지키기로 했다.</color>\n");
            sb.AppendLine("<b>제우스 · 그리스 신화</b>  <color=#9FD8FF>신화 · 각색</color>");
            sb.AppendLine("하늘과 번개의 신으로 전해집니다. 기록을 시험하는 탄막은 게임 각색이며, 이번에는 시련의 형상으로만 등장합니다.\n");

            sb.AppendLine("<b>스사노오 · 일본 신화</b>  <color=#9FD8FF>신화 · 각색</color>");
            sb.AppendLine("오로치와 검의 전승으로 알려져 있습니다. 두 시험 형상, 바람의 간격·바람 베기·폭풍의 길은 게임 각색입니다.");
            sb.AppendLine("<color=#C8D6E5>실패 뒤에도 길을 고쳐 지킨 인간의 기록을 보고, 봉인보다 동행을 택했습니다.</color>\n");

            sb.AppendLine("<b>아테나 · 그리스 신화</b>  <color=#9FD8FF>신화 · 각색</color>");
            sb.AppendLine("지혜와 전쟁의 신으로 전해집니다. 판단의 방패·올빼미의 눈·수호자의 시선과 기록의 시험은 게임 각색입니다.");
            sb.AppendLine("<color=#C8D6E5>잘못에서 배우고 서로를 지키는 인간의 선택을 보고, 함께 기억을 지키기로 했습니다.</color>\n");

            sb.AppendLine("<b>클레오파트라 · 프톨레마이오스 왕조</b>  <color=#9FD8FF>사실 · 후대 기록 · 각색</color>");
            sb.AppendLine("이집트의 여왕이었습니다. 언어 능력에 관한 설명은 후대 저술의 기록이며, 기록의 수호상·파로스의 빛·알렉산드리아의 서고는 게임 각색입니다.\n");

            sb.AppendLine("<b>토모에 고젠 · 『헤이케 이야기』</b>  <color=#9FD8FF>사실 · 전승 · 각색</color>");
            sb.AppendLine("겐페이 전쟁을 다룬 군기문학에 전하는 인물입니다. 실존과 전투 일화는 확정하지 않습니다. 교차 사격·겹시위·교차하는 바람과 차림새는 게임 각색입니다.\n");

            sb.AppendLine("<b>무측천 · 중국의 주 왕조</b>  <color=#9FD8FF>사실 · 후대 기록 · 각색</color>");
            sb.AppendLine("690년부터 705년까지 황제로 재위했습니다. 후대의 평가와 일화를 사실로 단정하지 않습니다. 질서의 인장·기록의 칙령·정연한 서책은 게임 각색입니다.\n");

            sb.AppendLine("<b>마리 퀴리 · 관측과 연구</b>  <color=#9FD8FF>사실 · 전승 구분 · 각색</color>");
            sb.AppendLine("1903년 물리학상과 1911년 화학상 수상자입니다. 관측 표식·분광의 빛·관측의 스펙트럼은 게임 창작이며, 방사선을 안전하거나 치유하는 빛으로 나타내지 않습니다.\n");

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
