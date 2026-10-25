using UnityEngine;

namespace HistoryHeroes.Skills
{
    public enum FusionType
    {
        Evolution, // 액티브 MAX + 패시브 보유 → 액티브가 진화 형태로 바뀜
        Merge,     // 액티브 Lv3+ + 액티브 Lv3+ → 하나로 합쳐짐, 액티브 칸 1개 비움
    }

    /// <summary>조합 레시피 (GameDesign 6-2).</summary>
    [CreateAssetMenu(menuName = "History Heroes/Skill/Fusion Recipe", fileName = "Fusion_")]
    public sealed class FusionRecipe : ScriptableObject
    {
        public string id;
        public FusionType type;

        [Header("재료")]
        public ActiveSkillData materialA;
        [Tooltip("진화면 패시브, 합체면 액티브")]
        public SkillData materialB;
        [Tooltip("진화: 재료 A 최대 레벨 / 합체: 3")]
        public int requiredLevelA = 5;
        [Tooltip("진화: 1 (보유만) / 합체: 3")]
        public int requiredLevelB = 1;

        [Header("결과")]
        public ActiveSkillData result;
        [Tooltip("비어 있으면 모든 영웅")]
        public string heroOnlyId;

        /// <summary>
        /// 합체 시작 레벨 = 1 + (재료의 Lv3 초과분 합 ÷ 2, 내림), 최대 3. (GameDesign 6-2)
        /// Lv5까지 올린 뒤 합체해도 손해가 없게 한다.
        /// </summary>
        public int MergeStartLevel(int levelA, int levelB)
        {
            int excess = Mathf.Max(0, levelA - 3) + Mathf.Max(0, levelB - 3);
            return Mathf.Min(3, 1 + excess / 2);
        }
    }
}
