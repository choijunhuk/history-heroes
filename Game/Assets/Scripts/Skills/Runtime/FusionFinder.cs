using System.Collections.Generic;

namespace HistoryHeroes.Skills
{
    /// <summary>지금 가진 스킬로 만들 수 있는 조합 찾기 (GameDesign 6-2).</summary>
    public static class FusionFinder
    {
        /// <summary>
        /// ✍️ TODO (개발자가 직접 구현할 부분, Docs/Dev/v0.1-Plan.md 12단계)
        /// 규칙 (GameDesign 6-2):
        ///   - 진화: 재료 A(액티브)가 requiredLevelA(보통 최대 레벨) 이상 + 재료 B(패시브)를 가지고 있음
        ///   - 합체: 재료 A, B(둘 다 액티브)가 각각 requiredLevelA/B(3) 이상
        ///   - 이 영웅이 쓸 수 있는 레시피만 (recipe.heroOnlyId가 비었거나 같음)
        ///   - 이미 만든 결과(loadout.IsCompleted)와 "이번 판에서 숨기기"(loadout.IsHidden)는 빼기
        ///   - 조합 결과는 다시 재료가 되지 않는다 (재료가 IsFusionResult면 제외)
        /// 힌트: loadout.Find(recipe.materialA)로 인스턴스를 찾고 Level을 비교한다.
        ///
        /// 지금은 빈 목록을 돌려주는 임시 코드라 조합 카드가 나오지 않는다.
        /// </summary>
        public static List<FusionRecipe> FindAvailableFusions(SkillLoadout loadout, IEnumerable<FusionRecipe> recipes, string heroId)
        {
            // ---- 임시 코드 (TODO로 바꿀 것) ----
            return new List<FusionRecipe>();
        }
    }
}
