using HistoryHeroes.Heroes;
using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>새 영웅 이순신 (확장 결정 12): 철갑 숫자 · 별 강화 · 명단 · 전용 액티브 · 학익진 조합</summary>
    public sealed class YiSunsinTests
    {
        private static HeroData Load() => UnityEditor.AssetDatabase.LoadAssetAtPath<HeroRoster>("Assets/Data/Heroes/HeroRoster.asset")?.Find("yisunsin");

        [Test]
        public void Ironclad_StarsRaiseGuardAndCap()
        {
            var yi = ScriptableObject.CreateInstance<HeroData>();
            yi.stopGuard = 0.25f;
            yi.stopGuardCap = 2f;
            yi.star3StopGuard = 0.3f;
            yi.star5StopGuardCap = 3f;
            try
            {
                Assert.AreEqual(0.25f, HeroSignature.For(yi, 1).StopGuard, 1e-5f);
                Assert.AreEqual(2f, HeroSignature.For(yi, 1).StopGuardCap, 1e-5f);
                Assert.AreEqual(0.3f, HeroSignature.For(yi, 3).StopGuard, 1e-5f, "★3 멈춤 받는 피해 -30%");
                Assert.AreEqual(2f, HeroSignature.For(yi, 4).StopGuardCap, 1e-5f);
                Assert.AreEqual(3f, HeroSignature.For(yi, 5).StopGuardCap, 1e-5f, "★5 쌓기 상한 300%");
                StringAssert.Contains("★3 멈춤 받는 피해 -30%", HeroSignature.StarText(yi));

                var khan = ScriptableObject.CreateInstance<HeroData>();
                Assert.AreEqual(0f, HeroSignature.For(khan, 5).StopGuard, "다른 영웅은 철갑 없음");
                Object.DestroyImmediate(khan);
            }
            finally
            {
                Object.DestroyImmediate(yi);
            }
        }

        [Test]
        public void Roster_HasYiWithExclusiveAndFusion()
        {
            HeroData yi = Load();
            Assert.IsNotNull(yi, "명단에 이순신 (장면 빌더)");
            Assert.AreEqual(HeroClass.Gunner, yi.heroClass);
            Assert.AreEqual(1250f, yi.maxHp);
            Assert.AreEqual(0.25f, yi.stopGuard, 1e-5f);
            Assert.IsNotNull(yi.projectilePrefab, "총통 포탄");
            Assert.IsNotNull(yi.exclusiveActive, "전용 액티브");
            Assert.AreEqual("yi_turtle", yi.exclusiveActive.id);
            Assert.IsNotNull(yi.exclusiveActive.behaviourPrefab, "거북선 돌격 동작");

            var db = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            FusionRecipe recipe = db.FindRecipe("yi_hakikjin");
            Assert.IsNotNull(recipe, "학익진 조합");
            Assert.AreEqual(FusionType.Evolution, recipe.type);
            Assert.AreEqual("yisunsin", recipe.heroOnlyId, "이순신만");
            Assert.IsNotNull(((ActiveSkillData)recipe.result).behaviourPrefab, "학익진 동작");
            CollectionAssert.IsEmpty(db.FindProblems(), "스킬 데이터 문제 없음");
        }
    }
}
