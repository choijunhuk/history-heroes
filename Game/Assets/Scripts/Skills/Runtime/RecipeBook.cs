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

        public static bool IsTargeted(FusionRecipe recipe) => IsTargeted(SaveSystem.Data, recipe);

        public static bool IsTargeted(SaveData save, FusionRecipe recipe)
        {
            return save != null && recipe != null && !string.IsNullOrEmpty(recipe.id) &&
                   save.discoveredRecipes != null && save.discoveredRecipes.Contains(recipe.id) &&
                   save.targetRecipes != null && save.targetRecipes.Contains(recipe.id);
        }

        /// <summary>발견한 레시피만 목표로 고르고, 이미 고른 목표는 2개 제한에 세지 않는다.</summary>
        public static bool TrySetTarget(SaveData save, FusionRecipe recipe, bool targeted)
        {
            if (save == null || recipe == null || string.IsNullOrEmpty(recipe.id) ||
                save.discoveredRecipes == null || !save.discoveredRecipes.Contains(recipe.id)) return false;

            bool selected = save.targetRecipes != null && save.targetRecipes.Contains(recipe.id);
            if (selected == targeted) return false;
            if (targeted)
            {
                if (save.targetRecipes != null && save.targetRecipes.Count >= 2) return false;
                save.targetRecipes ??= new System.Collections.Generic.List<string>();
                save.targetRecipes.Add(recipe.id);
            }
            else save.targetRecipes.Remove(recipe.id);
            return true;
        }

        /// <summary>도감의 선택은 즉시 저장한다. 저장 실패 시 변경 전으로 되돌린다.</summary>
        public static SaveSystem.CommitResult TrySetTarget(FusionRecipe recipe, bool targeted)
        {
            return SaveSystem.Commit(() => TrySetTarget(SaveSystem.Data, recipe, targeted));
        }

        public static int Count => SaveSystem.Data.discoveredRecipes.Count;
    }
}
