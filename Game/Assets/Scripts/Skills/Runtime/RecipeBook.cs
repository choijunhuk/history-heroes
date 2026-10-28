using HistoryHeroes.Records;

namespace HistoryHeroes.Skills
{
    /// <summary>
    /// 발견한 조합 레시피 (조합 도감). 저장 파일에 남는다 (v0.2).
    /// 한 번 만든 조합의 재료에만 카드에 "조합 재료"를 표시한다 (GameDesign 6-2).
    /// 발견하면 표시만 해 두고, 실제 쓰기는 안전한 시점(방 클리어·판 끝)에 한다.
    /// </summary>
    public static class RecipeBook
    {
        public static void Discover(FusionRecipe recipe)
        {
            if (Common.GameSession.Practice) return;
            if (recipe == null || string.IsNullOrEmpty(recipe.id)) return;
            var found = SaveSystem.Data.discoveredRecipes;
            if (found.Contains(recipe.id)) return;
            found.Add(recipe.id);
            SaveSystem.MarkDirty();
        }

        public static bool IsDiscovered(FusionRecipe recipe)
        {
            return recipe != null && !string.IsNullOrEmpty(recipe.id) && SaveSystem.Data.discoveredRecipes.Contains(recipe.id);
        }

        public static int Count => SaveSystem.Data.discoveredRecipes.Count;
    }
}
