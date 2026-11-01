using System.Collections.Generic;

namespace HistoryHeroes.Skills
{
    /// <summary>지금 가진 스킬로 만들 수 있는 조합 찾기 (GameDesign 6-2).</summary>
    public static class FusionFinder
    {
        /// <summary>
        /// 지금 조건을 채운 조합 레시피 목록 (GameDesign 6-2).
        ///   - 진화: 재료 A(액티브)가 requiredLevelA(최대 레벨) 이상 + 재료 B(패시브)를 가지고 있음
        ///   - 합체: 재료 A, B(둘 다 액티브)가 각각 requiredLevelA/B(3) 이상
        ///   - 이 영웅이 쓸 수 있는 레시피만 (heroOnlyId가 비었거나 같음)
        ///   - 이미 만든 조합, "이번 판에서 숨기기"한 조합은 빼기 (같은 결과는 한 판에 1개)
        ///   - 조합 결과는 다시 재료가 되지 않는다 (연쇄 조합 금지)
        /// </summary>
        public static List<FusionRecipe> FindAvailableFusions(SkillLoadout loadout, IEnumerable<FusionRecipe> recipes, string heroId)
        {
            var available = new List<FusionRecipe>();
            if (loadout == null || recipes == null) return available;

            foreach (FusionRecipe recipe in recipes)
            {
                if (recipe == null || recipe.materialA == null || recipe.materialB == null || recipe.result == null) continue;
                if (!string.IsNullOrEmpty(recipe.heroOnlyId) && recipe.heroOnlyId != heroId) continue;
                if (loadout.IsCompleted(recipe) || loadout.IsHidden(recipe)) continue;
                if (loadout.IsBanned(recipe.materialA) || loadout.IsBanned(recipe.materialB)) continue;
                if (loadout.Find(recipe.result) != null) continue; // 같은 결과를 이미 가지고 있음

                SkillInstance a = loadout.Find(recipe.materialA);
                SkillInstance b = loadout.Find(recipe.materialB);
                if (a == null || b == null) continue;
                if (a.IsFusionResult || b.IsFusionResult) continue;
                if (a.Level < recipe.requiredLevelA || b.Level < recipe.requiredLevelB) continue;

                available.Add(recipe);
            }

            return available;
        }
    }
}
