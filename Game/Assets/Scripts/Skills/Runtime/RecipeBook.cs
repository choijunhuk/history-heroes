using System.Collections.Generic;
using UnityEngine;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 발견한 조합 레시피 (도감). 한 번 만든 조합의 재료에만 카드에 "조합 재료"를 표시한다 (GameDesign 6-2).
    /// 지금은 앱을 켜 둔 동안만 기억한다. 저장은 v0.2 저장 시스템에서 붙인다.
    /// </summary>
    public static class RecipeBook
    {
        private static readonly HashSet<string> Discovered = new HashSet<string>();

        public static void Discover(FusionRecipe recipe)
        {
            if (recipe != null && !string.IsNullOrEmpty(recipe.id)) Discovered.Add(recipe.id);
        }

        public static bool IsDiscovered(FusionRecipe recipe)
        {
            return recipe != null && Discovered.Contains(recipe.id);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Discovered.Clear();
    }
}
