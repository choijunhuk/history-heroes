using System.Linq;
using HistoryHeroes.Heroes;
using HistoryHeroes.Story;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class CodexPresentationTests
    {
        [Test]
        public void EveryRosterHeroHasOneBiographyWithSeparateSourceLabels()
        {
            var roster = AssetDatabase.LoadAssetAtPath<HeroRoster>("Assets/Data/Heroes/HeroRoster.asset");
            Assert.IsNotNull(roster);
            Assert.AreEqual(21, roster.heroes.Count);
            CollectionAssert.AreEquivalent(roster.heroes.Select(hero => hero.id), HeroStories.HeroIds);
            foreach (HeroData hero in roster.heroes)
            {
                string text = HeroStories.For(hero.id);
                StringAssert.Contains(hero.displayName, text, hero.id);
                StringAssert.Contains("사실:", text, hero.id);
                StringAssert.Contains("전승:", text, hero.id);
                StringAssert.Contains("각색:", text, hero.id);
            }
        }

        [Test]
        public void UnknownHeroDoesNotBorrowAnotherPersonsStory()
        {
            Assert.IsEmpty(HeroStories.For(null));
            Assert.IsEmpty(HeroStories.For("unknown"));
        }

        [Test]
        public void LockedRecipeCardHidesNameAndMaterials()
        {
            var recipe = ScriptableObject.CreateInstance<FusionRecipe>();
            var result = ScriptableObject.CreateInstance<ActiveSkillData>();
            try
            {
                result.displayName = "강철 회오리";
                recipe.result = result;
                Assert.AreEqual("???", CodexPanel.RecipeTitle(recipe, false));
                Assert.AreEqual("강철 회오리", CodexPanel.RecipeTitle(recipe, true));
                Assert.AreEqual("???", CodexPanel.RecipeTitle(null, true));
            }
            finally
            {
                Object.DestroyImmediate(recipe);
                Object.DestroyImmediate(result);
            }
        }
    }
}
